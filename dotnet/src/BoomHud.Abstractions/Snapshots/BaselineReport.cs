using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoomHud.Abstractions.Snapshots;

/// <summary>
/// Status of a frame comparison.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrameCompareStatus
{
    /// <summary>Frame unchanged (hashes match).</summary>
    Unchanged,

    /// <summary>Frame changed (hashes differ).</summary>
    Changed,

    /// <summary>Frame exists in current but not in baseline.</summary>
    MissingBaseline,

    /// <summary>Frame exists in baseline but not in current.</summary>
    MissingCurrent,

    /// <summary>Frame changed but comparison is non-actionable (e.g., Godot version mismatch).</summary>
    ChangedNonActionable,

    /// <summary>Frame dimensions differ (non-actionable, viewport change).</summary>
    DimensionMismatch
}

/// <summary>
/// Comparison result for a single frame.
/// </summary>
public sealed record FrameCompareResult
{
    /// <summary>
    /// Frame index (0-based).
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; init; }

    /// <summary>
    /// State name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Comparison status.
    /// </summary>
    [JsonPropertyName("status")]
    public FrameCompareStatus Status { get; init; }

    /// <summary>
    /// Whether this comparison is actionable (false if Godot versions differ).
    /// </summary>
    [JsonPropertyName("actionable")]
    public bool Actionable { get; init; } = true;

    /// <summary>
    /// Baseline frame hash (null if missing).
    /// </summary>
    [JsonPropertyName("baselineHash")]
    public string? BaselineHash { get; init; }

    /// <summary>
    /// Current frame hash (null if missing).
    /// </summary>
    [JsonPropertyName("currentHash")]
    public string? CurrentHash { get; init; }

    /// <summary>
    /// Relative path to baseline PNG (null if missing).
    /// </summary>
    [JsonPropertyName("baselinePath")]
    public string? BaselinePath { get; init; }

    /// <summary>
    /// Relative path to current PNG (null if missing).
    /// </summary>
    [JsonPropertyName("currentPath")]
    public string? CurrentPath { get; init; }

    /// <summary>
    /// Relative path to diff PNG (null if not generated or not changed).
    /// </summary>
    [JsonPropertyName("diffPath")]
    public string? DiffPath { get; init; }

    /// <summary>
    /// Diff metrics (null if not computed or unchanged).
    /// </summary>
    [JsonPropertyName("diffMetrics")]
    public DiffMetrics? DiffMetrics { get; init; }
}

/// <summary>
/// Quantitative diff metrics for a frame comparison.
/// </summary>
public sealed record DiffMetrics
{
    /// <summary>Baseline image width.</summary>
    [JsonPropertyName("baselineWidth")]
    public int BaselineWidth { get; init; }

    /// <summary>Baseline image height.</summary>
    [JsonPropertyName("baselineHeight")]
    public int BaselineHeight { get; init; }

    /// <summary>Current image width.</summary>
    [JsonPropertyName("currentWidth")]
    public int CurrentWidth { get; init; }

    /// <summary>Current image height.</summary>
    [JsonPropertyName("currentHeight")]
    public int CurrentHeight { get; init; }

    /// <summary>Total pixels in comparison area (max dimensions).</summary>
    [JsonPropertyName("totalPixels")]
    public long TotalPixels { get; init; }

    /// <summary>Number of pixels that changed (above tolerance).</summary>
    [JsonPropertyName("changedPixels")]
    public long ChangedPixels { get; init; }

    /// <summary>Percentage of changed pixels (0-100).</summary>
    [JsonPropertyName("changedPercent")]
    public double ChangedPercent { get; init; }

    /// <summary>Maximum per-channel delta observed (0-255).</summary>
    [JsonPropertyName("maxDelta")]
    public int MaxDelta { get; init; }

    /// <summary>Mean per-channel delta across all pixels.</summary>
    [JsonPropertyName("meanDelta")]
    public double MeanDelta { get; init; }

    /// <summary>Whether dimensions match between baseline and current.</summary>
    [JsonPropertyName("dimensionsMatch")]
    public bool DimensionsMatch => BaselineWidth == CurrentWidth && BaselineHeight == CurrentHeight;
}

/// <summary>
/// Summary statistics for baseline comparison.
/// </summary>
public sealed record BaselineCompareSummary
{
    /// <summary>Total frames compared.</summary>
    [JsonPropertyName("total")]
    public int Total { get; init; }

