using System;
using System.Collections;
using Aporta.Extensions.Endpoint;

namespace Aporta.Extensions.Hardware;

public class AccessCredentialReceivedEventArgs : EventArgs
{
    public AccessCredentialReceivedEventArgs(IAccessPoint accessPoint, BitArray cardData, ushort bitCount)
    {
        AccessPoint = accessPoint;
        CardData = cardData;
        BitCount = bitCount;
    }
    public IAccessPoint AccessPoint { get; }
        
    public BitArray CardData { get; }
        
    public ushort BitCount { get; }
}