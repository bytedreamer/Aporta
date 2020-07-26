using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using Aporta.Extensions;
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
            Task.Factory.StartNew(async () =>
            {
                await using var pipeServer =
                    new NamedPipeServerStream("Aporta.TestDriverAccessPoint", PipeDirection.In);
                while (true)
                {
                    await pipeServer.WaitForConnectionAsync();
                    using var reader = new StreamReader(pipeServer);
                    string bitArrayString = await reader.ReadLineAsync() ?? string.Empty;
                    
                    OnAccessCredentialReceived(bitArrayString.ToBitArray(), (ushort)bitArrayString.Length);
                }
            }, TaskCreationOptions.LongRunning);
            
            _endPoints.AddRange(new IEndpoint[] 
            {
                new TestAccessPoint{Name="Reader 1", Id = "R1", ExtensionId = ExtensionId},
                new TestControlPoint{Name="Output 1", Id = "O1", ExtensionId = ExtensionId},
                new TestControlPoint{Name="Output 2", Id = "O2", ExtensionId = ExtensionId}
            });

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
            return Task.FromResult(string.Empty);
        }

        public event EventHandler<EventArgs> UpdatedEndpoints;
        public event EventHandler<AccessCredentialReceivedEventArgs> AccessCredentialReceived;

        protected virtual void OnUpdatedEndpoints()
        {
            UpdatedEndpoints?.Invoke(this, EventArgs.Empty);
        }

        private void OnAccessCredentialReceived(BitArray cardData, ushort bitCount)
        {
            AccessCredentialReceived?.Invoke(this,
                new AccessCredentialReceivedEventArgs((IAccessPoint) _endPoints[0], cardData, bitCount));
        }
    }
}