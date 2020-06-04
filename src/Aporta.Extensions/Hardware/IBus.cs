using System.Collections.Generic;

namespace Aporta.Extensions.Hardware
{
    public interface IBus : IExtension
    {
        IEnumerable<IDevice> Devices { get; }
    }
}