using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aporta.Shared.Models.Flex;

public class FlexObjRef
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; }

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; }

    [JsonPropertyName("externalId")]
    public string ExternalId { get; set; }
}

public class FlexListResponse<T>
{
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("max")]
    public int Max { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("instanceList")]
    public List<T> InstanceList { get; set; } = new();
}

public class FlexInstanceResponse<T>
{
    [JsonPropertyName("instance")]
    public T Instance { get; set; }
}

public class FlexVoid
{
}

public class FlexAuthenticateRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; }

    [JsonPropertyName("apiClientType")]
    public int? ApiClientType { get; set; }
}

public class FlexAuthenticateResult
{
    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; set; }

    [JsonPropertyName("sessionToken")]
    public string SessionToken { get; set; }

    [JsonPropertyName("evtDevRef")]
    public FlexEvtDevRef EvtDevRef { get; set; }

    [JsonPropertyName("softwareVersion")]
    public string SoftwareVersion { get; set; }

    [JsonPropertyName("softwareVersionTimestamp")]
    public string SoftwareVersionTimestamp { get; set; }

    [JsonPropertyName("timeZone")]
    public string TimeZone { get; set; }

    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; }
}

public class FlexDoorAccessModifiers
{
    [JsonPropertyName("extDoorTime")]
    public bool? ExtDoorTime { get; set; }
}

public class FlexSchedRestriction
{
    [JsonPropertyName("sched")]
    public FlexObjRef Sched { get; set; }

    [JsonPropertyName("invert")]
    public bool? Invert { get; set; }
}

public class FlexDoorMode
{
    [JsonPropertyName("staticState")]
    public int? StaticState { get; set; }

    [JsonPropertyName("allowUniquePin")]
    public bool? AllowUniquePin { get; set; }

    [JsonPropertyName("allowCard")]
    public bool? AllowCard { get; set; }

    [JsonPropertyName("requireConfirmingPinWithCard")]
    public bool? RequireConfirmingPinWithCard { get; set; }
}