    /// <summary>Unchanged frames (hash match).</summary>
    [JsonPropertyName("unchanged")]
    public int Unchanged { get; init; }

    /// <summary>Changed frames (hash mismatch).</summary>
    [JsonPropertyName("changed")]
    public int Changed { get; init; }

    /// <summary>Changed but non-actionable frames (Godot version mismatch).</summary>
    [JsonPropertyName("changedNonActionable")]
    public int ChangedNonActionable { get; init; }

    /// <summary>Frames with dimension mismatch (non-actionable).</summary>
    [JsonPropertyName("dimensionMismatch")]
    public int DimensionMismatch { get; init; }

    /// <summary>Frames missing in baseline.</summary>
    [JsonPropertyName("missingBaseline")]
    public int MissingBaseline { get; init; }

    /// <summary>Frames missing in current.</summary>
    [JsonPropertyName("missingCurrent")]
    public int MissingCurrent { get; init; }

    /// <summary>Whether baselines are compatible (same Godot version if both have it).</summary>
    [JsonPropertyName("compatible")]
    public bool Compatible { get; init; } = true;

    /// <summary>Incompatibility reason (if not compatible).</summary>
    [JsonPropertyName("incompatibilityReason")]
    public string? IncompatibilityReason { get; init; }

    /// <summary>Number of actionable changes (excludes non-actionable and dimension mismatch).</summary>
    [JsonPropertyName("actionableChanges")]
    public int ActionableChanges => Changed;

    /// <summary>Tolerance used for per-channel delta comparison (0-255).</summary>
    [JsonPropertyName("tolerance")]
    public int Tolerance { get; init; }

    /// <summary>Minimum changed percent threshold for noise filtering.</summary>
    [JsonPropertyName("minChangedPercent")]
    public double MinChangedPercent { get; init; }

    /// <summary>Maximum changed percent among actionable frames (for threshold checks).</summary>
    [JsonPropertyName("maxChangedPercent")]
    public double MaxChangedPercent { get; init; }

    /// <summary>Frames exceeding the fail threshold (if set).</summary>
    [JsonPropertyName("framesExceedingThreshold")]
    public int FramesExceedingThreshold { get; init; }
}

/// <summary>
/// Full baseline comparison report.
/// </summary>
public sealed record BaselineReport
{
    /// <summary>
    /// Report format version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Timestamp when comparison was run (ISO 8601).
    /// </summary>
    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; init; } = DateTime.UtcNow.ToString("O");

    /// <summary>
    /// BoomHud CLI version.
    /// </summary>
    [JsonPropertyName("toolVersion")]
    public string? ToolVersion { get; init; }

    /// <summary>
    /// Baseline manifest info.
    /// </summary>
    [JsonPropertyName("baseline")]
    public ManifestInfo? Baseline { get; init; }

    /// <summary>
    /// Current manifest info.
    /// </summary>
    [JsonPropertyName("current")]
    public ManifestInfo? Current { get; init; }

    /// <summary>
    /// Comparison summary statistics.
    /// </summary>
    [JsonPropertyName("summary")]
    public required BaselineCompareSummary Summary { get; init; }

    /// <summary>
    /// Per-frame comparison results.
    /// </summary>
    [JsonPropertyName("frames")]
    public IReadOnlyList<FrameCompareResult> Frames { get; init; } = [];

    /// <summary>
    /// Serializes to JSON.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, WriteOptions);

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

/// <summary>
/// Info about a manifest for comparison.
/// </summary>
public sealed record ManifestInfo
{
    /// <summary>Path to manifest file.</summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    /// <summary>Godot version used (if recorded).</summary>
    [JsonPropertyName("godotVersion")]
    public string? GodotVersion { get; init; }

    /// <summary>Tool version used.</summary>
    [JsonPropertyName("toolVersion")]
    public string? ToolVersion { get; init; }

    /// <summary>Target backend.</summary>
    [JsonPropertyName("target")]
    public string? Target { get; init; }

    /// <summary>Total input hash (combined hash of all inputs).</summary>
    [JsonPropertyName("inputHash")]
    public string? InputHash { get; init; }

    /// <summary>Number of snapshots.</summary>
    [JsonPropertyName("snapshotCount")]
    public int SnapshotCount { get; init; }
}
