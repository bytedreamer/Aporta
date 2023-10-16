using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            _logger.LogInformation(
                "Credential with {BitCount} bits and raw data of {@CardData} received on door '{Name}'",
                eventArgs.BitCount, BuildRawBitString(eventArgs.CardData), eventArgs.Access.Name);

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

            if (matchingDoorStrike == null)
            {
                _logger.LogInformation("Door '{Name}' didn't have a strike assigned", matchingDoor.Name);
                return;
            }

            if (eventArgs.CardData.Count != eventArgs.BitCount)
            {
                _logger.LogInformation("Door '{Name}' card read doesn't match bit count", matchingDoor.Name);
                return;
            }

            if (await IsAccessGranted(eventArgs.CardData, matchingDoor, accessPoint))
            {
                await OpenDoor(eventArgs.Access, matchingDoorStrike, 3);
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

    private async Task<bool> IsAccessGranted(BitArray cardData, Door matchingDoor, Endpoint accessPoint)
    {
        var rawBitsString = BuildRawBitString(cardData);
        
        var assignedCredential = await _credentialRepository.AssignedCredential(rawBitsString);
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
                    EventReason = EventReason.CredentialNotEnrolled
                })
            });

            if (assignedCredential == null)
            {
                await _credentialRepository.Insert(new Credential
                    { Number = rawBitsString, LastEvent = eventId });
            }
            else
            {
                await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);
            }
            
            await _hubContext.Clients.All.SendAsync(Methods.NewEventReceived, eventId);

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
                    EventReason = EventReason.AccessNotAssigned
                })
            });
            await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);
            
            await _hubContext.Clients.All.SendAsync(Methods.NewEventReceived, eventId);
                
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
                EventReason = EventReason.None
            })
        });
        await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);
        
        await _hubContext.Clients.All.SendAsync(Methods.NewEventReceived, eventId);
            
        return true;
    }

    private static string BuildRawBitString(BitArray cardData)
    {
        var cardNumberBuilder = new StringBuilder();
        foreach (bool bit in cardData)
        {
            cardNumberBuilder.Append(bit ? "1" : "0");
        }

        return cardNumberBuilder.ToString();
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