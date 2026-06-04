using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BoomHud.Abstractions.Runtime;

public static class RuntimeSurfaceProtocol
{
    public const string CurrentVersion = "0.1";
    public const string BasicCatalogId = "boomhud.runtime.basic.v1";
}

public sealed record RuntimeSurfaceDocument
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = RuntimeSurfaceProtocol.CurrentVersion;

    [JsonPropertyName("surfaceId")]
    public required string SurfaceId { get; init; }

    [JsonPropertyName("catalogId")]
    public required string CatalogId { get; init; }

    [JsonPropertyName("revision")]
    public long Revision { get; init; }

    [JsonPropertyName("root")]
    public required RuntimeComponentNode Root { get; init; }

    [JsonPropertyName("dataModel")]
    public JsonObject? DataModel { get; init; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed record RuntimeComponentNode
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("layout")]
    public RuntimeLayoutSpec? Layout { get; init; }

    [JsonPropertyName("properties")]
    public IReadOnlyDictionary<string, RuntimeValue> Properties { get; init; } = new Dictionary<string, RuntimeValue>();

    [JsonPropertyName("bindings")]
    public IReadOnlyList<RuntimeBinding> Bindings { get; init; } = [];

    [JsonPropertyName("actions")]
    public IReadOnlyList<RuntimeActionDescriptor> Actions { get; init; } = [];

    [JsonPropertyName("children")]
    public IReadOnlyList<RuntimeComponentNode> Children { get; init; } = [];

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed record RuntimeLayoutSpec
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "vertical";

    [JsonPropertyName("width")]
    public double? Width { get; init; }

    [JsonPropertyName("height")]
    public double? Height { get; init; }

    [JsonPropertyName("minWidth")]
    public double? MinWidth { get; init; }

    [JsonPropertyName("minHeight")]
    public double? MinHeight { get; init; }

    [JsonPropertyName("maxWidth")]
    public double? MaxWidth { get; init; }

    [JsonPropertyName("maxHeight")]
    public double? MaxHeight { get; init; }

    [JsonPropertyName("gap")]
    public double? Gap { get; init; }

    [JsonPropertyName("padding")]
    public double? Padding { get; init; }

    [JsonPropertyName("align")]
    public string? Align { get; init; }

    [JsonPropertyName("justify")]
    public string? Justify { get; init; }
}

public sealed record RuntimeValue
{
    [JsonPropertyName("literal")]
    public JsonNode? Literal { get; init; }

    [JsonPropertyName("binding")]
    public RuntimeBinding? Binding { get; init; }
}

public sealed record RuntimeBinding
{
    [JsonPropertyName("property")]
    public string? Property { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("mode")]
    public string Mode { get; init; } = RuntimeBindingModes.OneWay;

    [JsonPropertyName("format")]
    public string? Format { get; init; }

    [JsonPropertyName("fallback")]
    public JsonNode? Fallback { get; init; }
}

public static class RuntimeBindingModes
{
    public const string OneWay = "oneWay";
    public const string TwoWay = "twoWay";
    public const string OneTime = "oneTime";
}

public sealed record RuntimeActionDescriptor
{
    [JsonPropertyName("event")]
    public required string Event { get; init; }

    [JsonPropertyName("command")]
    public required string Command { get; init; }

    [JsonPropertyName("payload")]
    public JsonObject? Payload { get; init; }
}

public sealed record RuntimeDataModelUpdate
{
    [JsonPropertyName("surfaceId")]
    public required string SurfaceId { get; init; }

    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("operation")]
    public string Operation { get; init; } = RuntimeDataModelOperations.Replace;

    [JsonPropertyName("value")]
    public JsonNode? Value { get; init; }
}

public static class RuntimeDataModelOperations
{
    public const string Replace = "replace";
    public const string Merge = "merge";
    public const string Remove = "remove";
}
