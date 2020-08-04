using System;
using Aporta.Extensions.Endpoint;

namespace Aporta.Extensions.Hardware
{
    public class OutputStateChangedEventArgs : EventArgs
    {
        public OutputStateChangedEventArgs(IControlPoint controlPoint, bool newState)
        {
            ControlPoint = controlPoint;
            NewState = newState;
        }
        public IControlPoint ControlPoint { get; }
        
        public bool NewState { get; }
    }
}