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

namespace Aporta.Core.Services;

public class InputService
{
    private readonly InputRepository _inputRepository;
    private readonly DoorRepository _doorRepository;
    private readonly EndpointRepository _endpointRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;
    private readonly ExtensionService _extensionService;

    public InputService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext,
        ExtensionService extensionService)
    {
        _hubContext = hubContext;
        _extensionService = extensionService;
        _doorRepository = new DoorRepository(dataAccess);
        _inputRepository = new InputRepository(dataAccess);
        _endpointRepository = new EndpointRepository(dataAccess);

        _extensionService.StateChanged += ExtensionServiceOnStateChanged;
    }

    private async void ExtensionServiceOnStateChanged(object sender, StateChangedEventArgs eventArgs)
    {
        try
        {
            var input = await _inputRepository.GetForDriverId(eventArgs.Endpoint.Id);

            await _hubContext.Clients.All.SendAsync(Methods.InputStateChanged, input.Id,
                eventArgs.State);
        }
        catch
        {
            // ignored
        }
    }

    public async Task<IEnumerable<Input>> GetAll()
    {
        return await _inputRepository.GetAll();
    }
        
    public async Task<Input> Get(int inputId)
    {
        return await _inputRepository.Get(inputId);
    }

    public async Task Insert(Input input)
    {
        await _inputRepository.Insert(input);
            
        await _hubContext.Clients.All.SendAsync(Methods.InputInserted, input.Id);
    }

    public async Task Delete(int id)
    {
        await _inputRepository.Delete(id);
            
        await _hubContext.Clients.All.SendAsync(Methods.InputDeleted, id);
    }

    public async Task<IEnumerable<Endpoint>> AvailableMonitorPoints()
    {
        var endpoints = await _endpointRepository.GetAll();
        var inputs = await _inputRepository.GetAll();
        var doors = (await _doorRepository.GetAll()).ToArray();
        return endpoints.Where(endpoint =>
            endpoint.Type == EndpointType.Input &&
            !inputs.Select(input => input.EndpointId).Contains(endpoint.Id) && 
            !doors.Select(door => door.DoorContactEndpointId).Contains(endpoint.Id) &&
            !doors.Select(door => door.RequestToExitEndpointId).Contains(endpoint.Id));
    }

    public async Task SetState(int inputId, bool state)
    {
        var input = await _inputRepository.Get(inputId);
        var endpoint = await _endpointRepository.Get(input.EndpointId);
        await _extensionService.GetMonitorPoint(endpoint.ExtensionId, endpoint.DriverEndpointId).SetState(state);

        await _hubContext.Clients.All.SendAsync(Methods.InputStateChanged, input.Id, state);
    }

    public async Task<bool?> GetState(int inputId)
    {
        var input = await _inputRepository.Get(inputId);
        var endpoint = await _endpointRepository.Get(input.EndpointId);
        return await _extensionService.GetMonitorPoint(endpoint.ExtensionId, endpoint.DriverEndpointId).GetState();
    }
}