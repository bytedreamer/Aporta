using System;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;
using Aporta.Drivers.OSDP.Shared;
using OSDP.Net;

namespace Aporta.Drivers.OSDP
{
    public class OSDPAccessPoint : IAccessPoint
    {
        private readonly ControlPanel _controlPanel;
        private readonly Guid _connectionId;
        private readonly Device _device;
        private readonly Reader _reader;

        public OSDPAccessPoint(Guid extensionId, ControlPanel controlPanel, Guid connectionId, Device device, Reader reader)
        {
            _controlPanel = controlPanel;
            _connectionId = connectionId;
            _device = device;
            _reader = reader;
            ExtensionId = extensionId;
        }

        public string Name => _reader.Name;

        public Guid ExtensionId { get; }
        
        public string Id => $"{_device.Address}:R{_reader.Number}";
        public Task<bool> GetOnlineStatus()
        {
            return Task.FromResult(true);
        }
    }
}