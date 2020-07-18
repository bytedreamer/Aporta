using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using NUnit.Framework;

namespace Aporta.Core.Tests.DataAccess
{
    [TestFixture]
    public class SqLiteDataAccessTests
    {
        [Test]
        public async Task NoFileFound()
        {
            // Arrange
            var dataAccess = new SqLiteDataAccess();
            
            // Act
            int currentVersion = await dataAccess.CurrentVersion();

            // Assert
            Assert.AreEqual(-1, currentVersion);
        }
    }
}