using System.Collections.Generic;

namespace Aporta.Drivers.OSDP.Shared;

/// <summary>
/// Specifies the type of connection used for OSDP communication.
/// </summary>
public enum ConnectionType
{
    /// <summary>
    /// Serial port connection (RS-485).
    /// </summary>
    Serial,

    /// <summary>
    /// TCP/IP network connection (OSDP over IP).
    /// </summary>
    Tcp
}

/// <summary>
/// Represents a bus used for communication.
/// </summary>
public class Bus
{
    /// <summary>
    /// Gets or sets the connection type (Serial or TCP).
    /// </summary>
    public ConnectionType ConnectionType { get; set; } = ConnectionType.Serial;

    /// <summary>
    /// Gets or sets the name of the port used for communication.
    /// Also used as a unique identifier/key for the bus connection.
    /// </summary>
    public string PortName { get; set; }

    /// <summary>
    /// Gets or sets the baud rate used for communication.
    /// For TCP connections, this is used for OSDP timing simulation.
    /// </summary>
    public int BaudRate { get; set; }

    /// <summary>
    /// Gets or sets the TCP host address for TCP connections.
    /// </summary>
    public string TcpHost { get; set; }

    /// <summary>
    /// Gets or sets the TCP port number for TCP connections.
    /// Default is 9843 (standard OSDP over IP port).
    /// </summary>
    public int TcpPort { get; set; } = 9843;

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