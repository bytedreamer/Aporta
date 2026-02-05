using System.Net.Sockets;
using System.Threading;
using Aporta.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Z9.Protobuf;
using Z9.Spcore.Proto;

namespace Aporta.Core.Tests.Services;

[TestFixture]
public class Z9OpenCommunityProtocolServiceTests
{
    private Z9OpenCommunityProtocolService _z9OpenCommunityProtocolService;

    [SetUp]
    public void Setup()
    {
        _z9OpenCommunityProtocolService = new Z9OpenCommunityProtocolService(NullLogger<Z9OpenCommunityProtocolService>.Instance);
        _z9OpenCommunityProtocolService.Start(0); // OS-assigned port
    }

    [TearDown]
    public void TearDown()
    {
        _z9OpenCommunityProtocolService?.Dispose();
    }

    private (TcpClient client, SpCoreMessageInputStream mis, SpCoreMessageOutputStream mos) ConnectAsHost()
    {
        var client = new TcpClient("127.0.0.1", _z9OpenCommunityProtocolService.Port);
        var stream = client.GetStream();
        var mis = new SpCoreMessageInputStream(stream);
        var mos = new SpCoreMessageOutputStream(stream);
        return (client, mis, mos);
    }

    private static void SendIdentification(SpCoreMessageOutputStream mos)
    {
        var message = new SpCoreMessage
        {
            Type = SpCoreMessage.Types.Type.Identification,
            Identification = new Identification
            {
                Id = "test-host",
                SoftwareVersion = "1.0.0-test",
                ProtocolVersion = "0.1",
                MaxBodyLength = SpCoreMessageHeader.MAX_LENGTH,
                SpCoreDevMod = DevMod.IoControllerZ9Spcore,
                ProtocolCapabilities = new ProtocolCapabilities
                {
                    SupportsIdentificationPassword = false,
                    SupportsIdentificationPasswordUpstream = false
                }
            }
        };
        mos.Write(message);
    }

    [Test]
    public void Identification_HostConnects_BothSidesIdentify()
    {
        var (client, mis, mos) = ConnectAsHost();

        try
        {
            // Send identification from host
            SendIdentification(mos);

            // Read panel's identification response
            var response = mis.Read();
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Type, Is.EqualTo(SpCoreMessage.Types.Type.Identification));
            Assert.That(response.Identification.Id, Is.EqualTo("aporta-panel"));

            // Panel should track state
            Thread.Sleep(100); // Allow panel thread to update state
            Assert.That(_z9OpenCommunityProtocolService.IsClientConnected, Is.True);
            Assert.That(_z9OpenCommunityProtocolService.IsIdentified, Is.True);
        }
        finally
        {
            client.Close();
        }

        Assert.That(_z9OpenCommunityProtocolService.LastException, Is.Null);
    }

    [Test]
    public void DbChange_HostSendsSchedule_PanelRespondsSuccess()
    {
        var (client, mis, mos) = ConnectAsHost();

        try
        {
            // Identify first
            SendIdentification(mos);
            var identResponse = mis.Read();
            Assert.That(identResponse.Type, Is.EqualTo(SpCoreMessage.Types.Type.Identification));

            // Send DbChange
            var dbChange = new DbChange();
            dbChange.Sched.Add(new Sched { Unid = 1, Name = "Test Schedule" });
            var dbChangeMsg = new SpCoreMessage
            {
                Type = SpCoreMessage.Types.Type.DbChange,
                DbChange = dbChange
            };
            dbChangeMsg.DbChange.RequestId = 42;
            mos.Write(dbChangeMsg);

            // Read response
            var response = mis.Read();
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Type, Is.EqualTo(SpCoreMessage.Types.Type.DbChangeResp));
            Assert.That(response.DbChangeResp.RequestId, Is.EqualTo(42));
            Assert.That(response.DbChangeResp.ExceptionCase, Is.EqualTo(DbChangeResp.ExceptionOneofCase.None));

            lock (_z9OpenCommunityProtocolService.ReceivedDbChanges)
            {
                Assert.That(_z9OpenCommunityProtocolService.ReceivedDbChanges.Count, Is.EqualTo(1));
            }
        }
        finally
        {
            client.Close();
        }

        Assert.That(_z9OpenCommunityProtocolService.LastException, Is.Null);
    }

    [Test]
    public void DevActionReq_HostSendsDoorUnlock_PanelRespondsSuccess()
    {
        var (client, mis, mos) = ConnectAsHost();

        try
        {
            // Identify first
            SendIdentification(mos);
            var identResponse = mis.Read();
            Assert.That(identResponse.Type, Is.EqualTo(SpCoreMessage.Types.Type.Identification));

            // Send DevActionReq
            var devActionMsg = new SpCoreMessage
            {
                Type = SpCoreMessage.Types.Type.DevActionReq,
                DevActionReq = new DevActionReq
                {
                    DevUnid = 1,
                    DevActionType = DevActionType.DoorMomentaryUnlock,
                    RequestId = 99
                }
            };
            mos.Write(devActionMsg);

            // Read response
            var response = mis.Read();
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Type, Is.EqualTo(SpCoreMessage.Types.Type.DevActionResp));
            Assert.That(response.DevActionResp.RequestId, Is.EqualTo(99));
            Assert.That(response.DevActionResp.ExceptionCase, Is.EqualTo(DevActionResp.ExceptionOneofCase.None));

            lock (_z9OpenCommunityProtocolService.ReceivedDevActions)
            {
                Assert.That(_z9OpenCommunityProtocolService.ReceivedDevActions.Count, Is.EqualTo(1));
            }
        }
        finally
        {
            client.Close();
        }

        Assert.That(_z9OpenCommunityProtocolService.LastException, Is.Null);
    }

    [Test]
    public void ConnectAndDisconnect_CleanLifecycle()
    {
        var (client, mis, mos) = ConnectAsHost();

        try
        {
            SendIdentification(mos);
            var identResponse = mis.Read();
            Assert.That(identResponse.Type, Is.EqualTo(SpCoreMessage.Types.Type.Identification));

            Thread.Sleep(100);
            Assert.That(_z9OpenCommunityProtocolService.IsClientConnected, Is.True);
        }
        finally
        {
            client.Close();
        }

        // Give the panel time to detect the disconnect
        Thread.Sleep(500);
        Assert.That(_z9OpenCommunityProtocolService.IsClientConnected, Is.False);
        Assert.That(_z9OpenCommunityProtocolService.LastException, Is.Null);
    }
}
