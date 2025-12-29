using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoomHud.Dsl.Figma;

/// <summary>
/// Response shape for Figma variables API.
/// Typically returned from /v1/files/:key/variables.
/// </summary>
public sealed class FigmaVariablesResponse
{
    [JsonPropertyName("variableCollections")]
    public Dictionary<string, FigmaVariableSet>? VariableCollections { get; set; }

    [JsonPropertyName("variables")]
    public Dictionary<string, FigmaVariable>? Variables { get; set; }
}

public sealed class FigmaVariableSet
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("defaultModeId")]
    public string? DefaultModeId { get; set; }

    [JsonPropertyName("modes")]
    public List<FigmaVariableMode>? Modes { get; set; }

    [JsonPropertyName("variableIds")]
    public List<string>? VariableIds { get; set; }
}

public sealed class FigmaVariableMode
{
    [JsonPropertyName("modeId")]
    public string ModeId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class FigmaVariable
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Resolved type of the variable, e.g. "COLOR", "FLOAT", "STRING".
    /// </summary>
    [JsonPropertyName("resolvedType")]
    public string ResolvedType { get; set; } = string.Empty;

    /// <summary>
    /// Map of modeId -&gt; concrete value for that mode.
    /// For COLOR this is typically an object with r/g/b/a; for FLOAT a number.
    /// </summary>
    [JsonPropertyName("valuesByMode")]
    public Dictionary<string, JsonElement>? ValuesByMode { get; set; }
}
