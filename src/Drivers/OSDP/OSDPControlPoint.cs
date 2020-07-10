using System;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;
using OSDP.Net;
using OSDP.Net.Model.CommandData;

namespace Aporta.Drivers.OSDP
{
    public class OSDPControlPoint : IControlPoint
    {
        private readonly ControlPanel _controlPanel;
        private readonly Guid _connectionId;
        private readonly byte _address;
        private readonly byte _outputNumber;

        public OSDPControlPoint( ControlPanel controlPanel, Guid connectionId, byte address, byte outputNumber)
        {
            _controlPanel = controlPanel;
            _connectionId = connectionId;
            _address = address;
            _outputNumber = outputNumber;
        }

        public string Name => "Test";
        
        public int Id { get; set; }
    
        public async Task Set(bool state)
        {
            await _controlPanel.OutputControl(_connectionId, _address,
                new OutputControls(new[]
                {
                    new OutputControl(_outputNumber,
                        state
                            ? OutputControlCode.PermanentStateOnAbortTimedOperation
                            : OutputControlCode.PermanentStateOffAbortTimedOperation, 0)
                }));
        }
    }
}