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

    // Z9 Open protobuf repositories
    private readonly DataFormatRepository _dataFormatRepository;
    private readonly DataLayoutRepository _dataLayoutRepository;
    private readonly CredTemplateRepository _credTemplateRepository;
    private readonly PrivRepository _privRepository;
    private readonly SchedRepository _schedRepository;
    private readonly HolRepository _holRepository;
    private readonly HolCalRepository _holCalRepository;
    private readonly HolTypeRepository _holTypeRepository;
    private readonly Z9CredRepository _z9CredRepository;

    // Received OSDP CredReader configurations from Z9
    private readonly List<OsdpReaderConfig> _osdpReaderConfigs = new();

    // Mapping from OSDP endpoint ID prefix to Z9 device unid
    // Key format: "{host}:{port}:{osdpAddress}" (e.g., "localhost:9843:0")
    private readonly Dictionary<string, OsdpReaderConfig> _endpointToConfig = new();

    // Pending door strike actuators (received before their door's reader)
    // Key: logicalParentUnid (door unid), Value: output number
    private readonly Dictionary<int, int> _pendingStrikeActuators = new();

    // Pending door contact sensors (received before their door's reader)
    // Key: logicalParentUnid (door unid), Value: input number
    private readonly Dictionary<int, int> _pendingDoorContactSensors = new();

    // Pending REX sensors (received before their door's reader)
    // Key: logicalParentUnid (door unid), Value: input number
    private readonly Dictionary<int, int> _pendingRexSensors = new();

    // Door info received from Z9 (unid -> name mapping)
    private readonly Dictionary<int, string> _doorInfo = new();

    // Door config values (unid -> value)
    private readonly Dictionary<int, bool> _doorActivateStrikeOnRex = new();
    private readonly Dictionary<int, int?> _doorStrikeTimeMs = new();
    private readonly Dictionary<int, int?> _doorExtendedStrikeTimeMs = new();
    private readonly Dictionary<int, int?> _doorHeldTimeMs = new();
    private readonly Dictionary<int, int?> _doorExtendedHeldTimeMs = new();

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
        public int? DoorUnid { get; set; }
        public int? StrikeOutputNumber { get; set; }
        public int? DoorContactInputNumber { get; set; }
        public int? RexInputNumber { get; set; }
        public bool ActivateStrikeOnRex { get; set; }
        public int? StrikeTimeMs { get; set; }
        public int? ExtendedStrikeTimeMs { get; set; }
        public int? HeldTimeMs { get; set; }
        public int? ExtendedHeldTimeMs { get; set; }
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
        _dataFormatRepository = new DataFormatRepository(dataAccess);
        _dataLayoutRepository = new DataLayoutRepository(dataAccess);
        _credTemplateRepository = new CredTemplateRepository(dataAccess);
        _privRepository = new PrivRepository(dataAccess);
        _schedRepository = new SchedRepository(dataAccess);
        _holRepository = new HolRepository(dataAccess);
        _holCalRepository = new HolCalRepository(dataAccess);
        _holTypeRepository = new HolTypeRepository(dataAccess);
        _z9CredRepository = new Z9CredRepository(dataAccess);
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
                _logger.LogInformation("Received DevActionReq (requestId={RequestId}, type={Type}, devUnid={DevUnid})",
                    message.DevActionReq.RequestId, message.DevActionReq.DevActionType, message.DevActionReq.DevUnid);
                HandleDevActionReq(message.DevActionReq);
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

        // Process deletes first (order: most dependent -> least dependent)
        // Cred deletes
        if (dbChange.CredDeleteAllCase == DbChange.CredDeleteAllOneofCase.CredDeleteAll && dbChange.CredDeleteAll)
        {
            _logger.LogInformation("Deleting all credentials");
            // Note: We don't have a DeleteAll that also cleans up persons/assignments
            // For now, just delete credentials
        }
        foreach (var credDelete in dbChange.CredDelete)
        {
            _logger.LogInformation("Deleting credential {Unid}", credDelete);
            _credentialRepository.RevokePerson(credDelete, credDelete).GetAwaiter().GetResult();
            _credentialRepository.Delete(credDelete).GetAwaiter().GetResult();
            if (AutoCreatePersonForCredential)
            {
                _personRepository.Delete(credDelete).GetAwaiter().GetResult();
            }
        }

        // Priv deletes
        if (dbChange.PrivDeleteAllCase == DbChange.PrivDeleteAllOneofCase.PrivDeleteAll && dbChange.PrivDeleteAll)
        {
            _logger.LogInformation("Deleting all privileges");
            _privRepository.DeleteAll().GetAwaiter().GetResult();
        }
        foreach (var privDelete in dbChange.PrivDelete)
        {
            _logger.LogInformation("Deleting privilege {Unid}", privDelete);
            _privRepository.Delete(privDelete).GetAwaiter().GetResult();
        }

        // CredTemplate deletes
        if (dbChange.CredTemplateDeleteAllCase == DbChange.CredTemplateDeleteAllOneofCase.CredTemplateDeleteAll && dbChange.CredTemplateDeleteAll)
        {
            _logger.LogInformation("Deleting all credential templates");
            _credTemplateRepository.DeleteAll().GetAwaiter().GetResult();
        }
        foreach (var credTemplateDelete in dbChange.CredTemplateDelete)
        {
            _logger.LogInformation("Deleting credential template {Unid}", credTemplateDelete);
            _credTemplateRepository.Delete(credTemplateDelete).GetAwaiter().GetResult();
        }

        // DataLayout deletes
        if (dbChange.DataLayoutDeleteAllCase == DbChange.DataLayoutDeleteAllOneofCase.DataLayoutDeleteAll && dbChange.DataLayoutDeleteAll)
        {
            _logger.LogInformation("Deleting all data layouts");
            _dataLayoutRepository.DeleteAll().GetAwaiter().GetResult();
        }
        foreach (var dataLayoutDelete in dbChange.DataLayoutDelete)
        {
            _logger.LogInformation("Deleting data layout {Unid}", dataLayoutDelete);
            _dataLayoutRepository.Delete(dataLayoutDelete).GetAwaiter().GetResult();
        }

        // DataFormat deletes
        if (dbChange.DataFormatDeleteAllCase == DbChange.DataFormatDeleteAllOneofCase.DataFormatDeleteAll && dbChange.DataFormatDeleteAll)
        {
            _logger.LogInformation("Deleting all data formats");
            _dataFormatRepository.DeleteAll().GetAwaiter().GetResult();
        }
        foreach (var dataFormatDelete in dbChange.DataFormatDelete)
        {
            _logger.LogInformation("Deleting data format {Unid}", dataFormatDelete);
            _dataFormatRepository.Delete(dataFormatDelete).GetAwaiter().GetResult();
        }

        // Sched deletes
        if (dbChange.SchedDeleteAllCase == DbChange.SchedDeleteAllOneofCase.SchedDeleteAll && dbChange.SchedDeleteAll)
        {
            _logger.LogInformation("Deleting all schedules");
            _schedRepository.DeleteAll().GetAwaiter().GetResult();
        }
        foreach (var schedDelete in dbChange.SchedDelete)
        {
            _logger.LogInformation("Deleting schedule {Unid}", schedDelete);
            _schedRepository.Delete(schedDelete).GetAwaiter().GetResult();
        }

        // Hol deletes
        if (dbChange.HolDeleteAllCase == DbChange.HolDeleteAllOneofCase.HolDeleteAll && dbChange.HolDeleteAll)
        {
            _logger.LogInformation("Deleting all holidays");
            _holRepository.DeleteAll().GetAwaiter().GetResult();
        }
        foreach (var holDelete in dbChange.HolDelete)
        {
            _logger.LogInformation("Deleting holiday {Unid}", holDelete);
            _holRepository.Delete(holDelete).GetAwaiter().GetResult();
        }

        // HolCal deletes
        if (dbChange.HolCalDeleteAllCase == DbChange.HolCalDeleteAllOneofCase.HolCalDeleteAll && dbChange.HolCalDeleteAll)
        {
            _logger.LogInformation("Deleting all holiday calendars");
            _holCalRepository.DeleteAll().GetAwaiter().GetResult();
        }
        foreach (var holCalDelete in dbChange.HolCalDelete)
        {
            _logger.LogInformation("Deleting holiday calendar {Unid}", holCalDelete);
            _holCalRepository.Delete(holCalDelete).GetAwaiter().GetResult();
        }

        // HolType deletes
        if (dbChange.HolTypeDeleteAllCase == DbChange.HolTypeDeleteAllOneofCase.HolTypeDeleteAll && dbChange.HolTypeDeleteAll)
        {
            _logger.LogInformation("Deleting all holiday types");
            _holTypeRepository.DeleteAll().GetAwaiter().GetResult();
        }
        foreach (var holTypeDelete in dbChange.HolTypeDelete)
        {
            _logger.LogInformation("Deleting holiday type {Unid}", holTypeDelete);
            _holTypeRepository.Delete(holTypeDelete).GetAwaiter().GetResult();
        }

        // Process inserts/updates (order: least dependent -> most dependent)
        // HolType (no dependencies)
        foreach (var holType in dbChange.HolType)
        {
            ProcessHolType(holType);
        }

        // HolCal (no dependencies)
        foreach (var holCal in dbChange.HolCal)
        {
            ProcessHolCal(holCal);
        }

        // Hol (depends on HolCal)
        foreach (var hol in dbChange.Hol)
        {
            ProcessHol(hol);
        }

        // Sched (may reference HolCal)
        foreach (var sched in dbChange.Sched)
        {
            ProcessSched(sched);
        }

        // DataFormat (no dependencies)
        foreach (var dataFormat in dbChange.DataFormat)
        {
            ProcessDataFormat(dataFormat);
        }

        // DataLayout (depends on DataFormat)
        foreach (var dataLayout in dbChange.DataLayout)
        {
            ProcessDataLayout(dataLayout);
        }

        // CredTemplate (depends on DataLayout)
        foreach (var credTemplate in dbChange.CredTemplate)
        {
            ProcessCredTemplate(credTemplate);
        }

        // Priv (may reference Sched)
        foreach (var priv in dbChange.Priv)
        {
            ProcessPriv(priv);
        }

        // Cred (depends on CredTemplate, may reference Priv)
        foreach (var cred in dbChange.Cred)
        {
            ProcessCredential(cred);
        }

        // Dev (extract OSDP reader configurations - stored in memory for now)
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
        _dataFormatRepository.Upsert(dataFormat).GetAwaiter().GetResult();
    }

    private void ProcessDataLayout(DataLayout dataLayout)
    {
        SpCoreProtoUtil.InitRequired(dataLayout);
        _logger.LogDebug("Storing DataLayout: unid={Unid}, name={Name}, type={Type}",
            dataLayout.Unid, dataLayout.Name, dataLayout.LayoutType);
        _dataLayoutRepository.Upsert(dataLayout).GetAwaiter().GetResult();
    }

    private void ProcessCredTemplate(CredTemplate credTemplate)
    {
        SpCoreProtoUtil.InitRequired(credTemplate);
        _logger.LogDebug("Storing CredTemplate: unid={Unid}, name={Name}",
            credTemplate.Unid, credTemplate.Name);
        _credTemplateRepository.Upsert(credTemplate).GetAwaiter().GetResult();
    }

    private void ProcessPriv(Priv priv)
    {
        SpCoreProtoUtil.InitRequired(priv);
        _logger.LogDebug("Storing Priv: unid={Unid}, name={Name}, type={Type}",
            priv.Unid, priv.Name, priv.PrivType);
        _privRepository.Upsert(priv).GetAwaiter().GetResult();
    }

    private void ProcessSched(Sched sched)
    {
        SpCoreProtoUtil.InitRequired(sched);
        _logger.LogDebug("Storing Sched: unid={Unid}, name={Name}",
            sched.Unid, sched.Name);
        _schedRepository.Upsert(sched).GetAwaiter().GetResult();
    }

    private void ProcessHol(Hol hol)
    {
        SpCoreProtoUtil.InitRequired(hol);
        _logger.LogDebug("Storing Hol: unid={Unid}, name={Name}",
            hol.Unid, hol.Name);
        _holRepository.Upsert(hol).GetAwaiter().GetResult();
    }

    private void ProcessHolCal(HolCal holCal)
    {
        SpCoreProtoUtil.InitRequired(holCal);
        _logger.LogDebug("Storing HolCal: unid={Unid}, name={Name}",
            holCal.Unid, holCal.Name);
        _holCalRepository.Upsert(holCal).GetAwaiter().GetResult();
    }

    private void ProcessHolType(HolType holType)
    {
        SpCoreProtoUtil.InitRequired(holType);
        _logger.LogDebug("Storing HolType: unid={Unid}, name={Name}",
            holType.Unid, holType.Name);
        _holTypeRepository.Upsert(holType).GetAwaiter().GetResult();
    }

    private void ProcessDev(Dev dev)
    {
        SpCoreProtoUtil.InitRequired(dev);

        if (dev.DevType == DevType.Actuator)
        {
            ProcessActuatorDev(dev);
            return;
        }

        if (dev.DevType == DevType.Sensor)
        {
            ProcessSensorDev(dev);
            return;
        }

        if (dev.DevType == DevType.Door)
        {
            ProcessDoorDev(dev);
            return;
        }

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
            BaudRate = dev.Speed > 0 ? dev.Speed : 9600,
            DoorUnid = dev.LogicalParentUnidCase == Dev.LogicalParentUnidOneofCase.LogicalParentUnid
                ? dev.LogicalParentUnid
                : null
        };

        _logger.LogInformation(
            "Received OSDP CredReader config: Name={Name}, Host={Host}:{TcpPort}, OsdpAddress={OsdpAddress}",
            osdpConfig.Name, osdpConfig.Host, osdpConfig.TcpPort, osdpConfig.OsdpAddress);

        // Check for pending strike actuator for this door
        if (osdpConfig.DoorUnid != null)
        {
            lock (_pendingStrikeActuators)
            {
                if (_pendingStrikeActuators.TryGetValue(osdpConfig.DoorUnid.Value, out var outputNumber))
                {
                    osdpConfig.StrikeOutputNumber = outputNumber;
                    _pendingStrikeActuators.Remove(osdpConfig.DoorUnid.Value);
                    _logger.LogInformation("Applied pending door strike actuator: output {OutputNumber} on reader {Name}",
                        outputNumber, osdpConfig.Name);
                }
            }

            // Check for pending door contact sensor for this door
            lock (_pendingDoorContactSensors)
            {
                if (_pendingDoorContactSensors.TryGetValue(osdpConfig.DoorUnid.Value, out var inputNumber))
                {
                    osdpConfig.DoorContactInputNumber = inputNumber;
                    _pendingDoorContactSensors.Remove(osdpConfig.DoorUnid.Value);
                    _logger.LogInformation("Applied pending door contact sensor: input {InputNumber} on reader {Name}",
                        inputNumber, osdpConfig.Name);
                }
            }

            // Check for pending REX sensor for this door
            lock (_pendingRexSensors)
            {
                if (_pendingRexSensors.TryGetValue(osdpConfig.DoorUnid.Value, out var rexInputNumber))
                {
                    osdpConfig.RexInputNumber = rexInputNumber;
                    _pendingRexSensors.Remove(osdpConfig.DoorUnid.Value);
                    _logger.LogInformation("Applied pending REX sensor: input {InputNumber} on reader {Name}",
                        rexInputNumber, osdpConfig.Name);
                }
            }

            // Check for door config values
            lock (_doorActivateStrikeOnRex)
            {
                if (_doorActivateStrikeOnRex.TryGetValue(osdpConfig.DoorUnid.Value, out var activateStrike))
                {
                    osdpConfig.ActivateStrikeOnRex = activateStrike;
                }
            }
            lock (_doorStrikeTimeMs)
            {
                if (_doorStrikeTimeMs.TryGetValue(osdpConfig.DoorUnid.Value, out var strikeTimeMs))
                {
                    osdpConfig.StrikeTimeMs = strikeTimeMs;
                }
            }
            lock (_doorExtendedStrikeTimeMs)
            {
                if (_doorExtendedStrikeTimeMs.TryGetValue(osdpConfig.DoorUnid.Value, out var extStrikeTimeMs))
                {
                    osdpConfig.ExtendedStrikeTimeMs = extStrikeTimeMs;
                }
            }
            lock (_doorHeldTimeMs)
            {
                if (_doorHeldTimeMs.TryGetValue(osdpConfig.DoorUnid.Value, out var heldTimeMs))
                {
                    osdpConfig.HeldTimeMs = heldTimeMs;
                }
            }
            lock (_doorExtendedHeldTimeMs)
            {
                if (_doorExtendedHeldTimeMs.TryGetValue(osdpConfig.DoorUnid.Value, out var extHeldTimeMs))
                {
                    osdpConfig.ExtendedHeldTimeMs = extHeldTimeMs;
                }
            }
        }

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

    private void ProcessActuatorDev(Dev dev)
    {
        if (dev.DevUse != DevUse.ActuatorDoorStrike)
            return;

        if (dev.LogicalParentUnidCase != Dev.LogicalParentUnidOneofCase.LogicalParentUnid)
        {
            _logger.LogWarning("Door strike actuator {Name} (unid={Unid}) has no logicalParentUnid (door), skipping",
                dev.Name, dev.Unid);
            return;
        }

        var doorUnid = dev.LogicalParentUnid;
        var outputNumber = 0;
        if (!string.IsNullOrWhiteSpace(dev.Address))
        {
            int.TryParse(dev.Address, out outputNumber);
        }

        _logger.LogInformation("Received door strike actuator: output {OutputNumber}, door unid={DoorUnid}",
            outputNumber, doorUnid);

        // Try to find matching reader config by door unid
        OsdpReaderConfig matchingConfig = null;
        lock (_osdpReaderConfigs)
        {
            matchingConfig = _osdpReaderConfigs.Find(c => c.DoorUnid == doorUnid);
        }

        if (matchingConfig != null)
        {
            matchingConfig.StrikeOutputNumber = outputNumber;
            _logger.LogInformation("Assigned door strike output {OutputNumber} to reader {Name} (door unid={DoorUnid})",
                outputNumber, matchingConfig.Name, doorUnid);
        }
        else
        {
            // Reader for this door hasn't arrived yet - store for later
            lock (_pendingStrikeActuators)
            {
                _pendingStrikeActuators[doorUnid] = outputNumber;
            }
            _logger.LogInformation("Reader for door unid={DoorUnid} not yet received, storing pending strike actuator", doorUnid);
        }
    }

    private void ProcessSensorDev(Dev dev)
    {
        if (dev.DevUse == DevUse.SensorDoorContact)
        {
            ProcessSensorWithPending(dev, "door contact",
                (config, inputNum) => config.DoorContactInputNumber = inputNum,
                _pendingDoorContactSensors);
        }
        else if (dev.DevUse == DevUse.SensorRex)
        {
            ProcessSensorWithPending(dev, "REX",
                (config, inputNum) => config.RexInputNumber = inputNum,
                _pendingRexSensors);
        }
    }

    private void ProcessSensorWithPending(Dev dev, string sensorLabel,
        Action<OsdpReaderConfig, int> assignInputNumber,
        Dictionary<int, int> pendingDictionary)
    {
        if (dev.LogicalParentUnidCase != Dev.LogicalParentUnidOneofCase.LogicalParentUnid)
        {
            _logger.LogWarning("{SensorLabel} sensor {Name} (unid={Unid}) has no logicalParentUnid (door), skipping",
                sensorLabel, dev.Name, dev.Unid);
            return;
        }

        var doorUnid = dev.LogicalParentUnid;
        var inputNumber = 0;
        if (!string.IsNullOrWhiteSpace(dev.Address))
        {
            int.TryParse(dev.Address, out inputNumber);
        }

        _logger.LogInformation("Received {SensorLabel} sensor: input {InputNumber}, door unid={DoorUnid}",
            sensorLabel, inputNumber, doorUnid);

        OsdpReaderConfig matchingConfig = null;
        lock (_osdpReaderConfigs)
        {
            matchingConfig = _osdpReaderConfigs.Find(c => c.DoorUnid == doorUnid);
        }

        if (matchingConfig != null)
        {
            assignInputNumber(matchingConfig, inputNumber);
            _logger.LogInformation("Assigned {SensorLabel} input {InputNumber} to reader {Name} (door unid={DoorUnid})",
                sensorLabel, inputNumber, matchingConfig.Name, doorUnid);
        }
        else
        {
            lock (pendingDictionary)
            {
                pendingDictionary[doorUnid] = inputNumber;
            }
            _logger.LogInformation("Reader for door unid={DoorUnid} not yet received, storing pending {SensorLabel} sensor",
                doorUnid, sensorLabel);
        }
    }

    private void ProcessDoorDev(Dev dev)
    {
        var doorName = dev.Name ?? $"Door {dev.Unid}";
        lock (_doorInfo)
        {
            _doorInfo[dev.Unid] = doorName;
        }

        // Extract door config values
        if (dev.ExtDoor?.DoorConfig != null)
        {
            var doorConfig = dev.ExtDoor.DoorConfig;
            var activateStrikeOnRex = doorConfig.ActivateStrikeOnRex;
            int? strikeTimeMs = doorConfig.StrikeTimeCase == DoorConfig.StrikeTimeOneofCase.StrikeTime
                ? doorConfig.StrikeTime : null;
            int? extendedStrikeTimeMs = doorConfig.ExtendedStrikeTimeCase == DoorConfig.ExtendedStrikeTimeOneofCase.ExtendedStrikeTime
                ? doorConfig.ExtendedStrikeTime : null;
            int? heldTimeMs = doorConfig.HeldTimeCase == DoorConfig.HeldTimeOneofCase.HeldTime
                ? doorConfig.HeldTime : null;
            int? extendedHeldTimeMs = doorConfig.ExtendedHeldTimeCase == DoorConfig.ExtendedHeldTimeOneofCase.ExtendedHeldTime
                ? doorConfig.ExtendedHeldTime : null;

            lock (_doorActivateStrikeOnRex)
            {
                _doorActivateStrikeOnRex[dev.Unid] = activateStrikeOnRex;
            }
            lock (_doorStrikeTimeMs)
            {
                _doorStrikeTimeMs[dev.Unid] = strikeTimeMs;
            }
            lock (_doorExtendedStrikeTimeMs)
            {
                _doorExtendedStrikeTimeMs[dev.Unid] = extendedStrikeTimeMs;
            }
            lock (_doorHeldTimeMs)
            {
                _doorHeldTimeMs[dev.Unid] = heldTimeMs;
            }
            lock (_doorExtendedHeldTimeMs)
            {
                _doorExtendedHeldTimeMs[dev.Unid] = extendedHeldTimeMs;
            }

            // Apply to existing reader config if already loaded
            lock (_osdpReaderConfigs)
            {
                var config = _osdpReaderConfigs.Find(c => c.DoorUnid == dev.Unid);
                if (config != null)
                {
                    config.ActivateStrikeOnRex = activateStrikeOnRex;
                    config.StrikeTimeMs = strikeTimeMs;
                    config.ExtendedStrikeTimeMs = extendedStrikeTimeMs;
                    config.HeldTimeMs = heldTimeMs;
                    config.ExtendedHeldTimeMs = extendedHeldTimeMs;
                }
            }

            _logger.LogInformation("Received Door Dev: unid={Unid}, name={Name}, activateStrikeOnRex={ActivateStrikeOnRex}, strikeTimeMs={StrikeTimeMs}, extendedStrikeTimeMs={ExtendedStrikeTimeMs}, heldTimeMs={HeldTimeMs}, extendedHeldTimeMs={ExtendedHeldTimeMs}",
                dev.Unid, doorName, activateStrikeOnRex, strikeTimeMs, extendedStrikeTimeMs, heldTimeMs, extendedHeldTimeMs);
        }
        else
        {
            _logger.LogInformation("Received Door Dev: unid={Unid}, name={Name}", dev.Unid, doorName);
        }
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

        _logger.LogInformation("Processing credential Unid={Unid} Number={Number} Name={Name} PrivBindings={BindingCount}",
            cred.Unid, bitString, personName, cred.PrivBindings.Count);

        // Store the full Z9 Cred proto (for privilege checking)
        _z9CredRepository.Upsert(cred).GetAwaiter().GetResult();

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

        var credTemplate = _credTemplateRepository.Get(cred.CredTemplateUnid).GetAwaiter().GetResult();
        if (credTemplate == null)
        {
            _logger.LogDebug("CredTemplate {Unid} not found", cred.CredTemplateUnid);
            return null;
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
            var dataFormats = _dataFormatRepository.GetAll().GetAwaiter().GetResult();
            foreach (var df in dataFormats)
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
            return null;
        }

        // Get specific data layout
        if (cardPinTemplate.DataLayoutUnidCase != CardPinTemplate.DataLayoutUnidOneofCase.DataLayoutUnid)
        {
            _logger.LogDebug("CardPinTemplate has no DataLayoutUnid");
            return null;
        }

        var dataLayout = _dataLayoutRepository.Get(cardPinTemplate.DataLayoutUnid).GetAwaiter().GetResult();
        if (dataLayout == null)
        {
            _logger.LogDebug("DataLayout {Unid} not found", cardPinTemplate.DataLayoutUnid);
            return null;
        }

        // Get the data format from BasicDataLayout
        if (dataLayout.ExtBasicDataLayout == null ||
            dataLayout.ExtBasicDataLayout.DataFormatUnidCase != BasicDataLayout.DataFormatUnidOneofCase.DataFormatUnid)
        {
            _logger.LogDebug("DataLayout {Unid} has no BasicDataLayout or DataFormatUnid", dataLayout.Unid);
            return null;
        }

        var dataFormat = _dataFormatRepository.Get(dataLayout.ExtBasicDataLayout.DataFormatUnid).GetAwaiter().GetResult();
        if (dataFormat == null)
        {
            _logger.LogDebug("DataFormat {Unid} not found", dataLayout.ExtBasicDataLayout.DataFormatUnid);
            return null;
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

        var dataFormats = _dataFormatRepository.GetAll().GetAwaiter().GetResult();
        foreach (var dataFormat in dataFormats)
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

    /// <summary>
    /// Event raised when a DevActionReq is received from the host.
    /// </summary>
    public event EventHandler<DevActionReq> DevActionRequested;

    private void HandleDevActionReq(DevActionReq req)
    {
        try
        {
            DevActionRequested?.Invoke(this, req);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling DevActionReq (requestId={RequestId})", req.RequestId);
            SendDevActionResp(req.RequestId, ex.Message);
            return;
        }
        SendDevActionResp(req.RequestId);
    }

    private void SendDevActionResp(long requestId, string exception = null)
    {
        var resp = new DevActionResp
        {
            RequestId = requestId
        };
        if (exception != null)
        {
            resp.Exception = exception;
        }

        var message = new SpCoreMessage
        {
            Type = SpCoreMessage.Types.Type.DevActionResp,
            DevActionResp = resp
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

        // Only set EvtSubCode for denied events (access granted events don't have a sub-code)
        if (!isGranted)
        {
            evt.EvtSubCode = subCode;
        }

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

    /// <summary>
    /// Sends a door state event (unlocked/locked/opened/closed) to Z9.
    /// </summary>
    /// <param name="config">The OSDP reader configuration (to look up door info).</param>
    /// <param name="evtCode">The event code (DoorUnlocked, DoorLocked, DoorOpened, DoorClosed).</param>
    public void SendDoorStateEvent(OsdpReaderConfig config, EvtCode evtCode)
    {
        if (config == null)
        {
            _logger.LogWarning("Cannot send door state event: config is null");
            return;
        }

        if (!IsConnected)
        {
            _logger.LogWarning("Cannot send door state event: not connected");
            return;
        }

        if (!config.DoorUnid.HasValue)
        {
            _logger.LogWarning("Cannot send door state event: reader {Name} has no DoorUnid", config.Name);
            return;
        }

        var doorUnid = config.DoorUnid.Value;
        string doorName;
        lock (_doorInfo)
        {
            if (!_doorInfo.TryGetValue(doorUnid, out doorName))
            {
                doorName = $"Door {doorUnid}";
            }
        }

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
                Unid = doorUnid,
                Name = doorName,
                DevType = DevType.Door
            }
        };

        var message = new SpCoreMessage
        {
            Type = SpCoreMessage.Types.Type.Evt
        };
        message.Evt.Add(evt);

        _logger.LogInformation("Sending {EvtCode} event for door {DoorName} (unid={DoorUnid})",
            evtCode, doorName, doorUnid);

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
