using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aporta.Core.Models.Flex;

public class FlexPriv
{
    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; }

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("externalId")]
    public string ExternalId { get; set; }

    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("privType")]
    public int? PrivType { get; set; }

    // DoorAccessPriv extension (privType == 0)
    [JsonPropertyName("elements")]
    public List<FlexDoorAccessPrivElement> Elements { get; set; }
}

public class FlexDoorAccessPrivElement
{
    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("door")]
    public FlexObjRef Door { get; set; }

    [JsonPropertyName("schedRestriction")]
    public FlexSchedRestriction SchedRestriction { get; set; }
}
