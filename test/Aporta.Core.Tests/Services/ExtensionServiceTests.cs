using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.Hubs;
using Aporta.Core.Services;
using Aporta.Shared.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using SignalR_UnitTestingSupportCommon.IHubContextSupport;

namespace Aporta.Core.Tests.Services
{
    [TestFixture]
    public class ExtensionServiceTests
    {
        private readonly Guid _extensionId = Guid.Parse("225B748E-FB15-4428-92F7-218BB4CC2813");
        private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
        private readonly ILoggerFactory _loggerFactory = new NullLoggerFactory();
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
        public async Task Startup()
        {
            // Arrange
            var extensionService = new ExtensionService(_dataAccess,
                    new UnitTestingSupportForIHubContext<DataChangeNotificationHub>().IHubContextMock.Object,
                    _loggerFactory.CreateLogger<ExtensionService>(), _loggerFactory)
                {CurrentDirectory = Environment.CurrentDirectory};

            // Act
            await extensionService.Startup();

            // Assert
            Assert.AreEqual(1, extensionService.Extensions.Count());
            Assert.IsFalse(extensionService.Extensions.First().Loaded);
            
            extensionService.Shutdown();
        }

        [Test]
        public async Task Enable()
        {
            // Arrange
            var hubContextSupport = new UnitTestingSupportForIHubContext<DataChangeNotificationHub>();
            var extensionService = new ExtensionService(_dataAccess, hubContextSupport.IHubContextMock.Object,
                    _loggerFactory.CreateLogger<ExtensionService>(), _loggerFactory)
                {CurrentDirectory = Environment.CurrentDirectory};
            await extensionService.Startup();

            // Act
            await extensionService.EnableExtension(_extensionId, true);

            // Assert
            Assert.That(() => extensionService.Extensions.First().Loaded,
                Is.True.After(1000, 100));
            hubContextSupport.ClientsAllMock
                .Verify(
                    x => x.SendCoreAsync(
                        Methods.ExtensionDataChanged, 
                        new object[] {_extensionId}, 
                        It.IsAny<CancellationToken>())
                );
            
            extensionService.Shutdown();
        }
    }
}