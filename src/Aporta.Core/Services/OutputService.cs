using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Extensions.Hardware;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace Aporta.Core.Services
{
    public class OutputService
    {
        private readonly DoorRepository _doorRepository;
        private readonly OutputRepository _outputRepository;
        private readonly EndpointRepository _endpointRepository;
        private readonly IHubContext<DataChangeNotificationHub> _hubContext;
        private readonly ExtensionService _extensionService;

        public OutputService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext,
            ExtensionService extensionService)
        {
            _hubContext = hubContext;
            _extensionService = extensionService;
            _doorRepository = new DoorRepository(dataAccess);
            _outputRepository = new OutputRepository(dataAccess);
            _endpointRepository = new EndpointRepository(dataAccess);

            _extensionService.StateChanged += ExtensionServiceOnOutputStateChanged;
        }

        private async void ExtensionServiceOnOutputStateChanged(object sender, StateChangedEventArgs eventArgs)
        {
            if (eventArgs.ControlPointState == null)
            {
                return;
            }
            
            try
            {
                var output = await _outputRepository.GetForDriverId(eventArgs.Endpoint.Id);

                await _hubContext.Clients.All.SendAsync(Methods.OutputStateChanged, output.Id, eventArgs.ControlPointState.NewState);
            }
            catch
            {
                // ignored
            }
        }

        public async Task<IEnumerable<Output>> GetAll()
        {
            return await _outputRepository.GetAll();
        }
        
        public async Task<Output> Get(int outputId)
        {
            return await _outputRepository.Get(outputId);
        }

        public async Task Insert(Output output)
        {
            await _outputRepository.Insert(output);
            
            await _hubContext.Clients.All.SendAsync(Methods.OutputInserted, output.Id);
        }

        public async Task Delete(int id)
        {
            await _outputRepository.Delete(id);
            
            await _hubContext.Clients.All.SendAsync(Methods.OutputDeleted, id);
        }

        public async Task<IEnumerable<Endpoint>> AvailableControlPoints()
        {
            var endpoints = await _endpointRepository.GetAll();
            var doors = await _doorRepository.GetAll();
            var outputs = await _outputRepository.GetAll();
            return endpoints.Where(endpoint =>
                endpoint.Type == EndpointType.Output &&
                !outputs.Select(output => output.EndpointId).Contains(endpoint.Id) &&
                !doors.Select(door => door.DoorStrikeEndpointId).Contains(endpoint.Id));
        }

        public async Task SetState(int outputId, bool state)
        {
            var output = await _outputRepository.Get(outputId);
            var endpoint = await _endpointRepository.Get(output.EndpointId);
            await _extensionService.GetControlPoint(endpoint.ExtensionId, endpoint.DriverEndpointId).SetState(state);
            
            await _hubContext.Clients.All.SendAsync(Methods.OutputStateChanged, output.Id, state);
        }

        public async Task<bool?> GetState(int outputId)
        {
            var output = await _outputRepository.Get(outputId);
            var endpoint = await _endpointRepository.Get(output.EndpointId);
            return await _extensionService.GetControlPoint(endpoint.ExtensionId, endpoint.DriverEndpointId).GetState();
        }
    }
}