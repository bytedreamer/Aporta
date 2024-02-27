using Aporta.Shared.Models;

namespace Aporta.Drivers.OSDP.Shared.Actions;

public enum ActionType
{
    [Description("Add serial bus")]
    AddBus,
    [Description("Remove serial bus")]
    RemoveBus,
    [Description("Add/Update OSDP device")]
    AddUpdateDevice,
    [Description("Remove OSDP device")]
    RemoveDevice,
    [Description("Rescan available ports")]
    AvailablePorts,
    [Description("Clear OSDP device Identity")]
    ClearDeviceIdentity,
    [Description("Reset OSDP device security to install mode")]
    ResetToInstallMode,
    [Description("Reset OSDP device security to clear text")]
    ResetToClear,
    [Description("Rotate OSDP device encryption key")]
    RotateKey,
    [Description("Disable PKOC support on OSDP device")]
    DisablePKOC,
    [Description("Enable PKOC support on OSDP device")]
    EnablePKOC
}