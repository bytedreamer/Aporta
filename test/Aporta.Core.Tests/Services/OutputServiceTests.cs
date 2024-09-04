using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Core.Services;
using Aporta.Extensions;
using Aporta.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using SignalR_UnitTestingSupportCommon.IHubContextSupport;

namespace Aporta.Core.Tests.Services;

[TestFixture]
public class OutputServiceTests
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
            _loggerFactory) {CurrentDirectory = Environment.CurrentDirectory};
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
    public async Task SetState()
    {
        // Arrange
        var outputService = new OutputService(_dataAccess,
            new UnitTestingSupportForIHubContext<DataChangeNotificationHub>().IHubContextMock.Object,
            _extensionService);
        
        var outputs = new[]
        {
            new Output {Name = "TestOutput1", EndpointId = 2},
            new Output {Name = "TestOutput2", EndpointId = 3}
        };
        var outputRepository = new OutputRepository(_dataAccess);
        foreach (var output in outputs)
        {
            await outputRepository.Insert(output);
        }

        // Act
        await outputService.SetState(outputs[0].Id, true);
        await outputService.SetState(outputs[1].Id, false);

        // Assert
        Assert.That(await outputService.GetState(outputs[0].Id), Is.True);
        Assert.That(await outputService.GetState(outputs[1].Id), Is.False);
    }
}