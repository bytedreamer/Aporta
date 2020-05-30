using System.Data;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using NUnit.Framework;

namespace Aporta.Core.Tests.DataAccess
{
    public class MigrationTests
    {
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
            _persistConnection.Dispose();
        }

        [Test]
        public async Task CreateFromMissingDatabase()
        {
            // Arrange
            // Act 
            await _dataAccess.UpdateSchema();

            // Assert
            Assert.AreEqual(0, await _dataAccess.CurrentVersion());
        }
    }
}