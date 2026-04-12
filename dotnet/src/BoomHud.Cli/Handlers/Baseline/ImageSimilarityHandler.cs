using System.Text.Json;
using System.Text.Json.Serialization;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Snapshots;
using BoomHud.Generators.VisualIR;
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

    public FileInfo? VisualIrFile { get; init; }

    public FileInfo? VisualRefinementOutFile { get; init; }

    public FileInfo? ActualLayoutFile { get; init; }

    public FileInfo? MeasuredLayoutOutFile { get; init; }

    public string NormalizeMode { get; init; } = "off";

    public double? FailBelowOverallPercent { get; init; }

    public int Tolerance { get; init; } = 8;

    public int VisualRefinementIterationBudget { get; init; } = 4;

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

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

public sealed record MeasuredLayoutComparison
{
    public required string LocalPath { get; init; }

    public required string ExpectedStableId { get; init; }

    public string? ExpectedSourceId { get; init; }

    public required string ActualName { get; init; }

    public required string ActualNodeType { get; init; }

    public required AxisSizing ExpectedWidthSizing { get; init; }

    public required AxisSizing ExpectedHeightSizing { get; init; }

    public required LayoutParticipation ExpectedParticipation { get; init; }

    public double ActualX { get; init; }

    public double ActualY { get; init; }

    public double ActualWidth { get; init; }

    public double ActualHeight { get; init; }

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

        if (options.VisualIrFile != null && !options.VisualIrFile.Exists)
        {
            Console.Error.WriteLine($"Error: Visual IR artifact not found: {options.VisualIrFile.FullName}");
            return 1;
        }

        if (options.ActualLayoutFile != null && !options.ActualLayoutFile.Exists)
        {
            Console.Error.WriteLine($"Error: Actual layout snapshot not found: {options.ActualLayoutFile.FullName}");
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
                var analysis = AnalyzeSpatialDiff(scoreReferencePath, scoreCandidatePath, options.Tolerance);
                var recursiveAnalysis = BuildRecursiveAnalysis(scoreReferencePath, scoreCandidatePath, options.Tolerance);
                var findings = BuildFindings(metrics, normalization, analysis);

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
                    Version = "1.3",
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
                    Analysis = analysis,
                    RecursiveAnalysis = recursiveAnalysis,
                    Findings = findings,
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

                if (options.VisualIrFile != null)
                {
                    EmitVisualRefinementArtifact(options.VisualIrFile, options.VisualRefinementOutFile, report, options.VisualRefinementIterationBudget);

                    if (options.ActualLayoutFile != null)
                    {
                        EmitMeasuredLayoutArtifact(options.VisualIrFile, options.ActualLayoutFile, options.MeasuredLayoutOutFile);
                    }
                }

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

    internal static ImageSimilaritySpatialAnalysis AnalyzeSpatialDiff(string baselinePath, string currentPath, int tolerance)
    {
        using var baseline = Image.Load<Rgba32>(baselinePath);
        using var current = Image.Load<Rgba32>(currentPath);

        var width = Math.Max(baseline.Width, current.Width);
        var height = Math.Max(baseline.Height, current.Height);
        var totalPixels = Math.Max(1, width * height);
        const int alphaThreshold = 16;

        var edgeBandWidth = Math.Max(1, (int)Math.Round(width * 0.2));
        var edgeBandHeight = Math.Max(1, (int)Math.Round(height * 0.2));
        var rightBandStart = Math.Max(0, width - edgeBandWidth);
        var bottomBandStart = Math.Max(0, height - edgeBandHeight);

        var centerStartX = width / 3;
        var centerEndX = width - centerStartX;
        var centerStartY = height / 3;
        var centerEndY = height - centerStartY;

        long baselineOpaque = 0;
        long currentOpaque = 0;

        long leftEdgeTotal = 0;
        long rightEdgeTotal = 0;
        long topEdgeTotal = 0;
        long bottomEdgeTotal = 0;
        long centerBandTotal = 0;

        long leftEdgeChanged = 0;
        long rightEdgeChanged = 0;
        long topEdgeChanged = 0;
        long bottomEdgeChanged = 0;
        long centerBandChanged = 0;

        long leftThirdTotal = 0;
        long centerThirdTotal = 0;
        long rightThirdTotal = 0;
        long topThirdTotal = 0;
        long middleThirdTotal = 0;
        long bottomThirdTotal = 0;

        long leftThirdChanged = 0;
        long centerThirdChanged = 0;
        long rightThirdChanged = 0;
        long topThirdChanged = 0;
        long middleThirdChanged = 0;
        long bottomThirdChanged = 0;

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

                if (baselinePixel.A > alphaThreshold)
                {
                    baselineOpaque++;
                }

                if (currentPixel.A > alphaThreshold)
                {
                    currentOpaque++;
                }

                var dr = Math.Abs(currentPixel.R - baselinePixel.R);
                var dg = Math.Abs(currentPixel.G - baselinePixel.G);
                var db = Math.Abs(currentPixel.B - baselinePixel.B);
                var da = Math.Abs(currentPixel.A - baselinePixel.A);
                var changed = Math.Max(Math.Max(dr, dg), Math.Max(db, da)) > tolerance;

                var isLeftEdge = x < edgeBandWidth;
                var isRightEdge = x >= rightBandStart;
                var isTopEdge = y < edgeBandHeight;
                var isBottomEdge = y >= bottomBandStart;
                var isCenterBand = x >= centerStartX && x < centerEndX && y >= centerStartY && y < centerEndY;

                if (isLeftEdge)
                {
                    leftEdgeTotal++;
                    if (changed)
                    {
                        leftEdgeChanged++;
                    }
                }

                if (isRightEdge)
                {
                    rightEdgeTotal++;
                    if (changed)
                    {
                        rightEdgeChanged++;
                    }
                }

                if (isTopEdge)
                {
                    topEdgeTotal++;
                    if (changed)
                    {
                        topEdgeChanged++;
                    }
                }

                if (isBottomEdge)
                {
                    bottomEdgeTotal++;
                    if (changed)
                    {
                        bottomEdgeChanged++;
                    }
                }

                if (isCenterBand)
                {
                    centerBandTotal++;
                    if (changed)
                    {
                        centerBandChanged++;
                    }
                }

                if (x < width / 3)
                {
                    leftThirdTotal++;
                    if (changed)
                    {
                        leftThirdChanged++;
                    }
                }
                else if (x < (width * 2) / 3)
                {
                    centerThirdTotal++;
                    if (changed)
                    {
                        centerThirdChanged++;
                    }
                }
                else
                {
                    rightThirdTotal++;
                    if (changed)
                    {
                        rightThirdChanged++;
                    }
                }

                if (y < height / 3)
                {
                    topThirdTotal++;
                    if (changed)
                    {
                        topThirdChanged++;
                    }
                }
                else if (y < (height * 2) / 3)
                {
                    middleThirdTotal++;
                    if (changed)
                    {
                        middleThirdChanged++;
                    }
                }
                else
                {
                    bottomThirdTotal++;
                    if (changed)
                    {
                        bottomThirdChanged++;
                    }
                }
            }
        }

