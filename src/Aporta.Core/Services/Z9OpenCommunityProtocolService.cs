using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
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

    // Data formats/layouts/templates received from Z9 (keyed by unid)
    private readonly Dictionary<int, DataFormat> _dataFormats = new();
    private readonly Dictionary<int, DataLayout> _dataLayouts = new();
    private readonly Dictionary<int, CredTemplate> _credTemplates = new();

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

        // Process DataFormat first (needed by DataLayout)
        foreach (var dataFormat in dbChange.DataFormat)
        {
            ProcessDataFormat(dataFormat);
        }

        // Process DataLayout (needed by CredTemplate)
        foreach (var dataLayout in dbChange.DataLayout)
        {
            ProcessDataLayout(dataLayout);
        }

        // Process CredTemplate (needed by Cred)
        foreach (var credTemplate in dbChange.CredTemplate)
        {
            ProcessCredTemplate(credTemplate);
        }

        // Process credentials
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

    private void ProcessDataFormat(DataFormat dataFormat)
    {
        SpCoreProtoUtil.InitRequired(dataFormat);
        _logger.LogDebug("Storing DataFormat: unid={Unid}, name={Name}, type={Type}",
            dataFormat.Unid, dataFormat.Name, dataFormat.DataFormatType);
        lock (_dataFormats)
        {
            _dataFormats[dataFormat.Unid] = dataFormat;
        }
    }

    private void ProcessDataLayout(DataLayout dataLayout)
    {
        SpCoreProtoUtil.InitRequired(dataLayout);
        _logger.LogDebug("Storing DataLayout: unid={Unid}, name={Name}, type={Type}",
            dataLayout.Unid, dataLayout.Name, dataLayout.LayoutType);
        lock (_dataLayouts)
        {
            _dataLayouts[dataLayout.Unid] = dataLayout;
        }
    }

    private void ProcessCredTemplate(CredTemplate credTemplate)
    {
        SpCoreProtoUtil.InitRequired(credTemplate);
        _logger.LogDebug("Storing CredTemplate: unid={Unid}, name={Name}",
            credTemplate.Unid, credTemplate.Name);
        lock (_credTemplates)
        {
            _credTemplates[credTemplate.Unid] = credTemplate;
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

        if (cred.CardPin?.CredNum == null || cred.CardPin.CredNum.BytesCase == BigIntegerData.BytesOneofCase.None)
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

        var credNum = SpCoreProtoUtil.ToBigInteger(cred.CardPin.CredNum);
        var facilityCode = cred.CardPin.FacilityCodeCase == CardPin.FacilityCodeOneofCase.FacilityCode
            ? (int?)cred.CardPin.FacilityCode : null;

        // Find the data format to encode the credential number as bits
        var bitString = EncodeCredentialAsBits(cred, credNum, facilityCode);
        if (string.IsNullOrEmpty(bitString))
        {
            // Fallback: store as decimal string if no format available
            bitString = credNum.ToString();
            _logger.LogWarning("No data format found for credential {Unid}, storing as decimal: {Number}",
                cred.Unid, bitString);
        }

        var personName = !string.IsNullOrWhiteSpace(cred.Name) ? cred.Name : bitString;

        _logger.LogInformation("Processing credential Unid={Unid} Number={Number} Name={Name}",
            cred.Unid, bitString, personName);

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
            Number = bitString,
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

    /// <summary>
    /// Encodes a credential number as a bit string using the data format from the credential's template.
    /// </summary>
    private string EncodeCredentialAsBits(Cred cred, BigInteger credNum, int? facilityCode)
    {
        // Find the credential template
        if (cred.CredTemplateUnid == 0)
        {
            _logger.LogDebug("Credential {Unid} has no CredTemplateUnid", cred.Unid);
            return null;
        }

        CredTemplate credTemplate;
        lock (_credTemplates)
        {
            if (!_credTemplates.TryGetValue(cred.CredTemplateUnid, out credTemplate))
            {
                _logger.LogDebug("CredTemplate {Unid} not found", cred.CredTemplateUnid);
                return null;
            }
        }

        // Get the data layout from the template
        var cardPinTemplate = credTemplate.CardPinTemplate;
        if (cardPinTemplate == null)
        {
            _logger.LogDebug("CredTemplate {Unid} has no CardPinTemplate", credTemplate.Unid);
            return null;
        }

        // If anyDataLayout is true, try all available formats
        if (cardPinTemplate.AnyDataLayoutCase == CardPinTemplate.AnyDataLayoutOneofCase.AnyDataLayout
            && cardPinTemplate.AnyDataLayout)
        {
            // Use the first available binary format
            lock (_dataFormats)
            {
                foreach (var df in _dataFormats.Values)
                {
                    if (df.DataFormatType == DataFormatType.Binary && df.ExtBinaryFormat != null)
                    {
                        var bits = EncodeWithBinaryFormatter(df, credNum, facilityCode);
                        if (bits != null)
                        {
                            _logger.LogDebug("Encoded credential using format {Name}", df.Name);
                            return bits;
                        }
                    }
                }
            }
            return null;
        }

        // Get specific data layout
        if (cardPinTemplate.DataLayoutUnidCase != CardPinTemplate.DataLayoutUnidOneofCase.DataLayoutUnid)
        {
            _logger.LogDebug("CardPinTemplate has no DataLayoutUnid");
            return null;
        }

        DataLayout dataLayout;
        lock (_dataLayouts)
        {
            if (!_dataLayouts.TryGetValue(cardPinTemplate.DataLayoutUnid, out dataLayout))
            {
                _logger.LogDebug("DataLayout {Unid} not found", cardPinTemplate.DataLayoutUnid);
                return null;
            }
        }

        // Get the data format from BasicDataLayout
        if (dataLayout.ExtBasicDataLayout == null ||
            dataLayout.ExtBasicDataLayout.DataFormatUnidCase != BasicDataLayout.DataFormatUnidOneofCase.DataFormatUnid)
        {
            _logger.LogDebug("DataLayout {Unid} has no BasicDataLayout or DataFormatUnid", dataLayout.Unid);
            return null;
        }

        DataFormat dataFormat;
        lock (_dataFormats)
        {
            if (!_dataFormats.TryGetValue(dataLayout.ExtBasicDataLayout.DataFormatUnid, out dataFormat))
            {
                _logger.LogDebug("DataFormat {Unid} not found", dataLayout.ExtBasicDataLayout.DataFormatUnid);
                return null;
            }
        }

        if (dataFormat.DataFormatType != DataFormatType.Binary || dataFormat.ExtBinaryFormat == null)
        {
            _logger.LogDebug("DataFormat {Unid} is not binary", dataFormat.Unid);
            return null;
        }

        return EncodeWithBinaryFormatter(dataFormat, credNum, facilityCode);
    }

    /// <summary>
    /// Encodes a credential number into a bit string using the existing BinaryFormatter.
    /// </summary>
    private string EncodeWithBinaryFormatter(DataFormat dataFormat, BigInteger credNum, int? facilityCode)
    {
        try
        {
            var binaryFormat = dataFormat.ExtBinaryFormat;
            var bitCount = binaryFormat.MaxBits;

            // Create a BitBuffer with the required size
            var bb = new BitBuffer(bitCount, bitCount);

            // Create DecodedRead with the field values
            var decodedRead = new DecodedRead();
            decodedRead.GetElements().Add(new DecodedReadElement(DataFormatField.CredNum, credNum));
            if (facilityCode.HasValue)
            {
                decodedRead.GetElements().Add(new DecodedReadElement(DataFormatField.FacilityCode, facilityCode.Value));
            }

            // Use BinaryFormatter to encode
            var formatter = new BinaryFormatter(dataFormat);
            formatter.Encode(bb, decodedRead);

            return bb.ToBinaryString();
        }
        catch (BinaryFormatterException ex)
        {
            _logger.LogDebug(ex, "Failed to encode credential with format {Name}", dataFormat.Name);
            return null;
        }
    }

    /// <summary>
    /// Decodes a raw bit string from a card read into a credential number by trying all known formats.
    /// </summary>
    /// <param name="rawBits">The raw bit string from the card reader (e.g., "10110010000110000001110011")</param>
    /// <returns>Tuple of (credNum, facilityCode, formatName) if decoded, or null if no format matched.</returns>
    public (BigInteger credNum, int? facilityCode, string formatName)? DecodeCardRead(string rawBits)
    {
        if (string.IsNullOrEmpty(rawBits))
            return null;

        BitBuffer bb;
        try
        {
            bb = BitBuffer.FromBinaryString(rawBits);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Invalid bit string: {RawBits}", rawBits);
            return null;
        }

        lock (_dataFormats)
        {
            foreach (var dataFormat in _dataFormats.Values)
            {
                if (dataFormat.DataFormatType != DataFormatType.Binary || dataFormat.ExtBinaryFormat == null)
                    continue;

                try
                {
                    var formatter = new BinaryFormatter(dataFormat);
                    var decodedRead = formatter.Decode(bb);

                    // Extract credential number
                    var credNumElements = decodedRead.GetElementsMatchingField(DataFormatField.CredNum);
                    if (credNumElements.Count == 0)
                        continue;

                    var credNum = credNumElements[0].GetValue();

                    // Extract facility code if present
                    int? facilityCode = null;
                    var fcElements = decodedRead.GetElementsMatchingField(DataFormatField.FacilityCode);
                    if (fcElements.Count > 0)
                    {
                        facilityCode = (int)fcElements[0].GetValue();
                    }

                    _logger.LogDebug("Decoded card read using format {Name}: credNum={CredNum}, fc={FacilityCode}",
                        dataFormat.Name, credNum, facilityCode);

                    return (credNum, facilityCode, dataFormat.Name);
                }
                catch (BinaryFormatterException)
                {
                    // This format didn't match, try next
                }
            }
        }

        _logger.LogDebug("No format matched for card read: {RawBits}", rawBits);
        return null;
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

    /// <summary>
    /// Sends an access event (granted or denied) to Z9.
    /// </summary>
    /// <param name="config">The OSDP reader configuration (to determine the device).</param>
    /// <param name="isGranted">True if access was granted, false if denied.</param>
    /// <param name="credNum">The decoded credential number (null if unable to decode).</param>
    /// <param name="facilityCode">The facility code (null if not available or not decoded).</param>
    /// <param name="rawBits">The raw bit string from the card read (stored in EvtData if credNum is null).</param>
    /// <param name="subCode">Optional sub-code providing the reason for denial.</param>
    public void SendAccessEvent(OsdpReaderConfig config, bool isGranted, BigInteger? credNum, int? facilityCode, string rawBits, EvtSubCode subCode = EvtSubCode.AccessDeniedUnknownCredNum)
    {
        if (config == null)
        {
            _logger.LogWarning("Cannot send access event: config is null");
            return;
        }

        if (!IsConnected)
        {
            _logger.LogWarning("Cannot send access event: not connected");
            return;
        }

        var evtCode = isGranted ? EvtCode.DoorAccessGranted : EvtCode.DoorAccessDenied;
        var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var evt = new Evt
        {
            EvtCode = evtCode,
            EvtSubCode = subCode,
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

        // Add decoded credential number to EvtCredRef if available
        if (credNum.HasValue)
        {
            evt.EvtCredRef = new EvtCredRef
            {
                CredNum = SpCoreProtoUtil.ToBigIntegerData(credNum.Value)
            };
            if (facilityCode.HasValue)
            {
                evt.EvtCredRef.FacilityCode = facilityCode.Value;
            }
        }
        else if (!string.IsNullOrEmpty(rawBits))
        {
            // Unable to decode - store raw bits in Data field
            evt.Data = rawBits;
        }

        var message = new SpCoreMessage
        {
            Type = SpCoreMessage.Types.Type.Evt
        };
        message.Evt.Add(evt);

        _logger.LogInformation("Sending {EvtCode} event for reader {Name} (unid={Unid}), credNum={CredNum}, fc={FacilityCode}, rawBits={RawBits}, subCode={SubCode}",
            evtCode, config.Name, config.Unid, credNum?.ToString() ?? "(none)", facilityCode?.ToString() ?? "(none)", rawBits ?? "(none)", subCode);

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
