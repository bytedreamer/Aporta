using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models;
using Microsoft.Extensions.Logging;
using Z9.Protobuf;
using Z9.Spcore.Proto;

namespace Aporta.Core.Services;

public class Z9OpenCommunityProtocolService : IDisposable
{
    private const bool AutoCreatePersonForCredential = true;

    private readonly ILogger<Z9OpenCommunityProtocolService> _logger;
    private readonly CredentialRepository _credentialRepository;
    private readonly PersonRepository _personRepository;
    private TcpListener _listener;
    private Thread _thread;
    private volatile bool _stopping;
    private TcpClient _client;
    private SpCoreMessageInputStream _mis;
    private SpCoreMessageOutputStream _mos;
    private readonly object _writeLock = new();

    public int Port { get; private set; }
    public bool IsClientConnected { get; private set; }
    public bool IsIdentified { get; private set; }
    public List<DbChange> ReceivedDbChanges { get; } = new();
    public List<DevActionReq> ReceivedDevActions { get; } = new();
    public Exception LastException { get; private set; }

    public Z9OpenCommunityProtocolService(
        ILogger<Z9OpenCommunityProtocolService> logger,
        IDataAccess dataAccess)
    {
        _logger = logger;
        _credentialRepository = new CredentialRepository(dataAccess);
        _personRepository = new PersonRepository(dataAccess);
    }

    public void Start(int port = 7000)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _logger.LogInformation("Z9/Open Community protocol service started on port {Port}", Port);

