using Aporta.Extensions;
using Microsoft.Extensions.Logging;

using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Timer = System.Timers.Timer;

namespace Aporta.Drivers.IonoPiMax;

/// <summary>
/// Driver implementation for the Iono Pi Max unit
/// </summary>
public class IonoPiMaxDriver : IHardwareDriver
{
    private const double RequiredFirmwareVersion = 1.7;
        
    private const string IonoPiMaxKernelPath = "/sys/class/ionopimax/";
    private const string FirmwarePath = "mcu/fw_version";
    private const string SerialPortInvertPath = "serial/rs232_rs485_inv";
        
    private const string RelaysPath = "digital_out";
    private const int RelayCount = 4;
    
    private const string DigitalInputPath = "digital_in";
    private const int DigitalInputCount = 4;

    private readonly List<IEndpoint> _endpoints = new ();
    private readonly Dictionary<string, bool> _watchValues = new();

    private Timer? _timer;
    private readonly double _timerInterval = 500;
    
    private ILogger<IonoPiMaxDriver>? _logger;

    public Guid Id => Guid.Parse("00CED75B-02B7-4224-B298-B0EA2B763D1D");

    public IEnumerable<IEndpoint> Endpoints => _endpoints;
        
    public string Name => "Iono Pi Max";

    public void Load(string configuration, IDataEncryption dataEncryption, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<IonoPiMaxDriver>();
            
        CheckThatIonoPiMaxKernelExists();
        CheckCorrectFirmwareIsLoaded();
        EnableRS485Port();
        AddRelays();
        AddDigitalInputs();
        
        MonitorEndpoints();

        OnUpdatedEndpoints();
    }

    private void MonitorEndpoints()
    {
        _timer = new Timer(_timerInterval);
        _timer.Elapsed += async (_, _) =>
        {
            await Task.WhenAll(_endpoints.Where(endpoint => endpoint is IOutput or IInput).Select(async endpoint =>
            {
                switch (endpoint)
                {
                    case IOutput output:
                    {
                        bool currentState = await output.GetState();
                        bool foundValue = _watchValues.TryGetValue(output.Id, out bool oldSate);
                        if (!foundValue || currentState != oldSate)
                        {
                            OnStateChanged(output, currentState);
                            _watchValues[output.Id] = currentState;
                        }

                        break;
                    }
                    case IInput input:
                    {
                        bool currentState = await input.GetState();
                        bool foundValue = _watchValues.TryGetValue(input.Id, out bool oldSate);
                        if (!foundValue || currentState != oldSate)
                        {
                            OnStateChanged(input, currentState);
                            _watchValues[input.Id] = currentState;
                        }

                        break;
                    }
                }
            })).ConfigureAwait(false);
        };
        _timer.Start();
    }

    private void AddRelays()
    {
        string relayPath = Path.Combine(IonoPiMaxKernelPath, RelaysPath);
        for (var index = 1; index <= RelayCount; index++)
        {
            var relay = new Relay($"{Name} Relay {index}", Id, $"{relayPath}/o{index}");
            _endpoints.Add(relay);
        }
    }
    
    private void AddDigitalInputs()
    {
        string digitalInputPath = Path.Combine(IonoPiMaxKernelPath, DigitalInputPath);
        for (var index = 1; index <= DigitalInputCount; index++)
        {
            var endpoint = new DigitalInput($"{Name} Digital Input {index}", Id, $"{digitalInputPath}/di{index}");
            _endpoints.Add(endpoint);
        }
    }

    private void CheckThatIonoPiMaxKernelExists()
    {
        if (Directory.Exists(IonoPiMaxKernelPath)) return;

        throw new Exception("Iono Pi Max kernel module is not installed");
    }
        
    private void CheckCorrectFirmwareIsLoaded()
    {
        string firmwareFile = Path.Combine(IonoPiMaxKernelPath, FirmwarePath);
        double.TryParse(File.ReadAllText(firmwareFile), out double firmware);
        _logger?.LogInformation("Iono Pi Max firmware version is {Firmware}", firmware);
        if (firmware < RequiredFirmwareVersion)
        {
            throw new Exception($"Iono Pi Max firmware need to be at least {RequiredFirmwareVersion}");
        }
    }
        
    private static void EnableRS485Port()
    {
        File.WriteAllText(Path.Combine(IonoPiMaxKernelPath, SerialPortInvertPath), "1");
    }

    public void Unload()
    {
        _timer?.Dispose();
    }

    public string CurrentConfiguration()
    {
        return string.Empty;
    }

    public string ScrubSensitiveConfigurationData(string jsonConfigurationString)
    {
        return jsonConfigurationString;
    }

    public async Task<string> PerformAction(string action, string parameters)
    {
        return await Task.FromResult(string.Empty);
    }

    /// <inheritdoc />
    public event EventHandler<EventArgs>? UpdatedEndpoints;
        
    protected virtual void OnUpdatedEndpoints()
    {
        UpdatedEndpoints?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public event EventHandler<AccessCredentialReceivedEventArgs>? AccessCredentialReceived;
        
    protected virtual void OnAccessCredentialReceived(AccessCredentialReceivedEventArgs eventArgs)
    {
        AccessCredentialReceived?.Invoke(this, eventArgs);
    }

    /// <inheritdoc />
    public event EventHandler<StateChangedEventArgs>? StateChanged;
        
    protected virtual void OnStateChanged(IEndpoint endpoint, bool state)
    {
        _logger?.LogInformation("State changed for {EndpointName} to {State}", endpoint.Name, state);
        StateChanged?.Invoke(this, new StateChangedEventArgs(endpoint, state));
    }

    /// <inheritdoc />
    public event EventHandler<OnlineStatusChangedEventArgs>? OnlineStatusChanged;
        
    protected virtual void OnOnlineStatusChanged(OnlineStatusChangedEventArgs eventArgs)
    {
        OnlineStatusChanged?.Invoke(this, eventArgs);
    }
}