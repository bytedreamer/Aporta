using System;
using System.Threading.Tasks;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;

namespace Aporta.Drivers.TestDriver
{
    public class TestDriver : IHardwareDriver
    {
        public string Name => "Test Driver";

        public Guid Id => Guid.Parse("225B748E-FB15-4428-92F7-218BB4CC2813");
            
        public void Load(string configuration, ILoggerFactory loggerFactory)
        {

        }

        public void Unload()
        {

        }

        public string InitialConfiguration()
        {
            return string.Empty;
        }

        public Task<string> PerformAction(string action, string parameters)
        {
            throw new NotImplementedException();
        }

        public event EventHandler<AddEndpointsEventArgs> AddEndpoints;
    }
}