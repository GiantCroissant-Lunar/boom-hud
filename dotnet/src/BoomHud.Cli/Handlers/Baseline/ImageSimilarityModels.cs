using System.Text.Json;
using System.Text.Json.Serialization;
using BoomHud.Abstractions.Snapshots;
using BoomHud.Generators.VisualIR;

namespace BoomHud.Cli.Handlers.Baseline;

/// <summary>
/// Options for single-image similarity scoring.
/// </summary>
public sealed record ImageSimilarityOptions
{
    public FileInfo? ReferenceFile { get; init; }

    public FileInfo? CandidateFile { get; init; }

    public FileInfo? OutFile { get; init; }

    public FileInfo? DiffFile { get; init; }

    public FileInfo? VisualIrFile { get; init; }

    public FileInfo? VisualRefinementOutFile { get; init; }

    public FileInfo? ActualLayoutFile { get; init; }

    public FileInfo? MeasuredLayoutOutFile { get; init; }

    public FileInfo? PencilSourceFile { get; init; }

    public FileInfo? PatchedPenOutFile { get; init; }

    public string NormalizeMode { get; init; } = "off";

    public double? FailBelowOverallPercent { get; init; }

    public int Tolerance { get; init; } = 8;

    public int VisualRefinementIterationBudget { get; init; } = 4;

    public bool AutoApplyPencilPatch { get; init; }

    public bool PrintSummary { get; init; } = true;

    public bool Verbose { get; init; }
}

/// <summary>
/// Structured report for one reference/candidate image comparison.
/// </summary>
public sealed record ImageSimilarityReport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public required string Version { get; init; }

    public required string ReferencePath { get; init; }

    public required string CandidatePath { get; init; }

    public required int Tolerance { get; init; }

    public ImageNormalizationInfo? Normalization { get; init; }

    public required DiffMetrics Metrics { get; init; }

    public required double PixelIdentityPercent { get; init; }

    public required double DeltaSimilarityPercent { get; init; }

    public required double OverallSimilarityPercent { get; init; }

    public double? FailBelowOverallPercent { get; init; }

    public bool? PassedThreshold { get; init; }

    public string? DiffPath { get; init; }

    public ImageSimilaritySpatialAnalysis? Analysis { get; init; }

    public ImageSimilarityRecursiveScoreNode? RecursiveAnalysis { get; init; }

    public IReadOnlyList<ImageSimilarityFinding> Findings { get; init; } = Array.Empty<ImageSimilarityFinding>();

    public string? Notes { get; init; }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonOptions);
    }
}

public sealed record ActualLayoutSnapshot
{
    public required string Version { get; init; }

    public required string BackendFamily { get; init; }

    public string? CaptureId { get; init; }

    public string? TargetName { get; init; }

    public required ActualLayoutNode Root { get; init; }
}

public sealed record ActualLayoutNode
{
    public required string LocalPath { get; init; }

    public required string Name { get; init; }

    public required string NodeType { get; init; }

    public required double X { get; init; }

    public required double Y { get; init; }

    public required double Width { get; init; }

    public required double Height { get; init; }

    public double ScaleX { get; init; } = 1;

    public double ScaleY { get; init; } = 1;

    public double PreferredWidth { get; init; } = -1;

    public double PreferredHeight { get; init; } = -1;

    public string? Text { get; init; }

    public double FontSize { get; init; } = -1;

    public bool WrapText { get; init; }

    public bool ClipContent { get; init; }

    public double PaddingLeft { get; init; }

    public double PaddingTop { get; init; }

    public double PaddingRight { get; init; }

    public double PaddingBottom { get; init; }

    public double MarginLeft { get; init; }

    public double MarginTop { get; init; }

    public double MarginRight { get; init; }

    public double MarginBottom { get; init; }

    public IReadOnlyList<ActualLayoutNode> Children { get; init; } = Array.Empty<ActualLayoutNode>();
}

public sealed record MeasuredLayoutReport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public required string Version { get; init; }

    public required string DocumentName { get; init; }

    public required string BackendFamily { get; init; }

    public string? CaptureId { get; init; }

    public string? TargetName { get; init; }

    public required string ExpectedRootStableId { get; init; }

    public required string ActualRootName { get; init; }

    public IReadOnlyList<MeasuredLayoutComparison> Comparisons { get; init; } = Array.Empty<MeasuredLayoutComparison>();

    public IReadOnlyList<MeasuredLayoutIssue> Issues { get; init; } = Array.Empty<MeasuredLayoutIssue>();

    public IReadOnlyList<MeasuredLayoutSemanticClassSummary> SemanticClassSummaries { get; init; } = Array.Empty<MeasuredLayoutSemanticClassSummary>();

    public IReadOnlyList<MeasuredLayoutSourceSemanticSummary> SourceSemanticSummaries { get; init; } = Array.Empty<MeasuredLayoutSourceSemanticSummary>();

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

