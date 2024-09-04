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
using Aporta.Shared.Models;

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

        // Wait for endpoints to be inserted
        var endpointRepository = new EndpointRepository(_dataAccess);
        using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while ((await endpointRepository.GetAll()).Count() != 5 && !cancellationTokenSource.Token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationTokenSource.Token);
        }
            
        if(cancellationTokenSource.Token.IsCancellationRequested) 
        {
            Assert.Fail("Timeout waiting for endpoints to be inserted");
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
        var inputService = new InputService(_dataAccess,
            new UnitTestingSupportForIHubContext<DataChangeNotificationHub>().IHubContextMock.Object,
            _extensionService);
        var inputs = new[]
        {
            new Input {Name = "TestInput1", EndpointId = 4},
            new Input {Name = "TestInput2", EndpointId = 5}
        };

        var inputRepository = new InputRepository(_dataAccess);
        foreach (var input in inputs)
        {
            await inputRepository.Insert(input);
        }

        // Act
        await SendInputState("I2", true);

        // Assert
        Assert.That(async () => await inputService.GetState(inputs[0].Id),
            Is.False.After(1000, 100));
        Assert.That(async () => await inputService.GetState(inputs[1].Id),
            Is.True.After(1000, 100));
    }

    [Test]
    public async Task ReceiveStateChange()
    {
        // Arrange
        var hubContext = new UnitTestingSupportForIHubContext<DataChangeNotificationHub>();
        var inputService = new InputService(_dataAccess, hubContext.IHubContextMock.Object, _extensionService);
        var inputs = new[]
        {
            new Input {Name = "TestInput1", EndpointId = 4},
            new Input {Name = "TestInput2", EndpointId = 5}
        };

        var inputRepository = new InputRepository(_dataAccess);
        foreach (var input in inputs)
        {
            await inputRepository.Insert(input);
        }

        // Act
        await SendInputState("I2", true);

        // Assert
        // Wait for state to be updated on service before verifying
        Assert.That(async () => await inputService.GetState(inputs[1].Id),
            Is.True.After(1000, 100));
        hubContext.ClientsAllMock.Verify(clientProxy =>
            clientProxy.SendCoreAsync(Methods.InputStateChanged, new object[] {2, true},
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