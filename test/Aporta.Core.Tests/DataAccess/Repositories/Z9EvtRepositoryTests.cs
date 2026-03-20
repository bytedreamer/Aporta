using System;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models;
using NUnit.Framework;
using Z9.Spcore.Proto;

namespace Aporta.Core.Tests.DataAccess.Repositories;

public class Z9EvtRepositoryTests
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
    public async Task Insert()
    {
        // Arrange
        var repository = new Z9EvtRepository(_dataAccess);

        var evt1 = new Evt
        {
            EvtCode = EvtCode.DoorAccessGranted,
            HwTime = new DateTimeData { Millis = 1000 },
            DbTime = new DateTimeData { Millis = 1000 },
            Priority = 0
        };
        var evt2 = new Evt
        {
            EvtCode = EvtCode.DoorAccessDenied,
            HwTime = new DateTimeData { Millis = 2000 },
            DbTime = new DateTimeData { Millis = 2000 },
            Priority = 0
        };

        // Act
        int id1 = await repository.Insert(evt1);
        int id2 = await repository.Insert(evt2);

        // Assert — auto-increment IDs
        Assert.That(id1, Is.EqualTo(1));
        Assert.That(id2, Is.EqualTo(2));
        Assert.That(evt1.Unid, Is.EqualTo(1));
        Assert.That(evt2.Unid, Is.EqualTo(2));

        // Verify round-trip via Get
        var retrieved = await repository.Get(id1);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.EvtCode, Is.EqualTo(EvtCode.DoorAccessGranted));
        Assert.That(retrieved.HwTime.Millis, Is.EqualTo(1000));
    }

    [Test]
    public async Task GetAll_Paginated()
    {
        // Arrange
        var repository = new Z9EvtRepository(_dataAccess);

        for (int i = 0; i < 5; i++)
        {
            await repository.Insert(new Evt
            {
                EvtCode = EvtCode.DoorAccessGranted,
                HwTime = new DateTimeData { Millis = i * 1000 },
                DbTime = new DateTimeData { Millis = i * 1000 },
                Priority = 0
            });
        }

        // Act — page 1, size 2 (should get IDs 5,4 since ORDER BY id DESC)
        var page1 = await repository.GetAll(1, 2);

        // Assert
        Assert.That(page1.TotalItems, Is.EqualTo(5));
        Assert.That(page1.PageNumber, Is.EqualTo(1));
        Assert.That(page1.PageSize, Is.EqualTo(2));
        var page1Items = page1.Items.ToList();
        Assert.That(page1Items, Has.Count.EqualTo(2));
        Assert.That(page1Items[0].HwTime.Millis, Is.EqualTo(4000)); // most recent first
        Assert.That(page1Items[1].HwTime.Millis, Is.EqualTo(3000));

        // Act — page 3, size 2 (should get 1 item: ID 1)
        var page3 = await repository.GetAll(3, 2);
        var page3Items = page3.Items.ToList();
        Assert.That(page3Items, Has.Count.EqualTo(1));
        Assert.That(page3Items[0].HwTime.Millis, Is.EqualTo(0));
    }

    [Test]
    public async Task Delete()
    {
        // Arrange
        var repository = new Z9EvtRepository(_dataAccess);

        var evt = new Evt
        {
            EvtCode = EvtCode.CredReaderOnline,
            HwTime = new DateTimeData { Millis = 1000 },
            DbTime = new DateTimeData { Millis = 1000 }
        };
        int id = await repository.Insert(evt);

        // Act
        await repository.Delete(id);

        // Assert
        Assert.That(await repository.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Count()
    {
        // Arrange
        var repository = new Z9EvtRepository(_dataAccess);
        Assert.That(await repository.Count(), Is.EqualTo(0));

        // Act
        await repository.Insert(new Evt { EvtCode = EvtCode.DoorAccessGranted });
        await repository.Insert(new Evt { EvtCode = EvtCode.DoorAccessDenied });
        await repository.Insert(new Evt { EvtCode = EvtCode.DoorUnlocked });

        // Assert
        Assert.That(await repository.Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task Insert_WithEventDataJson_RoundTrips()
    {
        // Arrange
        var repository = new Z9EvtRepository(_dataAccess);

        var eventData = new EventData
        {
            EventReason = EventReason.CredentialExpired,
            Endpoint = new Endpoint { Id = 1, Name = "Reader 1", DriverEndpointId = "ep1", ExtensionId = Guid.Empty },
            CardNumber = "12345"
        };
        var eventDataJson = JsonSerializer.Serialize(eventData);

        var evt = new Evt
        {
            EvtCode = EvtCode.DoorAccessDenied,
            EvtSubCode = EvtSubCode.AccessDeniedExpired,
            HwTime = new DateTimeData { Millis = 1000 },
            DbTime = new DateTimeData { Millis = 1000 },
            Data = eventDataJson
        };

        // Act
        int id = await repository.Insert(evt);
        var retrieved = await repository.Get(id);

        // Assert
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Data, Is.EqualTo(eventDataJson));
        Assert.That(retrieved.EvtCode, Is.EqualTo(EvtCode.DoorAccessDenied));
        Assert.That(retrieved.EvtSubCode, Is.EqualTo(EvtSubCode.AccessDeniedExpired));

        var roundTrippedData = JsonSerializer.Deserialize<EventData>(retrieved.Data);
        Assert.That(roundTrippedData.EventReason, Is.EqualTo(EventReason.CredentialExpired));
        Assert.That(roundTrippedData.CardNumber, Is.EqualTo("12345"));
    }
}
