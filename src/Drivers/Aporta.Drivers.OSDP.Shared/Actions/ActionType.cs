using Aporta.Shared.Models;

namespace Aporta.Drivers.OSDP.Shared.Actions;

public enum ActionType
{
    [Description("Add Serial Bus")]
    AddBus,
    [Description("Remove Serial Bus")]
    RemoveBus,
    [Description("Add/Update OSDP Device")]
    AddUpdateDevice,
    [Description("Remove OSDP Device")]
    RemoveDevice,
    [Description("Rescan Available Ports")]
    AvailablePorts,
    [Description("Clear OSDP Device Identity")]
    ClearDeviceIdentity
}