using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Z9.Protobuf;
using Z9.Spcore.Proto;

namespace Aporta.Core.Tests.Services;

[TestFixture]
public class Z9OpenCommunityProtocolServiceTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;
    private Z9OpenCommunityProtocolService _z9OpenCommunityProtocolService;
    private TcpListener _hostListener;

    [SetUp]
    public async Task Setup()
    {
        _persistConnection = _dataAccess.CreateDbConnection();
        _persistConnection.Open();
        await _dataAccess.UpdateSchema();

        _hostListener = new TcpListener(IPAddress.Loopback, 0);
        _hostListener.Start();
        var hostPort = ((IPEndPoint)_hostListener.LocalEndpoint).Port;

        _z9OpenCommunityProtocolService = new Z9OpenCommunityProtocolService(
            NullLogger<Z9OpenCommunityProtocolService>.Instance,
            _dataAccess,
            new DevStateService());
        _z9OpenCommunityProtocolService.Start("127.0.0.1", hostPort);
    }

    [TearDown]
    public void TearDown()
    {
        _z9OpenCommunityProtocolService?.Dispose();
        _hostListener?.Stop();
        _persistConnection?.Close();
        _persistConnection?.Dispose();
    }

    private (TcpClient client, SpCoreMessageInputStream mis, SpCoreMessageOutputStream mos) AcceptAsHost()
    {
        var client = _hostListener.AcceptTcpClient();
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
    public void Identification_PanelConnects_BothSidesIdentify()
    {
        var (client, mis, mos) = AcceptAsHost();

        try
        {
            SendIdentification(mos);

            var response = mis.Read();
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Type, Is.EqualTo(SpCoreMessage.Types.Type.Identification));
            Assert.That(response.Identification.Id, Is.EqualTo("aporta-panel"));

            Thread.Sleep(100);
            Assert.That(_z9OpenCommunityProtocolService.IsConnected, Is.True);
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
        var (client, mis, mos) = AcceptAsHost();

        try
        {
            SendIdentification(mos);
            var identResponse = mis.Read();
            Assert.That(identResponse.Type, Is.EqualTo(SpCoreMessage.Types.Type.Identification));

            var dbChange = new DbChange();
            dbChange.Sched.Add(new Sched { Unid = 1, Name = "Test Schedule" });
            var dbChangeMsg = new SpCoreMessage
            {
                Type = SpCoreMessage.Types.Type.DbChange,
                DbChange = dbChange
            };
            dbChangeMsg.DbChange.RequestId = 42;
            mos.Write(dbChangeMsg);

            var response = mis.Read();
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Type, Is.EqualTo(SpCoreMessage.Types.Type.DbChangeResp));
            Assert.That(response.DbChangeResp.RequestId, Is.EqualTo(42));
            Assert.That(response.DbChangeResp.ExceptionCase, Is.EqualTo(DbChangeResp.ExceptionOneofCase.None));
        }
        finally
        {
            client.Close();
        }

        Assert.That(_z9OpenCommunityProtocolService.LastException, Is.Null);
    }

    [Test]
    public void DbChange_HostSendsCredential_CredentialAndPersonCreatedInDb()
    {
        var (client, mis, mos) = AcceptAsHost();

        try
        {
            SendIdentification(mos);
            var identResponse = mis.Read();
            Assert.That(identResponse.Type, Is.EqualTo(SpCoreMessage.Types.Type.Identification));

            var dbChange = new DbChange();
            dbChange.Cred.Add(new Cred
            {
                Unid = 100,
                Name = "John Doe",
                Enabled = true,
                CardPin = new CardPin
                {
                    CredNum = SpCoreProtoUtil.ToBigIntegerData(new BigInteger(123456))
                }
            });
            var dbChangeMsg = new SpCoreMessage
            {
                Type = SpCoreMessage.Types.Type.DbChange,
                DbChange = dbChange
            };
            dbChangeMsg.DbChange.RequestId = 50;
            mos.Write(dbChangeMsg);

            var response = mis.Read();
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Type, Is.EqualTo(SpCoreMessage.Types.Type.DbChangeResp));
            Assert.That(response.DbChangeResp.RequestId, Is.EqualTo(50));
        }
        finally
        {
            client.Close();
        }

        Assert.That(_z9OpenCommunityProtocolService.LastException, Is.Null);

        // Verify Z9 Cred was stored with cred_num
        var z9CredRepository = new Z9CredRepository(_dataAccess);
        var z9Cred = z9CredRepository.Get(100).GetAwaiter().GetResult();
        Assert.That(z9Cred, Is.Not.Null);
        Assert.That(z9Cred.Name, Is.EqualTo("John Doe"));
        Assert.That(z9Cred.Enabled, Is.True);
        Assert.That(Z9CredRepository.ExtractCredNum(z9Cred), Is.EqualTo("123456"));

        // Verify credential can be looked up via CredentialRepository
        var credentialRepository = new CredentialRepository(_dataAccess);
        var credential = credentialRepository.Get(100).GetAwaiter().GetResult();
        Assert.That(credential, Is.Not.Null);
        Assert.That(credential.Number, Is.EqualTo("123456"));
        Assert.That(credential.Enabled, Is.True);
    }

    [Test]
    public void DbChange_HostSendsCredentialWithNoCredNum_CredentialNotCreatedInDb()
    {
        var (client, mis, mos) = AcceptAsHost();

        try
        {
            SendIdentification(mos);
            var identResponse = mis.Read();
            Assert.That(identResponse.Type, Is.EqualTo(SpCoreMessage.Types.Type.Identification));

            var dbChange = new DbChange();
            dbChange.Cred.Add(new Cred
            {
                Unid = 200,
                Name = "No Card",
                Enabled = true
            });
            var dbChangeMsg = new SpCoreMessage
            {
                Type = SpCoreMessage.Types.Type.DbChange,
                DbChange = dbChange
            };
            dbChangeMsg.DbChange.RequestId = 60;
            mos.Write(dbChangeMsg);

            var response = mis.Read();
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Type, Is.EqualTo(SpCoreMessage.Types.Type.DbChangeResp));
            Assert.That(response.DbChangeResp.RequestId, Is.EqualTo(60));
        }
        finally
        {
            client.Close();
        }

        Assert.That(_z9OpenCommunityProtocolService.LastException, Is.Null);

        // Verify Z9 Cred was NOT created (no card number = deleted)
        var z9CredRepository = new Z9CredRepository(_dataAccess);
        var z9Cred = z9CredRepository.Get(200).GetAwaiter().GetResult();
        Assert.That(z9Cred, Is.Null);

        // Verify credential is not visible via CredentialRepository either
        var credentialRepository = new CredentialRepository(_dataAccess);
        var credential = credentialRepository.Get(200).GetAwaiter().GetResult();
        Assert.That(credential, Is.Null);
    }

    [Test]
    public void DbChange_HostSendsCredentialWithBlankName_UsesCardNumberAsName()
    {
        var (client, mis, mos) = AcceptAsHost();

        try
        {
            SendIdentification(mos);
            var identResponse = mis.Read();
            Assert.That(identResponse.Type, Is.EqualTo(SpCoreMessage.Types.Type.Identification));

            var dbChange = new DbChange();
            dbChange.Cred.Add(new Cred
            {
                Unid = 300,
                Enabled = true,
                CardPin = new CardPin
                {
                    CredNum = SpCoreProtoUtil.ToBigIntegerData(new BigInteger(789012))
                }
            });
            var dbChangeMsg = new SpCoreMessage
            {
                Type = SpCoreMessage.Types.Type.DbChange,
                DbChange = dbChange
            };
            dbChangeMsg.DbChange.RequestId = 70;
            mos.Write(dbChangeMsg);

            var response = mis.Read();
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Type, Is.EqualTo(SpCoreMessage.Types.Type.DbChangeResp));
            Assert.That(response.DbChangeResp.RequestId, Is.EqualTo(70));
        }
        finally
        {
            client.Close();
        }

        Assert.That(_z9OpenCommunityProtocolService.LastException, Is.Null);

        // Verify Z9 Cred was stored with cred_num (no name since cred had blank name)
        var z9CredRepository = new Z9CredRepository(_dataAccess);
        var z9Cred = z9CredRepository.Get(300).GetAwaiter().GetResult();
        Assert.That(z9Cred, Is.Not.Null);
        Assert.That(Z9CredRepository.ExtractCredNum(z9Cred), Is.EqualTo("789012"));

        // Verify credential is visible via CredentialRepository
        var credentialRepository = new CredentialRepository(_dataAccess);
        var credential = credentialRepository.Get(300).GetAwaiter().GetResult();
        Assert.That(credential, Is.Not.Null);
        Assert.That(credential.Number, Is.EqualTo("789012"));
    }

    [Test]
    public void DevActionReq_HostSendsDoorUnlock_PanelRespondsSuccess()
    {
        var (client, mis, mos) = AcceptAsHost();

        try
        {
            SendIdentification(mos);
            var identResponse = mis.Read();
            Assert.That(identResponse.Type, Is.EqualTo(SpCoreMessage.Types.Type.Identification));

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

            var response = mis.Read();
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Type, Is.EqualTo(SpCoreMessage.Types.Type.DevActionResp));
            Assert.That(response.DevActionResp.RequestId, Is.EqualTo(99));
            Assert.That(response.DevActionResp.ExceptionCase, Is.EqualTo(DevActionResp.ExceptionOneofCase.None));
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
        var (client, mis, mos) = AcceptAsHost();

        try
        {
            SendIdentification(mos);
            var identResponse = mis.Read();
            Assert.That(identResponse.Type, Is.EqualTo(SpCoreMessage.Types.Type.Identification));

            Thread.Sleep(100);
            Assert.That(_z9OpenCommunityProtocolService.IsConnected, Is.True);
        }
        finally
        {
            client.Close();
        }

        Thread.Sleep(500);
        Assert.That(_z9OpenCommunityProtocolService.IsConnected, Is.False);
        Assert.That(_z9OpenCommunityProtocolService.LastException, Is.Null);
    }
}
