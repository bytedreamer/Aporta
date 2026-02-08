using System;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Services;
using Aporta.Shared.Models;
using NUnit.Framework;
using Z9.Spcore.Proto;

namespace Aporta.Core.Tests.Services;

public class EventServiceTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;

    [SetUp]
    public async Task Setup()
    {
        _persistConnection = _dataAccess.CreateDbConnection();
        _persistConnection.Open();

        await _dataAccess.UpdateSchema();
    }

    [TearDown]
    public void TearDown()
    {
        _persistConnection?.Close();
        _persistConnection?.Dispose();
    }

    [Test]
    public async Task Get_ReturnsCorrectEventDto()
    {
        // Arrange
        var repository = new Z9EvtRepository(_dataAccess);
        var service = new EventService(_dataAccess);

        var eventData = new EventData
        {
            EventReason = EventReason.None,
            Endpoint = new Endpoint { Id = 1, Name = "Reader 1", DriverEndpointId = "ep1", ExtensionId = Guid.Empty }
        };
        var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var evt = new Evt
        {
            EvtCode = EvtCode.DoorAccessGranted,
            HwTime = new DateTimeData { Millis = nowMillis },
            DbTime = new DateTimeData { Millis = nowMillis },
            Consumed = false,
            Priority = 0,
            Data = JsonSerializer.Serialize(eventData)
        };

        int id = await repository.Insert(evt);

        // Act
        var result = await service.Get(id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(id));
        Assert.That(result.Type, Is.EqualTo(EventType.AccessGranted));
        Assert.That(result.Data, Is.Not.Null.And.Not.Empty);

        var roundTrippedData = JsonSerializer.Deserialize<EventData>(result.Data);
        Assert.That(roundTrippedData.EventReason, Is.EqualTo(EventReason.None));

        // Verify timestamp is close to now
        var expectedTime = DateTimeOffset.FromUnixTimeMilliseconds(nowMillis).UtcDateTime;
        Assert.That(result.Timestamp, Is.EqualTo(expectedTime));
    }

    [Test]
    public async Task Get_DeniedEvent_ReturnsCorrectType()
    {
        // Arrange
        var repository = new Z9EvtRepository(_dataAccess);
        var service = new EventService(_dataAccess);

        var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var evt = new Evt
        {
            EvtCode = EvtCode.DoorAccessDenied,
            EvtSubCode = EvtSubCode.AccessDeniedExpired,
            HwTime = new DateTimeData { Millis = nowMillis },
            DbTime = new DateTimeData { Millis = nowMillis },
            Data = JsonSerializer.Serialize(new EventData
            {
                EventReason = EventReason.CredentialExpired,
                Endpoint = new Endpoint { Id = 1, Name = "Reader 1", DriverEndpointId = "ep1", ExtensionId = Guid.Empty }
            })
        };

        int id = await repository.Insert(evt);

        // Act
        var result = await service.Get(id);

        // Assert
        Assert.That(result.Type, Is.EqualTo(EventType.AccessDenied));
    }

    [Test]
    public async Task Get_NonExistentId_ReturnsNull()
    {
        // Arrange
        var service = new EventService(_dataAccess);

        // Act
        var result = await service.Get(999);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetAll_ReturnsPaginatedEvents()
    {
        // Arrange
        var repository = new Z9EvtRepository(_dataAccess);
        var service = new EventService(_dataAccess);

        for (int i = 0; i < 5; i++)
        {
            var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + i;
            await repository.Insert(new Evt
            {
                EvtCode = EvtCode.DoorAccessGranted,
                HwTime = new DateTimeData { Millis = nowMillis },
                DbTime = new DateTimeData { Millis = nowMillis },
                Data = JsonSerializer.Serialize(new EventData
                {
                    EventReason = EventReason.None,
                    Endpoint = new Endpoint { Id = 1, Name = "Reader 1", DriverEndpointId = "ep1", ExtensionId = Guid.Empty }
                })
            });
        }

        // Act
        var page = await service.GetAll(1, 3);

        // Assert
        Assert.That(page.TotalItems, Is.EqualTo(5));
        Assert.That(page.PageNumber, Is.EqualTo(1));
        Assert.That(page.PageSize, Is.EqualTo(3));
        var items = page.Items.ToList();
        Assert.That(items, Has.Count.EqualTo(3));
        Assert.That(items.All(e => e.Type == EventType.AccessGranted), Is.True);
    }
}
