using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
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
                    OnAccessCredentialReceived((await reader.ReadLineAsync() ?? string.Empty).Split(':')
                        .Select(hex => Convert.ToByte(hex, 16)));
                }
            }, TaskCreationOptions.LongRunning);
        }

        public string Name { get; internal set; }
        
        public Guid ExtensionId { get; internal set; }
        
        public string Id { get; internal set;}
        
        public event EventHandler<AccessCredentialReceivedEventArgs> AccessCredentialReceived;

        protected virtual void OnAccessCredentialReceived(IEnumerable<byte> cardData)
        {
            AccessCredentialReceived?.Invoke(this, new AccessCredentialReceivedEventArgs(this, cardData));
        }
    }
}