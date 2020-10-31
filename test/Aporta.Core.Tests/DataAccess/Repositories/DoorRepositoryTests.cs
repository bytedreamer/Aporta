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
    public class DoorRepositoryTests
    {
        private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
        private IDbConnection _persistConnection;
        private Guid _extensionId;
        private int _inAccessEndpointId;
        private int _outAccessEndpointId;
        private int _doorContactEndpointId;
        private int _requestToExitEndpointId;
        private int _doorStrikeEndpointId;

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

            var inAccessEndpoint = new Endpoint
                {Name = "InAccessTest", Type = EndpointType.Reader, ExtensionId = _extensionId};
            var outAccessEndpoint = new Endpoint
                {Name = "OutAccessTest", Type = EndpointType.Reader, ExtensionId = _extensionId};
            var doorContactEndpoint = new Endpoint
                {Name = "DoorContactTest", Type = EndpointType.Input, ExtensionId = _extensionId};
            var requestToExitEndpoint = new Endpoint
                {Name = "RequestToExitTest", Type = EndpointType.Input, ExtensionId = _extensionId};
            var doorStrikeEndpoint = new Endpoint
                {Name = "DoorStrikeTest", Type = EndpointType.Output, ExtensionId = _extensionId};
            
            var endpointRepository = new EndpointRepository(_dataAccess);
            
            await endpointRepository.Insert(inAccessEndpoint);
            _inAccessEndpointId = inAccessEndpoint.Id;
            await endpointRepository.Insert(outAccessEndpoint);
            _outAccessEndpointId = outAccessEndpoint.Id;
            await endpointRepository.Insert(doorContactEndpoint);
            _doorContactEndpointId = doorContactEndpoint.Id;
            await endpointRepository.Insert(requestToExitEndpoint);
            _requestToExitEndpointId = requestToExitEndpoint.Id;
            await endpointRepository.Insert(doorStrikeEndpoint);
            _doorStrikeEndpointId = doorStrikeEndpoint.Id;
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
            var doors = new[]
            {
                new Door {Name = "DoorTest1", InAccessEndpointId = _inAccessEndpointId},
                new Door
                {
                    Name = "DoorTest2", InAccessEndpointId = _inAccessEndpointId,
                    OutAccessEndpointId = _outAccessEndpointId, DoorContactEndpointId = _doorContactEndpointId,
                    RequestToExitEndpointId = _requestToExitEndpointId, DoorStrikeEndpointId = _doorStrikeEndpointId
                }
            };

            var doorRepository = new DoorRepository(_dataAccess);
            foreach (var door in doors)
            {
                await doorRepository.Insert(door);
            }

            // Act 
            var actualDoor = await doorRepository.Get(2);

            // Assert
            Assert.AreEqual(2, doors[1].Id);
            Assert.AreEqual(2, actualDoor.Id);
            Assert.AreEqual("DoorTest2", actualDoor.Name);
            Assert.AreEqual(_inAccessEndpointId, actualDoor.InAccessEndpointId);
            Assert.AreEqual(_outAccessEndpointId, actualDoor.OutAccessEndpointId);
            Assert.AreEqual(_doorContactEndpointId, actualDoor.DoorContactEndpointId);
            Assert.AreEqual(_requestToExitEndpointId, actualDoor.RequestToExitEndpointId);
            Assert.AreEqual(_doorStrikeEndpointId, actualDoor.DoorStrikeEndpointId);
        }

        [Test]
        public async Task Delete()
        {
            // Arrange
            var doors = new[]
            {
                new Door {Name = "DoorTest1"},
                new Door {Name = "DoorTest2"}
            };
            
            var doorRepository = new DoorRepository(_dataAccess);
            foreach (var door in doors)
            {
                await doorRepository.Insert(door);   
            }

            // Act 
            await doorRepository.Delete(2);

            // Assert
            var actualDoors = await doorRepository.GetAll();
            Assert.AreEqual(1, actualDoors.Count());
        }
    }
}