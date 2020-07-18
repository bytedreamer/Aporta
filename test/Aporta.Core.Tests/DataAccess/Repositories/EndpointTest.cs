using System;
using System.Data;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Models;
using Aporta.Shared.Models;
using NUnit.Framework;

namespace Aporta.Core.Tests.DataAccess.Repositories
{
    [TestFixture]
    public class EndpointTest
    {
        private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
        private IDbConnection _persistConnection;
        private Guid _extensionId;
        
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
        }

        [TearDown]
        public void TearDown()
        {
            _persistConnection?.Close();
            _persistConnection?.Dispose();
        }

        [Test]
        public async Task InsertThenGet()
        {
            // Arrange
            var endpoints = new[]
            {
                new Endpoint {Name = "Test1", Type = EndpointType.Output, ExtensionId = _extensionId},
                new Endpoint {Name = "Test2", Type = EndpointType.Input, ExtensionId = _extensionId},
                new Endpoint {Name = "Test3", Type = EndpointType.Reader, ExtensionId = _extensionId}
            };

            var endpointRepository = new EndpointRepository(_dataAccess);
            foreach (var endpoint in endpoints)
            {
                await endpointRepository.Insert(endpoint);   
            }

            // Act 
            var actualEndpoint = await endpointRepository.Get(3);

            // Assert
            Assert.AreEqual(3, endpoints[2].Id);
            Assert.AreEqual(3, actualEndpoint.Id);
            Assert.AreEqual("Test3", actualEndpoint.Name);
            Assert.AreEqual(EndpointType.Reader, actualEndpoint.Type);
            Assert.AreEqual(_extensionId, actualEndpoint.ExtensionId);
        }
    }
}