namespace BoomHud.Cli.Handlers.Baseline;

/// <summary>
/// Options for baseline diff handler, mapping 1:1 to CLI flags.
/// </summary>
public sealed record BaselineDiffOptions
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
    /// Output directory for diff artifacts.
    /// Default: ./ui/diffs
    /// </summary>
    public DirectoryInfo? OutputDir { get; init; }

    /// <summary>
    /// Output report file. Default: diff-report.json in output directory.
    /// </summary>
    public FileInfo? ReportFile { get; init; }

    /// <summary>
    /// Per-channel delta tolerance (0-255). Pixels within tolerance are considered unchanged.
    /// </summary>
    public int Tolerance { get; init; }

    /// <summary>
    /// Enable verbose output.
    /// </summary>
    public bool Verbose { get; init; }
}