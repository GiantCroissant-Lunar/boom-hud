using BoomHud.Abstractions.IR;

namespace BoomHud.Generators.VisualIR;

// Compiler-owned visual fidelity snapshot derived from the prepared HudDocument.
public sealed record VisualDocument
{
    public required string DocumentName { get; init; }

    public required string BackendFamily { get; init; }

    public required string SourceGenerationMode { get; init; }

    public required VisualNode Root { get; init; }

    public IReadOnlyList<VisualComponentDefinition> Components { get; init; } = [];

    public IReadOnlyList<MetricProfileDefinition> MetricProfiles { get; init; } = [];
}

public sealed record VisualComponentDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required VisualNode Root { get; init; }
}

public sealed record VisualNode
{
    public required string StableId { get; init; }

    public string? SourceId { get; init; }

    public string? SourceNodeId { get; init; }

    public required VisualNodeKind Kind { get; init; }

    public required ComponentType SourceType { get; init; }

    public string? ComponentRefId { get; init; }

    public string? SemanticClass { get; init; }

    public string? SourceSemanticRole { get; init; }

    public string? SourceAssetRealization { get; init; }

    public string? MetricProfileId { get; init; }

    public IReadOnlyDictionary<string, object?> StaticProperties { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> PropertyOverrides { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);

    public required VisualBox Box { get; init; }

    public required EdgeContract EdgeContract { get; init; }

    public TypographyContract? Typography { get; init; }

    public IconContract? Icon { get; init; }

    public IReadOnlyList<VisualNode> Children { get; init; } = [];
}

public sealed record VisualBox
{
    public required ComponentType SourceType { get; init; }

    public LayoutType? LayoutType { get; init; }

    public Dimension? Width { get; init; }

    public Dimension? Height { get; init; }

    public Dimension? MinWidth { get; init; }

    public Dimension? MinHeight { get; init; }

    public Dimension? MaxWidth { get; init; }

    public Dimension? MaxHeight { get; init; }

    public Spacing? Gap { get; init; }

    public Spacing? Padding { get; init; }

    public Spacing? Margin { get; init; }

    public Dimension? Left { get; init; }

    public Dimension? Top { get; init; }

    public bool IsAbsolutePositioned { get; init; }

    public bool ClipContent { get; init; }

    public Alignment? Align { get; init; }

    public Justification? Justify { get; init; }

    public double? Weight { get; init; }

    public Color? Background { get; init; }

    public BorderSpec? Border { get; init; }

    public double? BorderRadius { get; init; }

    public double? Opacity { get; init; }
}

public sealed record EdgeContract
{
    public required LayoutParticipation Participation { get; init; }

    public required AxisSizing WidthSizing { get; init; }

    public required AxisSizing HeightSizing { get; init; }

    public required EdgePin HorizontalPin { get; init; }

    public required EdgePin VerticalPin { get; init; }

    public required OverflowBehavior OverflowX { get; init; }

    public required OverflowBehavior OverflowY { get; init; }

    public required WrapPressurePolicy WrapPressure { get; init; }
}

public sealed record TypographyContract
{
    public required string SemanticClass { get; init; }

    public string? ResolvedFontFamily { get; init; }

    public double? ResolvedFontSize { get; init; }

    public double? ResolvedLineHeight { get; init; }

    public double? ResolvedLetterSpacing { get; init; }

    public required bool WrapText { get; init; }

    public string? TextGrowth { get; init; }

    public string? SizeBand { get; init; }
}

public sealed record IconContract
{
    public required string SemanticClass { get; init; }

    public string? ResolvedFontFamily { get; init; }

    public double? ResolvedFontSize { get; init; }

    public required double BaselineOffset { get; init; }

    public required bool OpticalCentering { get; init; }

    public required string SizeMode { get; init; }

    public string? SizeBand { get; init; }
}

public sealed record MetricProfileDefinition
{
    public required string Id { get; init; }

    public required string BackendFamily { get; init; }

    public required string SemanticClass { get; init; }

