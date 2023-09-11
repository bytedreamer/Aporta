using System;
using System.Collections;
using Aporta.Extensions.Endpoint;

namespace Aporta.Extensions.Hardware;

public class AccessCredentialReceivedEventArgs : EventArgs
{
    public AccessCredentialReceivedEventArgs(IAccess access, BitArray cardData, ushort bitCount)
    {
        Access = access;
        CardData = cardData;
        BitCount = bitCount;
    }
    public IAccess Access { get; }
        
    public BitArray CardData { get; }
        
    public ushort BitCount { get; }
}