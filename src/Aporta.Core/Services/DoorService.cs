using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace Aporta.Core.Services
{
    public class DoorService
    {
        private readonly DoorRepository _doorRepository;
        private readonly EndpointRepository _endpointRepository;
        private readonly IHubContext<DataChangeNotificationHub> _hubContext;
        private readonly ExtensionService _extensionService;

        public DoorService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext,
            ExtensionService extensionService)
        {
            _hubContext = hubContext;
            _extensionService = extensionService;
            _doorRepository = new DoorRepository(dataAccess);
            _endpointRepository = new EndpointRepository(dataAccess);

            _extensionService.StateChanged += ExtensionServiceOnDoorStateChanged;
            _extensionService.AccessCredentialReceived += ExtensionServiceOnAccessCredentialReceived;
        }

        private void ExtensionServiceOnDoorStateChanged(object sender, StateChangedEventArgs eventArgs)
        {
            if (eventArgs.AccessPointState == null)
            {
                return;
            }
            
            try
            {
                //var output = await _outputRepository.GetForDriverId(eventArgs.Endpoint.Id);

                //await _hubContext.Clients.All.SendAsync(Methods.OutputStateChanged, output.Id, eventArgs.ControlPointState.NewState);
            }
            catch
            {
                // ignored
            }
        }

        private async void ExtensionServiceOnAccessCredentialReceived(object sender,
            AccessCredentialReceivedEventArgs eventArgs)
        {
            var endpoints =
                (await _endpointRepository.GetAll()).ToArray();
            var doors = await _doorRepository.GetAll();
            
            var matchingDoor = doors.FirstOrDefault(door =>
                endpoints.Any(endpoint => endpoint.DriverEndpointId == eventArgs.AccessPoint.Id));

            if (matchingDoor != null)
            {
                var matchingDoorStrike = endpoints.FirstOrDefault(endpoint => matchingDoor.DoorStrikeEndpointId == endpoint.Id);
                if (matchingDoorStrike != null)
                {
                    await _extensionService
                        .GetControlPoint(matchingDoorStrike.ExtensionId, matchingDoorStrike.DriverEndpointId)
                        .SetState(true);
                    await Task.Delay(TimeSpan.FromSeconds(matchingDoor.StrikeTimer));
                    await _extensionService
                        .GetControlPoint(matchingDoorStrike.ExtensionId, matchingDoorStrike.DriverEndpointId)
                        .SetState(false);
                }
            }
        }

        public async Task<IEnumerable<Endpoint>> AvailableAccessPoints()
        {
            var endpoints = await _endpointRepository.GetAll();
            var doors = (await _doorRepository.GetAll()).ToArray();
            return endpoints.Where(endpoint =>
                endpoint.Type == EndpointType.Reader &&
                !doors.Select(door => door.InAccessEndpointId).Contains(endpoint.Id) &&
                !doors.Select(door => door.OutAccessEndpointId).Contains(endpoint.Id));
        }

        public async Task<IEnumerable<Door>> GetAll()
        {
            return await _doorRepository.GetAll();
        }
        
        public async Task<Door> Get(int doorId)
        {
            return await _doorRepository.Get(doorId);
        }

        public async Task Insert(Door door)
        {
            await _doorRepository.Insert(door);
            
            await _hubContext.Clients.All.SendAsync(Methods.DoorInserted, door.Id);
        }

        public async Task Delete(int id)
        {
            await _doorRepository.Delete(id);
            
            await _hubContext.Clients.All.SendAsync(Methods.DoorDeleted, id);
        }
    }
}