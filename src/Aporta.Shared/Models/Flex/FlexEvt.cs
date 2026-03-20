using System.Text.Json.Serialization;

namespace Aporta.Shared.Models.Flex;

public class FlexEvt
{
    [JsonPropertyName("unid")]
    public long? Unid { get; set; }

    [JsonPropertyName("hwTime")]
    public string HwTime { get; set; }

    [JsonPropertyName("dbTime")]
    public string DbTime { get; set; }

    [JsonPropertyName("hwTimeZone")]
    public string HwTimeZone { get; set; }

    [JsonPropertyName("evtCode")]
    public int? EvtCode { get; set; }

    [JsonPropertyName("evtCodeText")]
    public string EvtCodeText { get; set; }

    [JsonPropertyName("externalEvtCodeText")]
    public string ExternalEvtCodeText { get; set; }

    [JsonPropertyName("externalEvtCodeId")]
    public string ExternalEvtCodeId { get; set; }

    [JsonPropertyName("evtSubCode")]
    public int? EvtSubCode { get; set; }

    [JsonPropertyName("evtSubCodeText")]
    public string EvtSubCodeText { get; set; }

    [JsonPropertyName("externalSubCodeText")]
    public string ExternalSubCodeText { get; set; }

    [JsonPropertyName("externalSubCodeId")]
    public string ExternalSubCodeId { get; set; }

    [JsonPropertyName("evtModifiers")]
    public FlexEvtModifiers EvtModifiers { get; set; }

    [JsonPropertyName("priority")]
    public int? Priority { get; set; }

    [JsonPropertyName("data")]
    public string Data { get; set; }

    [JsonPropertyName("evtDevRef")]
    public FlexEvtDevRef EvtDevRef { get; set; }

    [JsonPropertyName("evtControllerRef")]
    public FlexEvtDevRef EvtControllerRef { get; set; }

    [JsonPropertyName("evtCredRef")]
    public FlexEvtCredRef EvtCredRef { get; set; }

    [JsonPropertyName("evtSchedRef")]
    public FlexEvtSchedRef EvtSchedRef { get; set; }

    [JsonPropertyName("consumed")]
    public bool? Consumed { get; set; }

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; }
}

public class FlexEvtModifiers
{
    [JsonPropertyName("usedCard")]
    public bool? UsedCard { get; set; }

    [JsonPropertyName("usedPin")]
    public bool? UsedPin { get; set; }
}

public class FlexEvtDevRef
{
    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("logicalAddress")]
    public int? LogicalAddress { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; }

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; }

    [JsonPropertyName("externalId")]
    public string ExternalId { get; set; }

    [JsonPropertyName("devPlatform")]
    public int? DevPlatform { get; set; }

    [JsonPropertyName("devType")]
    public int? DevType { get; set; }

    [JsonPropertyName("devSubType")]
    public int? DevSubType { get; set; }

    [JsonPropertyName("devMod")]
    public int? DevMod { get; set; }

    [JsonPropertyName("devUse")]
    public int? DevUse { get; set; }

    [JsonPropertyName("externalDevTypeText")]
    public string ExternalDevTypeText { get; set; }

    [JsonPropertyName("externalDevTypeId")]
    public string ExternalDevTypeId { get; set; }

    [JsonPropertyName("externalDevModText")]
    public string ExternalDevModText { get; set; }

    [JsonPropertyName("externalDevModId")]
    public string ExternalDevModId { get; set; }
}

public class FlexEvtCredRef
{
    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("credTemplateRef")]
    public FlexEvtCredTemplateRef CredTemplateRef { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("credNum")]
    public string CredNum { get; set; }

    [JsonPropertyName("facilityCode")]
    public int? FacilityCode { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; }

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; }
}

public class FlexEvtCredTemplateRef
{
    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; }

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; }
}

public class FlexEvtSchedRef
{
    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; }

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; }

    [JsonPropertyName("invert")]
    public bool? Invert { get; set; }
}
