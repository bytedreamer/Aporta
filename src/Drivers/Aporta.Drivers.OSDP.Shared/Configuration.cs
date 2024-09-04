using System.Collections.Generic;

namespace Aporta.Drivers.OSDP.Shared;

/// <summary>
/// Represents the configuration for the OSDP driver.
/// </summary>
public class Configuration
{
    /// <summary>
    /// Represents the available ports for communication.
    /// </summary>
    public string[] AvailablePorts { get; set; } = [];

    /// <summary>
    /// Represents the buses used for communication.
    /// </summary>
    /// <remarks>
    /// The Buses property stores a list of Bus objects, which represent individual buses used for communication.
    /// </remarks>
    public List<Bus> Buses { get; init; } = [];
}