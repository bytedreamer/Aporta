using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;

namespace Aporta.Drivers.TestDriver
{
    public class TestDriver : IHardwareDriver
    {
        private readonly TestControlPoint[] _controlPoints = 
        {
            new TestControlPoint{Name="Output 1", Id = "O1"},
            new TestControlPoint{Name="Output 2", Id = "O2"}
        };
        
        public string Name => "Test Driver";

        public Guid Id => Guid.Parse("225B748E-FB15-4428-92F7-218BB4CC2813");

        public IEnumerable<IEndpoint> Endpoints => _controlPoints;

        public void Load(string configuration, ILoggerFactory loggerFactory)
        {
            foreach (var controlPoint in _controlPoints)
            {
                controlPoint.ExtensionId = Id;
            }
            
            OnUpdatedEndpoints();
        }

        public void Unload()
        {

        }

        public string CurrentConfiguration()
        {
            return string.Empty;
        }

        public Task<string> PerformAction(string action, string parameters)
        {
            throw new NotImplementedException();
        }

        public event EventHandler<EventArgs> UpdatedEndpoints;

        protected virtual void OnUpdatedEndpoints()
        {
            UpdatedEndpoints?.Invoke(this, EventArgs.Empty);
        }
    }
}