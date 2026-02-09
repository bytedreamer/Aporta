using System;
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
using Z9.Spcore.Proto;

namespace Aporta.Core.Services;

public class OutputService
{
    private readonly Z9DevRepository _z9DevRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;
    private readonly ExtensionService _extensionService;

    public OutputService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext,
        ExtensionService extensionService)
    {
        _hubContext = hubContext;
        _extensionService = extensionService;
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
        // Exclude pool z9_devs (DevPlatform=External) — only return user-assigned outputs
        var assignedDevs = devs.Where(d =>
            d.DevPlatformCase != Dev.DevPlatformOneofCase.DevPlatform ||
            d.DevPlatform != DevPlatform.External);
        return assignedDevs.Select(DevToOutput);
    }

    public async Task<Output> Get(int outputId)
    {
        var dev = await _z9DevRepository.Get(outputId);
        return dev == null ? null : DevToOutput(dev);
    }

    public async Task Insert(Output output)
    {
        // EndpointId is now a z9_dev unid (from the pool)
        var poolDev = await _z9DevRepository.Get(output.EndpointId);
        if (poolDev == null)
            throw new InvalidOperationException($"Pool z9_dev {output.EndpointId} not found");

        // Assign: update name, clear DevPlatform to remove from pool
        poolDev.Name = output.Name;
        poolDev.DevPlatform = DevPlatform.Z9Security; // Clear the External marker
        await _z9DevRepository.Upsert(poolDev);

        output.Id = poolDev.Unid;

        await _hubContext.Clients.All.SendAsync(Methods.OutputInserted, output.Id);
    }

    public async Task Delete(int id)
    {
        await _z9DevRepository.Delete(id);

        await _hubContext.Clients.All.SendAsync(Methods.OutputDeleted, id);
    }

    public async Task<IEnumerable<Endpoint>> AvailableControlPoints()
    {
        var availableActuators = await _z9DevRepository.GetAvailableByDevType(DevType.Actuator);
        var tasks = availableActuators.Select(d => DevToEndpoint(d, EndpointType.Output));
        return await Task.WhenAll(tasks);
    }

    public async Task SetState(int outputId, bool state)
    {
        var dev = await _z9DevRepository.Get(outputId);
        var extensionId = await _z9DevRepository.GetExtensionId(dev);
        if (!extensionId.HasValue)
            throw new InvalidOperationException($"No extension ID found for z9_dev {outputId}");

        await _extensionService.GetControlPoint(extensionId.Value, dev.ExternalId).SetState(state);

        await _hubContext.Clients.All.SendAsync(Methods.OutputStateChanged, dev.Unid, state);
    }

    public async Task<bool?> GetState(int outputId)
    {
        var dev = await _z9DevRepository.Get(outputId);
        var extensionId = await _z9DevRepository.GetExtensionId(dev);
        if (!extensionId.HasValue)
            return null;

        return await _extensionService.GetControlPoint(extensionId.Value, dev.ExternalId).GetState();
    }

    private static Output DevToOutput(Dev dev)
    {
        return new Output
        {
            Id = dev.Unid,
            Name = dev.Name,
            EndpointId = dev.Unid,
        };
    }

    private async Task<Endpoint> DevToEndpoint(Dev dev, EndpointType type)
    {
        var extensionId = await _z9DevRepository.GetExtensionId(dev);
        return new Endpoint
        {
            Id = dev.Unid,
            Name = dev.Name,
            DriverEndpointId = dev.ExternalId,
            ExtensionId = extensionId ?? Guid.Empty,
            Type = type,
        };
    }
}
