using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;

namespace Aporta.Drivers.IonoPiMax
{
    /// <summary>
    /// 
    /// </summary>
    public class IonoPiMaxDriver : IHardwareDriver
    {
        private const double RequiredFirmwareVersion = 1.7;
        
        private const string IonoPiMaxKernelPath = "/sys/class/ionopimax/";
        private const string FirmwarePath = "mcu/fw_version";
        private const string SerialPortInvertPath = "serial/rs232_rs485_inv";
        
        // ReSharper disable once CollectionNeverUpdated.Local
        private readonly List<IEndpoint> _endpoints = new ();
        private ILogger<IonoPiMaxDriver>? _logger;

        public Guid Id => Guid.Parse("00CED75B-02B7-4224-B298-B0EA2B763D1D");

        public IEnumerable<IEndpoint> Endpoints => _endpoints;

        public string Name => "Iono Pi Max";

        public void Load(string configuration, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<IonoPiMaxDriver>();
            
            CheckThatIonoPiMaxKernelExists();
            CheckCorrectFirmwareIsLoaded();
            EnableRS485Port();

            OnUpdatedEndpoints();
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
        }

        public string CurrentConfiguration()
        {
            return string.Empty;
        }

        public async Task<string> PerformAction(string action, string parameters)
        {
            return await Task.FromResult(string.Empty);
        }

        public event EventHandler<EventArgs>? UpdatedEndpoints;
        public event EventHandler<AccessCredentialReceivedEventArgs>? AccessCredentialReceived;
        public event EventHandler<StateChangedEventArgs>? StateChanged;
        public event EventHandler<OnlineStatusChangedEventArgs>? OnlineStatusChanged;

        protected virtual void OnUpdatedEndpoints()
        {
            UpdatedEndpoints?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnOnlineStatusChanged(OnlineStatusChangedEventArgs e)
        {
            OnlineStatusChanged?.Invoke(this, e);
        }

        protected virtual void OnStateChanged(IEndpoint endpoint, bool state)
        {
            StateChanged?.Invoke(this, new StateChangedEventArgs(endpoint, state));
        }

        protected virtual void OnAccessCredentialReceived(AccessCredentialReceivedEventArgs e)
        {
            AccessCredentialReceived?.Invoke(this, e);
        }
    }
}