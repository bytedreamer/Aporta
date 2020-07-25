using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.Hubs;
using Aporta.Core.Services;
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
        }

        [TearDown]
        public void TearDown()
        {
            _persistConnection?.Close();
            _persistConnection?.Dispose();
        }

        [Test]
        public async Task ReceiveCredential()
        {
            // Arrange
            IEnumerable<byte> receivedCardData = new byte[] { };
            IAccessPoint accessEndpoint = null;
            var cardData = new byte[] {0x01, 0x12};
            var accessPointService = new AccessPointService(_extensionService);
            accessPointService.AccessCredentialReceived += (sender, args) =>
            {
                accessEndpoint = args.AccessPoint;
                receivedCardData = args.CardData;
            };

            // Act
            await SendBadgeData(cardData);

            // Assert
            Assert.That(() => accessEndpoint?.Id == "R1" && receivedCardData.FirstOrDefault() == cardData.First(),
                Is.True.After(1000, 100));
        }

        private static async Task SendBadgeData(IEnumerable<byte> badgeData)
        {
            await using var pipeClient =
                new NamedPipeClientStream(".", "Aporta.TestAccessPoint", PipeDirection.Out);
            await pipeClient.ConnectAsync();
            await using var writer = new StreamWriter(pipeClient) {AutoFlush = true};
            await writer.WriteLineAsync(string.Join(":", badgeData.Select(b => b.ToString("X2"))));
        }
    }
}