using System.Text.Json;
using BoomHud.Abstractions.Snapshots;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace BoomHud.Cli.Handlers.Baseline;

/// <summary>
/// Handler for baseline diff command.
/// Generates diff artifacts for changed snapshot frames.
/// </summary>
public static class BaselineDiffHandler
{
    /// <summary>
    /// Executes baseline diff generation with the given options.
    /// </summary>
    /// <returns>Exit code: 0 for success, 1 for failure.</returns>
    public static int Execute(BaselineDiffOptions options)
    {
        try
        {
            var currentPath = options.CurrentDir?.FullName
                ?? Path.Combine(Environment.CurrentDirectory, "ui", "snapshots");

            if (!Directory.Exists(currentPath))
            {
                Console.Error.WriteLine($"Error: Current snapshots directory not found: {currentPath}");
                return 1;
            }

            var currentManifestPath = Path.Combine(currentPath, "snapshots.manifest.json");
            if (!File.Exists(currentManifestPath))
            {
                Console.Error.WriteLine($"Error: Current manifest not found: {currentManifestPath}");
                return 1;
            }

            var baselinePath = options.BaselineDir?.FullName;
            if (string.IsNullOrEmpty(baselinePath))
            {
                Console.Error.WriteLine("Error: --baseline directory is required");
                return 1;
            }

            if (!Directory.Exists(baselinePath))
            {
                Console.Error.WriteLine($"Error: Baseline directory not found: {baselinePath}");
                return 1;
            }

            var baselineManifestPath = Path.Combine(baselinePath, "snapshots.manifest.json");
            if (!File.Exists(baselineManifestPath))
            {
                Console.Error.WriteLine($"Error: Baseline manifest not found: {baselineManifestPath}");
                return 1;
            }

            var currentManifest = BaselineCompareHandler.LoadManifest(currentManifestPath);
            var baselineManifest = BaselineCompareHandler.LoadManifest(baselineManifestPath);

            if (currentManifest == null || baselineManifest == null)
            {
                Console.Error.WriteLine("Error: Could not parse manifests");
                return 1;
            }

            var outputPath = options.OutputDir?.FullName
                ?? Path.Combine(Environment.CurrentDirectory, "ui", "diffs");
            Directory.CreateDirectory(outputPath);

            Console.WriteLine($"Current: {currentPath}");
            Console.WriteLine($"Baseline: {baselinePath}");
            Console.WriteLine($"Output: {outputPath}");
            if (options.Tolerance > 0)
            {
                Console.WriteLine($"Tolerance: {options.Tolerance}");
            }

            var report = BaselineCompareHandler.CompareManifestsWithMetrics(
                currentManifest,
                baselineManifest,
                currentPath,
                baselinePath,
                currentManifestPath,
                baselineManifestPath,
                options.Tolerance,
                minChangedPercent: 0,
                protectedFrames: []);

            var diffResults = GenerateDiffImages(report, currentPath, baselinePath, outputPath, options.Tolerance, options.Verbose);
            var updatedFrames = report.Frames
                .Select(frame => frame with { DiffPath = diffResults.GetValueOrDefault(frame.Name) })
                .ToList();
            var updatedReport = report with { Frames = updatedFrames };

            var reportPath = options.ReportFile?.FullName ?? Path.Combine(outputPath, "diff-report.json");
            WriteReport(updatedReport, reportPath);

            Console.WriteLine($"✓ Generated {diffResults.Count} diff image set(s)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    internal static Dictionary<string, string> GenerateDiffImages(
        BaselineReport report,
        string currentPath,
        string baselinePath,
        string outputPath,
        int tolerance,
        bool verbose)
    {
        var diffPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var changedFrames = report.Frames
            .Where(frame =>
                frame.Status == FrameCompareStatus.Changed ||
                frame.Status == FrameCompareStatus.ChangedNonActionable ||
                frame.Status == FrameCompareStatus.DimensionMismatch)
            .ToList();

        if (changedFrames.Count == 0)
        {
            Console.WriteLine("No changed frames to diff");
            return diffPaths;
        }

        Console.WriteLine($"Generating diffs for {changedFrames.Count} changed frames...");

        foreach (var frame in changedFrames)
        {
            if (string.IsNullOrEmpty(frame.BaselinePath) || string.IsNullOrEmpty(frame.CurrentPath))
            {
                continue;
            }

            var baselineFile = Path.Combine(baselinePath, frame.BaselinePath);
            var currentFile = Path.Combine(currentPath, frame.CurrentPath);

            if (!File.Exists(baselineFile) || !File.Exists(currentFile))
            {
                if (verbose)
                {
                    Console.WriteLine($"  Skipping {frame.Name}: missing file");
                }

                continue;
            }

            var prefix = frame.Index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
            var safeName = SanitizeFileName(frame.Name);
            var baselineOutName = $"{prefix}_{safeName}__baseline.png";
            var currentOutName = $"{prefix}_{safeName}__current.png";
            var diffOutName = $"{prefix}_{safeName}__diff.png";

            var baselineOutPath = Path.Combine(outputPath, baselineOutName);
            var currentOutPath = Path.Combine(outputPath, currentOutName);
            var diffOutPath = Path.Combine(outputPath, diffOutName);

            try
            {
                File.Copy(baselineFile, baselineOutPath, overwrite: true);
                File.Copy(currentFile, currentOutPath, overwrite: true);
                GeneratePixelDiff(baselineFile, currentFile, diffOutPath, tolerance);

                diffPaths[frame.Name] = diffOutName;

                if (verbose)
                {
                    Console.WriteLine($"  ✓ {frame.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  ✗ {frame.Name}: {ex.Message}");
            }
        }

        return diffPaths;
    }

    internal static void GeneratePixelDiff(string baselinePath, string currentPath, string outputPath, int tolerance = 0)
    {
        using var baseline = Image.Load<Rgba32>(baselinePath);
        using var current = Image.Load<Rgba32>(currentPath);

        var width = Math.Max(baseline.Width, current.Width);
        var height = Math.Max(baseline.Height, current.Height);

        using var diff = new Image<Rgba32>(width, height);

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

                var maxDelta = Math.Max(Math.Max(dr, dg), Math.Max(db, da));

                if (maxDelta > tolerance)
                {
                    var intensity = (byte)Math.Min(255, (dr + dg + db + da) / 2 + 128);
                    diff[x, y] = new Rgba32(intensity, 0, intensity, 255);
                }
                else
                {
                    var gray = (byte)((currentPixel.R + currentPixel.G + currentPixel.B) / 6);
                    diff[x, y] = new Rgba32(gray, gray, gray, 128);
                }
            }
        }

        using var outputStream = File.Create(outputPath);
        diff.Save(outputStream, new PngEncoder());
    }

    private static void WriteReport(BaselineReport report, string reportPath)
    {
        var outputDir = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        File.WriteAllText(reportPath, report.ToJson());
        Console.WriteLine($"Report written: {reportPath}");
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            sanitized.Append(invalid.Contains(c) ? '_' : c);
        }

        return sanitized.ToString();
    }
}