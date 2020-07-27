using System;
using System.Data;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Core.Services;
using Aporta.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SignalR_UnitTestingSupportCommon.IHubContextSupport;

namespace Aporta.Core.Tests.Services
{
    [TestFixture]
    public class OutputServiceTests
    {
        private readonly Guid _extensionId = Guid.Parse("225B748E-FB15-4428-92F7-218BB4CC2813");
        private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
        private readonly ILoggerFactory _loggerFactory = new NullLoggerFactory();
        private ExtensionService _extensionService;
        private EndpointRepository _endpointRepository;
        private OutputRepository _outputRepository;
        private IDbConnection _persistConnection;

        [SetUp]
        public async Task Setup()
        {
            _persistConnection = _dataAccess.CreateDbConnection();
            _persistConnection.Open();

            await _dataAccess.UpdateSchema();
            _extensionService = new ExtensionService(_dataAccess,
                new UnitTestingSupportForIHubContext<DataChangeNotificationHub>().IHubContextMock.Object,
                _loggerFactory.CreateLogger<ExtensionService>(),
                _loggerFactory);
            await _extensionService.Startup();
            await _extensionService.EnableExtension(_extensionId, true);
            
            _endpointRepository = new EndpointRepository(_dataAccess);
            _outputRepository = new OutputRepository(_dataAccess);
        }

        [TearDown]
        public void TearDown()
        {
            _persistConnection?.Close();
            _persistConnection?.Dispose();
        }

        [Test]
        public async Task SetState()
        {
            // Arrange
            var outputService = new OutputService(_outputRepository, _endpointRepository, _extensionService);
            var outputs = new[]
            {
                new Output{Name = "TestOutput1", EndpointId = 2},
                new Output{Name = "TestOutput2", EndpointId = 3} 
            };
            
            foreach (var output in outputs)
            {
                await _outputRepository.Insert(output);
            }
            
            // Act
            await outputService.Set(outputs[0].Id, true);
            await outputService.Set(outputs[1].Id, false);

            // Assert
            Assert.IsTrue(await outputService.Get(outputs[0].Id));
            Assert.IsFalse(await outputService.Get(outputs[1].Id));
        }
    }
}