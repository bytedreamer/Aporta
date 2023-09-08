using System.Data;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models;
using NUnit.Framework;

namespace Aporta.Core.Tests.DataAccess.Repositories;

[TestFixture]
public class GlobalSettingRepositoryTests
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
        var globalSettings = new[]
        {
            new GlobalSetting {Name = "Test1", Value = "Value1"},
            new GlobalSetting {Name = "Test2", Value = "Value2"},
            new GlobalSetting {Name = "Test3", Value = "Value3"}
        };
                
        var globalSettingRepository = new GlobalSettingRepository(_dataAccess);
        foreach (var extension in globalSettings)
        {
            await globalSettingRepository.Insert(extension);   
        }

        // Act 
        string actualValue = await globalSettingRepository.Get(globalSettings[1].Name);

        // Assert
        Assert.AreEqual(globalSettings[1].Value, actualValue);
    }
        
    [Test]
    public async Task Update()
    {
        // Arrange
        var globalSettings = new[]
        {
            new GlobalSetting {Name = "Test1", Value = "Value1"},
            new GlobalSetting {Name = "Test2", Value = "Value2"},
            new GlobalSetting {Name = "Test3", Value = "Value3"}
        };
            
        var globalSettingRepository = new GlobalSettingRepository(_dataAccess);
        foreach (var extension in globalSettings)
        {
            await globalSettingRepository.Insert(extension);   
        }
            
        var updatedGlobalSetting = globalSettings[0];
        updatedGlobalSetting.Value = "NewValue1";

        // Act 
        await globalSettingRepository.Update(globalSettings[0]);
        var actualValue = await globalSettingRepository.Get(updatedGlobalSetting.Name);

        // Assert
        Assert.AreEqual("NewValue1", actualValue);
    }
}