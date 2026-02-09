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

public class OutputService
{
    private readonly EndpointRepository _endpointRepository;
    private readonly Z9DevRepository _z9DevRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;
    private readonly ExtensionService _extensionService;

    public OutputService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext,
        ExtensionService extensionService)
    {
        _hubContext = hubContext;
        _extensionService = extensionService;
        _endpointRepository = new EndpointRepository(dataAccess);
        _z9DevRepository = new Z9DevRepository(dataAccess);

        _extensionService.StateChanged += ExtensionServiceOnOutputStateChanged;
    }

    private async void ExtensionServiceOnOutputStateChanged(object sender, StateChangedEventArgs eventArgs)
    {
        try
        {
            var dev = await _z9DevRepository.GetForDriverId(eventArgs.Endpoint.Id);

            if (dev != null && dev.DevType == DevType.Actuator)
            {
                await _hubContext.Clients.All.SendAsync(Methods.OutputStateChanged, dev.Unid, eventArgs.State);
            }
        }
        catch
        {
            // ignored
        }
    }

    public async Task<IEnumerable<Output>> GetAll()
    {
        var devs = await _z9DevRepository.GetAllByDevType(DevType.Actuator);
        var tasks = devs.Select(async dev => await DevToOutput(dev));
        return await Task.WhenAll(tasks);
    }

    public async Task<Output> Get(int outputId)
    {
        var dev = await _z9DevRepository.Get(outputId);
        return dev == null ? null : await DevToOutput(dev);
    }

    public async Task Insert(Output output)
    {
        var endpoint = await _endpointRepository.Get(output.EndpointId);
        var dev = new Dev
        {
            Name = output.Name,
            DevType = DevType.Actuator,
            ExternalId = endpoint.DriverEndpointId,
        };
        SpCoreProtoUtil.InitRequired(dev);
        var id = await _z9DevRepository.Insert(dev);
        output.Id = id;

        await _hubContext.Clients.All.SendAsync(Methods.OutputInserted, output.Id);
    }

    public async Task Delete(int id)
    {
        await _z9DevRepository.Delete(id);

        await _hubContext.Clients.All.SendAsync(Methods.OutputDeleted, id);
    }

    public async Task<IEnumerable<Endpoint>> AvailableControlPoints()
    {
        var endpoints = await _endpointRepository.GetAll();
        var actuators = await _z9DevRepository.GetAllByDevType(DevType.Actuator);
        var actuatorExternalIds = actuators
            .Where(d => !string.IsNullOrEmpty(d.ExternalId))
            .Select(d => d.ExternalId)
            .ToHashSet();
        return endpoints.Where(endpoint =>
            endpoint.Type == EndpointType.Output &&
            !actuatorExternalIds.Contains(endpoint.DriverEndpointId));
    }

    public async Task SetState(int outputId, bool state)
    {
        var dev = await _z9DevRepository.Get(outputId);
        var endpoint = await _endpointRepository.GetByDriverEndpointId(dev.ExternalId);
        await _extensionService.GetControlPoint(endpoint.ExtensionId, endpoint.DriverEndpointId).SetState(state);

        await _hubContext.Clients.All.SendAsync(Methods.OutputStateChanged, dev.Unid, state);
    }

    public async Task<bool?> GetState(int outputId)
    {
        var dev = await _z9DevRepository.Get(outputId);
        var endpoint = await _endpointRepository.GetByDriverEndpointId(dev.ExternalId);
        return await _extensionService.GetControlPoint(endpoint.ExtensionId, endpoint.DriverEndpointId).GetState();
    }

    private async Task<Output> DevToOutput(Dev dev)
    {
        var endpointId = 0;
        if (!string.IsNullOrEmpty(dev.ExternalId))
        {
            var endpoint = await _endpointRepository.GetByDriverEndpointId(dev.ExternalId);
            if (endpoint != null)
                endpointId = endpoint.Id;
        }
        return new Output
        {
            Id = dev.Unid,
            Name = dev.Name,
            EndpointId = endpointId,
        };
    }
}
