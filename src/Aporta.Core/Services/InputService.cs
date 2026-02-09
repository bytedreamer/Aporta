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

public class InputService
{
    private readonly Z9DevRepository _z9DevRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;
    private readonly ExtensionService _extensionService;

    public InputService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext,
        ExtensionService extensionService)
    {
        _hubContext = hubContext;
        _extensionService = extensionService;
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
        // Only return user-assigned inputs (Enabled=true)
        var assignedDevs = devs.Where(d => d.Enabled);
        return assignedDevs.Select(DevToInput);
    }

    public async Task<Input> Get(int inputId)
    {
        var dev = await _z9DevRepository.Get(inputId);
        return dev == null ? null : DevToInput(dev);
    }

    public async Task Insert(Input input)
    {
        // EndpointId is now a z9_dev unid (from the pool)
        var poolDev = await _z9DevRepository.Get(input.EndpointId);
        if (poolDev == null)
            throw new InvalidOperationException($"Pool z9_dev {input.EndpointId} not found");

        // Assign: update name, clear DevPlatform to remove from pool
        poolDev.Name = input.Name;
        poolDev.Enabled = true;
        await _z9DevRepository.Upsert(poolDev);

        input.Id = poolDev.Unid;

        await _hubContext.Clients.All.SendAsync(Methods.InputInserted, input.Id);
    }

    public async Task Delete(int id)
    {
        await _z9DevRepository.Delete(id);

        await _hubContext.Clients.All.SendAsync(Methods.InputDeleted, id);
    }

    public async Task<IEnumerable<Endpoint>> AvailableMonitorPoints()
    {
        var availableSensors = await _z9DevRepository.GetAvailableByDevType(DevType.Sensor);
        var tasks = availableSensors.Select(d => DevToEndpoint(d, EndpointType.Input));
        return await Task.WhenAll(tasks);
    }

    public async Task<bool?> GetState(int inputId)
    {
        var dev = await _z9DevRepository.Get(inputId);
        var extensionId = await _z9DevRepository.GetExtensionId(dev);
        if (!extensionId.HasValue)
            return null;

        return await _extensionService.GetMonitorPoint(extensionId.Value, dev.ExternalId).GetState();
    }

    private static Input DevToInput(Dev dev)
    {
        return new Input
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
