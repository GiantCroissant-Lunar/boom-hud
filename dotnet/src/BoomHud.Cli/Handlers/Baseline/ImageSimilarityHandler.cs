using System.Text.Json;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Snapshots;
using BoomHud.Cli.Handlers.Pencil;
using BoomHud.Gen.Pencil;
using BoomHud.Generators.VisualIR;
using SixLabors.ImageSharp;
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
}
