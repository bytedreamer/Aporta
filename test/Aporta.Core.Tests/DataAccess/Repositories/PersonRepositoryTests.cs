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
    public class PersonRepositoryTests
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

            // Act 
            var actualPerson = await personRepository.Get(2);

            // Assert
            Assert.AreEqual(2, people[1].Id);
            Assert.AreEqual(2, actualPerson.Id);
            Assert.AreEqual("First2", actualPerson.FirstName);
            Assert.AreEqual("Last2", actualPerson.LastName);
            Assert.IsTrue(actualPerson.Enabled);
        }

        [Test]
        public async Task Delete()
        {
            // Arrange
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

            // Act 
            await personRepository.Delete(2);

            // Assert
            var actualPeople = await personRepository.GetAll();
            Assert.AreEqual(1, actualPeople.Count());
        }
    }
}