public sealed record MeasuredLayoutSemanticClassSummary
{
    public required string SemanticClass { get; init; }

    public required int ComparisonCount { get; init; }

    public required int IssueCount { get; init; }

    public IReadOnlyList<string> IssueCategories { get; init; } = Array.Empty<string>();
}

public sealed record MeasuredLayoutSourceSemanticSummary
{
    public required string SourceSemanticRole { get; init; }

    public string? SourceAssetRealization { get; init; }

    public required int ComparisonCount { get; init; }

    public required int IssueCount { get; init; }

    public IReadOnlyList<string> IssueCategories { get; init; } = Array.Empty<string>();
}

public sealed record MeasuredLayoutComparison
{
    public required string LocalPath { get; init; }

    public required string ExpectedStableId { get; init; }

    public string? ExpectedSourceId { get; init; }

    public string? ExpectedSemanticClass { get; init; }

    public string? ExpectedSourceSemanticRole { get; init; }

    public string? ExpectedSourceAssetRealization { get; init; }

    public required string ActualName { get; init; }

    public required string ActualNodeType { get; init; }

    public required AxisSizing ExpectedWidthSizing { get; init; }

    public required AxisSizing ExpectedHeightSizing { get; init; }

    public required LayoutParticipation ExpectedParticipation { get; init; }

    public double ActualX { get; init; }

    public double ActualY { get; init; }

    public double ActualWidth { get; init; }

    public double ActualHeight { get; init; }

    public double ActualScaleX { get; init; }

    public double ActualScaleY { get; init; }

    public double? ExpectedStartInsetX { get; init; }

    public double? ExpectedStartInsetY { get; init; }

    public double? ExpectedAvailableWidth { get; init; }

    public double ActualPreferredWidth { get; init; }

    public double ActualPreferredHeight { get; init; }

    public double? ExpectedFontSize { get; init; }

    public double ActualFontSize { get; init; }

    public bool ExpectedWrapText { get; init; }

    public bool ActualWrapText { get; init; }

    public bool ExpectedClipContent { get; init; }

    public bool ActualClipContent { get; init; }

    public int ExpectedChildCount { get; init; }

    public int ActualChildCount { get; init; }
}

public sealed record MeasuredLayoutIssue
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

public sealed record ImageNormalizationInfo
{
    public required string Mode { get; init; }

    public required int ReferenceWidth { get; init; }

    public required int ReferenceHeight { get; init; }

    public required int CandidateWidth { get; init; }

    public required int CandidateHeight { get; init; }
}

public sealed record ImageSimilarityBounds
{
    public required int X { get; init; }

    public required int Y { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }
}

public sealed record ImageSimilarityPhaseScore
{
    public required string Phase { get; init; }

    public required double SimilarityPercent { get; init; }
}

public sealed record ImageSimilarityRecursiveScoreNode
{
    public required string Level { get; init; }

    public required ImageSimilarityBounds Bounds { get; init; }

    public required double OverallSimilarityPercent { get; init; }

    public IReadOnlyList<ImageSimilarityPhaseScore> Phases { get; init; } = Array.Empty<ImageSimilarityPhaseScore>();

    public IReadOnlyList<ImageSimilarityRecursiveScoreNode> Children { get; init; } = Array.Empty<ImageSimilarityRecursiveScoreNode>();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImageSimilarityFindingSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ImageSimilarityFinding
{
    public required string Category { get; init; }

    public required ImageSimilarityFindingSeverity Severity { get; init; }

    public string? Region { get; init; }

    public required string Summary { get; init; }

    public string? ProbableFixArea { get; init; }

    public string? SuggestedAction { get; init; }
}

public sealed record ImageSimilaritySpatialAnalysis
{
    public required double BaselineOpaquePercent { get; init; }

    public required double CandidateOpaquePercent { get; init; }

    public required double OpaqueCoverageDeltaPercent { get; init; }

    public required double LeftEdgeChangedPercent { get; init; }

    public required double RightEdgeChangedPercent { get; init; }

    public required double TopEdgeChangedPercent { get; init; }

    public required double BottomEdgeChangedPercent { get; init; }

    public required double CenterBandChangedPercent { get; init; }

    public required double LeftThirdChangedPercent { get; init; }

    public required double CenterThirdChangedPercent { get; init; }

    public required double RightThirdChangedPercent { get; init; }

    public required double TopThirdChangedPercent { get; init; }

    public required double MiddleThirdChangedPercent { get; init; }

    public required double BottomThirdChangedPercent { get; init; }

    public required string DominantHorizontalRegion { get; init; }

    public required string DominantVerticalRegion { get; init; }

    public required string DominantBand { get; init; }
}
