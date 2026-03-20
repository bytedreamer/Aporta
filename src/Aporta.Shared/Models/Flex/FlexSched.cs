using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aporta.Shared.Models.Flex;

public class FlexSched
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

    [JsonPropertyName("externalId")]
    public string ExternalId { get; set; }

    [JsonPropertyName("elements")]
    public List<FlexSchedElement> Elements { get; set; }
}

public class FlexSchedElement
{
    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("schedDays")]
    public List<int> SchedDays { get; set; }

    [JsonPropertyName("holidays")]
    public bool? Holidays { get; set; }

    [JsonPropertyName("holTypes")]
    public List<FlexObjRef> HolTypes { get; set; }

    [JsonPropertyName("start")]
    public string Start { get; set; }

    [JsonPropertyName("stop")]
    public string Stop { get; set; }

    [JsonPropertyName("plusDays")]
    public int? PlusDays { get; set; }
}
