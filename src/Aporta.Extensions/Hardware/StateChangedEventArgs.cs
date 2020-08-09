using System;
using Aporta.Extensions.Endpoint;

namespace Aporta.Extensions.Hardware
{
    public class StateChangedEventArgs : EventArgs
    {
        public StateChangedEventArgs(IControlPoint controlPoint, ControlPointState controlPointState)
        {
            Endpoint = controlPoint;
            ControlPointState = controlPointState;
        }
        
        public StateChangedEventArgs(IMonitorPoint monitorPoint, MonitorPointState monitorPointState)
        {
            Endpoint = monitorPoint;
            MonitorPointState = monitorPointState;
        }
        
        public IEndpoint Endpoint { get; }
        
        public ControlPointState ControlPointState { get; }
        
        public MonitorPointState MonitorPointState { get; }
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
    
    public class MonitorPointState
    {
        public MonitorPointState(bool newState, bool? oldState = null)
        {
            NewState = newState;
            OldState = oldState;
        }
        
        public bool NewState { get; }
        public bool? OldState { get; }
    }
}