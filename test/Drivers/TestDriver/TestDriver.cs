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
        // ReSharper disable once InconsistentNaming
        private static readonly Guid ExtensionId = Guid.Parse("225B748E-FB15-4428-92F7-218BB4CC2813");
        private readonly List<IEndpoint> _endPoints = new List<IEndpoint>();

        public string Name => "Test Driver";

        public Guid Id => ExtensionId;

        public IEnumerable<IEndpoint> Endpoints => _endPoints;

        public void Load(string configuration, ILoggerFactory loggerFactory)
        {
            _endPoints.AddRange(new IEndpoint[] 
            {
                new TestAccessPoint{Name="Reader 1", Id = "R1", ExtensionId = ExtensionId},
                new TestControlPoint{Name="Output 1", Id = "O1", ExtensionId = ExtensionId},
                new TestControlPoint{Name="Output 2", Id = "O2", ExtensionId = ExtensionId}
            });

            foreach (var endpoint in _endPoints)
            {
                if (endpoint is IAccessPoint accessEndpoint)
                {
                    accessEndpoint.AccessCredentialReceived += AccessEndpointOnAccessCredentialReceived;
                }
            }
            
            OnUpdatedEndpoints();
        }

        public void Unload()
        {
            foreach (var endpoint in _endPoints)
            {
                if (endpoint is IAccessPoint accessEndpoint)
                {
                    accessEndpoint.AccessCredentialReceived -= AccessEndpointOnAccessCredentialReceived;
                }
            }
        }

        public string CurrentConfiguration()
        {
            return string.Empty;
        }

        public Task<string> PerformAction(string action, string parameters)
        {
            return Task.FromResult(string.Empty);
        }

        public event EventHandler<EventArgs> UpdatedEndpoints;
        public event EventHandler<AccessCredentialReceivedEventArgs> AccessCredentialReceived;

        protected virtual void OnUpdatedEndpoints()
        {
            UpdatedEndpoints?.Invoke(this, EventArgs.Empty);
        }
        
        private void AccessEndpointOnAccessCredentialReceived(object sender, AccessCredentialReceivedEventArgs eventArgs)
        {
            AccessCredentialReceived?.Invoke(this, eventArgs);
        }
    }
}