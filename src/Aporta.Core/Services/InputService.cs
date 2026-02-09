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
using Z9.Protobuf;
using Z9.Spcore.Proto;

namespace Aporta.Core.Services;

public class InputService
{
    private readonly EndpointRepository _endpointRepository;
    private readonly Z9DevRepository _z9DevRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;
    private readonly ExtensionService _extensionService;

    public InputService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext,
        ExtensionService extensionService)
    {
        _hubContext = hubContext;
        _extensionService = extensionService;
        _endpointRepository = new EndpointRepository(dataAccess);
        _z9DevRepository = new Z9DevRepository(dataAccess);

        _extensionService.StateChanged += ExtensionServiceOnStateChanged;
    }

    private async void ExtensionServiceOnStateChanged(object sender, StateChangedEventArgs eventArgs)
    {
        try
        {
            var dev = await _z9DevRepository.GetForDriverId(eventArgs.Endpoint.Id);

            if (dev != null && dev.DevType == DevType.Sensor)
            {
                await _hubContext.Clients.All.SendAsync(Methods.InputStateChanged, dev.Unid,
                    eventArgs.State);
            }
        }
        catch
        {
            // ignored
        }
    }

    public async Task<IEnumerable<Input>> GetAll()
    {
        var devs = await _z9DevRepository.GetAllByDevType(DevType.Sensor);
        var tasks = devs.Select(async dev => await DevToInput(dev));
        return await Task.WhenAll(tasks);
    }

    public async Task<Input> Get(int inputId)
    {
        var dev = await _z9DevRepository.Get(inputId);
        return dev == null ? null : await DevToInput(dev);
    }

    public async Task Insert(Input input)
    {
        var endpoint = await _endpointRepository.Get(input.EndpointId);
        var dev = new Dev
        {
            Name = input.Name,
            DevType = DevType.Sensor,
            ExternalId = endpoint.DriverEndpointId,
        };
        SpCoreProtoUtil.InitRequired(dev);
        var id = await _z9DevRepository.Insert(dev);
        input.Id = id;

        await _hubContext.Clients.All.SendAsync(Methods.InputInserted, input.Id);
    }

    public async Task Delete(int id)
    {
        await _z9DevRepository.Delete(id);

        await _hubContext.Clients.All.SendAsync(Methods.InputDeleted, id);
    }

    public async Task<IEnumerable<Endpoint>> AvailableMonitorPoints()
    {
        var endpoints = await _endpointRepository.GetAll();
        var sensors = await _z9DevRepository.GetAllByDevType(DevType.Sensor);
        var sensorExternalIds = sensors
            .Where(d => !string.IsNullOrEmpty(d.ExternalId))
            .Select(d => d.ExternalId)
            .ToHashSet();
        return endpoints.Where(endpoint =>
            endpoint.Type == EndpointType.Input &&
            !sensorExternalIds.Contains(endpoint.DriverEndpointId));
    }

    public async Task<bool?> GetState(int inputId)
    {
        var dev = await _z9DevRepository.Get(inputId);
        var endpoint = await _endpointRepository.GetByDriverEndpointId(dev.ExternalId);
        return await _extensionService.GetMonitorPoint(endpoint.ExtensionId, dev.ExternalId).GetState();
    }

    private async Task<Input> DevToInput(Dev dev)
    {
        var endpointId = 0;
        if (!string.IsNullOrEmpty(dev.ExternalId))
        {
            var endpoint = await _endpointRepository.GetByDriverEndpointId(dev.ExternalId);
            if (endpoint != null)
                endpointId = endpoint.Id;
        }
        return new Input
        {
            Id = dev.Unid,
            Name = dev.Name,
            EndpointId = endpointId,
        };
    }
}
