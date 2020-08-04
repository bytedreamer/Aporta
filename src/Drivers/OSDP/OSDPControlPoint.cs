using System;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;
using OSDP.Drivers.Shared;
using OSDP.Net;
using OSDP.Net.Model.CommandData;

namespace Aporta.Drivers.OSDP
{
    public class OSDPControlPoint : IControlPoint
    {
        private readonly ControlPanel _controlPanel;
        private readonly Guid _connectionId;
        private readonly Device _device;
        private readonly Output _output;

        public OSDPControlPoint(Guid extensionId, ControlPanel controlPanel, Guid connectionId, Device device, Output output)
        {
            _controlPanel = controlPanel;
            _connectionId = connectionId;
            _device = device;
            _output = output;
            ExtensionId = extensionId;
        }

        public string Name => _output.Name;

        public Guid ExtensionId { get; }
        
        public string Id => $"{_device.Address}:O{_output.Number}";

        public async Task<bool> GetState()
        {
            return (await _controlPanel.OutputStatus(_connectionId, _device.Address)).OutputStatuses.ToArray()[
                _output.Number];
        }

        public async Task SetState(bool state)
        {
            await _controlPanel.OutputControl(_connectionId, _device.Address,
                new OutputControls(new[]
                {
                    new OutputControl(_output.Number,
                        state
                            ? OutputControlCode.PermanentStateOnAbortTimedOperation
                            : OutputControlCode.PermanentStateOffAbortTimedOperation, 0)
                }));

            await GetState();
        }
    }
}