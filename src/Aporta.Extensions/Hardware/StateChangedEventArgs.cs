using System;
using Aporta.Extensions.Endpoint;

namespace Aporta.Extensions.Hardware;

public class StateChangedEventArgs : EventArgs
{
    public StateChangedEventArgs(IEndpoint endpoint, bool state)
    {
        Endpoint = endpoint;
        State = state;
    }
        
    public IEndpoint Endpoint { get; }
        
    public bool State { get; }
}