using Aporta.Drivers.Virtual.Shared;
using Aporta.Drivers.Virtual.Shared.Actions;
using Aporta.Shared.Calls;
using Aporta.Shared.Models;
using Newtonsoft.Json;
using Moq;
using Castle.Core.Logging;
using Aporta.Extensions;

namespace Aporta.Drivers.Virtual.Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void AddInput()
        {
            //arrange
            var virtualDriver = new VirtualDriver();
            var inputToAdd = new AddInputParameter() { Name = "Input 1" };

            Mock<Microsoft.Extensions.Logging.ILoggerFactory> _mockLoggerFactory = new();
            Mock<Aporta.Extensions.IDataEncryption> _mockDataEncryption = new();

            //act
            virtualDriver.Load(string.Empty, _mockDataEncryption.Object, _mockLoggerFactory.Object);

            virtualDriver.PerformAction(ActionType.AddInput.ToString(), JsonConvert.SerializeObject(inputToAdd));

            //assert
            var configuration = JsonConvert.DeserializeObject<Configuration>(virtualDriver.CurrentConfiguration());

            Assert.IsInstanceOf<Shared.Input>(configuration?.Inputs.Find(x => x.Name == inputToAdd.Name));


        }

        [Test]
        public void RemoveInput()
        {
            //arrange
            var virtualDriver = new VirtualDriver();
            var input1 = new Shared.Input() { Number = 1, Name = "Input 1" };
            var input2 = new Shared.Input() { Number = 1, Name = "Input 2" };
            var input3 = new Shared.Input() { Number = 1, Name = "Input 3" };

            Mock<Microsoft.Extensions.Logging.ILoggerFactory> _mockLoggerFactory = new();
            Mock<Aporta.Extensions.IDataEncryption> _mockDataEncryption = new();

            var initialConfig = new Configuration();
            initialConfig.Inputs.Add(input1);
            initialConfig.Inputs.Add(input2);
            initialConfig.Inputs.Add(input3);

            //act
            virtualDriver.Load(string.Empty, _mockDataEncryption.Object, _mockLoggerFactory.Object);

            var inputToRemove = input2;

            virtualDriver.PerformAction(ActionType.RemoveInput.ToString(), JsonConvert.SerializeObject(inputToRemove));

            //assert
            var newConfig = JsonConvert.DeserializeObject<Configuration>(virtualDriver.CurrentConfiguration());

            Assert.IsNull(newConfig?.Inputs.Find(x => x.Name == inputToRemove.Name && x.Number == inputToRemove.Number));
        }

        [Test]
        public void AddOutput()
        {
            Assert.Fail();
        }

        [Test]
        public void RemoveOutput()
        {
            Assert.Fail();
        }

    }
}