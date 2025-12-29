namespace BoomHud.Abstractions.IR;

/// <summary>
/// Root document representing a parsed HUD component definition.
/// </summary>
public sealed record HudDocument
{
    /// <summary>
    /// Name of the component being defined.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional metadata about the component.
    /// </summary>
    public ComponentMetadata? Metadata { get; init; }

    /// <summary>
    /// Root component node of the HUD.
    /// </summary>
    public required ComponentNode Root { get; init; }

    /// <summary>
    /// Named styles defined in this document.
    /// </summary>
    public IReadOnlyDictionary<string, StyleSpec> Styles { get; init; } = new Dictionary<string, StyleSpec>();

    /// <summary>
    /// Reusable component definitions available to this document (e.g., extracted from Figma COMPONENTs).
    /// </summary>
    public IReadOnlyDictionary<string, HudComponentDefinition> Components { get; init; } = new Dictionary<string, HudComponentDefinition>();
}

/// <summary>
/// Metadata about a component definition.
/// </summary>
public sealed record ComponentMetadata
{
    public string? Description { get; init; }
    public string? Version { get; init; }
    public string? Author { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}
