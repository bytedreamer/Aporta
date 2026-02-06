using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Aporta.Core.Services;

/// <summary>
/// Event args for access decision events, used to notify external systems.
/// </summary>
public class AccessDecisionEventArgs : EventArgs
{
    public string EndpointId { get; set; }
    public bool IsGranted { get; set; }
    public string CardNumber { get; set; }
    public string PersonName { get; set; }
    public EventReason Reason { get; set; }
}

public class AccessService
{
    private readonly ExtensionService _extensionService;
    private readonly ILogger<AccessService> _logger;
    private readonly DoorRepository _doorRepository;
    private readonly EndpointRepository _endpointRepository;
    private readonly CredentialRepository _credentialRepository;
    private readonly EventRepository _eventRepository;
    private readonly ConcurrentDictionary<string, Task> _processAccessCredential = new();
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;

    /// <summary>
    /// Event raised when an access decision is made (granted or denied).
    /// External systems can subscribe to forward the decision.
    /// </summary>
    public event EventHandler<AccessDecisionEventArgs> AccessDecisionMade;

    /// <summary>
    /// Represents a service for accessing and managing system access.
    /// </summary>
    public AccessService(IDataAccess dataAccess, ExtensionService extensionService,
        IHubContext<DataChangeNotificationHub> hubContext, ILogger<AccessService> logger)
    {
        _doorRepository = new DoorRepository(dataAccess);
        _credentialRepository = new CredentialRepository(dataAccess);
        _endpointRepository = new EndpointRepository(dataAccess);
        _eventRepository = new EventRepository(dataAccess);
        _extensionService = extensionService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public void Startup()
    {
        _extensionService.AccessCredentialReceived += ExtensionServiceOnAccessCredentialReceived;
    }

    public void Shutdown()
    {
        _processAccessCredential.Clear();
            
        _extensionService.AccessCredentialReceived -= ExtensionServiceOnAccessCredentialReceived;
    }

    private void ExtensionServiceOnAccessCredentialReceived(object sender,
        AccessCredentialReceivedEventArgs eventArgs)
    {
        bool foundTask = _processAccessCredential.TryGetValue(eventArgs.Access.Id, out var existingTask);

        if (foundTask && existingTask is { Status: TaskStatus.WaitingForActivation })
        {
            return;
        }

        _processAccessCredential[eventArgs.Access.Id] = Task.Run(() => ProcessAccessRequest(eventArgs));
    }

    private async Task ProcessAccessRequest(AccessCredentialReceivedEventArgs eventArgs)
    {
        try
        {
            var endpoints = (await _endpointRepository.GetAll()).ToArray();

            var matchingDoor = await MatchingDoor(eventArgs.Access.Id, endpoints);

            if (matchingDoor == null)
            {
                _logger.LogInformation(
                    "Credential received from '{Name}' was not assigned to a door", eventArgs.Access.Name);
                return;
            }

            var accessPoint = endpoints.First(endpoint => endpoint.DriverEndpointId == eventArgs.Access.Id);

            var matchingDoorStrike = MatchingDoorStrike(matchingDoor.DoorStrikeEndpointId, endpoints);

            if (!eventArgs.Handler.IsValid())
            {
                return;
            }

            if (await IsAccessGranted(eventArgs.Handler.MatchingCardData, matchingDoor, accessPoint))
            {
                // Only open door if strike is assigned - community controllers may not have one
                if (matchingDoorStrike != null)
                {
                    await OpenDoor(eventArgs.Access, matchingDoorStrike, 3);
                }
                else
                {
                    _logger.LogInformation("Door '{Name}' access granted but no strike assigned to operate", matchingDoor.Name);
                    await eventArgs.Access.AccessGrantedNotification();
                }
            }
            else
            {
                await eventArgs.Access.AccessDeniedNotification();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to process access event");
        }
    }

    private async Task<bool> IsAccessGranted(string matchingCardData, Door matchingDoor, Endpoint accessPoint)
    {
        // First try matching by raw bit string
        var assignedCredential = await _credentialRepository.AssignedCredential(matchingCardData);

        // If not found and the card data looks like a bit string, try matching by decoded credential number
        if (assignedCredential == null && !string.IsNullOrEmpty(matchingCardData) &&
            matchingCardData.All(c => c == '0' || c == '1'))
        {
            // Try to decode as credential number - simple extraction for 35-bit format
            // Format: bits 0-1 = unused, bits 2-33 = 32-bit cred num, bit 34 = parity
            if (matchingCardData.Length == 35)
            {
                var credNumBits = matchingCardData.Substring(2, 32);
                var credNum = Convert.ToInt64(credNumBits, 2);
                assignedCredential = await _credentialRepository.AssignedCredential(credNum.ToString());
                if (assignedCredential != null)
                {
                    _logger.LogDebug("Matched credential by decoded 35-bit credNum: {CredNum}", credNum);
                }
            }
        }
        int eventId;
        if (assignedCredential?.Person == null)
        {
            _logger.LogInformation("Door '{Name}' badge requires enrollment", matchingDoor.Name);

            eventId = await _eventRepository.Insert(new Event
            {
                EndpointId = accessPoint.Id, Type = EventType.AccessDenied,
                Data = JsonSerializer.Serialize(new EventData
                {
                    Door = matchingDoor,
                    Endpoint = accessPoint,
                    EventReason = EventReason.CredentialNotEnrolled,
                    CardNumber = matchingCardData
                })
            });

            if (assignedCredential == null)
            {
                await _credentialRepository.Insert(new Credential
                    { Number = matchingCardData, LastEvent = eventId });
            }
            else
            {
                await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);
            }

            await _hubContext.Clients.All.SendAsync(Methods.NewEventReceived, eventId);

            // Notify external systems of access decision
            AccessDecisionMade?.Invoke(this, new AccessDecisionEventArgs
            {
                EndpointId = accessPoint.DriverEndpointId,
                IsGranted = false,
                CardNumber = matchingCardData,
                PersonName = null,
                Reason = EventReason.CredentialNotEnrolled
            });

            return false;
        }

        if (!AccessGranted())
        {
            _logger.LogInformation("Door '{Name}' denied access", matchingDoor.Name);

            eventId = await _eventRepository.Insert(new Event
            {
                EndpointId = accessPoint.Id, Type = EventType.AccessDenied,
                Data = JsonSerializer.Serialize(new EventData
                {
                    Door = matchingDoor,
                    Endpoint = accessPoint,
                    Person = assignedCredential.Person,
                    EventReason = EventReason.AccessNotAssigned,
                    CardNumber = matchingCardData
                })
            });
            await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);

            await _hubContext.Clients.All.SendAsync(Methods.NewEventReceived, eventId);

            // Notify external systems of access decision
            AccessDecisionMade?.Invoke(this, new AccessDecisionEventArgs
            {
                EndpointId = accessPoint.DriverEndpointId,
                IsGranted = false,
                CardNumber = matchingCardData,
                PersonName = assignedCredential.Person.FirstName,
                Reason = EventReason.AccessNotAssigned
            });

            return false;
        }

        _logger.LogInformation("Door '{Name}' granted access", matchingDoor.Name);

        eventId = await _eventRepository.Insert(new Event
        {
            EndpointId = accessPoint.Id, Type = EventType.AccessGranted,
            Data = JsonSerializer.Serialize(new EventData
            {
                Door = matchingDoor,
                Endpoint = accessPoint,
                Person = assignedCredential.Person,
                EventReason = EventReason.None,
                CardNumber = matchingCardData
            })
        });
        await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);