        var leftEdgeChangedPercent = Percent(leftEdgeChanged, leftEdgeTotal);
        var rightEdgeChangedPercent = Percent(rightEdgeChanged, rightEdgeTotal);
        var topEdgeChangedPercent = Percent(topEdgeChanged, topEdgeTotal);
        var bottomEdgeChangedPercent = Percent(bottomEdgeChanged, bottomEdgeTotal);
        var centerBandChangedPercent = Percent(centerBandChanged, centerBandTotal);

        var leftThirdChangedPercent = Percent(leftThirdChanged, leftThirdTotal);
        var centerThirdChangedPercent = Percent(centerThirdChanged, centerThirdTotal);
        var rightThirdChangedPercent = Percent(rightThirdChanged, rightThirdTotal);
        var topThirdChangedPercent = Percent(topThirdChanged, topThirdTotal);
        var middleThirdChangedPercent = Percent(middleThirdChanged, middleThirdTotal);
        var bottomThirdChangedPercent = Percent(bottomThirdChanged, bottomThirdTotal);

        var baselineOpaquePercent = Percent(baselineOpaque, totalPixels);
        var currentOpaquePercent = Percent(currentOpaque, totalPixels);

        return new ImageSimilaritySpatialAnalysis
        {
            BaselineOpaquePercent = baselineOpaquePercent,
            CandidateOpaquePercent = currentOpaquePercent,
            OpaqueCoverageDeltaPercent = Math.Round(currentOpaquePercent - baselineOpaquePercent, 4),
            LeftEdgeChangedPercent = leftEdgeChangedPercent,
            RightEdgeChangedPercent = rightEdgeChangedPercent,
            TopEdgeChangedPercent = topEdgeChangedPercent,
            BottomEdgeChangedPercent = bottomEdgeChangedPercent,
            CenterBandChangedPercent = centerBandChangedPercent,
            LeftThirdChangedPercent = leftThirdChangedPercent,
            CenterThirdChangedPercent = centerThirdChangedPercent,
            RightThirdChangedPercent = rightThirdChangedPercent,
            TopThirdChangedPercent = topThirdChangedPercent,
            MiddleThirdChangedPercent = middleThirdChangedPercent,
            BottomThirdChangedPercent = bottomThirdChangedPercent,
            DominantHorizontalRegion = MaxLabel(
                ("left", leftThirdChangedPercent),
                ("center", centerThirdChangedPercent),
                ("right", rightThirdChangedPercent)),
            DominantVerticalRegion = MaxLabel(
                ("top", topThirdChangedPercent),
                ("middle", middleThirdChangedPercent),
                ("bottom", bottomThirdChangedPercent)),
            DominantBand = MaxLabel(
                ("left-edge", leftEdgeChangedPercent),
                ("right-edge", rightEdgeChangedPercent),
                ("top-edge", topEdgeChangedPercent),
                ("bottom-edge", bottomEdgeChangedPercent),
                ("center-band", centerBandChangedPercent))
        };
    }

    internal static ImageSimilarityRecursiveScoreNode BuildRecursiveAnalysis(string baselinePath, string currentPath, int tolerance)
    {
        using var baseline = Image.Load<Rgba32>(baselinePath);
        using var current = Image.Load<Rgba32>(currentPath);

        var rootBounds = new Rectangle(0, 0, Math.Max(baseline.Width, current.Width), Math.Max(baseline.Height, current.Height));
        return BuildRecursiveNode(baseline, current, rootBounds, tolerance, depth: 0);
    }

    internal static RecursiveFidelityScoreNode? ConvertRecursiveAnalysis(ImageSimilarityRecursiveScoreNode? node)
    {
        if (node == null)
        {
            return null;
        }

        return new RecursiveFidelityScoreNode
        {
            Level = node.Level,
            RegionId = BuildRegionId(node),
            OverallSimilarityPercent = node.OverallSimilarityPercent,
            Phases = node.Phases
                .Select(static phase => new RecursiveFidelityPhaseScore
                {
                    Phase = phase.Phase,
                    SimilarityPercent = phase.SimilarityPercent
                })
                .ToList(),
            Children = node.Children
                .Select(ConvertRecursiveAnalysis)
                .Where(static child => child != null)
                .Cast<RecursiveFidelityScoreNode>()
                .ToList()
        };
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

    private static List<ImageSimilarityFinding> BuildFindings(
        DiffMetrics metrics,
        ImageNormalizationInfo? normalization,
        ImageSimilaritySpatialAnalysis analysis)
    {
        var findings = new List<ImageSimilarityFinding>();
        var coverageDelta = Math.Abs(analysis.OpaqueCoverageDeltaPercent);
        var normalized = normalization != null;

        if (!metrics.DimensionsMatch && !normalized)
        {
            findings.Add(new ImageSimilarityFinding
            {
                Category = "dimension-mismatch",
                Severity = ImageSimilarityFindingSeverity.Warning,
                Region = "full-canvas",
                Summary = "Reference and candidate dimensions differ, so the raw similarity score is dominated by canvas mismatch before component-level fidelity is considered.",
                ProbableFixArea = "capture/scoring normalization",
                SuggestedAction = "Match output canvas size or rerun the score with --normalize stretch before treating the percentage as authoritative."
            });
        }

        if (coverageDelta >= 5)
        {
            var candidateHasMoreCoverage = analysis.OpaqueCoverageDeltaPercent > 0;
            findings.Add(new ImageSimilarityFinding
            {
                Category = "content-coverage-mismatch",
                Severity = ImageSimilarityFindingSeverity.Warning,
                Region = analysis.DominantBand,
                Summary = candidateHasMoreCoverage
                    ? "The candidate covers noticeably more visible area than the reference, which usually points to overflow, oversized bounds, or extra background fill."
                    : "The candidate covers noticeably less visible area than the reference, which usually points to collapsed content, missing elements, or overly aggressive clipping.",
                ProbableFixArea = "layout translation or generated sizing",
                SuggestedAction = candidateHasMoreCoverage
                    ? "Inspect root sizing, overflow, and fill/stretch handling for the generated backend."
                    : "Inspect collapsed containers, missing child mounting, and fill/stretch policies for the generated backend."
            });
        }

        var dominantBandPercent = GetDominantBandPercent(analysis);
        var maxEdgeBandPercent = new[]
        {
            analysis.LeftEdgeChangedPercent,
            analysis.RightEdgeChangedPercent,
            analysis.TopEdgeChangedPercent,
            analysis.BottomEdgeChangedPercent
        }.Max();

        if (maxEdgeBandPercent >= metrics.ChangedPercent + 8 && maxEdgeBandPercent >= analysis.CenterBandChangedPercent + 5)
        {
            findings.Add(new ImageSimilarityFinding
            {
                Category = "edge-alignment-mismatch",
                Severity = ImageSimilarityFindingSeverity.Info,
                Region = analysis.DominantBand,
                Summary = $"Changed pixels are concentrated around the {analysis.DominantBand}, which suggests anchoring, padding, or wrap pressure against that edge.",
                ProbableFixArea = "layout translation or text wrapping",
                SuggestedAction = "Inspect edge padding, anchoring, fill width, and any labels that wrap or truncate near the dominant edge."
            });
        }

        if ((metrics.DimensionsMatch || normalized) && coverageDelta < 6 && metrics.ChangedPercent >= 12 && metrics.MeanDelta <= 96)
        {
            findings.Add(new ImageSimilarityFinding
            {
                Category = "text-or-icon-metrics-mismatch",
                Severity = ImageSimilarityFindingSeverity.Info,
                Region = dominantBandPercent == analysis.CenterBandChangedPercent ? "center-content" : analysis.DominantBand,
                Summary = "Most differences look like medium-strength visual drift rather than missing content, which usually means font metrics, wrapping, icon centering, or spacing policy is off.",
                ProbableFixArea = "text/icon generator policy",
                SuggestedAction = "Tune font size, line height, wrap mode, and icon baseline or optical centering before revisiting broader layout rules."
            });
        }

        if ((metrics.DimensionsMatch || normalized) && metrics.ChangedPercent >= 45 && metrics.MeanDelta >= 96)
        {
            findings.Add(new ImageSimilarityFinding
            {
                Category = "global-layout-or-style-mismatch",
                Severity = ImageSimilarityFindingSeverity.Warning,
                Region = "full-canvas",
                Summary = "A large portion of the image differs with strong pixel deltas, which points to major layout, styling, or missing-asset divergence rather than fine-grained metric drift.",
                ProbableFixArea = "generator layout/style emission",
                SuggestedAction = "Validate generated hierarchy, root bounds, backgrounds, and style application before tuning smaller text or icon details."
            });
        }

        return findings;
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

        if (report.Findings.Count > 0)
        {
            Console.WriteLine("Findings:");
            foreach (var finding in report.Findings)
            {
                var region = string.IsNullOrWhiteSpace(finding.Region) ? string.Empty : $" [{finding.Region}]";
                Console.WriteLine($"  - {finding.Category}{region}: {finding.Summary}");
            }
        }

        if (!string.IsNullOrEmpty(report.Notes))
        {
            Console.WriteLine($"Notes:              {report.Notes}");
        }

        if (report.RecursiveAnalysis != null)
        {
            Console.WriteLine($"Recursive root:     {report.RecursiveAnalysis.Level} ({report.RecursiveAnalysis.OverallSimilarityPercent:F2}%)");
        }
    }

    private static void EmitVisualRefinementArtifact(
        FileInfo visualIrFile,
        FileInfo? requestedOutput,
        ImageSimilarityReport report,
        int iterationBudget)
    {
        var visualDocument = JsonSerializer.Deserialize<VisualDocument>(File.ReadAllText(visualIrFile.FullName));
        if (visualDocument == null)
        {
            throw new InvalidOperationException($"Failed to deserialize Visual IR artifact '{visualIrFile.FullName}'.");
        }

        var summary = VisualRefinementPlanner.Plan(
            visualDocument,
            ConvertRecursiveAnalysis(report.RecursiveAnalysis),
            iterationBudget);

        var outputPath = requestedOutput?.FullName ?? ResolveDefaultVisualRefinementPath(visualIrFile, visualDocument);
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputPath, VisualRefinementPlanner.ToJson(summary));
    }

    private static void EmitMeasuredLayoutArtifact(
        FileInfo visualIrFile,
        FileInfo actualLayoutFile,
        FileInfo? requestedOutput)
    {
        var visualDocument = JsonSerializer.Deserialize<VisualDocument>(File.ReadAllText(visualIrFile.FullName));
        if (visualDocument == null)
        {
            throw new InvalidOperationException($"Failed to deserialize Visual IR artifact '{visualIrFile.FullName}'.");
        }

        var actualLayout = JsonSerializer.Deserialize<ActualLayoutSnapshot>(File.ReadAllText(actualLayoutFile.FullName));
        if (actualLayout == null)
        {
            throw new InvalidOperationException($"Failed to deserialize actual layout snapshot '{actualLayoutFile.FullName}'.");
        }

        var report = BuildMeasuredLayoutReport(visualDocument, actualLayout);
        var outputPath = requestedOutput?.FullName ?? ResolveDefaultMeasuredLayoutPath(actualLayoutFile, visualDocument);
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputPath, report.ToJson());
    }

    private static string ResolveDefaultVisualRefinementPath(FileInfo visualIrFile, VisualDocument visualDocument)
        => Path.Combine(
            visualIrFile.DirectoryName ?? Environment.CurrentDirectory,
            $"{visualDocument.DocumentName}.visual-refinement.json");

    private static string ResolveDefaultMeasuredLayoutPath(FileInfo actualLayoutFile, VisualDocument visualDocument)
        => Path.Combine(
            actualLayoutFile.DirectoryName ?? Environment.CurrentDirectory,
            $"{visualDocument.DocumentName}.measured-layout.json");

    internal static MeasuredLayoutReport BuildMeasuredLayoutReport(VisualDocument visualDocument, ActualLayoutSnapshot actualLayout)
    {
        var componentMap = visualDocument.Components.ToDictionary(component => component.Id, StringComparer.Ordinal);
        var expectedRoot = ExpandEffectiveVisualNode(ResolveExpectedLayoutRoot(visualDocument, actualLayout), componentMap);
        var comparisons = new List<MeasuredLayoutComparison>();
        var issues = new List<MeasuredLayoutIssue>();

        CompareLayoutNode(expectedRoot, actualLayout.Root, null, null, "root", comparisons, issues, childIndex: null);

        return new MeasuredLayoutReport
        {
            Version = "1.0",
            DocumentName = visualDocument.DocumentName,
            BackendFamily = actualLayout.BackendFamily,
            CaptureId = actualLayout.CaptureId,
            TargetName = actualLayout.TargetName,
            ExpectedRootStableId = expectedRoot.StableId,
            ActualRootName = actualLayout.Root.Name,
            Comparisons = comparisons,
            Issues = issues
        };
    }

    private static VisualNode ResolveExpectedLayoutRoot(VisualDocument visualDocument, ActualLayoutSnapshot actualLayout)
    {
        var candidates = FlattenVisualNodes(visualDocument.Root).ToList();
        var targetName = actualLayout.TargetName;
        var actualRootName = actualLayout.Root.Name;

        static string NormalizeName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            foreach (var suffix in new[] { "Root", "View", "Panel", "Container", "Host" })
            {
                if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && trimmed.Length > suffix.Length)
                {
                    trimmed = trimmed[..^suffix.Length];
                    break;
                }
            }

            return new string(trimmed.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        }

        var normalizedTarget = NormalizeName(targetName);
        var normalizedActualRoot = NormalizeName(actualRootName);
        var normalizedDocument = NormalizeName(visualDocument.DocumentName);

        var scored = candidates
            .Select(node => new
            {
                Node = node,
                Score = Score(node)
            })
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Node.StableId, StringComparer.Ordinal)
            .ToList();

        return scored.FirstOrDefault(entry => entry.Score > 0)?.Node ?? visualDocument.Root;

        int Score(VisualNode node)
        {
            var score = 0;
            var normalizedSourceId = NormalizeName(node.SourceId);
            var normalizedSourceNodeId = NormalizeName(node.SourceNodeId);
            var normalizedStableId = NormalizeName(node.StableId);

            score += MatchScore(normalizedTarget, normalizedSourceId, 120);
            score += MatchScore(normalizedTarget, normalizedSourceNodeId, 100);
            score += MatchScore(normalizedTarget, normalizedStableId, 80);
            score += MatchScore(normalizedActualRoot, normalizedSourceId, 100);
            score += MatchScore(normalizedActualRoot, normalizedSourceNodeId, 80);
            score += MatchScore(normalizedDocument, normalizedSourceId, 40);
            score += MatchScore(normalizedDocument, normalizedSourceNodeId, 30);
            if (string.Equals(node.StableId, "root", StringComparison.Ordinal))
            {
                score += 10;
            }

            return score;
        }

        static int MatchScore(string left, string right, int weight)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return 0;
            }

            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                return weight;
            }

            return right.Contains(left, StringComparison.Ordinal) || left.Contains(right, StringComparison.Ordinal)
                ? Math.Max(1, weight / 2)
                : 0;
        }
    }

    private static IEnumerable<VisualNode> FlattenVisualNodes(VisualNode root)
    {
        yield return root;
        foreach (var child in root.Children)
        {
            foreach (var descendant in FlattenVisualNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static VisualNode ExpandEffectiveVisualNode(
        VisualNode node,
        IReadOnlyDictionary<string, VisualComponentDefinition> componentMap)
    {
        if (!string.IsNullOrWhiteSpace(node.ComponentRefId)
            && componentMap.TryGetValue(node.ComponentRefId, out var componentDefinition))
        {
            return ExpandVisualNodeFromTemplate(
                node,
                componentDefinition.Root,
                componentMap,
                node.StableId);
        }

        var expandedChildren = node.Children
            .Select(child => ExpandEffectiveVisualNode(child, componentMap))
            .ToList();

        return node with
        {
            Children = expandedChildren
        };
    }

    private static VisualNode ExpandVisualNodeFromTemplate(
        VisualNode instanceNode,
        VisualNode templateNode,
        IReadOnlyDictionary<string, VisualComponentDefinition> componentMap,
        string stableId)
    {
        var expandedChildren = templateNode.Children
            .Select((child, index) => ExpandTemplateChild(child, componentMap, $"{stableId}/{index}"))
            .ToList();

        return templateNode with
        {
            StableId = stableId,
            SourceId = instanceNode.SourceId ?? templateNode.SourceId,
            SourceNodeId = instanceNode.SourceNodeId ?? templateNode.SourceNodeId,
            ComponentRefId = instanceNode.ComponentRefId,
            SemanticClass = instanceNode.SemanticClass ?? templateNode.SemanticClass,
            MetricProfileId = instanceNode.MetricProfileId ?? templateNode.MetricProfileId,
            PropertyOverrides = instanceNode.PropertyOverrides,
            Children = expandedChildren
        };
    }

    private static VisualNode ExpandTemplateChild(
        VisualNode templateNode,
        IReadOnlyDictionary<string, VisualComponentDefinition> componentMap,
        string stableId)
    {
        if (!string.IsNullOrWhiteSpace(templateNode.ComponentRefId)
            && componentMap.TryGetValue(templateNode.ComponentRefId, out var nestedDefinition))
        {
            return ExpandVisualNodeFromTemplate(templateNode, nestedDefinition.Root, componentMap, stableId);
        }

        var expandedChildren = templateNode.Children
            .Select((child, index) => ExpandTemplateChild(child, componentMap, $"{stableId}/{index}"))
            .ToList();

        return templateNode with
        {
            StableId = stableId,
            Children = expandedChildren
        };
    }

    private static void CompareLayoutNode(
        VisualNode expected,
        ActualLayoutNode actual,
        VisualNode? expectedParent,
        ActualLayoutNode? actualParent,
        string localPath,
        List<MeasuredLayoutComparison> comparisons,
        List<MeasuredLayoutIssue> issues,
        int? childIndex)
    {
        var expectedStartInsetX = ResolveExpectedStartInsetX(expected, expectedParent, actualParent, childIndex);
        var expectedStartInsetY = ResolveExpectedStartInsetY(expected, expectedParent, actualParent, childIndex);
        var expectedAvailableWidth = ResolveExpectedAvailableWidth(expected, expectedParent, actualParent, childIndex);
        var expectedFontSize = expected.Icon?.ResolvedFontSize ?? expected.Typography?.ResolvedFontSize;
        var expectedWrapText = expected.Typography?.WrapText ?? false;
        var expectedClipContent = expected.Box.ClipContent
            || expected.EdgeContract.OverflowX == OverflowBehavior.Clip
            || expected.EdgeContract.OverflowY == OverflowBehavior.Clip;

        var comparison = new MeasuredLayoutComparison
        {
            LocalPath = localPath,
            ExpectedStableId = expected.StableId,
            ExpectedSourceId = expected.SourceId,
            ActualName = actual.Name,
            ActualNodeType = actual.NodeType,
            ExpectedWidthSizing = expected.EdgeContract.WidthSizing,
            ExpectedHeightSizing = expected.EdgeContract.HeightSizing,
            ExpectedParticipation = expected.EdgeContract.Participation,
            ActualX = actual.X,
            ActualY = actual.Y,
            ActualWidth = actual.Width,
            ActualHeight = actual.Height,
            ExpectedStartInsetX = expectedStartInsetX,
            ExpectedStartInsetY = expectedStartInsetY,
            ExpectedAvailableWidth = expectedAvailableWidth,
            ActualPreferredWidth = actual.PreferredWidth,
            ActualPreferredHeight = actual.PreferredHeight,
            ExpectedFontSize = expectedFontSize,
            ActualFontSize = actual.FontSize,
            ExpectedWrapText = expectedWrapText,
            ActualWrapText = actual.WrapText,
            ExpectedClipContent = expectedClipContent,
            ActualClipContent = actual.ClipContent,
            ExpectedChildCount = expected.Children.Count,
            ActualChildCount = actual.Children.Count
        };

        comparisons.Add(comparison);
        issues.AddRange(BuildMeasuredIssues(comparison));

        if (expected.Children.Count != actual.Children.Count)
        {
            issues.Add(new MeasuredLayoutIssue
            {
                Category = "child-structure-mismatch",
                Severity = "warning",
                LocalPath = localPath,
                Summary = $"Expected {expected.Children.Count} child nodes but realized {actual.Children.Count}.",
                SuggestedAction = "Inspect synthesis decomposition or backend child mounting for this subtree before tuning smaller metrics."
            });
        }

        var sharedChildCount = Math.Min(expected.Children.Count, actual.Children.Count);
        for (var index = 0; index < sharedChildCount; index++)
        {
            CompareLayoutNode(
                expected.Children[index],
                actual.Children[index],
                expected,
                actual,
                $"{localPath}/{index}",
                comparisons,
                issues,
                childIndex: index);
        }
    }

    private static IEnumerable<MeasuredLayoutIssue> BuildMeasuredIssues(MeasuredLayoutComparison comparison)
    {
        const double insetTolerance = 6;
        const double widthTolerance = 8;
        const double fontSizeTolerance = 1;

        if (comparison.ExpectedStartInsetX.HasValue && comparison.ActualX + insetTolerance < comparison.ExpectedStartInsetX.Value)
        {
            yield return new MeasuredLayoutIssue
            {
                Category = "start-edge-underflow",
                Severity = "warning",
                LocalPath = comparison.LocalPath,
                Summary = $"Left inset realized at {comparison.ActualX:F1}px but the Visual IR contract expects about {comparison.ExpectedStartInsetX.Value:F1}px.",
                SuggestedAction = "Inspect parent padding, child margin, and start-edge participation before changing gap or text metrics."
            };
        }

        if (comparison.ExpectedStartInsetX.HasValue && comparison.ActualX > comparison.ExpectedStartInsetX.Value + insetTolerance)
        {
            yield return new MeasuredLayoutIssue
            {
                Category = "start-edge-overshift",
                Severity = "info",
                LocalPath = comparison.LocalPath,
                Summary = $"Left inset realized at {comparison.ActualX:F1}px, overshooting the expected start inset of {comparison.ExpectedStartInsetX.Value:F1}px.",
                SuggestedAction = "Inspect absolute offset retention and parent padding accumulation for this node."
            };
        }

        if (comparison.ExpectedWidthSizing == AxisSizing.Fill
            && comparison.ExpectedAvailableWidth.HasValue
            && comparison.ExpectedAvailableWidth.Value > 0
            && comparison.ActualWidth < comparison.ExpectedAvailableWidth.Value - widthTolerance)
        {
            yield return new MeasuredLayoutIssue
            {
                Category = "fill-underflow",
                Severity = "warning",
                LocalPath = comparison.LocalPath,
                Summary = $"Node is expected to fill about {comparison.ExpectedAvailableWidth.Value:F1}px but only realized {comparison.ActualWidth:F1}px.",
                SuggestedAction = "Inspect fill/stretch realization, layout-group child control flags, and content-hug conflicts on the parent."
            };
        }

        if (comparison.ExpectedWidthSizing == AxisSizing.Hug
            && comparison.ExpectedAvailableWidth.HasValue
            && comparison.ActualPreferredWidth > 0
            && comparison.ActualWidth >= comparison.ExpectedAvailableWidth.Value - widthTolerance
            && comparison.ActualWidth > comparison.ActualPreferredWidth + widthTolerance)
        {
            yield return new MeasuredLayoutIssue
            {
                Category = "hug-stretched-to-fill",
                Severity = "warning",
                LocalPath = comparison.LocalPath,
                Summary = $"Node is expected to hug content but realized {comparison.ActualWidth:F1}px against an available width of {comparison.ExpectedAvailableWidth.Value:F1}px.",
                SuggestedAction = "Inspect child-control-width, flexible width, and content-size-fitter interaction for this subtree."
            };
        }

        if (!comparison.ExpectedWrapText
            && comparison.ActualPreferredWidth > 0
            && comparison.ActualWidth + widthTolerance < comparison.ActualPreferredWidth)
        {
            yield return new MeasuredLayoutIssue
            {
                Category = "wrap-pressure-risk",
                Severity = "info",
                LocalPath = comparison.LocalPath,
                Summary = $"Preferred text width is {comparison.ActualPreferredWidth:F1}px but realized width is {comparison.ActualWidth:F1}px, so the node is at risk of wrapping or compression.",
                SuggestedAction = "Treat this as a layout-width issue first, then tune font size or line height only if width realization is already correct."
            };
        }

        if (comparison.ExpectedFontSize.HasValue
            && comparison.ActualFontSize > 0
            && Math.Abs(comparison.ExpectedFontSize.Value - comparison.ActualFontSize) >= fontSizeTolerance)
        {
            yield return new MeasuredLayoutIssue
            {
                Category = "font-size-drift",
                Severity = "info",
                LocalPath = comparison.LocalPath,
                Summary = $"Expected font size is {comparison.ExpectedFontSize.Value:F1}px but realized font size is {comparison.ActualFontSize:F1}px.",
                SuggestedAction = "Inspect metric-profile selection for this semantic class before adjusting local layout heuristics."
            };
        }

        if (comparison.ExpectedClipContent != comparison.ActualClipContent)
        {
            yield return new MeasuredLayoutIssue
            {
                Category = "clip-mismatch",
                Severity = "info",
                LocalPath = comparison.LocalPath,
                Summary = comparison.ExpectedClipContent
                    ? "The Visual IR expects clipping, but the realized node is not clipping content."
                    : "The realized node is clipping content even though the Visual IR contract is visible overflow.",
                SuggestedAction = "Inspect overflow or mask emission for this subtree before changing content metrics."
            };
        }
    }

    private static double? ResolveExpectedStartInsetX(VisualNode node, VisualNode? parent, ActualLayoutNode? actualParent, int? childIndex)
    {
        if (node.EdgeContract.Participation == LayoutParticipation.Overlay)
        {
            return ResolvePixelDimension(node.Box.Left) ?? 0;
        }

        if (parent == null)
        {
            return 0;
        }

        var inset = (parent.Box.Padding?.Left ?? 0) + (node.Box.Margin?.Left ?? 0);
        if (childIndex.HasValue
            && parent.Box.LayoutType == LayoutType.Horizontal
            && actualParent != null
            && actualParent.Children.Count == parent.Children.Count)
        {
            inset += ResolveHorizontalGap(parent) * childIndex.Value;
            for (var index = 0; index < childIndex.Value; index++)
            {
                inset += actualParent.Children[index].Width;
                inset += (parent.Children[index].Box.Margin?.Left ?? 0) + (parent.Children[index].Box.Margin?.Right ?? 0);
            }
        }

        return inset;
    }

    private static double? ResolveExpectedStartInsetY(VisualNode node, VisualNode? parent, ActualLayoutNode? actualParent, int? childIndex)
    {
        if (node.EdgeContract.Participation == LayoutParticipation.Overlay)
        {
            return ResolvePixelDimension(node.Box.Top) ?? 0;
        }

        if (parent == null)
        {
            return 0;
        }

        var inset = (parent.Box.Padding?.Top ?? 0) + (node.Box.Margin?.Top ?? 0);
        if (childIndex.HasValue
            && parent.Box.LayoutType == LayoutType.Vertical
            && actualParent != null
            && actualParent.Children.Count == parent.Children.Count)
        {
            inset += ResolveVerticalGap(parent) * childIndex.Value;
            for (var index = 0; index < childIndex.Value; index++)
            {
                inset += actualParent.Children[index].Height;
                inset += (parent.Children[index].Box.Margin?.Top ?? 0) + (parent.Children[index].Box.Margin?.Bottom ?? 0);
            }
        }

        return inset;
    }

    private static double? ResolveExpectedAvailableWidth(
        VisualNode node,
        VisualNode? parent,
        ActualLayoutNode? actualParent,
        int? childIndex)
    {
        if (parent == null || actualParent == null)
        {
            return null;
        }

        var availableWidth = actualParent.Width
            - (parent.Box.Padding?.Left ?? 0)
            - (parent.Box.Padding?.Right ?? 0)
            - (node.Box.Margin?.Left ?? 0)
            - (node.Box.Margin?.Right ?? 0);

        if (childIndex.HasValue
            && parent.Box.LayoutType == LayoutType.Horizontal
            && childIndex.Value >= 0
            && childIndex.Value < parent.Children.Count
            && actualParent.Children.Count == parent.Children.Count)
        {
            availableWidth -= ResolveHorizontalGap(parent) * Math.Max(0, actualParent.Children.Count - 1);

            for (var index = 0; index < parent.Children.Count; index++)
            {
                if (index == childIndex.Value)
                {
                    continue;
                }

                var sibling = parent.Children[index];
                var siblingActual = actualParent.Children[index];
                if (sibling.EdgeContract.WidthSizing == AxisSizing.Fill)
                {
                    continue;
                }

                availableWidth -= siblingActual.Width;
                availableWidth -= (sibling.Box.Margin?.Left ?? 0) + (sibling.Box.Margin?.Right ?? 0);
            }
        }

        return availableWidth > 0 ? availableWidth : null;
    }

    private static double ResolveHorizontalGap(VisualNode node)
    {
        if (node.Box.Gap == null)
        {
            return 0;
        }

        var gap = node.Box.Gap.Value;
        if (gap.Left > 0 && gap.Right > 0)
        {
            return (gap.Left + gap.Right) / 2d;
        }

        return gap.Left > 0 ? gap.Left : gap.Right;
    }

    private static double ResolveVerticalGap(VisualNode node)
    {
        if (node.Box.Gap == null)
        {
            return 0;
        }

        var gap = node.Box.Gap.Value;
        if (gap.Top > 0 && gap.Bottom > 0)
        {
            return (gap.Top + gap.Bottom) / 2d;
        }

        return gap.Top > 0 ? gap.Top : gap.Bottom;
    }

    private static double? ResolvePixelDimension(Dimension? dimension)
    {
        if (dimension == null)
        {
            return null;
        }

        return dimension.Value.Unit switch
        {
            DimensionUnit.Pixels => dimension.Value.Value,
            DimensionUnit.Cells => dimension.Value.Value,
            _ => null
        };
    }

    private static string BuildRegionId(ImageSimilarityRecursiveScoreNode node)
    {
        if (string.Equals(node.Level, "screen/frame", StringComparison.Ordinal))
        {
            return "root";
        }

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{node.Level}@{node.Bounds.X},{node.Bounds.Y},{node.Bounds.Width}x{node.Bounds.Height}");
    }

    private static ImageSimilarityRecursiveScoreNode BuildRecursiveNode(
        Image<Rgba32> baseline,
        Image<Rgba32> current,
        Rectangle bounds,
        int tolerance,
        int depth)
    {
        var metrics = AnalyzeRegion(baseline, current, bounds, tolerance);
        var overallSimilarityPercent = ComputeOverallSimilarityPercent(new DiffMetrics
        {
            BaselineWidth = bounds.Width,
            BaselineHeight = bounds.Height,
            CurrentWidth = bounds.Width,
            CurrentHeight = bounds.Height,
            TotalPixels = metrics.TotalPixels,
            ChangedPixels = metrics.ChangedPixels,
            ChangedPercent = metrics.ChangedPercent,
            MeanDelta = metrics.MeanDelta,
            MaxDelta = metrics.MaxDelta
        });

        var phases = new List<ImageSimilarityPhaseScore>
        {
            new() { Phase = "structural-match", SimilarityPercent = Math.Round(Math.Clamp(100.0 - metrics.ChangedPercent, 0.0, 100.0), 2) },
            new() { Phase = "outer-frame-match", SimilarityPercent = Math.Round(Math.Clamp(100.0 - metrics.AverageEdgeChangedPercent, 0.0, 100.0), 2) },
            new() { Phase = "inner-layout-match", SimilarityPercent = Math.Round(Math.Clamp(100.0 - ((metrics.CenterChangedPercent * 0.7) + (Math.Abs(metrics.OpaqueCoverageDeltaPercent) * 0.3)), 0.0, 100.0), 2) },
            new() { Phase = "text-icon-metrics", SimilarityPercent = Math.Round(Math.Clamp((ComputeDeltaSimilarityPercent(metrics.MeanDelta) * 0.65) + ((100.0 - Math.Abs(metrics.CenterChangedPercent - metrics.AverageEdgeChangedPercent)) * 0.35), 0.0, 100.0), 2) },
            new() { Phase = "polish-offsets", SimilarityPercent = Math.Round(Math.Clamp(((100.0 - metrics.ChangedPercent) * 0.4) + (ComputeDeltaSimilarityPercent(metrics.MeanDelta) * 0.6), 0.0, 100.0), 2) }
        };

        var children = new List<ImageSimilarityRecursiveScoreNode>();
        if (depth < 3 && bounds.Width >= 16 && bounds.Height >= 16)
        {
            foreach (var childBounds in SplitQuadrants(bounds))
            {
                if (childBounds.Width <= 0 || childBounds.Height <= 0)
                {
                    continue;
                }

                children.Add(BuildRecursiveNode(baseline, current, childBounds, tolerance, depth + 1));
            }
        }

        return new ImageSimilarityRecursiveScoreNode
        {
            Level = ResolveLevelName(depth),
            Bounds = new ImageSimilarityBounds
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height
            },
            OverallSimilarityPercent = overallSimilarityPercent,
            Phases = phases,
            Children = children
        };
    }

    private static RegionAnalysis AnalyzeRegion(Image<Rgba32> baseline, Image<Rgba32> current, Rectangle bounds, int tolerance)
    {
        const int alphaThreshold = 16;

        long changedPixels = 0;
        long totalPixels = Math.Max(1, bounds.Width * bounds.Height);
        long deltaSum = 0;
        var maxDelta = 0;
        long baselineOpaque = 0;
        long currentOpaque = 0;
        long edgeChanged = 0;
        long edgeTotal = 0;
        long centerChanged = 0;
        long centerTotal = 0;

        var edgeBandWidth = Math.Max(1, (int)Math.Round(bounds.Width * 0.2));
        var edgeBandHeight = Math.Max(1, (int)Math.Round(bounds.Height * 0.2));
        var centerStartX = bounds.X + (bounds.Width / 3);
        var centerEndX = bounds.X + bounds.Width - (bounds.Width / 3);
        var centerStartY = bounds.Y + (bounds.Height / 3);
        var centerEndY = bounds.Y + bounds.Height - (bounds.Height / 3);

        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                var baselinePixel = (x < baseline.Width && y < baseline.Height)
                    ? baseline[x, y]
                    : new Rgba32(0, 0, 0, 0);

                var currentPixel = (x < current.Width && y < current.Height)
                    ? current[x, y]
                    : new Rgba32(0, 0, 0, 0);

                if (baselinePixel.A > alphaThreshold)
                {
                    baselineOpaque++;
                }

                if (currentPixel.A > alphaThreshold)
                {
                    currentOpaque++;
                }

                var dr = Math.Abs(currentPixel.R - baselinePixel.R);
                var dg = Math.Abs(currentPixel.G - baselinePixel.G);
                var db = Math.Abs(currentPixel.B - baselinePixel.B);
                var da = Math.Abs(currentPixel.A - baselinePixel.A);
                var delta = Math.Max(Math.Max(dr, dg), Math.Max(db, da));
                var changed = delta > tolerance;
                deltaSum += delta;
                maxDelta = Math.Max(maxDelta, delta);

                if (changed)
                {
                    changedPixels++;
                }

                var isEdge =
                    x < bounds.X + edgeBandWidth
                    || x >= bounds.Right - edgeBandWidth
                    || y < bounds.Y + edgeBandHeight
                    || y >= bounds.Bottom - edgeBandHeight;

                if (isEdge)
                {
                    edgeTotal++;
                    if (changed)
                    {
                        edgeChanged++;
                    }
                }

                var isCenter = x >= centerStartX && x < centerEndX && y >= centerStartY && y < centerEndY;
                if (isCenter)
                {
                    centerTotal++;
                    if (changed)
                    {
                        centerChanged++;
                    }
                }
            }
        }

        var changedPercent = Percent(changedPixels, totalPixels);
        var meanDelta = totalPixels == 0 ? 0 : Math.Round((double)deltaSum / totalPixels, 4);
        var baselineOpaquePercent = Percent(baselineOpaque, totalPixels);
        var currentOpaquePercent = Percent(currentOpaque, totalPixels);

        return new RegionAnalysis(
            TotalPixels: (int)totalPixels,
            ChangedPixels: (int)changedPixels,
            ChangedPercent: changedPercent,
            MeanDelta: meanDelta,
            MaxDelta: maxDelta,
            AverageEdgeChangedPercent: Percent(edgeChanged, edgeTotal),
            CenterChangedPercent: Percent(centerChanged, centerTotal),
            OpaqueCoverageDeltaPercent: Math.Round(currentOpaquePercent - baselineOpaquePercent, 4));
    }

    private static IReadOnlyList<Rectangle> SplitQuadrants(Rectangle bounds)
    {
        var leftWidth = bounds.Width / 2;
        var rightWidth = bounds.Width - leftWidth;
        var topHeight = bounds.Height / 2;
        var bottomHeight = bounds.Height - topHeight;

        return
        [
            new Rectangle(bounds.X, bounds.Y, leftWidth, topHeight),
            new Rectangle(bounds.X + leftWidth, bounds.Y, rightWidth, topHeight),
            new Rectangle(bounds.X, bounds.Y + topHeight, leftWidth, bottomHeight),
            new Rectangle(bounds.X + leftWidth, bounds.Y + topHeight, rightWidth, bottomHeight)
        ];
    }

    private static string ResolveLevelName(int depth)
        => depth switch
        {
            0 => "screen/frame",
            1 => "panel",
            2 => "card/cluster",
            _ => "atomic-motif"
        };

    private static double Percent(long numerator, long denominator)
    {
        if (denominator <= 0)
        {
            return 0;
        }

        return Math.Round((double)numerator / denominator * 100.0, 4);
    }

    private static string MaxLabel(params (string Label, double Value)[] values)
    {
        return values.OrderByDescending(item => item.Value).First().Label;
    }

    private static double GetDominantBandPercent(ImageSimilaritySpatialAnalysis analysis)
    {
        return analysis.DominantBand switch
        {
            "left-edge" => analysis.LeftEdgeChangedPercent,
            "right-edge" => analysis.RightEdgeChangedPercent,
            "top-edge" => analysis.TopEdgeChangedPercent,
            "bottom-edge" => analysis.BottomEdgeChangedPercent,
            "center-band" => analysis.CenterBandChangedPercent,
            _ => 0
        };
    }

    private sealed record RegionAnalysis(
        int TotalPixels,
        int ChangedPixels,
        double ChangedPercent,
        double MeanDelta,
        int MaxDelta,
        double AverageEdgeChangedPercent,
        double CenterChangedPercent,
        double OpaqueCoverageDeltaPercent);
}
