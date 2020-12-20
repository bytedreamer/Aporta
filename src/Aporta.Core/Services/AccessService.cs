using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly GlobalSettingService _globalSettingService;
        private readonly ILogger<AccessService> _logger;
        private readonly DoorRepository _doorRepository;
        private readonly EndpointRepository _endpointRepository;
        private readonly CredentialRepository _credentialRepository;
        private readonly IDataEncryption _dataEncryption;

        public AccessService(IDataAccess dataAccess, ExtensionService extensionService,
            GlobalSettingService globalSettingService, IDataEncryption dataEncryption, ILogger<AccessService> logger)
        {
            _extensionService = extensionService;
            _globalSettingService = globalSettingService;
            _logger = logger;
            _dataEncryption = dataEncryption;
            _doorRepository = new DoorRepository(dataAccess);
            _credentialRepository = new CredentialRepository(dataAccess);
            _endpointRepository = new EndpointRepository(dataAccess);
        }

        public void Startup()
        {
            _extensionService.AccessCredentialReceived += ExtensionServiceOnAccessCredentialReceived;
        }

        public void Shutdown()
        {
            _extensionService.AccessCredentialReceived -= ExtensionServiceOnAccessCredentialReceived;
        }

        private void ExtensionServiceOnAccessCredentialReceived(object sender,
            AccessCredentialReceivedEventArgs eventArgs)
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    var endpoints = (await _endpointRepository.GetAll()).ToArray();

                    var matchingDoor = await MatchingDoor(eventArgs.AccessPoint.Id, endpoints);

                    if (matchingDoor == null)
                    {
                        _logger.LogInformation(
                            $"Credential received from {eventArgs.AccessPoint.Name} was not assigned to a door.");
                        return;
                    }

                    var matchingDoorStrike = MatchingDoorStrike(matchingDoor.DoorStrikeEndpointId, endpoints);

                    if (matchingDoorStrike == null)
                    {
                        _logger.LogInformation($"Door {matchingDoor.Name} didn't have a strike assigned.");
                        return;
                    }

                    if (eventArgs.CardData.Count != eventArgs.BitCount)
                    {
                        _logger.LogInformation($"Door {matchingDoor.Name} card read doesn't match bit count.");
                        return;
                    }

                    var builder = new StringBuilder();
                    foreach (bool bit in eventArgs.CardData)
                    {
                        builder.Append(bit ? "1" : "0");
                    }

                    if (await RequiredEnrollment(builder.ToString()))
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
                    await OpenDoor(matchingDoorStrike, 3);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Unable to process access event.");
                }
            });
        }

        private async Task<bool> RequiredEnrollment(string cardNumber)
        {
            string hashedCardNumber =
                _dataEncryption.Hash(cardNumber, await _globalSettingService.GetCardNumberHashSalt());

            if (await _credentialRepository.AssignedCredential(hashedCardNumber) != null)
            {
                return false;
            }

            await _credentialRepository.Insert(new Credential
                {Number = hashedCardNumber});
            return true;
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