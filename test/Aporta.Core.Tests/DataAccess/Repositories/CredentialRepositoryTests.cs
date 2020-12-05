using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Models;
using Aporta.Shared.Models;
using NUnit.Framework;

namespace Aporta.Core.Tests.DataAccess.Repositories
{
    public class CredentialRepositoryTests
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
        public async Task Insert()
        {
            // Arrange
            DateTime currentTime = DateTime.UtcNow;
            var credentials = new[]
            {
                new Credential {Number = "2345342"},
                new Credential {Number = "5345234"},
            };

            var credentialRepository = new CredentialRepository(_dataAccess);
            foreach (var credential in credentials)
            {
                await credentialRepository.Insert(credential);
            }

            // Act 
            var actualCredential = await credentialRepository.Get(2);

            // Assert
            Assert.AreEqual(2, credentials[1].Id);
            Assert.AreEqual(2, actualCredential.Id);
            Assert.LessOrEqual(currentTime, actualCredential.EnrollDate);
            Assert.GreaterOrEqual(DateTime.UtcNow, actualCredential.EnrollDate);
            Assert.AreEqual("5345234", actualCredential.Number);
        }

        [Test]
        public async Task Delete()
        {
            // Arrange
            var credentials = new[]
            {
                new Credential {Number = "2345342"},
                new Credential {Number = "5345234"},
            };
            
            var credentialRepository = new CredentialRepository(_dataAccess);
            foreach (var credential in credentials)
            {
                await credentialRepository.Insert(credential);
            }

            // Act 
            await credentialRepository.Delete(2);

            // Assert
            var actualCredentials = await credentialRepository.GetAll();
            Assert.AreEqual(1, actualCredentials.Count());
        }
        
        [Test]
        public async Task IsMatchingNumber()
        {
            // Arrange
            var credentials = new[]
            {
                new Credential {Number = "2345342"},
                new Credential {Number = "5345234"},
            };
            
            var credentialRepository = new CredentialRepository(_dataAccess);
            foreach (var credential in credentials)
            {
                await credentialRepository.Insert(credential);
            }

            // Act 
            bool actual = await credentialRepository.IsMatchingNumber("5345234");

            // Assert
            Assert.IsTrue(actual);
        }
    }
}