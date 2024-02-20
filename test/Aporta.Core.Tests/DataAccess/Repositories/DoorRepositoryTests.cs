using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Models;
using Aporta.Shared.Models;
using NUnit.Framework;

namespace Aporta.Core.Tests.DataAccess.Repositories;

public class DoorRepositoryTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;
    private Guid _extensionId;
    private int _inAccessEndpointId;
    private int _outAccessEndpointId;
    private int _doorContactEndpointId;
    private int _requestToExitEndpointId;
    private int _doorStrikeEndpointId;

    [SetUp]
    public async Task Setup()
    {
        _persistConnection = _dataAccess.CreateDbConnection();
        _persistConnection.Open();

        await _dataAccess.UpdateSchema();

        _extensionId = Guid.NewGuid();
        var extensions = new[]
        {
            new ExtensionHost {Id = _extensionId, Name = "ExtensionTest", Enabled = false}
        };

        var extensionRepository = new ExtensionRepository(_dataAccess);
        foreach (var extension in extensions)
        {
            await extensionRepository.Insert(extension);
        }

        var inAccessEndpoint = new Endpoint
            {Name = "InAccessTest", Type = EndpointType.Reader, ExtensionId = _extensionId};
        var outAccessEndpoint = new Endpoint
            {Name = "OutAccessTest", Type = EndpointType.Reader, ExtensionId = _extensionId};
        var doorContactEndpoint = new Endpoint
            {Name = "DoorContactTest", Type = EndpointType.Input, ExtensionId = _extensionId};
        var requestToExitEndpoint = new Endpoint
            {Name = "RequestToExitTest", Type = EndpointType.Input, ExtensionId = _extensionId};
        var doorStrikeEndpoint = new Endpoint
            {Name = "DoorStrikeTest", Type = EndpointType.Output, ExtensionId = _extensionId};
            
        var endpointRepository = new EndpointRepository(_dataAccess);
            
        await endpointRepository.Insert(inAccessEndpoint);
        _inAccessEndpointId = inAccessEndpoint.Id;
        await endpointRepository.Insert(outAccessEndpoint);
        _outAccessEndpointId = outAccessEndpoint.Id;
        await endpointRepository.Insert(doorContactEndpoint);
        _doorContactEndpointId = doorContactEndpoint.Id;
        await endpointRepository.Insert(requestToExitEndpoint);
        _requestToExitEndpointId = requestToExitEndpoint.Id;
        await endpointRepository.Insert(doorStrikeEndpoint);
        _doorStrikeEndpointId = doorStrikeEndpoint.Id;
    }

    [TearDown]
    public void TearDown()
    {
        _persistConnection?.Close();
        _persistConnection?.Dispose();
    }

    [Test]
    public async Task Insert()
    {
        // Arrange
        var doors = new[]
        {
            new Door {Name = "DoorTest1", InAccessEndpointId = _inAccessEndpointId},
            new Door
            {
                Name = "DoorTest2", InAccessEndpointId = _inAccessEndpointId,
                OutAccessEndpointId = _outAccessEndpointId, DoorContactEndpointId = _doorContactEndpointId,
                RequestToExitEndpointId = _requestToExitEndpointId, DoorStrikeEndpointId = _doorStrikeEndpointId
            }
        };

        var doorRepository = new DoorRepository(_dataAccess);
        foreach (var door in doors)
        {
            await doorRepository.Insert(door);
        }

        // Act 
        var actualDoor = await doorRepository.Get(2);

        // Assert
        Assert.That(doors[1].Id, Is.EqualTo(2));
        Assert.That(actualDoor.Id, Is.EqualTo(2));
        Assert.That(actualDoor.Name, Is.EqualTo("DoorTest2"));
        Assert.That(actualDoor.InAccessEndpointId, Is.EqualTo(_inAccessEndpointId));
        Assert.That(actualDoor.OutAccessEndpointId, Is.EqualTo(_outAccessEndpointId));
        Assert.That(actualDoor.DoorContactEndpointId, Is.EqualTo(_doorContactEndpointId));
        Assert.That(actualDoor.RequestToExitEndpointId, Is.EqualTo(_requestToExitEndpointId));
        Assert.That(actualDoor.DoorStrikeEndpointId, Is.EqualTo(_doorStrikeEndpointId));
    }

    [Test]
    public async Task Delete()
    {
        // Arrange
        var doors = new[]
        {
            new Door {Name = "DoorTest1"},
            new Door {Name = "DoorTest2"}
        };
            
        var doorRepository = new DoorRepository(_dataAccess);
        foreach (var door in doors)
        {
            await doorRepository.Insert(door);   
        }

        // Act 
        await doorRepository.Delete(2);

        // Assert
        var actualDoors = await doorRepository.GetAll();
        Assert.That(1, Is.EqualTo(actualDoors.Count()));
    }
}