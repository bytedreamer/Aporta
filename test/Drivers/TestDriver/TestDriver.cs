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
            
        public void Load(string configuration, ILoggerFactory loggerFactory)
        {
            foreach (var controlPoint in _controlPoints)
            {
                controlPoint.ExtensionId = Id;
            }
            
            OnAddEndpoints(_controlPoints);
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

        protected virtual void OnAddEndpoints(IEnumerable<IEndpoint> endpoints)
        {
            AddEndpoints?.Invoke(this, new AddEndpointsEventArgs {Endpoints = endpoints});
        }
    }

    public sealed class TestControlPoint : IControlPoint
    {
        private bool _currentState;

        public string Name { get; internal set; }
        
        public Guid ExtensionId { get; internal set; }
        
        public string Id { get; internal set;}

        public Task<bool> Get()
        {
            return Task.FromResult(_currentState);
        }

        public Task Set(bool state)
        {
            _currentState = state;
            OnControlPointStateChanged(_currentState);
            return Task.CompletedTask;
        }

        public event EventHandler<bool> ControlPointStateChanged;

        private void OnControlPointStateChanged(bool state)
        {
            ControlPointStateChanged?.Invoke(this, state);
        }
    }
}