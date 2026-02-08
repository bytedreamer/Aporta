using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aporta.Core.Models.Flex;

public class FlexCred
{
    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; }

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; }

    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("effective")]
    public string Effective { get; set; }

    [JsonPropertyName("expires")]
    public string Expires { get; set; }

    [JsonPropertyName("credTemplate")]
    public FlexObjRef CredTemplate { get; set; }

    [JsonPropertyName("cardPin")]
    public FlexCardPin CardPin { get; set; }

    [JsonPropertyName("doorAccessModifiers")]
    public FlexDoorAccessModifiers DoorAccessModifiers { get; set; }

    [JsonPropertyName("privBindings")]
    public List<FlexCredPrivBinding> PrivBindings { get; set; }
}

public class FlexCardPin
{
    [JsonPropertyName("credNum")]
    public string CredNum { get; set; }

    [JsonPropertyName("facilityCode")]
    public int? FacilityCode { get; set; }

    [JsonPropertyName("pin")]
    public string Pin { get; set; }

    [JsonPropertyName("pinUnique")]
    public bool? PinUnique { get; set; }
}

public class FlexCredPrivBinding
{
    [JsonPropertyName("schedRestriction")]
    public FlexSchedRestriction SchedRestriction { get; set; }

    [JsonPropertyName("priv")]
    public FlexObjRef Priv { get; set; }

    [JsonPropertyName("devAsDoorAccessPriv")]
    public FlexObjRef DevAsDoorAccessPriv { get; set; }

    [JsonPropertyName("unid")]
    public int? Unid { get; set; }
}

public class FlexCredTemplate
{
    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; }

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("cardPinTemplate")]
    public FlexCardPinTemplate CardPinTemplate { get; set; }

    [JsonPropertyName("priority")]
    public int? Priority { get; set; }
}

public class FlexCardPinTemplate
{
    [JsonPropertyName("credComponentPresence")]
    public int? CredComponentPresence { get; set; }

    [JsonPropertyName("credNumPresence")]
    public int? CredNumPresence { get; set; }

    [JsonPropertyName("pinPresence")]
    public int? PinPresence { get; set; }

    [JsonPropertyName("pinUnique")]
    public bool? PinUnique { get; set; }

    [JsonPropertyName("minPinLength")]
    public int? MinPinLength { get; set; }

    [JsonPropertyName("maxPinLength")]
    public int? MaxPinLength { get; set; }

    [JsonPropertyName("facilityCode")]
    public int? FacilityCode { get; set; }

    [JsonPropertyName("dataLayout")]
    public FlexObjRef DataLayout { get; set; }

    [JsonPropertyName("anyDataLayout")]
    public bool? AnyDataLayout { get; set; }
}
