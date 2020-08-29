using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
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

        public AccessService(IDataAccess dataAccess, ExtensionService extensionService, ILogger<AccessService> logger)
        {
            _extensionService = extensionService;
            _logger = logger;
            _doorRepository = new DoorRepository(dataAccess);
            _endpointRepository = new EndpointRepository(dataAccess);

            _extensionService.AccessCredentialReceived += ExtensionServiceOnAccessCredentialReceived;
        }
        
        private async void ExtensionServiceOnAccessCredentialReceived(object sender,
            AccessCredentialReceivedEventArgs eventArgs)
        {
            var endpoints = (await _endpointRepository.GetAll()).ToArray();
            
            var matchingDoor = await MatchingDoor(eventArgs.AccessPoint.Id, endpoints);

            if (matchingDoor == null)
            {
                _logger.LogInformation($"Credential received from {eventArgs.AccessPoint.Name} was not assigned to a door.");
                return;
            }

            var matchingDoorStrike = MatchingDoorStrike(matchingDoor.DoorStrikeEndpointId, endpoints);

            if (matchingDoorStrike == null)            
            {
                _logger.LogInformation($"Door {matchingDoor.Name} didn't have a strike assigned.");
                return;
            }
            
            if (EnrollMode())
            {
                _logger.LogInformation($"Door {matchingDoor.Name} enrolled badge.");
                return;
            }

            if (!AccessGranted())
            {
                _logger.LogInformation($"Door {matchingDoor.Name} denied access.");
                return;
            }

            _logger.LogInformation($"Door {matchingDoor.Name} granted access.");
            await OpenDoor(matchingDoorStrike, matchingDoor.StrikeTimer);
        }

        private bool EnrollMode()
        {
            return false;
        }

        private async Task OpenDoor(Endpoint matchingDoorStrike, int strikeTimer)
        {
            await _extensionService
                .GetControlPoint(matchingDoorStrike.ExtensionId, matchingDoorStrike.DriverEndpointId)
                .SetState(true);
            await Task.Delay(TimeSpan.FromSeconds(strikeTimer));
            await _extensionService
                .GetControlPoint(matchingDoorStrike.ExtensionId, matchingDoorStrike.DriverEndpointId)
                .SetState(false);
        }

        private bool AccessGranted()
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