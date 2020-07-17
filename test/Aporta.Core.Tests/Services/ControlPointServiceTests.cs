using System;
using System.Data;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.Hubs;
using Aporta.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SignalR_UnitTestingSupportCommon.IHubContextSupport;

namespace Aporta.Core.Tests.Services
{
    public class ControlPointServiceTests
    {
        private readonly Guid _extensionId = Guid.Parse("225B748E-FB15-4428-92F7-218BB4CC2813");
        private readonly IDataAccess _dataAccess = new SqlLiteDataAccess(true);
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
                _loggerFactory.CreateLogger<ExtensionService>(),
                _loggerFactory);
            await _extensionService.Startup();
            await _extensionService.EnableExtension(_extensionId, true);
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
            var controlPointService = new ControlPointService(_extensionService);
            
            // Act
            await controlPointService.SetOutput(_extensionId, "O1", true);
            await controlPointService.SetOutput(_extensionId, "O2", false);

            // Assert
            Assert.IsTrue(await controlPointService.GetOutput(_extensionId, "O1"));
            Assert.IsFalse(await controlPointService.GetOutput(_extensionId, "O2"));
        }
    }
}