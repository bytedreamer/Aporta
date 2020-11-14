using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
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
        private readonly ConcurrentBag<NamedPipeServerStream> _serverPipes = new ConcurrentBag<NamedPipeServerStream>();
        private CancellationTokenSource _tokenSource;
        
        public string Name => "Test Driver";

        public Guid Id => ExtensionId;

        public IEnumerable<IEndpoint> Endpoints => _endPoints;

        public void Load(string configuration, ILoggerFactory loggerFactory)
        {
            _tokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(async () =>
            {
                var pipeServer =
                    new NamedPipeServerStream("Aporta.TestDriverAccessPoint", PipeDirection.In, 2,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                _serverPipes.Add(pipeServer);
                while (!_tokenSource.Token.IsCancellationRequested)
                {
                    await pipeServer.WaitForConnectionAsync(_tokenSource.Token);
                    
                    using var reader = new StreamReader(pipeServer);
                    string bitArrayString = await reader.ReadLineAsync() ?? string.Empty;

                    OnAccessCredentialReceived(bitArrayString.ToBitArray(), (ushort) bitArrayString.Length);
                }
            }, TaskCreationOptions.LongRunning);
            
            Task.Factory.StartNew(async () =>
            {
                var pipeServer =
                    new NamedPipeServerStream("Aporta.TestDriverMonitorPoint", PipeDirection.In, 2,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                _serverPipes.Add(pipeServer);
                while (!_tokenSource.Token.IsCancellationRequested)
                {
                    await pipeServer.WaitForConnectionAsync();

                    using var reader = new StreamReader(pipeServer);
                    string inputState = await reader.ReadLineAsync() ?? string.Empty;
                    var stateData = inputState.Split('|');
                    if (Endpoints.First(endpoint => endpoint.Id == stateData[0]) is TestMonitorPoint monitorPoint)
                    {
                        await monitorPoint.SetState(bool.Parse(stateData[1]));
                        OnStateChanged(new StateChangedEventArgs(monitorPoint,
                            bool.Parse(stateData[1])));
                    }
                }
            }, TaskCreationOptions.LongRunning);
            
            _endPoints.AddRange(new IEndpoint[] 
            {
                new TestAccessPoint{Name="Reader 1", Id = "R1", ExtensionId = ExtensionId},
                new TestControlPoint{Name="Output 1", Id = "O1", ExtensionId = ExtensionId},
                new TestControlPoint{Name="Output 2", Id = "O2", ExtensionId = ExtensionId},
                new TestMonitorPoint{Name="Input 1", Id = "I1", ExtensionId = ExtensionId},
                new TestMonitorPoint{Name="Input 2", Id = "I2", ExtensionId = ExtensionId}
            });

            OnUpdatedEndpoints();
        }

        public void Unload()
        {
            _tokenSource?.Cancel();
            foreach (var namedPipeServerStream in _serverPipes)
            {
                namedPipeServerStream?.Dispose();
            }
            _tokenSource?.Dispose();
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
        
        public event EventHandler<StateChangedEventArgs> StateChanged;
        public event EventHandler<OnlineStatusChangedEventArgs> OnlineStatusChanged;

        protected virtual void OnUpdatedEndpoints()
        {
            UpdatedEndpoints?.Invoke(this, EventArgs.Empty);
        }

        private void OnAccessCredentialReceived(BitArray cardData, ushort bitCount)
        {
            AccessCredentialReceived?.Invoke(this,
                new AccessCredentialReceivedEventArgs((IAccessPoint) _endPoints[0], cardData, bitCount));
        }

        protected virtual void OnStateChanged(StateChangedEventArgs e)
        {
            StateChanged?.Invoke(this, e);
        }
    }
}