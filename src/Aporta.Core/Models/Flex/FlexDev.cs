using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aporta.Core.Models.Flex;

public class FlexDev
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

    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("logicalAddress")]
    public int? LogicalAddress { get; set; }

    [JsonPropertyName("macAddress")]
    public string MacAddress { get; set; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("speed")]
    public int? Speed { get; set; }

    [JsonPropertyName("devType")]
    public int? DevType { get; set; }

    [JsonPropertyName("devSubType")]
    public int? DevSubType { get; set; }

    [JsonPropertyName("devMod")]
    public int? DevMod { get; set; }

    [JsonPropertyName("devPlatform")]
    public int? DevPlatform { get; set; }

    [JsonPropertyName("devUse")]
    public int? DevUse { get; set; }

    [JsonPropertyName("devModConfig")]
    public FlexDevModConfig DevModConfig { get; set; }

    [JsonPropertyName("physicalParent")]
    public FlexObjRef PhysicalParent { get; set; }

    [JsonPropertyName("logicalParent")]
    public FlexObjRef LogicalParent { get; set; }

    [JsonPropertyName("timeZone")]
    public string TimeZone { get; set; }

    [JsonPropertyName("ignoreDaylightSavings")]
    public bool? IgnoreDaylightSavings { get; set; }

    [JsonPropertyName("logicalChildren")]
    public List<FlexObjRef> LogicalChildren { get; set; }

    [JsonPropertyName("physicalChildren")]
    public List<FlexObjRef> PhysicalChildren { get; set; }

    // Type-specific config (polymorphic based on devType)
    [JsonPropertyName("doorConfig")]
    public FlexDoorConfig DoorConfig { get; set; }

    [JsonPropertyName("credReaderConfig")]
    public FlexCredReaderConfig CredReaderConfig { get; set; }

    [JsonPropertyName("controllerConfig")]
    public FlexControllerConfig ControllerConfig { get; set; }

    [JsonPropertyName("actuatorConfig")]
    public FlexActuatorConfig ActuatorConfig { get; set; }

    [JsonPropertyName("sensorConfig")]
    public FlexSensorConfig SensorConfig { get; set; }

    [JsonPropertyName("nodeDevConfig")]
    public FlexNodeDevConfig NodeDevConfig { get; set; }
}

public class FlexDevModConfig
{
    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("type")]
    public int? Type { get; set; }
}

public class FlexBaseDevConfig
{
    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; }

    [JsonPropertyName("devInitiatesConnection")]
    public bool? DevInitiatesConnection { get; set; }

    [JsonPropertyName("encryptionKeyRef")]
    public FlexObjRef EncryptionKeyRef { get; set; }

    [JsonPropertyName("encryptionKeyRefNext")]
    public FlexObjRef EncryptionKeyRefNext { get; set; }

    [JsonPropertyName("disableEncryption")]
    public bool? DisableEncryption { get; set; }

    [JsonPropertyName("unid")]
    public int? Unid { get; set; }
}

public class FlexDoorConfig : FlexBaseDevConfig
{
    [JsonPropertyName("defaultDoorMode")]
    public FlexDoorMode DefaultDoorMode { get; set; }

    [JsonPropertyName("activateStrikeOnRex")]
    public bool? ActivateStrikeOnRex { get; set; }

    [JsonPropertyName("strikeTime")]
    public int? StrikeTime { get; set; }

    [JsonPropertyName("extendedStrikeTime")]
    public int? ExtendedStrikeTime { get; set; }

    [JsonPropertyName("heldTime")]
    public int? HeldTime { get; set; }

    [JsonPropertyName("extendedHeldTime")]
    public int? ExtendedHeldTime { get; set; }
}

public class FlexCredReaderConfig : FlexBaseDevConfig
{
    [JsonPropertyName("commType")]
    public int? CommType { get; set; }

    [JsonPropertyName("tamperType")]
    public int? TamperType { get; set; }

    [JsonPropertyName("ledType")]
    public int? LedType { get; set; }

    [JsonPropertyName("serialPortAddress")]
    public string SerialPortAddress { get; set; }
}

public class FlexControllerConfig : FlexBaseDevConfig
{
}

public class FlexActuatorConfig : FlexBaseDevConfig
{
    [JsonPropertyName("invert")]
    public bool? Invert { get; set; }
}

public class FlexSensorConfig : FlexBaseDevConfig
{
    [JsonPropertyName("invert")]
    public bool? Invert { get; set; }
}

public class FlexNodeDevConfig : FlexBaseDevConfig
{
}
