namespace Aporta.Shared.Models.Flex;

public static class FlexEvtCodeHelper
{
    public static string GetEvtCodeText(int? evtCode)
    {
        return evtCode switch
        {
            9 => "Controller Startup",
            10 => "Controller Online",
            11 => "Controller Offline",
            15 => "Credential Reader Online",
            16 => "Credential Reader Offline",
            48 => "Access Granted",
            49 => "Access Denied",
            52 => "Door Forced",
            53 => "Door Not Forced",
            54 => "Door Held",
            55 => "Door Not Held",
            56 => "Door Opened",
            57 => "Door Closed",
            58 => "Door Locked",
            59 => "Door Unlocked",
            60 => "Door Mode: Unlocked",
            61 => "Door Mode: No Access",
            62 => "Door Mode: Card Only",
            63 => "Door Mode: Card and PIN",
            64 => "Door Mode: PIN Only",
            65 => "Door Mode: Card or PIN",
            86 => "Exit Requested",
            87 => "Momentary Unlock",
            113 => "On Primary Power",
            114 => "Off Primary Power",
            115 => "No Power",
            116 => "Battery OK",
            117 => "Battery Low",
            118 => "Battery Fail",
            125 => "Tamper Normal",
            126 => "Tamper",
            127 => "Schedule Active",
            128 => "Schedule Inactive",
            153 => "External",
            244 => "Battery Critical",
            259 => "Credential Reader Power Cycle",
            _ => $"Unknown ({evtCode})"
        };
    }

    public static string GetEvtSubCodeText(int? evtSubCode)
    {
        return evtSubCode switch
        {
            null => "",
            0 => "Inactive",
            6 => "Not Yet Effective",
            7 => "Expired",
            10 => "No Privileges",
            11 => "Outside Schedule",
            14 => "Unknown Card Number",
            15 => "Unknown Card Format",
            17 => "Unknown Unique PIN",
            20 => "Incorrect Facility Code",
            22 => "Door Mode: Static Locked",
            23 => "Door Mode: No Card",
            24 => "Door Mode: No Unique PIN",
            25 => "No Confirming PIN",
            27 => "Incorrect Confirming PIN",
            104 => "External",
            _ => $"Unknown ({evtSubCode})"
        };
    }
}
