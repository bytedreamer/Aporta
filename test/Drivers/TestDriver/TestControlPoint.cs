using System;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;

namespace Aporta.Drivers.TestDriver
{
    public sealed class TestControlPoint : IControlPoint
    {
        private bool _currentState;

        public string Name { get; internal set; }
        
        public Guid ExtensionId { get; internal set; }
        
        public string Id { get; internal set;}
        public Task<bool> GetOnlineStatus()
        {
            throw new NotImplementedException();
        }

        public Task<bool> GetState()
        {
            return Task.FromResult(_currentState);
        }

        public Task SetState(bool state)
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