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
    ClearDeviceIdentity,
    [Description("Reset OSDP Device To Install Mode Security")]
    ResetToInstallMode,
    [Description("Reset OSDP Device To Clear Text Security")]
    ResetToClear,
    [Description("Rotate OSDP Device Encryption Key")]
    RotateKey
}