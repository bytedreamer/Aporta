using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aporta.Shared.Models.Flex;

public class FlexDevStateRecord
{
    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("dev")]
    public FlexObjRef Dev { get; set; }

    [JsonPropertyName("devState")]
    public FlexDevState DevState { get; set; }
}

public class FlexDevState
{
    [JsonPropertyName("devAspectStates")]
    public List<FlexDevAspectStateEntry> DevAspectStates { get; set; } = new();
}

public class FlexDevAspectStateEntry
{
    [JsonPropertyName("key")]
    public int? Key { get; set; }

    [JsonPropertyName("value")]
    public FlexDevAspectState Value { get; set; }
}

public class FlexDevAspectState
{
    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("devAspect")]
    public int? DevAspect { get; set; }

    [JsonPropertyName("hwTime")]
    public string HwTime { get; set; }

    [JsonPropertyName("dbTime")]
    public string DbTime { get; set; }

    [JsonPropertyName("commState")]
    public int? CommState { get; set; }

    [JsonPropertyName("commStateStale")]
    public bool? CommStateStale { get; set; }

    [JsonPropertyName("activityState")]
    public int? ActivityState { get; set; }

    [JsonPropertyName("activityStateStale")]
    public bool? ActivityStateStale { get; set; }

    [JsonPropertyName("doorMode")]
    public FlexDoorMode DoorMode { get; set; }

    [JsonPropertyName("doorModeStale")]
    public bool? DoorModeStale { get; set; }

    [JsonPropertyName("externalState")]
    public string ExternalState { get; set; }

    [JsonPropertyName("externalStateStale")]
    public bool? ExternalStateStale { get; set; }
}
