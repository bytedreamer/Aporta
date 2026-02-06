using System;
using System.Collections.Generic;
using System.IO;
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
    private const int DefaultOsdpTcpPort = 9843;

    private readonly ILogger<Z9OpenCommunityProtocolService> _logger;
    private readonly CredentialRepository _credentialRepository;
    private readonly PersonRepository _personRepository;

    // Received OSDP CredReader configurations from Z9
    private readonly List<OsdpReaderConfig> _osdpReaderConfigs = new();

    // Mapping from OSDP endpoint ID prefix to Z9 device unid
    // Key format: "{host}:{port}:{osdpAddress}" (e.g., "localhost:9843:0")
    private readonly Dictionary<string, OsdpReaderConfig> _endpointToConfig = new();

    /// <summary>
    /// OSDP reader configuration extracted from Z9 Dev messages.
    /// </summary>
    public class OsdpReaderConfig
    {
        public int Unid { get; set; }
        public string Name { get; set; }
        public string Host { get; set; }
        public int TcpPort { get; set; } = DefaultOsdpTcpPort;
        public int OsdpAddress { get; set; }
        public int BaudRate { get; set; } = 9600;
    }
    private Thread _thread;
    private volatile bool _stopping;
    private TcpClient _client;
    private SpCoreMessageInputStream _mis;
    private SpCoreMessageOutputStream _mos;
    private readonly object _writeLock = new();
    private string _id = "aporta-panel";

    public bool IsConnected { get; private set; }
    public bool IsIdentified { get; private set; }
    public List<DbChange> ReceivedDbChanges { get; } = new();
    public List<DevActionReq> ReceivedDevActions { get; } = new();
    public List<OsdpReaderConfig> OsdpReaderConfigs => _osdpReaderConfigs;
    public Exception LastException { get; private set; }

    public Z9OpenCommunityProtocolService(
        ILogger<Z9OpenCommunityProtocolService> logger,
        IDataAccess dataAccess)
    {
        _logger = logger;
        _credentialRepository = new CredentialRepository(dataAccess);
        _personRepository = new PersonRepository(dataAccess);
    }

    public void Start(string host, int port, string id = null)
    {
        _stopping = false;
        if (!string.IsNullOrWhiteSpace(id))
            _id = id;

        _logger.LogInformation("Z9/Open Community protocol service connecting to {Host}:{Port} id={Id}", host, port, _id);

        _thread = new Thread(() => Run(host, port)) { IsBackground = true, Name = "Z9OpenCommunityProtocolService" };
        _thread.Start();
    }

    public void Stop()
    {
        _stopping = true;
        _client?.Close();
        _thread?.Join(TimeSpan.FromSeconds(5));

        _logger.LogInformation("Z9/Open Community protocol service stopped");
    }

    public void Dispose()
    {
        Stop();
    }

    private void Run(string host, int port)
    {
        try
        {
            _client = new TcpClient(host, port);
            IsConnected = true;
            _logger.LogInformation("Connected to Z9/Open Community host at {Host}:{Port}", host, port);

            var stream = _client.GetStream();
            _mis = new SpCoreMessageInputStream(stream);
            _mos = new SpCoreMessageOutputStream(stream);

            // Initiating side sends identification first
            SendIdentification();

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
            IsConnected = false;
        }
    }

    private void HandleMessage(SpCoreMessage message)
    {
        _logger.LogInformation("Received message type {Type}", message.Type);

        switch (message.Type)
        {
            case SpCoreMessage.Types.Type.Ping:
                break;

            case SpCoreMessage.Types.Type.Identification:
                IsIdentified = true;
                _logger.LogInformation("Z9/Open Community host identified");
                // Don't re-send - we already sent ours as the initiating side
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
                _logger.LogInformation("Received DevActionReq (requestId={RequestId})", message.DevActionReq.RequestId);
                SendDevActionResp(message.DevActionReq.RequestId);
                break;

            case SpCoreMessage.Types.Type.EvtControl:
                break;

            default:
                _logger.LogInformation("Ignoring unhandled message type {Type}", message.Type);
                break;
        }
    }

    private void HandleDbChange(DbChange dbChange)
    {
        _logger.LogInformation("Processing DbChange (requestId={RequestId})", dbChange.RequestId);

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

        // Process Dev messages - extract OSDP reader configurations
        foreach (var dev in dbChange.Dev)
        {
            ProcessDev(dev);
        }
    }

    private void ProcessDev(Dev dev)
    {
        SpCoreProtoUtil.InitRequired(dev);

        // Check if this is an OSDP credential reader
        if (dev.DevType != DevType.CredReader)
            return;

        var credReaderConfig = dev.ExtCredReader?.CredReaderConfig;
        if (credReaderConfig == null)
            return;

        // Check for OSDP comm type
        if (credReaderConfig.CommType != CredReaderCommType.OsdpHalfDuplex)
            return;

        var serialPortAddress = credReaderConfig.SerialPortAddress;
        if (string.IsNullOrWhiteSpace(serialPortAddress))
        {
            _logger.LogWarning("OSDP CredReader {Name} (unid={Unid}) has no serialPortAddress, skipping",
                dev.Name, dev.Unid);
            return;
        }

        // Parse host and port from serialPortAddress (e.g., "localhost:9843" or just "localhost")
        string host;
        int tcpPort;
        var colonIndex = serialPortAddress.LastIndexOf(':');
        if (colonIndex > 0 && int.TryParse(serialPortAddress.Substring(colonIndex + 1), out tcpPort))
        {
            host = serialPortAddress.Substring(0, colonIndex);
        }
        else
        {
            host = serialPortAddress;
            tcpPort = DefaultOsdpTcpPort;
        }

        var osdpConfig = new OsdpReaderConfig
        {
            Unid = dev.Unid,
            Name = dev.Name ?? $"OSDP Reader {dev.Unid}",
            Host = host,
            TcpPort = tcpPort,
            OsdpAddress = dev.Port,  // OSDP polling address (0-126)
            BaudRate = dev.Speed > 0 ? dev.Speed : 9600
        };

        _logger.LogInformation(
            "Received OSDP CredReader config: Name={Name}, Host={Host}:{TcpPort}, OsdpAddress={OsdpAddress}",
            osdpConfig.Name, osdpConfig.Host, osdpConfig.TcpPort, osdpConfig.OsdpAddress);

        lock (_osdpReaderConfigs)
        {
            // Remove existing config with same unid
            _osdpReaderConfigs.RemoveAll(c => c.Unid == osdpConfig.Unid);
            _osdpReaderConfigs.Add(osdpConfig);
        }

        // Store mapping from endpoint ID prefix to config for event correlation
        var endpointIdPrefix = $"{osdpConfig.Host}:{osdpConfig.TcpPort}:{osdpConfig.OsdpAddress}";
        lock (_endpointToConfig)
        {
            _endpointToConfig[endpointIdPrefix] = osdpConfig;
        }

        // Notify that OSDP configuration is available
        OsdpConfigurationReceived?.Invoke(this, osdpConfig);
    }

    /// <summary>
    /// Event raised when OSDP reader configuration is received from Z9.
    /// </summary>
    public event EventHandler<OsdpReaderConfig> OsdpConfigurationReceived;

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
                Id = _id,
                SoftwareVersion = "1.0.0",
                ProtocolVersion = "0.1",
                MaxBodyLength = SpCoreMessageHeader.MAX_LENGTH,
                SpCoreDevMod = DevMod.IoControllerCommunity,
                ProtocolCapabilities = new ProtocolCapabilities
                {
                    SupportsIdentificationPassword = true,
                    SupportsIdentificationPasswordUpstream = true
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

    /// <summary>
    /// Gets the OsdpReaderConfig for an endpoint ID, if available.
    /// </summary>
    /// <param name="endpointId">The OSDP endpoint ID (format: "host:port:address:R#")</param>
    /// <returns>The config if found, null otherwise.</returns>
    public OsdpReaderConfig GetConfigForEndpoint(string endpointId)
    {
        if (string.IsNullOrEmpty(endpointId))
            return null;

        // Endpoint ID format: "{host}:{port}:{address}:R{readerNum}"
        // We need to extract "{host}:{port}:{address}" to match our mapping
        var parts = endpointId.Split(':');
        if (parts.Length < 3)
            return null;

        // For "localhost:9843:0:R0" we want "localhost:9843:0"
        var endpointIdPrefix = $"{parts[0]}:{parts[1]}:{parts[2]}";

        lock (_endpointToConfig)
        {
            return _endpointToConfig.TryGetValue(endpointIdPrefix, out var config) ? config : null;
        }
    }

    /// <summary>
    /// Sends a CRED_READER_ONLINE or CRED_READER_OFFLINE event to Z9.
    /// </summary>
    /// <param name="config">The OSDP reader configuration.</param>
    /// <param name="isOnline">True if reader came online, false if offline.</param>
    public void SendCredReaderOnlineEvent(OsdpReaderConfig config, bool isOnline)
    {
        if (config == null)
        {
            _logger.LogWarning("Cannot send cred reader event: config is null");
            return;
        }

        if (!IsConnected)
        {
            _logger.LogWarning("Cannot send cred reader event: not connected");
            return;
        }

        var evtCode = isOnline ? EvtCode.CredReaderOnline : EvtCode.CredReaderOffline;
        var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var evt = new Evt
        {
            EvtCode = evtCode,
            HwTime = new DateTimeData { Millis = nowMillis },
            DbTime = new DateTimeData { Millis = nowMillis },
            Consumed = false,
            Priority = 0,
            EvtDevRef = new EvtDevRef
            {
                Unid = config.Unid,
                Name = config.Name,
                DevType = DevType.CredReader
            }
        };

        var message = new SpCoreMessage
        {
            Type = SpCoreMessage.Types.Type.Evt
        };
        message.Evt.Add(evt);

        _logger.LogInformation("Sending {EvtCode} event for reader {Name} (unid={Unid})",
            evtCode, config.Name, config.Unid);

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
