using System.Text.Json;
using BoomHud.Abstractions.Snapshots;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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

    public string NormalizeMode { get; init; } = "off";

    public double? FailBelowOverallPercent { get; init; }

    public int Tolerance { get; init; } = 8;

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

    public string? Notes { get; init; }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonOptions);
    }
}

public sealed record ImageNormalizationInfo
{
    public required string Mode { get; init; }

    public required int ReferenceWidth { get; init; }

    public required int ReferenceHeight { get; init; }

    public required int CandidateWidth { get; init; }

    public required int CandidateHeight { get; init; }
}

/// <summary>
/// Measures similarity for one reference/candidate image pair.
/// </summary>
public static class ImageSimilarityHandler
{
    private const string NormalizeOff = "off";
    private const string NormalizeStretch = "stretch";
    private const string NormalizeCover = "cover";

    public static int Execute(ImageSimilarityOptions options)
    {
        if (options.ReferenceFile == null)
        {
            Console.Error.WriteLine("Error: --reference is required");
            return 1;
        }

        if (options.CandidateFile == null)
        {
            Console.Error.WriteLine("Error: --candidate is required");
            return 1;
        }

        if (!options.ReferenceFile.Exists)
        {
            Console.Error.WriteLine($"Error: Reference image not found: {options.ReferenceFile.FullName}");
            return 1;
        }

        if (!options.CandidateFile.Exists)
        {
            Console.Error.WriteLine($"Error: Candidate image not found: {options.CandidateFile.FullName}");
            return 1;
        }

        var normalizeMode = NormalizeModeOrNull(options.NormalizeMode);
        if (normalizeMode == null)
        {
            Console.Error.WriteLine("Error: --normalize must be one of: off, stretch, cover");
            return 1;
        }

        if (options.FailBelowOverallPercent is < 0 or > 100)
        {
            Console.Error.WriteLine("Error: --fail-below must be between 0 and 100");
            return 1;
        }

        try
        {
            string? tempDir = null;

            try
            {
                var scoreReferencePath = options.ReferenceFile.FullName;
                var scoreCandidatePath = options.CandidateFile.FullName;
                ImageNormalizationInfo? normalization = null;

                if (!string.Equals(normalizeMode, NormalizeOff, StringComparison.OrdinalIgnoreCase))
                {
                    tempDir = Path.Combine(Path.GetTempPath(), "boomhud-image-score", Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
                    Directory.CreateDirectory(tempDir);

                    var normalized = NormalizeCandidateToReference(
                        options.ReferenceFile.FullName,
                        options.CandidateFile.FullName,
                        normalizeMode!,
                        tempDir);

                    scoreReferencePath = normalized.ReferencePath;
                    scoreCandidatePath = normalized.CandidatePath;
                    normalization = normalized.Info;
                }

                var metrics = BaselineCompareHandler.ComputeDiffMetrics(
                    scoreReferencePath,
                    scoreCandidatePath,
                    options.Tolerance);

                var pixelIdentityPercent = Math.Round(Math.Max(0, 100.0 - metrics.ChangedPercent), 4);
                var deltaSimilarityPercent = ComputeDeltaSimilarityPercent(metrics.MeanDelta);
                var overallSimilarityPercent = ComputeOverallSimilarityPercent(metrics);
                var passedThreshold = !options.FailBelowOverallPercent.HasValue || overallSimilarityPercent >= options.FailBelowOverallPercent.Value;

                string? diffPath = null;
                if (options.DiffFile != null)
                {
                    var diffDirectory = options.DiffFile.DirectoryName;
                    if (!string.IsNullOrEmpty(diffDirectory))
                    {
                        Directory.CreateDirectory(diffDirectory);
                    }

                    BaselineDiffHandler.GeneratePixelDiff(
                        scoreReferencePath,
                        scoreCandidatePath,
                        options.DiffFile.FullName,
                        options.Tolerance);

                    diffPath = options.DiffFile.FullName;
                }

                var report = new ImageSimilarityReport
                {
                    Version = "1.1",
                    ReferencePath = options.ReferenceFile.FullName,
                    CandidatePath = options.CandidateFile.FullName,
                    Tolerance = options.Tolerance,
                    Normalization = normalization,
                    Metrics = metrics,
                    PixelIdentityPercent = pixelIdentityPercent,
                    DeltaSimilarityPercent = deltaSimilarityPercent,
                    OverallSimilarityPercent = overallSimilarityPercent,
                    FailBelowOverallPercent = options.FailBelowOverallPercent,
                    PassedThreshold = options.FailBelowOverallPercent.HasValue ? passedThreshold : null,
                    DiffPath = diffPath,
                    Notes = BuildNotes(metrics, normalization, options.FailBelowOverallPercent, passedThreshold)
                };

                var outputPath = options.OutFile?.FullName
                    ?? Path.Combine(options.CandidateFile.DirectoryName ?? Environment.CurrentDirectory, "image-similarity-report.json");

                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                File.WriteAllText(outputPath, report.ToJson());

                if (options.PrintSummary || options.Verbose)
                {
                    PrintSummary(report, outputPath);
                }

                return passedThreshold ? 0 : 2;
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to score images: {ex.Message}");
            return 1;
        }
    }

    internal static double ComputeDeltaSimilarityPercent(double meanDelta)
    {
        var normalized = 1.0 - Math.Clamp(meanDelta / 255.0, 0.0, 1.0);
        return Math.Round(normalized * 100.0, 4);
    }

    internal static double ComputeOverallSimilarityPercent(DiffMetrics metrics)
    {
        var pixelIdentityPercent = Math.Max(0, 100.0 - metrics.ChangedPercent);
        var deltaSimilarityPercent = ComputeDeltaSimilarityPercent(metrics.MeanDelta);
        var weightedScore = (pixelIdentityPercent * 0.7) + (deltaSimilarityPercent * 0.3);
        return Math.Round(Math.Clamp(weightedScore, 0.0, 100.0), 2);
    }

    private static string? NormalizeModeOrNull(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return NormalizeOff;
        }

        return mode.Trim().ToLowerInvariant() switch
        {
            NormalizeOff => NormalizeOff,
            NormalizeStretch => NormalizeStretch,
            NormalizeCover => NormalizeCover,
            _ => null
        };
    }

    private static (string ReferencePath, string CandidatePath, ImageNormalizationInfo Info) NormalizeCandidateToReference(
        string referencePath,
        string candidatePath,
        string mode,
        string tempDir)
    {
        using var reference = Image.Load<Rgba32>(referencePath);
        using var candidate = Image.Load<Rgba32>(candidatePath);

        var normalizedCandidatePath = Path.Combine(tempDir, "candidate.normalized.png");
        var resizeMode = mode switch
        {
            NormalizeStretch => ResizeMode.Stretch,
            NormalizeCover => ResizeMode.Crop,
            _ => throw new InvalidOperationException($"Unsupported normalize mode: {mode}")
        };

        candidate.Mutate(image => image.Resize(new ResizeOptions
        {
            Mode = resizeMode,
            Size = new Size(reference.Width, reference.Height),
            Position = AnchorPositionMode.Center
        }));
        candidate.SaveAsPng(normalizedCandidatePath);

        return (
            referencePath,
            normalizedCandidatePath,
            new ImageNormalizationInfo
            {
                Mode = mode,
                ReferenceWidth = reference.Width,
                ReferenceHeight = reference.Height,
                CandidateWidth = candidate.Width,
                CandidateHeight = candidate.Height
            });
    }

    private static string BuildNotes(
        DiffMetrics metrics,
        ImageNormalizationInfo? normalization,
        double? failBelowOverallPercent,
        bool passedThreshold)
    {
        var notes = new List<string>();

        if (normalization != null)
        {
            notes.Add($"Candidate normalized to reference dimensions using '{normalization.Mode}'.");
        }
        else if (!metrics.DimensionsMatch)
        {
            notes.Add("Dimensions differ; alignment/crop normalization is recommended before treating this score as authoritative.");
        }
        else
        {
            notes.Add("Scores are comparable because dimensions match.");
        }

        if (failBelowOverallPercent.HasValue)
        {
            notes.Add(passedThreshold
                ? $"Threshold check passed at {failBelowOverallPercent.Value:F2}%."
                : $"Threshold check failed at {failBelowOverallPercent.Value:F2}%.");
        }

        return string.Join(" ", notes);
    }

    private static void PrintSummary(ImageSimilarityReport report, string outputPath)
    {
        Console.WriteLine();
        Console.WriteLine("=== Image Similarity ===");
        Console.WriteLine($"Reference:          {report.ReferencePath}");
        Console.WriteLine($"Candidate:          {report.CandidatePath}");
        Console.WriteLine($"Report:             {outputPath}");
        Console.WriteLine($"Tolerance:          {report.Tolerance}");
        if (report.Normalization != null)
        {
            Console.WriteLine($"Normalization:      {report.Normalization.Mode} ({report.Normalization.ReferenceWidth}x{report.Normalization.ReferenceHeight})");
        }
        Console.WriteLine($"Dimensions match:   {report.Metrics.DimensionsMatch}");
        Console.WriteLine($"Pixel identity:     {report.PixelIdentityPercent:F2}%");
        Console.WriteLine($"Delta similarity:   {report.DeltaSimilarityPercent:F2}%");
        Console.WriteLine($"Overall similarity: {report.OverallSimilarityPercent:F2}%");
        if (report.FailBelowOverallPercent.HasValue)
        {
            Console.WriteLine($"Threshold:          {report.FailBelowOverallPercent.Value:F2}% ({(report.PassedThreshold == true ? "pass" : "fail")})");
        }
        Console.WriteLine($"Changed pixels:     {report.Metrics.ChangedPixels}/{report.Metrics.TotalPixels} ({report.Metrics.ChangedPercent:F2}%)");
        Console.WriteLine($"Mean Δ:             {report.Metrics.MeanDelta:F2}");
        Console.WriteLine($"Max Δ:              {report.Metrics.MaxDelta}");

        if (!string.IsNullOrEmpty(report.DiffPath))
        {
            Console.WriteLine($"Diff image:         {report.DiffPath}");
        }

        if (!string.IsNullOrEmpty(report.Notes))
        {
            Console.WriteLine($"Notes:              {report.Notes}");
        }
    }
}