using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Aporta.Core.Tests.Services
{
    [TestFixture]
    public class MainServiceTests
    {
        private readonly Guid _extensionId = Guid.Parse("225B748E-FB15-4428-92F7-218BB4CC2813");
        private readonly IDataAccess _dataAccess = new SqlLiteDataAccess(true);
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
            var mainService = new MainService(_dataAccess, _loggerFactory.CreateLogger<MainService>(), _loggerFactory);
            
            // Act
            await mainService.Startup();

            // Assert
            Assert.AreEqual(1, mainService.Extensions.Count());
            Assert.IsFalse(mainService.Extensions.First().Loaded);
        }
        
        [Test]
        public async Task Enable()
        {
            // Arrange
            var mainService = new MainService(_dataAccess, _loggerFactory.CreateLogger<MainService>(), _loggerFactory);
            await mainService.Startup();
            
            // Act
            await mainService.EnableExtension(_extensionId, true);           

            // Assert
            Assert.IsTrue(mainService.Extensions.First().Loaded);
        }
    }
}