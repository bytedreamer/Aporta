using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Models;
using NUnit.Framework;

namespace Aporta.Core.Tests.DataAccess.Repositories
{
    [TestFixture]
    public class ExtensionRepositoryTests
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
        public async Task Get()
        {
            // Arrange
            var extensions = new[]
            {
                new ExtensionHost {Id = Guid.NewGuid(), Name = "Test1", Enabled = false},
                new ExtensionHost {Id = Guid.NewGuid(), Name = "Test2", Enabled = true},
                new ExtensionHost {Id = Guid.NewGuid(), Name = "Test3", Enabled = true}
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
            Assert.AreEqual("Test2", actualExtension.Name);
            Assert.AreEqual(true, actualExtension.Enabled);
        }
        
        [Test]
        public async Task GetAll()
        {
            // Arrange
            var extensions = new[]
            {
                new ExtensionHost {Id = Guid.NewGuid(), Name = "Test1", Enabled = false},
                new ExtensionHost {Id = Guid.NewGuid(), Name = "Test2", Enabled = true},
                new ExtensionHost {Id = Guid.NewGuid(), Name = "Test3", Enabled = true}
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
        
        [Test]
        public async Task Update()
        {
            // Arrange
            var extensions = new[]
            {
                new ExtensionHost {Id = Guid.NewGuid(), Name = "Test1", Enabled = false},
                new ExtensionHost {Id = Guid.NewGuid(), Name = "Test2", Enabled = true},
                new ExtensionHost {Id = Guid.NewGuid(), Name = "Test3", Enabled = true}
            };
                
            var extensionRepository = new ExtensionRepository(_dataAccess);
            foreach (var extension in extensions)
            {
                await extensionRepository.Insert(extension);   
            }
            var updatedExtension = extensions[0];
            updatedExtension.Enabled = true;
            updatedExtension.Configuration = "TestConfigure";
            
            // Act 
            await extensionRepository.Update(extensions[0]);
            var actualExtension = await extensionRepository.Get(updatedExtension.Id);

            // Assert
            Assert.IsTrue(actualExtension.Enabled);
            Assert.AreEqual("TestConfigure", actualExtension.Configuration);
        }
    }
}