using System.Collections.Generic;

namespace Aporta.Drivers.OSDP.Shared;

/// <summary>
/// Represents a bus used for communication.
/// </summary>
public class Bus
{
    /// <summary>
    /// Gets or sets the name of the port used for communication.
    /// </summary>
    public string PortName { get; set; }

    /// <summary>
    /// Gets or sets the baud rate used for communication.
    /// </summary>
    public int BaudRate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether tracing is enabled.
    /// Tracing provides runtime logging information for debugging purposes.
    /// </summary>
    public bool IsTracing { get; init; }

    /// <summary>
    /// Gets or sets the list of devices connected to the bus.
    /// </summary>
    public List<Device> Devices { get; init; } = [];
}