        _thread = new Thread(Run) { IsBackground = true, Name = "Z9OpenCommunityProtocolService" };
        _thread.Start();
    }

    public void Stop()
    {
        _stopping = true;
        _listener?.Stop();
        _client?.Close();
        _thread?.Join(TimeSpan.FromSeconds(5));

        _logger.LogInformation("Z9/Open Community protocol service stopped");
    }

    public void Dispose()
    {
        Stop();
    }

    private void Run()
    {
        try
        {
            _client = _listener.AcceptTcpClient();
            IsClientConnected = true;
            _logger.LogInformation("Z9/Open Community host connected");

            var stream = _client.GetStream();
            _mis = new SpCoreMessageInputStream(stream);
            _mos = new SpCoreMessageOutputStream(stream);

            while (!_stopping)
            {
                SpCoreMessage message;
                try
                {
                    message = _mis.Read();
                }
                catch (IOException)
                {
                    if (_stopping) break;
                    throw;
                }

                if (message != null)
                {
                    HandleMessage(message);
                }
            }
        }
        catch (SocketException) when (_stopping)
        {
            // Expected when stopping
        }
        catch (ObjectDisposedException) when (_stopping)
        {
            // Expected when stopping
        }
        catch (EndOfStreamException)
        {
            _logger.LogInformation("Z9/Open Community host disconnected");
        }
        catch (IOException ex) when (ex.InnerException is SocketException)
        {
            _logger.LogInformation("Z9/Open Community host disconnected");
        }
        catch (Exception ex)
        {
            LastException = ex;
            _logger.LogError(ex, "Z9/Open Community protocol service error");
        }
        finally
        {
            IsClientConnected = false;
        }
    }

    private void HandleMessage(SpCoreMessage message)
    {
        switch (message.Type)
        {
            case SpCoreMessage.Types.Type.Ping:
                break;

            case SpCoreMessage.Types.Type.Identification:
                IsIdentified = true;
                _logger.LogInformation("Z9/Open Community host identified");
                SendIdentification();
                break;

            case SpCoreMessage.Types.Type.DbChange:
                lock (ReceivedDbChanges)
                {
                    ReceivedDbChanges.Add(message.DbChange);
                }
                HandleDbChange(message.DbChange);
                SendDbChangeResp(message.DbChange.RequestId);
                break;

            case SpCoreMessage.Types.Type.DevActionReq:
                lock (ReceivedDevActions)
                {
                    ReceivedDevActions.Add(message.DevActionReq);
                }
                _logger.LogDebug("Received DevActionReq (requestId={RequestId})", message.DevActionReq.RequestId);
                SendDevActionResp(message.DevActionReq.RequestId);
                break;

            case SpCoreMessage.Types.Type.EvtControl:
                break;

            default:
                _logger.LogDebug("Ignoring message type {Type}", message.Type);
                break;
        }
    }

    private void HandleDbChange(DbChange dbChange)
    {
        _logger.LogDebug("Processing DbChange (requestId={RequestId})", dbChange.RequestId);

        // Not yet supported by Aporta, accepted silently:
        // Sched, HolCal, HolType, CredTemplate, DataLayout, DataFormat, Priv

        foreach (var cred in dbChange.Cred)
        {
            ProcessCredential(cred);
        }

        foreach (var credDelete in dbChange.CredDelete)
        {
            _logger.LogInformation("Deleting credential {Unid}", credDelete);
            _credentialRepository.Delete(credDelete).GetAwaiter().GetResult();
        }

        // TODO: Dev - map to Aporta devices/doors
    }

    private void ProcessCredential(Cred cred)
    {
        SpCoreProtoUtil.InitRequired(cred);

        var credNumber = "";
        if (cred.CardPin?.CredNum != null && cred.CardPin.CredNum.BytesCase != BigIntegerData.BytesOneofCase.None)
        {
            credNumber = SpCoreProtoUtil.ToBigInteger(cred.CardPin.CredNum).ToString();
        }

        if (string.IsNullOrEmpty(credNumber))
        {
            _logger.LogWarning(
                "Credential Unid={Unid} has no card number, deleting from Aporta if it exists", cred.Unid);
            _credentialRepository.RevokePerson(cred.Unid, cred.Unid).GetAwaiter().GetResult();
            _credentialRepository.Delete(cred.Unid).GetAwaiter().GetResult();
            if (AutoCreatePersonForCredential)
            {
                _personRepository.Delete(cred.Unid).GetAwaiter().GetResult();
            }
            return;
        }

        var personName = !string.IsNullOrWhiteSpace(cred.Name) ? cred.Name : credNumber;

        _logger.LogInformation("Processing credential Unid={Unid} Number={Number} Name={Name}",
            cred.Unid, credNumber, personName);

        if (AutoCreatePersonForCredential)
        {
            var person = new Person
            {
                FirstName = personName,
                Enabled = cred.Enabled
            };
            _personRepository.Upsert(person, cred.Unid).GetAwaiter().GetResult();
        }

        var credential = new Credential
        {
            Number = credNumber,
            Enabled = cred.Enabled
        };
        _credentialRepository.Upsert(credential, cred.Unid).GetAwaiter().GetResult();

        if (AutoCreatePersonForCredential)
        {
            _credentialRepository.RevokePerson(cred.Unid, cred.Unid).GetAwaiter().GetResult();
            _credentialRepository.AssignPerson(cred.Unid, cred.Unid, cred.Enabled)
                .GetAwaiter().GetResult();
        }
    }

    private void SendIdentification()
    {
        var message = new SpCoreMessage
        {
            Type = SpCoreMessage.Types.Type.Identification,
            Identification = new Identification
            {
                Id = "aporta-panel",
                SoftwareVersion = "1.0.0",
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

        WriteMessage(message);
    }

    private void SendDbChangeResp(long requestId)
    {
        var message = new SpCoreMessage
        {
            Type = SpCoreMessage.Types.Type.DbChangeResp,
            DbChangeResp = new DbChangeResp
            {
                RequestId = requestId
            }
        };

        WriteMessage(message);
    }

    private void SendDevActionResp(long requestId)
    {
        var message = new SpCoreMessage
        {
            Type = SpCoreMessage.Types.Type.DevActionResp,
            DevActionResp = new DevActionResp
            {
                RequestId = requestId
            }
        };

        WriteMessage(message);
    }

    private void WriteMessage(SpCoreMessage message)
    {
        lock (_writeLock)
        {
            _mos?.Write(message);
        }
    }
}
