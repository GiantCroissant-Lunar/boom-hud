using BoomHud.Abstractions.IR;

namespace BoomHud.Generators.SourceSemantics;

// Compiler-owned source-semantic snapshot derived from the prepared HudDocument.
public sealed record SourceSemanticDocument
{
    public required string DocumentName { get; init; }

    public required string BackendFamily { get; init; }

    public required string SourceGenerationMode { get; init; }

    public required SourceSemanticNode Root { get; init; }

    public IReadOnlyList<SourceSemanticComponentDefinition> Components { get; init; } = [];
}

public sealed record SourceSemanticComponentDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required SourceSemanticNode Root { get; init; }
}

public sealed record SourceSemanticNode
{
    public required string StableId { get; init; }

    public string? SourceId { get; init; }

    public string? SourceNodeId { get; init; }

    public required ComponentType SourceType { get; init; }

    public required string SemanticRole { get; init; }

    public required AssetRealizationKind AssetRealization { get; init; }

    public string? SizeBand { get; init; }

    public string? FontFamily { get; init; }

    public string? TextGrowth { get; init; }

    public IReadOnlyDictionary<string, object?> Facts { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    public IReadOnlyList<SourceSemanticNode> Children { get; init; } = [];
}

public enum AssetRealizationKind
{
    Native,
    LayoutContainer,
    ComponentInstance,
    ImageAsset,
    IconGlyph,
    TextPrimitive,
    Unknown
}
