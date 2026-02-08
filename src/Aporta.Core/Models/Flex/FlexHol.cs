using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aporta.Core.Models.Flex;

public class FlexHol
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

    [JsonPropertyName("holTypes")]
    public List<FlexObjRef> HolTypes { get; set; }

    [JsonPropertyName("allHolTypes")]
    public bool? AllHolTypes { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonPropertyName("numDays")]
    public int? NumDays { get; set; }

    [JsonPropertyName("repeat")]
    public bool? Repeat { get; set; }

    [JsonPropertyName("numYearsRepeat")]
    public int? NumYearsRepeat { get; set; }

    [JsonPropertyName("preserveSchedDay")]
    public bool? PreserveSchedDay { get; set; }

    [JsonPropertyName("holCal")]
    public FlexObjRef HolCal { get; set; }
}

public class FlexHolCal
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
}

public class FlexHolType
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
}
