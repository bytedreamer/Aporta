using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aporta.Core.Models.Flex;

public class FlexDataFormat
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

    [JsonPropertyName("dataFormatType")]
    public int? DataFormatType { get; set; }

    // BinaryFormat extension (dataFormatType == 1)
    [JsonPropertyName("minBits")]
    public int? MinBits { get; set; }

    [JsonPropertyName("maxBits")]
    public int? MaxBits { get; set; }

    [JsonPropertyName("elements")]
    public List<FlexBinaryElement> Elements { get; set; }

    [JsonPropertyName("supportReverseRead")]
    public bool? SupportReverseRead { get; set; }
}

public class FlexBinaryElement
{
    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("num")]
    public int? Num { get; set; }

    [JsonPropertyName("type")]
    public int? Type { get; set; }

    [JsonPropertyName("start")]
    public int? Start { get; set; }

    [JsonPropertyName("len")]
    public int? Len { get; set; }

    // FieldBinaryElement (type == 2)
    [JsonPropertyName("field")]
    public int? Field { get; set; }

    [JsonPropertyName("staticValue")]
    public string StaticValue { get; set; }

    // ParityBinaryElement (type == 1)
    [JsonPropertyName("odd")]
    public bool? Odd { get; set; }

    [JsonPropertyName("srcStart")]
    public int? SrcStart { get; set; }

    [JsonPropertyName("srcLen")]
    public int? SrcLen { get; set; }

    [JsonPropertyName("mask")]
    public string Mask { get; set; }

    // StaticBinaryElement (type == 0)
    [JsonPropertyName("value")]
    public string Value { get; set; }
}

public class FlexDataLayout
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

    [JsonPropertyName("layoutType")]
    public int? LayoutType { get; set; }

    [JsonPropertyName("priority")]
    public int? Priority { get; set; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    // BasicDataLayout extension (layoutType == 0)
    [JsonPropertyName("dataFormat")]
    public FlexObjRef DataFormat { get; set; }
}

public class FlexEncryptionKey
{
    [JsonPropertyName("unid")]
    public int? Unid { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; }

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; }

    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; }

    [JsonPropertyName("size")]
    public int? Size { get; set; }

    [JsonPropertyName("keyIdentifier")]
    public string KeyIdentifier { get; set; }

    [JsonPropertyName("bytes")]
    public string Bytes { get; set; }
}
