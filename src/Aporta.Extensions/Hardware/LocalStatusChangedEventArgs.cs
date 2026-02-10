using System;
using Aporta.Extensions.Endpoint;

namespace Aporta.Extensions.Hardware;

public class LocalStatusChangedEventArgs : EventArgs
{
    public IEndpoint Endpoint { get; }
    public bool? TamperState { get; }
    public bool PowerCycleDetected { get; }

    public LocalStatusChangedEventArgs(IEndpoint endpoint, bool? tamperState, bool powerCycleDetected)
    {
        Endpoint = endpoint;
        TamperState = tamperState;
        PowerCycleDetected = powerCycleDetected;
    }
}
