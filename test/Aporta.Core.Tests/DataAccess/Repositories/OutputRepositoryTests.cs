using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Models;
using Aporta.Shared.Models;
using NUnit.Framework;

namespace Aporta.Core.Tests.DataAccess.Repositories;

public class OutputRepositoryTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;
    private Guid _extensionId;
    private int _endpointId;
        
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

        var endpoint = new Endpoint {Name = "Test1", Type = EndpointType.Output, ExtensionId = _extensionId};
        var endpointRepository = new EndpointRepository(_dataAccess);
        await endpointRepository.Insert(endpoint);
        _endpointId = endpoint.Id;
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
        var outputs = new[]
        {
            new Output {Name = "Test1", EndpointId = _endpointId},
            new Output {Name = "Test2", EndpointId = _endpointId},
            new Output {Name = "Test3", EndpointId = _endpointId}
        };
            
        var outputRepository = new OutputRepository(_dataAccess);
        foreach (var output in outputs)
        {
            await outputRepository.Insert(output);   
        }

        // Act 
        var actualOutput = await outputRepository.Get(3);

        // Assert
        Assert.AreEqual(3, outputs[2].Id);
        Assert.AreEqual(3, actualOutput.Id);
        Assert.AreEqual("Test3", actualOutput.Name);
        Assert.AreEqual(_endpointId, actualOutput.EndpointId);
    }
        
    [Test]
    public async Task Delete()
    {
        // Arrange
        var outputs = new[]
        {
            new Output {Name = "Test1", EndpointId = _endpointId},
            new Output {Name = "Test2", EndpointId = _endpointId},
            new Output {Name = "Test3", EndpointId = _endpointId}
        };
            
        var outputRepository = new OutputRepository(_dataAccess);
        foreach (var output in outputs)
        {
            await outputRepository.Insert(output);   
        }

        // Act 
        await outputRepository.Delete(3);

        // Assert
        var actualOutputs = await outputRepository.GetAll();
        Assert.AreEqual(2, actualOutputs.Count());
    }
}