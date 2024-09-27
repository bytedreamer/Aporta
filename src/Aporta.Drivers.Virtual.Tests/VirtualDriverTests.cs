using Aporta.Drivers.Virtual.Shared;
using Aporta.Drivers.Virtual.Shared.Actions;
using Newtonsoft.Json;
using Moq;
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
            var inputToAdd = new AddInputParameter { Name = "Input 1" };

            Mock<Microsoft.Extensions.Logging.ILoggerFactory> mockLoggerFactory = new();
            Mock<IDataEncryption> mockDataEncryption = new();

            //act
            virtualDriver.Load(string.Empty, mockDataEncryption.Object, mockLoggerFactory.Object);

            virtualDriver.PerformAction(ActionType.AddInput.ToString(), JsonConvert.SerializeObject(inputToAdd));

            //assert
            var configuration = JsonConvert.DeserializeObject<Configuration>(virtualDriver.CurrentConfiguration());

            Assert.That(configuration?.Inputs.Find(x => x.Name == inputToAdd.Name), Is.InstanceOf<Input>());
        }

        [Test]
        public void RemoveInput()
        {
            //arrange
            var virtualDriver = new VirtualDriver();
            var input1 = new Input { Number = 1, Name = "Input 1" };
            var input2 = new Input { Number = 1, Name = "Input 2" };
            var input3 = new Input { Number = 1, Name = "Input 3" };

            Mock<Microsoft.Extensions.Logging.ILoggerFactory> mockLoggerFactory = new();
            Mock<IDataEncryption> mockDataEncryption = new();

            var initialConfig = new Configuration();
            initialConfig.Inputs.Add(input1);
            initialConfig.Inputs.Add(input2);
            initialConfig.Inputs.Add(input3);

            //act
            virtualDriver.Load(string.Empty, mockDataEncryption.Object, mockLoggerFactory.Object);

            var inputToRemove = input2;

            virtualDriver.PerformAction(ActionType.RemoveInput.ToString(), JsonConvert.SerializeObject(inputToRemove));

            //assert
            var newConfig = JsonConvert.DeserializeObject<Configuration>(virtualDriver.CurrentConfiguration());

            Assert.That(newConfig?.Inputs.Find(x => x.Name == inputToRemove.Name && x.Number == inputToRemove.Number),
                Is.Null);
        }

        [Test]
        public void AddOutput()
        {
            //arrange
            var virtualDriver = new VirtualDriver();
            var outputToAdd = new AddOutputParameter { Name = "Output 1" };

            Mock<Microsoft.Extensions.Logging.ILoggerFactory> mockLoggerFactory = new();
            Mock<IDataEncryption> mockDataEncryption = new();

            //act
            virtualDriver.Load(string.Empty, mockDataEncryption.Object, mockLoggerFactory.Object);

            virtualDriver.PerformAction(ActionType.AddOutput.ToString(), JsonConvert.SerializeObject(outputToAdd));

            //assert
            var configuration = JsonConvert.DeserializeObject<Configuration>(virtualDriver.CurrentConfiguration());

            Assert.That(configuration?.Outputs.Find(x => x.Name == outputToAdd.Name), Is.InstanceOf<Output>());
        }

        [Test]
        public void RemoveOutput()
        {
            //arrange
            var virtualDriver = new VirtualDriver();
            var output1 = new Output { Number = 1, Name = "Output 1" };
            var output2 = new Output { Number = 1, Name = "Output 2" };
            var output3 = new Output { Number = 1, Name = "Output 3" };

            Mock<Microsoft.Extensions.Logging.ILoggerFactory> mockLoggerFactory = new();
            Mock<IDataEncryption> mockDataEncryption = new();

            var initialConfig = new Configuration();
            initialConfig.Outputs.Add(output1);
            initialConfig.Outputs.Add(output2);
            initialConfig.Outputs.Add(output3);

            //act
            virtualDriver.Load(string.Empty, mockDataEncryption.Object, mockLoggerFactory.Object);

            var outputToRemove = output2;

            virtualDriver.PerformAction(ActionType.RemoveOutput.ToString(),
                JsonConvert.SerializeObject(outputToRemove));

            //assert
            var newConfig = JsonConvert.DeserializeObject<Configuration>(virtualDriver.CurrentConfiguration());

            Assert.That(newConfig?.Inputs.Find(x => x.Name == outputToRemove.Name && x.Number == outputToRemove.Number),
                Is.Null);
        }
    }
}