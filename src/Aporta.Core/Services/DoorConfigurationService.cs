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
    private readonly EndpointRepository _endpointRepository = new(dataAccess);
    private readonly Z9DevRepository _z9DevRepository = new(dataAccess);

    public async Task<IEnumerable<Endpoint>> AvailableAccessPoints()
    {
        var endpoints = await _endpointRepository.GetAll();
        var credReaders = await _z9DevRepository.GetAllByDevType(DevType.CredReader);
        var credReaderExternalIds = credReaders
            .Where(d => !string.IsNullOrEmpty(d.ExternalId))
            .Select(d => d.ExternalId)
            .ToHashSet();
        return endpoints.Where(endpoint =>
            endpoint.Type == EndpointType.Reader &&
            !credReaderExternalIds.Contains(endpoint.DriverEndpointId));
    }

    public async Task<IEnumerable<Endpoint>> AvailableEndPoints()
    {
        var endpoints = await _endpointRepository.GetAll();
        var allDevs = (await _z9DevRepository.GetAll()).ToArray();
        var assignedExternalIds = allDevs
            .Where(d => !string.IsNullOrEmpty(d.ExternalId))
            .Select(d => d.ExternalId)
            .ToHashSet();
        return endpoints.Where(endpoint =>
            !assignedExternalIds.Contains(endpoint.DriverEndpointId));
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

        // Create CredReader child for in-access
        if (door.InAccessEndpointId.HasValue)
        {
            var endpoint = await _endpointRepository.Get(door.InAccessEndpointId.Value);
            if (endpoint != null)
            {
                var childUnid = await CreateChildDev(doorUnid, DevType.CredReader, DevUse.ActuatorDoorStrike,
                    endpoint.DriverEndpointId, endpoint.Name, setDevUse: false);
                doorDev.LogicalChildrenUnid.Add(childUnid);
            }
        }

        // Create exit Door child + CredReader grandchild for out-access
        if (door.OutAccessEndpointId.HasValue)
        {
            var endpoint = await _endpointRepository.Get(door.OutAccessEndpointId.Value);
            if (endpoint != null)
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

                var grandchildUnid = await CreateChildDev(exitDoorUnid, DevType.CredReader, DevUse.ActuatorDoorStrike,
                    endpoint.DriverEndpointId, endpoint.Name, setDevUse: false);
                exitDoorDev.LogicalChildrenUnid.Add(grandchildUnid);
                await _z9DevRepository.Upsert(exitDoorDev);
            }
        }

        // Create Sensor child for door contact
        if (door.DoorContactEndpointId.HasValue)
        {
            var endpoint = await _endpointRepository.Get(door.DoorContactEndpointId.Value);
            if (endpoint != null)
            {
                var childUnid = await CreateChildDev(doorUnid, DevType.Sensor, DevUse.SensorDoorContact,
                    endpoint.DriverEndpointId, endpoint.Name);
                doorDev.LogicalChildrenUnid.Add(childUnid);
            }
        }

        // Create Sensor child for REX
        if (door.RequestToExitEndpointId.HasValue)
        {
            var endpoint = await _endpointRepository.Get(door.RequestToExitEndpointId.Value);
            if (endpoint != null)
            {
                var childUnid = await CreateChildDev(doorUnid, DevType.Sensor, DevUse.SensorRex,
                    endpoint.DriverEndpointId, endpoint.Name);
                doorDev.LogicalChildrenUnid.Add(childUnid);
            }
        }

        // Create Actuator child for door strike
        if (door.DoorStrikeEndpointId.HasValue)
        {
            var endpoint = await _endpointRepository.Get(door.DoorStrikeEndpointId.Value);
            if (endpoint != null)
            {
                var childUnid = await CreateChildDev(doorUnid, DevType.Actuator, DevUse.ActuatorDoorStrike,
                    endpoint.DriverEndpointId, endpoint.Name);
                doorDev.LogicalChildrenUnid.Add(childUnid);
            }
        }

        // Update Door Dev with children
        await _z9DevRepository.Upsert(doorDev);
        door.Id = doorUnid;

        await hubContext.Clients.All.SendAsync(Methods.DoorInserted, door.Id);
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

    private async Task<int> CreateChildDev(int parentUnid, DevType devType, DevUse devUse,
        string externalId, string name, bool setDevUse = true)
    {
        var dev = new Dev
        {
            Name = name,
            DevType = devType,
            ExternalId = externalId,
            LogicalParentUnid = parentUnid,
        };
        if (setDevUse)
        {
            dev.DevUse = devUse;
        }
        SpCoreProtoUtil.InitRequired(dev);
        return await _z9DevRepository.Insert(dev);
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
                    // In-access reader
                    var inEndpoint = await _endpointRepository.GetByDriverEndpointId(child.ExternalId);
                    if (inEndpoint != null)
                        door.InAccessEndpointId = inEndpoint.Id;
                    break;

                case DevType.Door:
                    // Exit door — get its CredReader grandchild for out-access
                    var grandchildren = (await _z9DevRepository.GetChildren(child.Unid)).ToArray();
                    var outReader = grandchildren.FirstOrDefault(gc => gc.DevType == DevType.CredReader);
                    if (outReader != null)
                    {
                        var outEndpoint = await _endpointRepository.GetByDriverEndpointId(outReader.ExternalId);
                        if (outEndpoint != null)
                            door.OutAccessEndpointId = outEndpoint.Id;
                    }
                    break;

                case DevType.Sensor when child.DevUseCase == Dev.DevUseOneofCase.DevUse && child.DevUse == DevUse.SensorDoorContact:
                    var contactEndpoint = await _endpointRepository.GetByDriverEndpointId(child.ExternalId);
                    if (contactEndpoint != null)
                        door.DoorContactEndpointId = contactEndpoint.Id;
                    break;

                case DevType.Sensor when child.DevUseCase == Dev.DevUseOneofCase.DevUse && child.DevUse == DevUse.SensorRex:
                    var rexEndpoint = await _endpointRepository.GetByDriverEndpointId(child.ExternalId);
                    if (rexEndpoint != null)
                        door.RequestToExitEndpointId = rexEndpoint.Id;
                    break;

                case DevType.Actuator when child.DevUseCase == Dev.DevUseOneofCase.DevUse && child.DevUse == DevUse.ActuatorDoorStrike:
                    var strikeEndpoint = await _endpointRepository.GetByDriverEndpointId(child.ExternalId);
                    if (strikeEndpoint != null)
                        door.DoorStrikeEndpointId = strikeEndpoint.Id;
                    break;
            }
        }

        return door;
    }
}
