using System;
using System.Collections;
using System.Data;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.Hubs;
using Aporta.Core.Services;
using Aporta.Extensions;
using Aporta.Extensions.Endpoint;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SignalR_UnitTestingSupportCommon.IHubContextSupport;

namespace Aporta.Core.Tests.Services
{
    public class AccessPointServiceTest
    {
        private readonly Guid _extensionId = Guid.Parse("225B748E-FB15-4428-92F7-218BB4CC2813");
        private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
        private readonly ILoggerFactory _loggerFactory = new NullLoggerFactory();
        private ExtensionService _extensionService;
        private IDbConnection _persistConnection;

        [SetUp]
        public async Task Setup()
        {
            _persistConnection = _dataAccess.CreateDbConnection();
            _persistConnection.Open();

            await _dataAccess.UpdateSchema();
            _extensionService = new ExtensionService(_dataAccess,
                new UnitTestingSupportForIHubContext<DataChangeNotificationHub>().IHubContextMock.Object,
                _loggerFactory.CreateLogger<ExtensionService>(),
                _loggerFactory);
            await _extensionService.Startup();
            await _extensionService.EnableExtension(_extensionId, true);
            Assert.That(() => _extensionService.Extensions.First().Loaded,
                Is.True.After(1000, 100));
        }

        [TearDown]
        public void TearDown()
        {
            _extensionService?.Shutdown();
                
            _persistConnection?.Close();
            _persistConnection?.Dispose();
        }

        [Test]
        public async Task ReceiveCredential()
        {
            // Arrange
            var receivedCardData = new BitArray(new bool[] { });
            ushort receivedBitCount = 0;
            IAccessPoint accessEndpoint = null;
            string bitArrayString = "110101010111";
            
            var cardData = bitArrayString.ToBitArray();
            using var accessPointService = new AccessPointService(_extensionService);
            accessPointService.AccessCredentialReceived += (sender, args) =>
            {
                accessEndpoint = args.AccessPoint;
                receivedCardData = args.CardData;
                receivedBitCount = args.BitCount;
            };

            // Act
            await SendBadgeData(cardData);

            // Assert
            Assert.That(() => accessEndpoint?.Id == "R1" && 
                              receivedCardData.ToBitString() == bitArrayString &&
                              receivedBitCount == 12,
                Is.True.After(1000, 100));
        }

        private static async Task SendBadgeData(BitArray bitArray)
        {
            await using var pipeClient =
                new NamedPipeClientStream(".", "Aporta.TestDriverAccessPoint", PipeDirection.Out, PipeOptions.Asynchronous);
            await pipeClient.ConnectAsync();
            await using var writer = new StreamWriter(pipeClient) {AutoFlush = true};
            await writer.WriteLineAsync(bitArray.ToBitString());
        }
    }
}