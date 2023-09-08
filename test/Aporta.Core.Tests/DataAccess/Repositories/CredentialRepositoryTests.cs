using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Models;
using Aporta.Shared.Models;
using Microsoft.Data.Sqlite;
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
        public async Task Get_AssignedCredential()
        {
            // Arrange
            var credentials = new[]
            {
                new Credential {Number = "2345342"},
                new Credential {Number = "5345234"}
            };
            
            var credentialRepository = new CredentialRepository(_dataAccess);
            foreach (var credential in credentials)
            {
                await credentialRepository.Insert(credential);
            }
            
            var people = new[]
            {
                new Person {FirstName = "First1", LastName = "Last1", Enabled = false},
                new Person {FirstName = "First2", LastName = "Last2", Enabled = true},
            };

            var personRepository = new PersonRepository(_dataAccess);
            foreach (var person in people)
            {
                await personRepository.Insert(person);
            }

            await credentialRepository.AssignPerson(credentials[1].Id, people[1].Id, true);

            // Act 
            var actual = await credentialRepository.Get(credentials[1].Id);

            // Assert
            Assert.AreEqual("5345234", actual.Number);
            Assert.AreEqual( people[1].Id, actual.AssignedPersonId);
            Assert.IsTrue(actual.Enabled);
        }
        
        [Test]
        public async Task Get_UnassignedCredential()
        {
            // Arrange
            var credentials = new[]
            {
                new Credential {Number = "2345342"},
                new Credential {Number = "5345234"}
            };
            
            var credentialRepository = new CredentialRepository(_dataAccess);
            foreach (var credential in credentials)
            {
                await credentialRepository.Insert(credential);
            }

            // Act 
            var actual = await credentialRepository.Get(credentials[1].Id);

            // Assert
            Assert.AreEqual("5345234", actual.Number);
            Assert.IsNull(actual.AssignedPersonId);
            Assert.IsNull(actual.Enabled);
        }

        [Test]
        public async Task Insert()
        { 
            // Arrange
            var credentials = new[]
            {
                new Credential {Number = "2345342", LastEvent = 4},
                new Credential {Number = "5345234", LastEvent = 5},
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
            Assert.AreEqual(5, actualCredential.LastEvent);
            Assert.AreEqual("5345234", actualCredential.Number);
        }
        
        [Test]
        public async Task Insert_DuplicateCardNumber()
        {
            // Arrange
            var credential = new Credential {Number = "2345342"};

            var credentialRepository = new CredentialRepository(_dataAccess);

            await credentialRepository.Insert(credential);
            
            // Assert
            async Task InsertCredential()
            {
                await credentialRepository.Insert(credential);
            }

            // Assert
            Assert.ThrowsAsync<SqliteException>(InsertCredential);
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
        public async Task AssignedCredential_NoAssignedPerson()
        {
            // Arrange
            var credentials = new[]
            {
                new Credential {Number = "2345342"},
                new Credential {Number = "5345234"}
            };
            
            var credentialRepository = new CredentialRepository(_dataAccess);
            foreach (var credential in credentials)
            {
                await credentialRepository.Insert(credential);
            }

            // Act 
            var actual = await credentialRepository.AssignedCredential("5345234");

            // Assert - Null person
            Assert.IsNull(actual.Person);
        }
        
        [Test]
        public async Task AssignedCredential_ActivePerson()
        {
            // Arrange
            var credentials = new[]
            {
                new Credential {Number = "2345342"},
                new Credential {Number = "5345234"}
            };
            
            var credentialRepository = new CredentialRepository(_dataAccess);
            foreach (var credential in credentials)
            {
                await credentialRepository.Insert(credential);
            }
            
            var people = new[]
            {
                new Person {FirstName = "First1", LastName = "Last1", Enabled = false},
                new Person {FirstName = "First2", LastName = "Last2", Enabled = true},
            };

            var personRepository = new PersonRepository(_dataAccess);
            foreach (var person in people)
            {
                await personRepository.Insert(person);
            }

            await credentialRepository.AssignPerson(credentials[1].Id, people[1].Id, true);

            // Act 
            var actual = await credentialRepository.AssignedCredential("5345234");

            // Assert
            Assert.AreEqual("5345234", actual.Number);
            Assert.IsTrue(actual.Enabled);
            Assert.AreEqual(actual.Person.Id, people[1].Id);
        }
        
        [Test]
        public async Task AssignedCredential_InactivePerson()
        {
            // Arrange
            var credentials = new[]
            {
                new Credential {Number = "2345342"},
                new Credential {Number = "5345234"}
            };
            
            var credentialRepository = new CredentialRepository(_dataAccess);
            foreach (var credential in credentials)
            {
                await credentialRepository.Insert(credential);
            }
            
            var people = new[]
            {
                new Person {FirstName = "First1", LastName = "Last1", Enabled = false},
                new Person {FirstName = "First2", LastName = "Last2", Enabled = true},
            };

            var personRepository = new PersonRepository(_dataAccess);
            foreach (var person in people)
            {
                await personRepository.Insert(person);
            }

            await credentialRepository.AssignPerson(credentials[1].Id, people[0].Id, true);

            // Act 
            var actual = await credentialRepository.AssignedCredential("5345234");

            // Assert
            Assert.AreEqual("5345234", actual.Number);
            Assert.IsFalse(actual.Enabled);
            Assert.AreEqual(actual.Person.Id, people[0].Id);
        }
        
        [Test]
        public async Task AssignedCredential_AssignmentNotEnabled()
        {
            // Arrange
            var credentials = new[]
            {
                new Credential {Number = "2345342"},
                new Credential {Number = "5345234"}
            };
            
            var credentialRepository = new CredentialRepository(_dataAccess);
            foreach (var credential in credentials)
            {
                await credentialRepository.Insert(credential);
            }
            
            var people = new[]
            {
                new Person {FirstName = "First1", LastName = "Last1", Enabled = true},
                new Person {FirstName = "First2", LastName = "Last2", Enabled = true},
            };

            var personRepository = new PersonRepository(_dataAccess);
            foreach (var person in people)
            {
                await personRepository.Insert(person);
            }

            await credentialRepository.AssignPerson(credentials[1].Id, people[0].Id, false);

            // Act 
            var actual = await credentialRepository.AssignedCredential("5345234");

            // Assert
            Assert.AreEqual("5345234", actual.Number);
            Assert.IsFalse(actual.Enabled);
            Assert.AreEqual(actual.Person.Id, people[0].Id);
        }
        
        [Test]
        public async Task UpdateLastEvent()
        {
            // Arrange
            var credential = new Credential { Number = "2345342", LastEvent = 4 };
            var credentialRepository = new CredentialRepository(_dataAccess);
            int credentialId = await credentialRepository.Insert(credential);

            // Act 
            await credentialRepository.UpdateLastEvent(credentialId, 5);

            // Assert
            var actualCredential = await credentialRepository.Get(credentialId);
            Assert.AreEqual(5, actualCredential.LastEvent);
        }
    }
}