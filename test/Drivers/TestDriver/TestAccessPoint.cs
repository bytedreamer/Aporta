using System;
using System.Collections;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using Aporta.Extensions;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;

namespace Aporta.Drivers.TestDriver
{
    public class TestAccessPoint : IAccessPoint
    {
        public TestAccessPoint()
        {
            Task.Factory.StartNew(async () =>
            {
                await using var pipeServer =
                    new NamedPipeServerStream("Aporta.TestAccessPoint", PipeDirection.In);
                while (true)
                {
                    await pipeServer.WaitForConnectionAsync();
                    using var reader = new StreamReader(pipeServer);
                    string bitArrayString = await reader.ReadLineAsync() ?? string.Empty;
                    
                    OnAccessCredentialReceived(bitArrayString.ToBitArray(), (ushort)bitArrayString.Length);
                }
            }, TaskCreationOptions.LongRunning);
        }

        public string Name { get; internal set; }
        
        public Guid ExtensionId { get; internal set; }
        
        public string Id { get; internal set;}
        
        public event EventHandler<AccessCredentialReceivedEventArgs> AccessCredentialReceived;

        private void OnAccessCredentialReceived(BitArray cardData, ushort bitCount)
        {
            AccessCredentialReceived?.Invoke(this, new AccessCredentialReceivedEventArgs(this, cardData, bitCount));
        }
    }
}