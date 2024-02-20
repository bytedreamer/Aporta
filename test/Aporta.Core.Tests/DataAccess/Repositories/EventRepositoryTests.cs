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

public class EventRepositoryTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;
    private Guid _extensionId;
    private int _accessEndpoint1Id;
    private int _accessEndpoint2Id;

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
            
        var accessEndpoint1 = new Endpoint
            {Name = "AccessTest1", Type = EndpointType.Reader, ExtensionId = _extensionId};
        var accessEndpoint2 = new Endpoint
            {Name = "AccessTest2", Type = EndpointType.Reader, ExtensionId = _extensionId};
            
        var endpointRepository = new EndpointRepository(_dataAccess);
            
        await endpointRepository.Insert(accessEndpoint1);
        _accessEndpoint1Id = accessEndpoint1.Id;
        await endpointRepository.Insert(accessEndpoint2);
        _accessEndpoint2Id = accessEndpoint2.Id;
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
        var events = new[]
        {
            new Event {EndpointId = _accessEndpoint1Id, Timestamp = new DateTime(2022, 1, 1), Type = EventType.AccessDenied},
            new Event {EndpointId = _accessEndpoint2Id, Timestamp = new DateTime(2024, 4, 4), Type = EventType.AccessGranted, Data = "{}"},
        };

        var eventRepository = new EventRepository(_dataAccess);
        foreach (var @event in events)
        {
            await eventRepository.Insert(@event);
        }

        // Act 
        var actualEvent = await eventRepository.Get(2);

        // Assert
        Assert.That(events[1].Id, Is.EqualTo(2));
        Assert.That(actualEvent.Id, Is.EqualTo(2));
        Assert.That(actualEvent.EndpointId, Is.EqualTo(_accessEndpoint2Id));
        Assert.That(actualEvent.Timestamp, Is.EqualTo(new DateTime(2024, 4, 4)));
        Assert.That(actualEvent.Type, Is.EqualTo(EventType.AccessGranted));
        Assert.That(actualEvent.Data, Is.EqualTo("{}"));
    }

    [Test]
    public async Task Delete()
    {
        // Arrange
        var events = new[]
        {
            new Event
            {
                EndpointId = _accessEndpoint1Id, Timestamp = new DateTime(2022, 1, 1), Type = EventType.AccessDenied
            },
            new Event
            {
                EndpointId = _accessEndpoint2Id, Timestamp = new DateTime(2024, 4, 4),
                Type = EventType.AccessGranted, Data = "{}"
            },
        };

        var eventRepository = new EventRepository(_dataAccess);
        foreach (var @event in events)
        {
            await eventRepository.Insert(@event);
        }

        // Act 
        await eventRepository.Delete(2);

        // Assert
        var actualEvents = await eventRepository.GetAll();
        Assert.That(1, Is.EqualTo(actualEvents.Count()));
    }
}