        await _hubContext.Clients.All.SendAsync(Methods.NewEventReceived, eventId);

        // Notify external systems of access decision
        AccessDecisionMade?.Invoke(this, new AccessDecisionEventArgs
        {
            EndpointId = accessPoint.DriverEndpointId,
            IsGranted = true,
            CardNumber = matchingCardData,
            PersonName = assignedCredential.Person.FirstName,
            Reason = EventReason.None
        });

        return true;
    }

    private async Task OpenDoor(IAccess access, Endpoint matchingDoorStrike, int strikeTimer)
    {
        var controlPoint =
            _extensionService.GetControlPoint(matchingDoorStrike.ExtensionId, matchingDoorStrike.DriverEndpointId); 
            
        async Task ControlStrike()
        {
            await controlPoint.SetState(true);
            await Task.Delay(TimeSpan.FromSeconds(strikeTimer));
            await controlPoint.SetState(false);
        } 

        var openDoorTasks = new[]
        {
            Task.Run(ControlStrike),
            Task.Run(access.AccessGrantedNotification)
        };

        await Task.WhenAll(openDoorTasks);
    }

    private static bool AccessGranted()
    {
        return true;
    }

    private async Task<Door> MatchingDoor(string accessPointId, Endpoint[] endpoints)
    {
        var doors = await _doorRepository.GetAll();

        var matchingDoor = doors.FirstOrDefault(door =>
            MatchingEndpointId(endpoints, accessPointId, door.InAccessEndpointId) ||
            MatchingEndpointId(endpoints, accessPointId, door.OutAccessEndpointId));
        return matchingDoor;
    }
        
    private static bool MatchingEndpointId(IEnumerable<Endpoint> endpoints, string accessPointId, int? endpointId)
    {
        if (endpointId == null) return false;
        return endpointId == endpoints.FirstOrDefault(endpoint => endpoint.DriverEndpointId == accessPointId)?.Id;
    }

    private static Endpoint MatchingDoorStrike(int? doorStrikeEndpointId, IEnumerable<Endpoint> endpoints)
    {
        return endpoints.FirstOrDefault(endpoint => doorStrikeEndpointId == endpoint.Id);
    }
}