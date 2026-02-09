using System;
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
using Z9.Protobuf;
using Z9.Spcore.Proto;

using Door = Aporta.Shared.Models.Door;

namespace Aporta.Core.Services;

public class DoorConfigurationService(
    IDataAccess dataAccess,
    IHubContext<DataChangeNotificationHub> hubContext,
    ILogger<DoorConfigurationService> logger)
{
    private readonly Z9DevRepository _z9DevRepository = new(dataAccess);

    public async Task<IEnumerable<Endpoint>> AvailableAccessPoints()
    {
        var available = await _z9DevRepository.GetAvailableByDevType(DevType.CredReader);
        var tasks = available.Select(async d =>
        {
            var extensionId = await _z9DevRepository.GetExtensionId(d);
            return new Endpoint
            {
                Id = d.Unid,
                Name = d.Name,
                DriverEndpointId = d.ExternalId,
                ExtensionId = extensionId ?? Guid.Empty,
                Type = EndpointType.Reader,
            };
        });
        return await Task.WhenAll(tasks);
    }

    public async Task<IEnumerable<Endpoint>> AvailableEndPoints()
    {
        var allAvailable = (await _z9DevRepository.GetAll())
            .Where(d => d.DevPlatformCase == Dev.DevPlatformOneofCase.DevPlatform &&
                        d.DevPlatform == DevPlatform.External &&
                        d.DevType != DevType.IoController);
        var tasks = allAvailable.Select(async d =>
        {
            var extensionId = await _z9DevRepository.GetExtensionId(d);
            var type = d.DevType switch
            {
                DevType.Actuator => EndpointType.Output,
                DevType.Sensor => EndpointType.Input,
                DevType.CredReader => EndpointType.Reader,
                _ => EndpointType.Output,
            };
            return new Endpoint
            {
                Id = d.Unid,
                Name = d.Name,
                DriverEndpointId = d.ExternalId,
                ExtensionId = extensionId ?? Guid.Empty,
                Type = type,
            };
        });
        return await Task.WhenAll(tasks);
    }

    public async Task<IEnumerable<Door>> GetAll()
    {
        var allDevs = (await _z9DevRepository.GetAllByDevType(DevType.Door)).ToArray();
        // Top-level doors: those with no logical parent, or whose parent is not a Door
        var topLevelDoors = new List<Dev>();
        foreach (var dev in allDevs)
        {
            if (dev.LogicalParentUnidCase != Dev.LogicalParentUnidOneofCase.LogicalParentUnid)
            {
                topLevelDoors.Add(dev);
                continue;
            }
            var parent = await _z9DevRepository.Get(dev.LogicalParentUnid);
            if (parent == null || parent.DevType != DevType.Door)
            {
                topLevelDoors.Add(dev);
            }
        }
        var doors = new List<Door>();
        foreach (var dev in topLevelDoors)
        {
            doors.Add(await BuildDoorDto(dev));
        }
        return doors;
    }

    public async Task<Door> Get(int doorId)
    {
        var dev = await _z9DevRepository.Get(doorId);
        if (dev == null || dev.DevType != DevType.Door) return null;
        return await BuildDoorDto(dev);
    }

    public async Task Insert(Door door)
    {
        logger.LogDebug("Request to insert door {Name}", door.Name);

        // Create the Door Dev
        var doorDev = new Dev
        {
            Name = door.Name,
            DevType = DevType.Door,
        };
        SpCoreProtoUtil.InitRequired(doorDev);
        var doorUnid = await _z9DevRepository.Insert(doorDev);

        // Assign CredReader child for in-access (from pool z9_dev)
        if (door.InAccessEndpointId.HasValue)
        {
            var childUnid = await AssignPoolDevAsDoorChild(
                door.InAccessEndpointId.Value, doorUnid, DevUse.ActuatorDoorStrike, setDevUse: false);
            if (childUnid.HasValue)
                doorDev.LogicalChildrenUnid.Add(childUnid.Value);
        }

        // Create exit Door child + CredReader grandchild for out-access
        if (door.OutAccessEndpointId.HasValue)
        {
            var poolDev = await _z9DevRepository.Get(door.OutAccessEndpointId.Value);
            if (poolDev != null)
            {
                var exitDoorDev = new Dev
                {
                    Name = $"{door.Name} - Exit",
                    DevType = DevType.Door,
                    LogicalParentUnid = doorUnid,
                };
                SpCoreProtoUtil.InitRequired(exitDoorDev);
                var exitDoorUnid = await _z9DevRepository.Insert(exitDoorDev);
                doorDev.LogicalChildrenUnid.Add(exitDoorUnid);

                var grandchildUnid = await AssignPoolDevAsDoorChild(
                    door.OutAccessEndpointId.Value, exitDoorUnid, DevUse.ActuatorDoorStrike, setDevUse: false);
                if (grandchildUnid.HasValue)
                {
                    exitDoorDev.LogicalChildrenUnid.Add(grandchildUnid.Value);
                    await _z9DevRepository.Upsert(exitDoorDev);
                }
            }
        }

        // Assign Sensor child for door contact (from pool z9_dev)
        if (door.DoorContactEndpointId.HasValue)
        {
            var childUnid = await AssignPoolDevAsDoorChild(
                door.DoorContactEndpointId.Value, doorUnid, DevUse.SensorDoorContact);
            if (childUnid.HasValue)
                doorDev.LogicalChildrenUnid.Add(childUnid.Value);
        }

        // Assign Sensor child for REX (from pool z9_dev)
        if (door.RequestToExitEndpointId.HasValue)
        {
            var childUnid = await AssignPoolDevAsDoorChild(
                door.RequestToExitEndpointId.Value, doorUnid, DevUse.SensorRex);
            if (childUnid.HasValue)
                doorDev.LogicalChildrenUnid.Add(childUnid.Value);
        }

        // Assign Actuator child for door strike (from pool z9_dev)
        if (door.DoorStrikeEndpointId.HasValue)
        {
            var childUnid = await AssignPoolDevAsDoorChild(
                door.DoorStrikeEndpointId.Value, doorUnid, DevUse.ActuatorDoorStrike);
            if (childUnid.HasValue)
                doorDev.LogicalChildrenUnid.Add(childUnid.Value);
        }

        // Update Door Dev with children
        await _z9DevRepository.Upsert(doorDev);
        door.Id = doorUnid;

        await hubContext.Clients.All.SendAsync(Methods.DoorInserted, door.Id);
    }

    /// <summary>
    /// Assigns a pool z9_dev as a door child by setting logicalParentUnid and clearing DevPlatform.
    /// Returns the z9_dev unid, or null if the pool dev was not found.
    /// </summary>
    private async Task<int?> AssignPoolDevAsDoorChild(int poolDevUnid, int doorUnid, DevUse devUse, bool setDevUse = true)
    {
        var poolDev = await _z9DevRepository.Get(poolDevUnid);
        if (poolDev == null) return null;

        poolDev.LogicalParentUnid = doorUnid;
        poolDev.DevPlatform = DevPlatform.Z9Security; // Clear the External marker
        if (setDevUse)
        {
            poolDev.DevUse = devUse;
        }
        await _z9DevRepository.Upsert(poolDev);
        return poolDev.Unid;
    }

    public async Task Delete(int id)
    {
        logger.LogDebug("Request to delete door with id of {Id}", id);

        var doorDev = await _z9DevRepository.Get(id);
        if (doorDev != null)
        {
            // Delete children recursively
            var children = await _z9DevRepository.GetChildren(id);
            foreach (var child in children)
            {
                // If child is an exit door, delete its grandchildren too
                if (child.DevType == DevType.Door)
                {
                    var grandchildren = await _z9DevRepository.GetChildren(child.Unid);
                    foreach (var grandchild in grandchildren)
                    {
                        await _z9DevRepository.Delete(grandchild.Unid);
                    }
                }
                await _z9DevRepository.Delete(child.Unid);
            }
            await _z9DevRepository.Delete(id);
        }

        await hubContext.Clients.All.SendAsync(Methods.DoorDeleted, id);
    }

    internal async Task<Door> BuildDoorDto(Dev doorDev)
    {
        var door = new Door
        {
            Id = doorDev.Unid,
            Name = doorDev.Name,
        };

        var children = (await _z9DevRepository.GetChildren(doorDev.Unid)).ToArray();

        foreach (var child in children)
        {
            switch (child.DevType)
            {
                case DevType.CredReader:
                    // In-access reader — EndpointId is the z9_dev unid
                    door.InAccessEndpointId = child.Unid;
                    break;

                case DevType.Door:
                    // Exit door — get its CredReader grandchild for out-access
                    var grandchildren = (await _z9DevRepository.GetChildren(child.Unid)).ToArray();
                    var outReader = grandchildren.FirstOrDefault(gc => gc.DevType == DevType.CredReader);
                    if (outReader != null)
                        door.OutAccessEndpointId = outReader.Unid;
                    break;

                case DevType.Sensor when child.DevUseCase == Dev.DevUseOneofCase.DevUse && child.DevUse == DevUse.SensorDoorContact:
                    door.DoorContactEndpointId = child.Unid;
                    break;

                case DevType.Sensor when child.DevUseCase == Dev.DevUseOneofCase.DevUse && child.DevUse == DevUse.SensorRex:
                    door.RequestToExitEndpointId = child.Unid;
                    break;

                case DevType.Actuator when child.DevUseCase == Dev.DevUseOneofCase.DevUse && child.DevUse == DevUse.ActuatorDoorStrike:
                    door.DoorStrikeEndpointId = child.Unid;
                    break;
            }
        }

        return door;
    }
}
