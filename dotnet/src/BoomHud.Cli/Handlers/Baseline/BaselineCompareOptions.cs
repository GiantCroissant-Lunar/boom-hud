namespace BoomHud.Cli.Handlers.Baseline;

/// <summary>
/// Fail condition mode for baseline comparison.
/// </summary>
public enum FailOnMode
{
    /// <summary>Never fail on changed frames.</summary>
    None,

    /// <summary>Fail if any actionable frame changed.</summary>
    Any,

    /// <summary>Fail if any frame exceeds the specified percent threshold.</summary>
    Percent
}

/// <summary>
/// Options for baseline compare handler, mapping 1:1 to CLI flags.
/// </summary>
public sealed record BaselineCompareOptions
{
    /// <summary>
    /// Current snapshots directory (with snapshots.manifest.json).
    /// Default: ./ui/snapshots
    /// </summary>
    public DirectoryInfo? CurrentDir { get; init; }

    /// <summary>
    /// Baseline snapshots directory (with snapshots.manifest.json).
    /// Required.
    /// </summary>
    public DirectoryInfo? BaselineDir { get; init; }

    /// <summary>
    /// Output report file. Default: baseline-report.json in current directory.
    /// </summary>
    public FileInfo? OutFile { get; init; }

    /// <summary>
    /// Print summary to stdout (for CI).
    /// </summary>
    public bool PrintSummary { get; init; }

    /// <summary>
    /// Output GitHub Actions step summary format.
    /// </summary>
    public bool GhActions { get; init; }

    /// <summary>
    /// Fail condition mode.
    /// </summary>
    public FailOnMode FailOn { get; init; } = FailOnMode.None;

    /// <summary>
    /// Fail percent threshold (only used when FailOn is Percent).
    /// </summary>
    public double? FailPercent { get; init; }

    /// <summary>
    /// Per-channel delta tolerance (0-255). Pixels within tolerance are considered unchanged.
    /// </summary>
    public int Tolerance { get; init; }

    /// <summary>
    /// Minimum changed percent to report as changed (noise filter).
    /// </summary>
    public double MinChangedPercent { get; init; }

    /// <summary>
    /// Protected frame names (stricter: always fail if changed, regardless of threshold).
    /// </summary>
    public string[] ProtectedFrames { get; init; } = [];

    /// <summary>
    /// Enable verbose output.
    /// </summary>
    public bool Verbose { get; init; }

    /// <summary>
    /// Parses the --fail-on string value and returns the appropriate options settings.
    /// </summary>
    public static (FailOnMode Mode, double? Percent) ParseFailOn(string? failOn, bool legacyFailOnChanged)
    {
        // Legacy --fail-on-changed flag
        if (legacyFailOnChanged)
        {
            return (FailOnMode.Any, null);
        }

        if (string.IsNullOrEmpty(failOn))
        {
            return (FailOnMode.None, null);
        }

        if (failOn.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            return (FailOnMode.Any, null);
        }

        if (failOn.StartsWith("percent:", StringComparison.OrdinalIgnoreCase))
        {
            var valueStr = failOn.Substring(8);
            if (double.TryParse(valueStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var threshold))
            {
                return (FailOnMode.Percent, threshold);
            }
        }

        // Unrecognized value - warn but continue with no fail
        Console.Error.WriteLine($"Warning: Unrecognized --fail-on value '{failOn}'. Use 'any' or 'percent:X'");
        return (FailOnMode.None, null);
    }
}
