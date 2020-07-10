using System.Data;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using NUnit.Framework;

namespace Aporta.Core.Tests.DataAccess
{
    public class MigrationTests
    {
        private const int CurrentVersion = 2;
        private readonly IDataAccess _dataAccess = new SqlLiteDataAccess(true);
        private IDbConnection _persistConnection;
        
        [SetUp]
        public void Setup()
        {
            _persistConnection = _dataAccess.CreateDbConnection();
            _persistConnection.Open();
        }

        [TearDown]
        public void TearDown()
        {
            _persistConnection?.Close();
            _persistConnection?.Dispose();
        }

        [Test]
        public async Task UpdateSchema_CreateFromMissingDatabase()
        {
            // Arrange
            // Act 
            await _dataAccess.UpdateSchema();

            // Assert
            Assert.AreEqual(CurrentVersion, await _dataAccess.CurrentVersion());
        }
        
        [Test]
        public async Task UpdateSchema_RunMultipleTimes()
        {
            // Arrange
            await _dataAccess.UpdateSchema();
            
            // Act 
            await _dataAccess.UpdateSchema();

            // Assert
            Assert.AreEqual(CurrentVersion, await _dataAccess.CurrentVersion());
        }
    }
}