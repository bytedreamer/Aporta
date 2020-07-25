using System;
using System.Collections.Generic;
using Aporta.Extensions.Endpoint;

namespace Aporta.Extensions.Hardware
{
    public class AccessCredentialReceivedEventArgs : EventArgs
    {
        public AccessCredentialReceivedEventArgs(IAccessPoint accessPoint, IEnumerable<byte> cardData)
        {
            AccessPoint = accessPoint;
            CardData = cardData;
        }
        public IAccessPoint AccessPoint { get; }
        public IEnumerable<byte> CardData { get; }
    }
}