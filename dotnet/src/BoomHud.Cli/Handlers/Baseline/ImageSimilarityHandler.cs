using System.Text.Json;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Snapshots;
using BoomHud.Cli.Handlers.Pencil;
using BoomHud.Gen.Pencil;
using BoomHud.Generators.VisualIR;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BoomHud.Cli.Handlers.Baseline;

/// <summary>
/// Measures similarity for one reference/candidate image pair.
/// </summary>
public static partial class ImageSimilarityHandler
{
    private const string NormalizeOff = "off";
    private const string NormalizeStretch = "stretch";
    private const string NormalizeCover = "cover";

    private sealed record VisualRefinementEmissionResult
    {
        public required string OutputPath { get; init; }

        public required string BackendFamily { get; init; }

        public required string DocumentName { get; init; }

        public string? PencilPatchPlanPath { get; init; }

        public string? PencilPatchScriptPath { get; init; }

        public string? PencilBatchOpsPath { get; init; }
    }

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

        if (options.PencilSourceFile != null && !options.PencilSourceFile.Exists)
        {
            Console.Error.WriteLine($"Error: Pencil source file not found: {options.PencilSourceFile.FullName}");
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

        if (options.AutoApplyPencilPatch && options.VisualIrFile == null)
        {
            Console.Error.WriteLine("Error: --auto-apply-pencil-patch requires --visual-ir.");
            return 1;
        }

        if (options.AutoApplyPencilPatch && options.PencilSourceFile == null)
        {
            Console.Error.WriteLine("Error: --auto-apply-pencil-patch requires --pencil-source.");
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

                MeasuredLayoutReport? measuredLayout = null;
                VisualRefinementEmissionResult? visualRefinement = null;
                if (options.VisualIrFile != null)
                {
                    if (options.ActualLayoutFile != null)
                    {
                        measuredLayout = EmitMeasuredLayoutArtifact(options.VisualIrFile, options.ActualLayoutFile, options.MeasuredLayoutOutFile);
                    }

                    visualRefinement = EmitVisualRefinementArtifact(
                        options.VisualIrFile,
                        options.VisualRefinementOutFile,
                        report,
                        options.VisualRefinementIterationBudget,
                        measuredLayout);
                }

                if (options.AutoApplyPencilPatch)
                {
                    AutoApplyPencilPatch(options, visualRefinement);
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

        baseline.ProcessPixelRows(current, (baselineAccessor, currentAccessor) =>
        {
            for (var y = 0; y < height; y++)
            {
                var baselineRow = (y < baseline.Height) ? baselineAccessor.GetRowSpan(y) : default;
                var currentRow = (y < current.Height) ? currentAccessor.GetRowSpan(y) : default;

                for (var x = 0; x < width; x++)
                {
                    var baselinePixel = (x < baseline.Width && y < baseline.Height)
                        ? baselineRow[x]
                        : new Rgba32(0, 0, 0, 0);

                    var currentPixel = (x < current.Width && y < current.Height)
                        ? currentRow[x]
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
        });

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

    private static VisualRefinementEmissionResult EmitVisualRefinementArtifact(
        FileInfo visualIrFile,
        FileInfo? requestedOutput,
        ImageSimilarityReport report,
        int iterationBudget,
        MeasuredLayoutReport? measuredLayout)
    {
        var visualDocument = JsonSerializer.Deserialize<VisualDocument>(File.ReadAllText(visualIrFile.FullName));
        if (visualDocument == null)
        {
            throw new InvalidOperationException($"Failed to deserialize Visual IR artifact '{visualIrFile.FullName}'.");
        }

        var summary = VisualRefinementPlanner.Plan(
            visualDocument,
            ConvertRecursiveAnalysis(report.RecursiveAnalysis),
            iterationBudget,
            measuredLayout?.Issues.Select(static issue => new VisualMeasuredIssue
            {
                Category = issue.Category,
                Severity = issue.Severity,
                LocalPath = issue.LocalPath,
                ExpectedSemanticClass = issue.ExpectedSemanticClass,
                ExpectedSourceSemanticRole = issue.ExpectedSourceSemanticRole,
                ExpectedSourceAssetRealization = issue.ExpectedSourceAssetRealization,
                Summary = issue.Summary,
                SuggestedAction = issue.SuggestedAction
            }).ToList());

        var outputPath = requestedOutput?.FullName ?? ResolveDefaultVisualRefinementPath(visualIrFile, visualDocument);
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputPath, VisualRefinementPlanner.ToJson(summary));

        string? patchPlanPath = null;
        string? patchScriptPath = null;
        string? batchOpsPath = null;

        if (string.Equals(visualDocument.BackendFamily, "pencil", StringComparison.OrdinalIgnoreCase)
            && PencilPatchPlanBuilder.Build(visualDocument, summary) is { } patchPlan)
        {
            patchPlanPath = ResolveDefaultPencilPatchPlanPath(outputPath, visualDocument);
            File.WriteAllText(
                patchPlanPath,
                PencilPatchPlanBuilder.ToJson(patchPlan));

            if (PencilPatchScriptBuilder.Build(patchPlan) is { } patchScript)
            {
                patchScriptPath = ResolveDefaultPencilPatchScriptPath(outputPath, visualDocument);
                File.WriteAllText(
                    patchScriptPath,
                    patchScript);
            }

            if (PencilBatchOpsBuilder.Build(patchPlan) is { } batchOps)
            {
                batchOpsPath = ResolveDefaultPencilBatchOpsPath(outputPath, visualDocument);
                File.WriteAllText(
                    batchOpsPath,
                    batchOps);
            }
        }

        return new VisualRefinementEmissionResult
        {
            OutputPath = outputPath,
            BackendFamily = visualDocument.BackendFamily,
            DocumentName = visualDocument.DocumentName,
            PencilPatchPlanPath = patchPlanPath,
            PencilPatchScriptPath = patchScriptPath,
            PencilBatchOpsPath = batchOpsPath
        };
    }

    private static MeasuredLayoutReport EmitMeasuredLayoutArtifact(
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
        return report;
    }

    private static string ResolveDefaultVisualRefinementPath(FileInfo visualIrFile, VisualDocument visualDocument)
        => Path.Combine(
            visualIrFile.DirectoryName ?? Environment.CurrentDirectory,
            $"{visualDocument.DocumentName}.visual-refinement.json");

    private static string ResolveDefaultMeasuredLayoutPath(FileInfo actualLayoutFile, VisualDocument visualDocument)
        => Path.Combine(
            actualLayoutFile.DirectoryName ?? Environment.CurrentDirectory,
            $"{visualDocument.DocumentName}.measured-layout.json");

    private static string ResolveDefaultPencilPatchPlanPath(string refinementPath, VisualDocument visualDocument)
        => Path.Combine(
            Path.GetDirectoryName(refinementPath) ?? Environment.CurrentDirectory,
            $"{visualDocument.DocumentName}.pen-patch-plan.json");

    private static string ResolveDefaultPencilPatchScriptPath(string refinementPath, VisualDocument visualDocument)
        => Path.Combine(
            Path.GetDirectoryName(refinementPath) ?? Environment.CurrentDirectory,
            $"{visualDocument.DocumentName}.pen-patch-script.txt");

    private static string ResolveDefaultPencilBatchOpsPath(string refinementPath, VisualDocument visualDocument)
        => Path.Combine(
            Path.GetDirectoryName(refinementPath) ?? Environment.CurrentDirectory,
            $"{visualDocument.DocumentName}.pen-batch-ops.txt");

    private static void AutoApplyPencilPatch(ImageSimilarityOptions options, VisualRefinementEmissionResult? visualRefinement)
    {
        if (visualRefinement == null)
        {
            throw new InvalidOperationException("Visual refinement emission did not run, so there is no Pencil patch to apply.");
        }

        if (!string.Equals(visualRefinement.BackendFamily, "pencil", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Auto-apply requires a Pencil Visual IR backend, but '{visualRefinement.DocumentName}' uses '{visualRefinement.BackendFamily}'.");
        }

        if (string.IsNullOrWhiteSpace(visualRefinement.PencilBatchOpsPath) || !File.Exists(visualRefinement.PencilBatchOpsPath))
        {
            if (options.PrintSummary || options.Verbose)
            {
                Console.WriteLine("Pencil auto-apply: no deterministic batch ops were emitted. No patched .pen file written.");
            }

            return;
        }

        var outputPath = options.PatchedPenOutFile?.FullName ?? ResolveDefaultPatchedPenOutput(options.PencilSourceFile!.FullName);
        var exitCode = PenPatchApplyHandler.Execute(new PenPatchApplyOptions
        {
            PenFile = options.PencilSourceFile,
            BatchOpsFile = new FileInfo(visualRefinement.PencilBatchOpsPath),
            OutFile = new FileInfo(outputPath),
            PrintSummary = false
        });

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to auto-apply deterministic Pencil patch ops to '{options.PencilSourceFile!.FullName}'.");
        }

        if (options.PrintSummary || options.Verbose)
        {
            Console.WriteLine($"Pencil auto-apply: wrote patched pen to {outputPath}");
        }
    }

    private static string ResolveDefaultPatchedPenOutput(string penFilePath)
    {
        var directory = Path.GetDirectoryName(penFilePath) ?? Environment.CurrentDirectory;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(penFilePath);
        var extension = Path.GetExtension(penFilePath);
        return Path.Combine(directory, fileNameWithoutExtension + ".patched" + extension);
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
            var baselineRow = (y < baseline.Height) ? baseline.DangerousGetPixelRowMemory(y).Span : default;
            var currentRow = (y < current.Height) ? current.DangerousGetPixelRowMemory(y).Span : default;

            for (var x = bounds.X; x < bounds.Right; x++)
            {
                var baselinePixel = (x < baseline.Width && y < baseline.Height)
                    ? baselineRow[x]
                    : new Rgba32(0, 0, 0, 0);

                var currentPixel = (x < current.Width && y < current.Height)
                    ? currentRow[x]
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
