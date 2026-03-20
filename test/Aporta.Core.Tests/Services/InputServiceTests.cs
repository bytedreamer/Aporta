using System;
using System.Data;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;
using NUnit.Framework;
using SignalR_UnitTestingSupportCommon.IHubContextSupport;

using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Core.Services;
using Aporta.Extensions;
using Aporta.Shared.Messaging;
using Z9.Spcore.Proto;

namespace Aporta.Core.Tests.Services;

[TestFixture]
public class InputServiceTests
{
    private readonly Guid _extensionId = Guid.Parse("225B748E-FB15-4428-92F7-218BB4CC2813");
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private readonly ILoggerFactory _loggerFactory = new NullLoggerFactory();
    private ExtensionService _extensionService;
    private IDbConnection _persistConnection;

    [SetUp]
    public async Task Setup()
    {
        _persistConnection = _dataAccess.CreateDbConnection();
        _persistConnection.Open();

        await _dataAccess.UpdateSchema();
        _extensionService = new ExtensionService(_dataAccess,
            new UnitTestingSupportForIHubContext<DataChangeNotificationHub>().IHubContextMock.Object,
            new Mock<IDataEncryption>().Object,
            _loggerFactory.CreateLogger<ExtensionService>(),
            _loggerFactory){CurrentDirectory = Environment.CurrentDirectory};
        await _extensionService.Startup();
        await _extensionService.EnableExtension(_extensionId, true);

        // Wait for pool z9_devs to be synced from driver (1 controller + 5 endpoints)
        var z9DevRepository = new Z9DevRepository(_dataAccess);
        using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while ((await z9DevRepository.GetAll()).Count() < 6 && !cancellationTokenSource.Token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationTokenSource.Token);
        }

        if(cancellationTokenSource.Token.IsCancellationRequested)
        {
            Assert.Fail("Timeout waiting for pool z9_devs to be synced");
        }
    }

    [TearDown]
    public void TearDown()
    {
        _extensionService.Shutdown();

        _persistConnection?.Close();
        _persistConnection?.Dispose();
    }

    [Test]
    public async Task GetState()
    {
        // Arrange
        var z9DevRepository = new Z9DevRepository(_dataAccess);
        // InputService is still instantiated to subscribe to StateChanged events
        _ = new InputService(_dataAccess,
            new UnitTestingSupportForIHubContext<DataChangeNotificationHub>().IHubContextMock.Object,
            _extensionService, new DevStateService());

        var available = (await z9DevRepository.GetAvailableByDevType(DevType.Sensor)).ToArray();
        Assert.That(available.Length, Is.EqualTo(2), "Expected 2 available sensor endpoints");

        // Assign sensors (set name + enabled)
        foreach (var dev in available)
        {
            dev.Name = $"TestInput_{dev.Unid}";
            dev.Enabled = true;
            await z9DevRepository.Upsert(dev);
        }

        // Act — send state via named pipe using the driver endpoint ID of the second input
        var secondDev = available[1];
        var extensionId = await z9DevRepository.GetExtensionId(secondDev);
        await SendInputState(secondDev.ExternalId, true);

        // Assert
        Assert.That(async () =>
            await _extensionService.GetMonitorPoint(extensionId.Value, available[0].ExternalId).GetState(),
            Is.False.After(1000, 100));
        Assert.That(async () =>
            await _extensionService.GetMonitorPoint(extensionId.Value, secondDev.ExternalId).GetState(),
            Is.True.After(1000, 100));
    }

    [Test]
    public async Task ReceiveStateChange()
    {
        // Arrange
        var hubContext = new UnitTestingSupportForIHubContext<DataChangeNotificationHub>();
        var z9DevRepository = new Z9DevRepository(_dataAccess);
        _ = new InputService(_dataAccess, hubContext.IHubContextMock.Object, _extensionService, new DevStateService());

        var available = (await z9DevRepository.GetAvailableByDevType(DevType.Sensor)).ToArray();
        Assert.That(available.Length, Is.EqualTo(2), "Expected 2 available sensor endpoints");

        // Assign sensors (set name + enabled)
        foreach (var dev in available)
        {
            dev.Name = $"TestInput_{dev.Unid}";
            dev.Enabled = true;
            await z9DevRepository.Upsert(dev);
        }

        // Act — send state via named pipe using the driver endpoint ID of the second input
        var secondDev = available[1];
        var extensionId = await z9DevRepository.GetExtensionId(secondDev);
        await SendInputState(secondDev.ExternalId, true);

        // Assert
        // Wait for state to be updated on service before verifying
        Assert.That(async () =>
            await _extensionService.GetMonitorPoint(extensionId.Value, secondDev.ExternalId).GetState(),
            Is.True.After(1000, 100));
        hubContext.ClientsAllMock.Verify(clientProxy =>
            clientProxy.SendCoreAsync(Methods.InputStateChanged, new object[] {secondDev.Unid, true},
                It.IsAny<CancellationToken>()));
    }

    private static async Task SendInputState(string id, bool state)
    {
        await using var pipeClient =
            new NamedPipeClientStream(".", "Aporta.TestDriverMonitorPoint", PipeDirection.Out, PipeOptions.Asynchronous);

        await pipeClient.ConnectAsync();
        await using var writer = new StreamWriter(pipeClient);
        writer.AutoFlush = true;
        await writer.WriteLineAsync($"{id}|{state}");
    }
}
