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
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Aporta.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Aporta.Core.Services
{
    public class AccessService
    {
        private readonly ExtensionService _extensionService;
        private readonly ILogger<AccessService> _logger;
        private readonly DoorRepository _doorRepository;
        private readonly EndpointRepository _endpointRepository;
        private readonly CredentialRepository _credentialRepository;
        private readonly EventRepository _eventRepository;
        private readonly ConcurrentDictionary<string, Task> _processAccessCredential = new();

        public AccessService(IDataAccess dataAccess, ExtensionService extensionService, ILogger<AccessService> logger)
        {
            _doorRepository = new DoorRepository(dataAccess);
            _credentialRepository = new CredentialRepository(dataAccess);
            _endpointRepository = new EndpointRepository(dataAccess);
            _eventRepository = new EventRepository(dataAccess);
            _extensionService = extensionService;
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
            bool foundTask = _processAccessCredential.TryGetValue(eventArgs.AccessPoint.Id, out var existingTask);

            if (foundTask && existingTask is { Status: TaskStatus.WaitingForActivation })
            {
                return;
            }

            _processAccessCredential[eventArgs.AccessPoint.Id] = Task.Run(() => ProcessAccessRequest(eventArgs));
        }

        private async Task ProcessAccessRequest(AccessCredentialReceivedEventArgs eventArgs)
        {
            try
            {
                var endpoints = (await _endpointRepository.GetAll()).ToArray();

                var matchingDoor = await MatchingDoor(eventArgs.AccessPoint.Id, endpoints);

                if (matchingDoor == null)
                {
                    _logger.LogInformation(
                        "Credential received from '{Name}' was not assigned to a door", eventArgs.AccessPoint.Name);
                    return;
                }

                var accessPoint = endpoints.First(endpoint => endpoint.DriverEndpointId == eventArgs.AccessPoint.Id);

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

                if (await IsAccessGranted(eventArgs.CardData, matchingDoor, accessPoint)) return;

                await OpenDoor(eventArgs.AccessPoint, matchingDoorStrike, 3);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unable to process access event");
            }
        }

        private async Task<bool> IsAccessGranted(BitArray cardData, Door matchingDoor, Endpoint accessPoint)
        {
            var cardNumberBuilder = new StringBuilder();
            foreach (bool bit in cardData)
            {
                cardNumberBuilder.Append(bit ? "1" : "0");
            }

            var assignedCredential = await _credentialRepository.AssignedCredential(cardNumberBuilder.ToString());
            if (assignedCredential?.Person == null)
            {
                _logger.LogInformation("Door '{Name}' badge requires enrollment", matchingDoor.Name);

                int eventId = await _eventRepository.Insert(new Event
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
                        { Number = cardNumberBuilder.ToString(), LastEvent = eventId });
                }
                else
                {
                    await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);
                }

                return false;
            }

            if (!AccessGranted())
            {
                _logger.LogInformation("Door '{Name}' denied access", matchingDoor.Name);
                return false;
            }

            _logger.LogInformation("Door '{Name}' granted access", matchingDoor.Name);
            return true;
        }

        private async Task OpenDoor(IAccessPoint accessPoint, Endpoint matchingDoorStrike, int strikeTimer)
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
                Task.Run(accessPoint.Beep)
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
}