    public TextMetricProfile? Text { get; init; }

    public IconMetricProfile? Icon { get; init; }
}

public sealed record TextMetricProfile
{
    public string? ResolvedFontFamily { get; init; }

    public double? ResolvedFontSize { get; init; }

    public double? ResolvedLineHeight { get; init; }

    public double? ResolvedLetterSpacing { get; init; }

    public required bool WrapText { get; init; }

    public string? TextGrowth { get; init; }

    public string? SizeBand { get; init; }
}

public sealed record IconMetricProfile
{
    public string? ResolvedFontFamily { get; init; }

    public double? ResolvedFontSize { get; init; }

    public required double BaselineOffset { get; init; }

    public required bool OpticalCentering { get; init; }

    public required string SizeMode { get; init; }

    public string? SizeBand { get; init; }
}

public enum VisualNodeKind
{
    Container,
    Text,
    Icon,
    Image,
    Interactive,
    Value,
    Collection,
    Spacer,
    Other
}

public enum AxisSizing
{
    Fixed,
    Fill,
    Hug
}

public enum EdgePin
{
    Start,
    Center,
    End
}

public enum WrapPressurePolicy
{
    Allow,
    Tight
}

public enum OverflowBehavior
{
    Visible,
    Clip
}

public enum LayoutParticipation
{
    NormalFlow,
    Overlay
}

public sealed record VisualSynthesisSummary
{
    public required int CandidateFamilyCount { get; init; }

    public required int ChosenFamilyCount { get; init; }

    public required int RewrittenOccurrenceCount { get; init; }

    public IReadOnlyList<VisualSynthesisFamilySummary> ComponentFamilies { get; init; } = [];
}

public sealed record VisualSynthesisFamilySummary
{
    public required string ComponentId { get; init; }

    public required string ComponentName { get; init; }

    public required string RootType { get; init; }

    public required int OccurrenceCount { get; init; }

    public required int NodeCount { get; init; }

    public required int Depth { get; init; }

    public required string SignatureHash { get; init; }

    public required int OverridePathCount { get; init; }
}

public sealed record RecursiveFidelityScoreNode
{
    public required string Level { get; init; }

    public required string RegionId { get; init; }

    public required double OverallSimilarityPercent { get; init; }

    public IReadOnlyList<RecursiveFidelityPhaseScore> Phases { get; init; } = [];

    public IReadOnlyList<RecursiveFidelityScoreNode> Children { get; init; } = [];
}

public sealed record RecursiveFidelityPhaseScore
{
    public required string Phase { get; init; }

    public required double SimilarityPercent { get; init; }
}

public sealed record VisualRefinementSummary
{
    public required int IterationBudget { get; init; }

    public required int IterationCount { get; init; }

    public required bool Converged { get; init; }

    public RecursiveFidelityScoreNode? ScoreTree { get; init; }

    public IReadOnlyList<VisualMeasuredIssue> MeasuredIssues { get; init; } = [];

    public IReadOnlyList<VisualRefinementAction> Actions { get; init; } = [];
}

public sealed record VisualRefinementAction
{
    public required int Iteration { get; init; }

    public required string TargetStableId { get; init; }

    public string? TargetSemanticClass { get; init; }

    public string? TargetSourceSemanticRole { get; init; }

    public string? TargetSourceAssetRealization { get; init; }

    public required string ReasonPhase { get; init; }

    public required string ActionType { get; init; }

    public required string Description { get; init; }

    public string? TriggerIssueCategory { get; init; }

    public string? TriggerIssueLocalPath { get; init; }
}

public sealed record VisualMeasuredIssue
{
    public required string Category { get; init; }

    public required string Severity { get; init; }

    public required string LocalPath { get; init; }

    public string? ExpectedSemanticClass { get; init; }

    public string? ExpectedSourceSemanticRole { get; init; }

    public string? ExpectedSourceAssetRealization { get; init; }

    public required string Summary { get; init; }

    public string? SuggestedAction { get; init; }
}
