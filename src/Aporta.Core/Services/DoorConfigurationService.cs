using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Aporta.Core.Services
{
    public class DoorConfigurationService
    {
        private readonly DoorRepository _doorRepository;
        private readonly EndpointRepository _endpointRepository;
        private readonly IHubContext<DataChangeNotificationHub> _hubContext;
        private readonly ILogger<DoorConfigurationService> _logger;

        public DoorConfigurationService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext,
            ILogger<DoorConfigurationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
            _doorRepository = new DoorRepository(dataAccess);
            _endpointRepository = new EndpointRepository(dataAccess);
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
            _logger.LogDebug($"Request to insert door {door.Name}");
            
            await _doorRepository.Insert(door);
            
            await _hubContext.Clients.All.SendAsync(Methods.DoorInserted, door.Id);
        }

        public async Task Delete(int id)
        {
            _logger.LogDebug($"Request to delete door with id of {id}");
            
            await _doorRepository.Delete(id);
            
            await _hubContext.Clients.All.SendAsync(Methods.DoorDeleted, id);
        }
    }
}