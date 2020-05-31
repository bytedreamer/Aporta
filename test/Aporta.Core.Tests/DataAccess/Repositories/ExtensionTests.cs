using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using NUnit.Framework;

namespace Aporta.Core.Tests.DataAccess.Repositories
{
    [TestFixture]
    public class ExtensionTests
    {
        private readonly IDataAccess _dataAccess = new SqlLiteDataAccess(true);
        private IDbConnection _persistConnection;
        
        [SetUp]
        public void Setup()
        {
            _persistConnection = _dataAccess.CreateDbConnection();
            _persistConnection.Open();

            _dataAccess.UpdateSchema();
        }

        [TearDown]
        public void TearDown()
        {
            _persistConnection?.Close();
            _persistConnection?.Dispose();
        }
        
        [Test]
        public async Task Get()
        {
            // Arrange
            var extensions = new[]
            {
                new Model.Extension {Id = Guid.NewGuid(), Name = "Test1"},
                new Model.Extension {Id = Guid.NewGuid(), Name = "Test2"},
                new Model.Extension {Id = Guid.NewGuid(), Name = "Test3"}
            };
                
            var extensionRepository = new ExtensionRepository(_dataAccess);
            foreach (var extension in extensions)
            {
                await extensionRepository.Insert(extension);   
            }

            // Act 
            var actualExtension = await extensionRepository.Get(extensions[1].Id);

            // Assert
            Assert.AreEqual(extensions[1].Id, actualExtension.Id);
            Assert.AreEqual(extensions[1].Name, actualExtension.Name);
        }
        
        [Test]
        public async Task GetAll()
        {
            // Arrange
            var extensions = new[]
            {
                new Model.Extension {Id = Guid.NewGuid(), Name = "Test1"},
                new Model.Extension {Id = Guid.NewGuid(), Name = "Test2"},
                new Model.Extension {Id = Guid.NewGuid(), Name = "Test3"}
            };
                
            var extensionRepository = new ExtensionRepository(_dataAccess);
            foreach (var extension in extensions)
            {
                await extensionRepository.Insert(extension);   
            }

            // Act 
            var actualExtensions = await extensionRepository.GetAll();

            // Assert
            Assert.AreEqual(3, actualExtensions.Count());
        }
    }
}