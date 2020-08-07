using System;
using Aporta.Extensions.Endpoint;

namespace Aporta.Extensions.Hardware
{
    public class StateChangedEventArgs : EventArgs
    {
        public StateChangedEventArgs(IControlPoint endpoint, ControlPointState controlPointState)
        {
            Endpoint = endpoint;
            ControlPointState = controlPointState;
        }
        public IEndpoint Endpoint { get; }
        
        public ControlPointState ControlPointState { get; }
    }

    public class ControlPointState
    {
        public ControlPointState(bool newState, bool? oldState = null)
        {
            NewState = newState;
            OldState = oldState;
        }
        
        public bool NewState { get; }
        public bool? OldState { get; }
    }
}