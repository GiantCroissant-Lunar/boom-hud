using System.Text.Json;
using BoomHud.Abstractions.Snapshots;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace BoomHud.Cli.Handlers.Baseline;

/// <summary>
/// Handler for baseline compare command.
/// Compares current snapshots against baseline and produces a report.
/// </summary>
public static class BaselineCompareHandler
{
    /// <summary>
    /// Executes baseline comparison with the given options.
    /// </summary>
    /// <returns>Exit code: 0 for success, 1 for failure.</returns>
    public static int Execute(BaselineCompareOptions options)
    {
        try
        {
            // Resolve current snapshots directory
            var currentPath = options.CurrentDir?.FullName
                ?? Path.Combine(Environment.CurrentDirectory, "ui", "snapshots");

            if (!Directory.Exists(currentPath))
            {
                Console.Error.WriteLine($"Error: Current snapshots directory not found: {currentPath}");
                return 1;
            }

            // Check for manifest
            var currentManifestPath = Path.Combine(currentPath, "snapshots.manifest.json");
            if (!File.Exists(currentManifestPath))
            {
                Console.Error.WriteLine($"Error: Current manifest not found: {currentManifestPath}");
                Console.Error.WriteLine("Run 'boomhud snapshot' first to generate snapshots.");
                return 1;
            }

            // Resolve baseline directory
            var baselinePath = options.BaselineDir?.FullName;
            if (string.IsNullOrEmpty(baselinePath))
            {
                Console.Error.WriteLine("Error: --baseline directory is required");
                return 1;
            }

            if (!Directory.Exists(baselinePath))
            {
                // Baseline directory missing - this is OK, just report "no baseline"
                Console.WriteLine("Warning: Baseline directory not found. This may be a new branch.");

                // Generate report with all frames marked as MissingBaseline
                var currentManifest = LoadManifest(currentManifestPath);
                if (currentManifest == null)
                {
                    Console.Error.WriteLine($"Error: Could not parse current manifest: {currentManifestPath}");
                    return 1;
                }

                var noBaselineReport = GenerateNoBaselineReport(currentManifest, currentPath);
                WriteReport(noBaselineReport, options.OutFile, currentPath, options.GhActions, options.PrintSummary, options.Verbose);

                return options.FailOn == FailOnMode.Any && noBaselineReport.Summary.Changed > 0 ? 1 : 0;
            }

            var baselineManifestPath = Path.Combine(baselinePath, "snapshots.manifest.json");
            if (!File.Exists(baselineManifestPath))
            {
                Console.Error.WriteLine($"Error: Baseline manifest not found: {baselineManifestPath}");
                return 1;
            }

            // Load both manifests
            var currentManifestDoc = LoadManifest(currentManifestPath);
            var baselineManifestDoc = LoadManifest(baselineManifestPath);

            if (currentManifestDoc == null)
            {
                Console.Error.WriteLine($"Error: Could not parse current manifest: {currentManifestPath}");
                return 1;
            }

            if (baselineManifestDoc == null)
            {
                Console.Error.WriteLine($"Error: Could not parse baseline manifest: {baselineManifestPath}");
                return 1;
            }

            if (options.Verbose)
            {
                Console.WriteLine($"Current: {currentPath}");
                Console.WriteLine($"  Godot: {currentManifestDoc.GodotVersion ?? "unknown"}");
                Console.WriteLine($"  Snapshots: {currentManifestDoc.Snapshots.Count}");
                Console.WriteLine($"Baseline: {baselinePath}");
                Console.WriteLine($"  Godot: {baselineManifestDoc.GodotVersion ?? "unknown"}");
                Console.WriteLine($"  Snapshots: {baselineManifestDoc.Snapshots.Count}");
                if (options.Tolerance > 0) Console.WriteLine($"  Tolerance: {options.Tolerance}");
                if (options.MinChangedPercent > 0) Console.WriteLine($"  Min changed %: {options.MinChangedPercent}");
                if (options.ProtectedFrames.Length > 0) Console.WriteLine($"  Protected frames: {string.Join(", ", options.ProtectedFrames)}");
            }

            // Perform comparison with metrics
            var report = CompareManifestsWithMetrics(
                currentManifestDoc, baselineManifestDoc,
                currentPath, baselinePath,
                currentManifestPath, baselineManifestPath,
                options.Tolerance, options.MinChangedPercent, options.ProtectedFrames);

            // Write report
            WriteReport(report, options.OutFile, currentPath, options.GhActions, options.PrintSummary, options.Verbose);

            // Determine exit code based on fail conditions
            return DetermineFailExitCode(report, options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    #region Manifest Loading

    internal static SnapshotOutputManifest? LoadManifest(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SnapshotOutputManifest>(json);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Report Generation

    private static BaselineReport GenerateNoBaselineReport(SnapshotOutputManifest current, string currentPath)
    {
        var frames = new List<FrameCompareResult>();
        var index = 0;

        foreach (var snap in current.Snapshots)
        {
            frames.Add(new FrameCompareResult
            {
                Index = index++,
                Name = snap.State,
                Status = FrameCompareStatus.MissingBaseline,
                CurrentHash = snap.Sha256,
                CurrentPath = snap.Path
            });
        }

        return new BaselineReport
        {
            ToolVersion = typeof(BaselineCompareHandler).Assembly.GetName().Version?.ToString(),
            Current = new ManifestInfo
            {
                Path = currentPath,
                GodotVersion = current.GodotVersion,
                ToolVersion = current.ToolVersion,
                Target = current.Target,
                InputHash = current.InputHashes.TryGetValue("manifest", out var manifestHash) ? manifestHash : null,
                SnapshotCount = current.Snapshots.Count
            },
            Summary = new BaselineCompareSummary
            {
                Total = frames.Count,
                Unchanged = 0,
                Changed = 0,
                MissingBaseline = frames.Count,
                MissingCurrent = 0,
                Compatible = true
            },
            Frames = frames
        };
    }

    internal static BaselineReport CompareManifestsWithMetrics(
        SnapshotOutputManifest current,
        SnapshotOutputManifest baseline,
        string currentPath,
        string baselinePath,
        string currentManifestPath,
        string baselineManifestPath,
        int tolerance,
        double minChangedPercent,
        string[] protectedFrames)
    {
        var frames = new List<FrameCompareResult>();
        var protectedSet = new HashSet<string>(protectedFrames, StringComparer.OrdinalIgnoreCase);

        // Build lookup for baseline by state name
        var baselineLookup = baseline.Snapshots.ToDictionary(s => s.State, s => s);
        var currentLookup = current.Snapshots.ToDictionary(s => s.State, s => s);

        // Union of all names
        var allNames = baselineLookup.Keys.Union(currentLookup.Keys).OrderBy(n => n).ToList();

        // Check compatibility (Godot version mismatch makes changes non-actionable)
        var compatible = true;
        string? incompatibilityReason = null;
        var isGodotVersionMismatch = false;

        if (!string.IsNullOrEmpty(baseline.GodotVersion) &&
            !string.IsNullOrEmpty(current.GodotVersion) &&
            baseline.GodotVersion != current.GodotVersion)
        {
            compatible = false;
            isGodotVersionMismatch = true;
            incompatibilityReason = $"Godot version mismatch: baseline={baseline.GodotVersion}, current={current.GodotVersion}";
        }

        var index = 0;
        var unchanged = 0;
        var changed = 0;
        var changedNonActionable = 0;
        var dimensionMismatch = 0;
        var missingBaseline = 0;
        var missingCurrent = 0;
        double maxChangedPercent = 0;
        var framesExceedingThreshold = 0;

        foreach (var name in allNames)
        {
            var hasBaseline = baselineLookup.TryGetValue(name, out var baselineSnap);
            var hasCurrent = currentLookup.TryGetValue(name, out var currentSnap);
            var isProtected = protectedSet.Contains(name);

            FrameCompareStatus status;
            var actionable = true;
            DiffMetrics? metrics = null;

            if (hasBaseline && hasCurrent)
            {
                if (baselineSnap!.Sha256 == currentSnap!.Sha256)
                {
                    status = FrameCompareStatus.Unchanged;
                    unchanged++;
                }
                else
                {
                    // Hash differs - compute metrics
                    var baselineFile = Path.Combine(baselinePath, baselineSnap.Path);
                    var currentFile = Path.Combine(currentPath, currentSnap.Path);

                    if (File.Exists(baselineFile) && File.Exists(currentFile))
                    {
                        metrics = ComputeDiffMetrics(baselineFile, currentFile, tolerance);

                        // Check for dimension mismatch
                        if (!metrics.DimensionsMatch)
                        {
                            status = FrameCompareStatus.DimensionMismatch;
                            actionable = false;
                            dimensionMismatch++;
                        }
                        // Check if below noise threshold
                        else if (metrics.ChangedPercent < minChangedPercent && !isProtected)
                        {
                            status = FrameCompareStatus.Unchanged;
                            unchanged++;
                        }
                        // Check if Godot version mismatch
                        else if (isGodotVersionMismatch)
                        {
                            status = FrameCompareStatus.ChangedNonActionable;
                            actionable = false;
                            changedNonActionable++;
                        }
                        else
                        {
                            status = FrameCompareStatus.Changed;
                            changed++;
                            maxChangedPercent = Math.Max(maxChangedPercent, metrics.ChangedPercent);
                        }
                    }
                    else
                    {
                        // Files missing - treat as changed without metrics
                        if (isGodotVersionMismatch)
                        {
                            status = FrameCompareStatus.ChangedNonActionable;
                            actionable = false;
                            changedNonActionable++;
                        }
                        else
                        {
                            status = FrameCompareStatus.Changed;
                            changed++;
                        }
                    }
                }
            }
            else if (!hasBaseline)
            {
                status = FrameCompareStatus.MissingBaseline;
                missingBaseline++;
            }
            else
            {
                status = FrameCompareStatus.MissingCurrent;
                missingCurrent++;
            }

            frames.Add(new FrameCompareResult
            {
                Index = index++,
                Name = name,
                Status = status,
                Actionable = actionable,
                BaselineHash = baselineSnap?.Sha256,
                CurrentHash = currentSnap?.Sha256,
                BaselinePath = baselineSnap?.Path,
                CurrentPath = currentSnap?.Path,
                DiffMetrics = metrics
            });
        }

        return new BaselineReport
        {
            ToolVersion = typeof(BaselineCompareHandler).Assembly.GetName().Version?.ToString(),
            Baseline = new ManifestInfo
            {
                Path = baselineManifestPath,
                GodotVersion = baseline.GodotVersion,
                ToolVersion = baseline.ToolVersion,
                Target = baseline.Target,
                InputHash = baseline.InputHashes.TryGetValue("manifest", out var baselineHash) ? baselineHash : null,
                SnapshotCount = baseline.Snapshots.Count
            },
            Current = new ManifestInfo
            {
                Path = currentManifestPath,
                GodotVersion = current.GodotVersion,
                ToolVersion = current.ToolVersion,
                Target = current.Target,
                InputHash = current.InputHashes.TryGetValue("manifest", out var manifestHash) ? manifestHash : null,
                SnapshotCount = current.Snapshots.Count
            },
            Summary = new BaselineCompareSummary
            {
                Total = frames.Count,
                Unchanged = unchanged,
                Changed = changed,
                ChangedNonActionable = changedNonActionable,
                DimensionMismatch = dimensionMismatch,
                MissingBaseline = missingBaseline,
                MissingCurrent = missingCurrent,
                Compatible = compatible,
                IncompatibilityReason = incompatibilityReason,
                Tolerance = tolerance,
                MinChangedPercent = minChangedPercent,
                MaxChangedPercent = maxChangedPercent,
                FramesExceedingThreshold = framesExceedingThreshold
            },
            Frames = frames
        };
    }

    #endregion

    #region Diff Metrics

    internal static DiffMetrics ComputeDiffMetrics(string baselinePath, string currentPath, int tolerance)
    {
        using var baseline = Image.Load<Rgba32>(baselinePath);
        using var current = Image.Load<Rgba32>(currentPath);
        return ComputeDiffMetrics(baseline, current, tolerance);
    }

    internal static DiffMetrics ComputeDiffMetrics(Image<Rgba32> baseline, Image<Rgba32> current, int tolerance)
    {

        var width = Math.Max(baseline.Width, current.Width);
        var height = Math.Max(baseline.Height, current.Height);
        var totalPixels = width * height;

        var changedPixels = 0;
        var maxDelta = 0;
        long totalDelta = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var baselinePixel = (x < baseline.Width && y < baseline.Height)
                    ? baseline[x, y]
                    : new Rgba32(0, 0, 0, 0);

                var currentPixel = (x < current.Width && y < current.Height)
                    ? current[x, y]
                    : new Rgba32(0, 0, 0, 0);

                var dr = Math.Abs(currentPixel.R - baselinePixel.R);
                var dg = Math.Abs(currentPixel.G - baselinePixel.G);
                var db = Math.Abs(currentPixel.B - baselinePixel.B);
                var da = Math.Abs(currentPixel.A - baselinePixel.A);

                var pixelMaxDelta = Math.Max(Math.Max(dr, dg), Math.Max(db, da));
                var pixelTotalDelta = dr + dg + db + da;

                if (pixelMaxDelta > tolerance)
                {
                    changedPixels++;
                    maxDelta = Math.Max(maxDelta, pixelMaxDelta);
                    totalDelta += pixelTotalDelta;
                }
            }
        }

        var changedPercent = totalPixels > 0 ? (double)changedPixels / totalPixels * 100 : 0;
        var meanDelta = changedPixels > 0 ? (double)totalDelta / changedPixels / 4 : 0; // Divide by 4 for RGBA avg

        return new DiffMetrics
        {
            BaselineWidth = baseline.Width,
            BaselineHeight = baseline.Height,
            CurrentWidth = current.Width,
            CurrentHeight = current.Height,
            TotalPixels = totalPixels,
            ChangedPixels = changedPixels,
            ChangedPercent = Math.Round(changedPercent, 4),
            MaxDelta = maxDelta,
            MeanDelta = Math.Round(meanDelta, 2)
        };
    }

    #endregion

    #region Report Output

    private static void WriteReport(
        BaselineReport report,
        FileInfo? outFile,
        string currentPath,
        bool ghActions,
        bool printSummary,
        bool verbose)
    {
        // Determine output path
        var outputPath = outFile?.FullName ?? Path.Combine(currentPath, "baseline-report.json");
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Write JSON report
        File.WriteAllText(outputPath, report.ToJson());
        Console.WriteLine($"Report written: {outputPath}");

        // Print summary
        if (printSummary || verbose)
        {
            PrintSummary(report);
        }

        // GitHub Actions step summary
        if (ghActions)
        {
            WriteGitHubActionsSummary(report);
        }
    }

    private static void PrintSummary(BaselineReport report)
    {
        Console.WriteLine();
        Console.WriteLine("=== Baseline Comparison Summary ===");
        Console.WriteLine($"Total frames: {report.Summary.Total}");
        Console.WriteLine($"  Unchanged:           {report.Summary.Unchanged}");
        Console.WriteLine($"  Changed:             {report.Summary.Changed}");
        if (report.Summary.ChangedNonActionable > 0)
        {
            Console.WriteLine($"  Non-actionable:      {report.Summary.ChangedNonActionable}");
        }
        if (report.Summary.DimensionMismatch > 0)
        {
            Console.WriteLine($"  Dimension mismatch:  {report.Summary.DimensionMismatch}");
        }
        Console.WriteLine($"  Missing baseline:    {report.Summary.MissingBaseline}");
        Console.WriteLine($"  Missing current:     {report.Summary.MissingCurrent}");

        if (report.Summary.Tolerance > 0 || report.Summary.MinChangedPercent > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Settings:");
            if (report.Summary.Tolerance > 0)
                Console.WriteLine($"  Tolerance:         {report.Summary.Tolerance}");
            if (report.Summary.MinChangedPercent > 0)
                Console.WriteLine($"  Min changed %:     {report.Summary.MinChangedPercent}");
        }

        if (!string.IsNullOrEmpty(report.Summary.IncompatibilityReason))
        {
            Console.WriteLine($"Warning: {report.Summary.IncompatibilityReason}");
        }

        // List changed frames with metrics
        var changedFrames = report.Frames
            .Where(f => f.Status == FrameCompareStatus.Changed)
            .OrderByDescending(f => f.DiffMetrics?.ChangedPercent ?? 0)
            .ToList();

        if (changedFrames.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Changed frames:");
            foreach (var frame in changedFrames)
            {
                if (frame.DiffMetrics != null)
                {
                    Console.WriteLine($"  - {frame.Name} ({frame.DiffMetrics.ChangedPercent:F2}% changed, max Δ={frame.DiffMetrics.MaxDelta})");
                }
                else
                {
                    Console.WriteLine($"  - {frame.Name}");
                }
            }
        }

        // List missing in current
        var missingCurrent = report.Frames.Where(f => f.Status == FrameCompareStatus.MissingCurrent).ToList();
        if (missingCurrent.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Missing in current (removed?):");
            foreach (var frame in missingCurrent)
            {
                Console.WriteLine($"  - {frame.Name}");
            }
        }
    }

    private static void WriteGitHubActionsSummary(BaselineReport report)
    {
        // Write to GITHUB_STEP_SUMMARY if available
        var summaryPath = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (string.IsNullOrEmpty(summaryPath))
        {
            Console.WriteLine("Warning: GITHUB_STEP_SUMMARY not set, skipping GitHub Actions summary");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## 📊 Baseline Comparison Report");
        sb.AppendLine();

        // Incompatibility banner (prominent warning at top)
        if (!report.Summary.Compatible)
        {
            sb.AppendLine("> [!WARNING]");
            sb.Append("> **Incompatible baselines**: ").AppendLine(report.Summary.IncompatibilityReason);
            sb.AppendLine("> Changes marked as non-actionable cannot be reliably compared.");
            sb.AppendLine();
        }

        // Summary table
        sb.AppendLine("| Metric | Count |");
        sb.AppendLine("|--------|-------|");
        sb.Append("| Total frames | ").Append(report.Summary.Total).AppendLine(" |");
        sb.Append("| ✅ Unchanged | ").Append(report.Summary.Unchanged).AppendLine(" |");
        sb.Append("| ⚠️ Changed | ").Append(report.Summary.Changed).AppendLine(" |");
        if (report.Summary.ChangedNonActionable > 0)
        {
            sb.Append("| 🔇 Changed (non-actionable) | ").Append(report.Summary.ChangedNonActionable).AppendLine(" |");
        }
        if (report.Summary.DimensionMismatch > 0)
        {
            sb.Append("| 📐 Dimension mismatch | ").Append(report.Summary.DimensionMismatch).AppendLine(" |");
        }
        sb.Append("| 🆕 New (no baseline) | ").Append(report.Summary.MissingBaseline).AppendLine(" |");
        sb.Append("| ❌ Removed | ").Append(report.Summary.MissingCurrent).AppendLine(" |");
        sb.AppendLine();

        // Threshold settings (if any)
        if (report.Summary.Tolerance > 0 || report.Summary.MinChangedPercent > 0)
        {
            sb.AppendLine("**Settings:**");
            if (report.Summary.Tolerance > 0)
            {
                sb.Append("- Tolerance: ").Append(report.Summary.Tolerance).AppendLine(" (per-channel delta)");
            }
            if (report.Summary.MinChangedPercent > 0)
            {
                sb.Append("- Min changed %: ").Append(report.Summary.MinChangedPercent).AppendLine(" (noise threshold)");
            }
            sb.AppendLine();
        }

        // Top offenders table (frames with highest change %)
        var changedFrames = report.Frames
            .Where(f => f.Status == FrameCompareStatus.Changed && f.DiffMetrics != null)
            .OrderByDescending(f => f.DiffMetrics!.ChangedPercent)
            .Take(10)
            .ToList();

        if (changedFrames.Count > 0)
        {
            sb.AppendLine("### 🔥 Top Changed Frames");
            sb.AppendLine();
            sb.AppendLine("| Frame | Changed % | Pixels | Max Δ | Mean Δ |");
            sb.AppendLine("|-------|-----------|--------|-------|--------|");
            foreach (var frame in changedFrames)
            {
                var m = frame.DiffMetrics!;
                sb.Append("| `").Append(frame.Name).Append("` | ")
                  .Append(m.ChangedPercent.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append("% | ")
                  .Append(m.ChangedPixels.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)).Append(" | ")
                  .Append(m.MaxDelta).Append(" | ")
                  .Append(m.MeanDelta.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(" |");
            }
            sb.AppendLine();

            // Expandable details with all changed frames
            var allChanged = report.Frames.Where(f => f.Status == FrameCompareStatus.Changed).ToList();
            if (allChanged.Count > 10)
            {
                sb.AppendLine("<details>");
                sb.Append("<summary>All ").Append(allChanged.Count).AppendLine(" changed frames</summary>");
                sb.AppendLine();
                sb.AppendLine("| Frame | Changed % |");
                sb.AppendLine("|-------|-----------|");
                foreach (var frame in allChanged.OrderByDescending(f => f.DiffMetrics?.ChangedPercent ?? 0))
                {
                    var pct = frame.DiffMetrics?.ChangedPercent.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A";
                    sb.Append("| `").Append(frame.Name).Append("` | ").Append(pct).AppendLine("% |");
                }
                sb.AppendLine();
                sb.AppendLine("</details>");
                sb.AppendLine();
            }
        }
        else
        {
            // Changed frames without metrics (legacy format)
            var changedNoMetrics = report.Frames.Where(f => f.Status == FrameCompareStatus.Changed).ToList();
            if (changedNoMetrics.Count > 0)
            {
                sb.AppendLine("### Changed Frames");
                sb.AppendLine();
                sb.AppendLine("<details>");
                sb.AppendLine("<summary>Click to expand</summary>");
                sb.AppendLine();
                sb.AppendLine("| Frame | Status |");
                sb.AppendLine("|-------|--------|");
                foreach (var frame in changedNoMetrics)
                {
                    sb.Append("| `").Append(frame.Name).AppendLine("` | ⚠️ Changed |");
                }
                sb.AppendLine();
                sb.AppendLine("</details>");
                sb.AppendLine();
            }
        }

        // Non-actionable changes (collapsed)
        var nonActionableFrames = report.Frames.Where(f => f.Status == FrameCompareStatus.ChangedNonActionable).ToList();
        if (nonActionableFrames.Count > 0)
        {
            sb.AppendLine("### Non-Actionable Changes (Godot version mismatch)");
            sb.AppendLine();
            sb.AppendLine("<details>");
            sb.AppendLine("<summary>Click to expand</summary>");
            sb.AppendLine();
            sb.Append("These ").Append(nonActionableFrames.Count).AppendLine(" frames changed but cannot be reliably compared due to Godot version mismatch.");
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        // New frames
        var newFrames = report.Frames.Where(f => f.Status == FrameCompareStatus.MissingBaseline).ToList();
        if (newFrames.Count > 0 && newFrames.Count <= 10)
        {
            sb.AppendLine("### New Frames (no baseline)");
            sb.AppendLine();
            foreach (var frame in newFrames)
            {
                sb.Append("- `").Append(frame.Name).AppendLine("`");
            }
            sb.AppendLine();
        }
        else if (newFrames.Count > 10)
        {
            sb.Append("### New Frames: ").Append(newFrames.Count).AppendLine(" (no baseline available)");
            sb.AppendLine();
        }

        // Artifacts links hint
        sb.AppendLine("---");
        sb.AppendLine("📎 See workflow artifacts for `compare-video` and `baseline-report`.");

        File.AppendAllText(summaryPath, sb.ToString());
        Console.WriteLine("GitHub Actions step summary written");
    }

    #endregion

    #region Exit Code Logic

    private static int DetermineFailExitCode(BaselineReport report, BaselineCompareOptions options)
    {
        // Never fail on non-actionable frames
        var actionableChanged = report.Frames
            .Where(f => f.Actionable && f.Status == FrameCompareStatus.Changed)
            .ToList();

        if (actionableChanged.Count == 0)
        {
            return 0; // No actionable changes
        }

        // Check protected frames first (always fail if any protected frame changed)
        if (options.ProtectedFrames.Length > 0)
        {
            var protectedSet = new HashSet<string>(options.ProtectedFrames, StringComparer.OrdinalIgnoreCase);
            var changedProtected = actionableChanged.Where(f => protectedSet.Contains(f.Name)).ToList();
            if (changedProtected.Count > 0)
            {
                Console.Error.WriteLine($"Error: {changedProtected.Count} protected frame(s) changed: {string.Join(", ", changedProtected.Select(f => f.Name))}");
                return 1;
            }
        }

        // Check fail-on-any
        if (options.FailOn == FailOnMode.Any)
        {
            return 1;
        }

        // Check threshold
        if (options.FailOn == FailOnMode.Percent && options.FailPercent.HasValue)
        {
            var exceeding = actionableChanged
                .Where(f => f.DiffMetrics != null && f.DiffMetrics.ChangedPercent > options.FailPercent.Value)
                .ToList();

            if (exceeding.Count > 0)
            {
                Console.Error.WriteLine($"Error: {exceeding.Count} frame(s) exceed {options.FailPercent.Value}% threshold");
                return 1;
            }
        }

        return 0;
    }

    #endregion
}
