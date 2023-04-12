using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using NUnit.Framework;

namespace Aporta.Core.Tests.DataAccess
{
    [TestFixture]
    public class SqLiteDataAccessTests
    {
        [Test]
        public void NoFileFound()
        {
            // Arrange
            var dataAccess = new SqLiteDataAccess();

            // Act
            async Task Act()
            {
                await dataAccess.CurrentVersion();
            }

            // Assert
            Assert.CatchAsync(async () => await Act());
        }
    }
}