using System.Text.Json;
using BoomHud.Abstractions.Snapshots;
using BoomHud.Cli.Handlers.Baseline;

namespace BoomHud.Cli.Handlers.Rules;

public sealed record FidelityFrontierOptimizerState
{
    public string OptimizerMode { get; init; } = "frontier";

    public int BeamWidth { get; init; } = 5;

    public int SearchDepth { get; init; } = 3;

    public int ExpansionBudget { get; init; } = 6;

    public string PrimarySurfaceId { get; init; } = "the-alters-crafting-ugui";

    public string? BaselineCandidateId { get; init; }

    public IReadOnlyList<FidelityFrontierCandidateState> Candidates { get; init; } = [];
}

public sealed record FidelityFrontierCandidateState
{
    public required string CandidateId { get; init; }

    public required string Label { get; init; }

    public required string SummaryPath { get; init; }

    public string? ParentCandidateId { get; init; }

    public int Depth { get; init; }

    public IReadOnlyList<string> AppliedActions { get; init; } = [];
}

public sealed record FidelityFrontierOptimizerSummary
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public required string OptimizerMode { get; init; }

    public required int BeamWidth { get; init; }

    public required int SearchDepth { get; init; }

    public required int ExpansionBudget { get; init; }

    public required string PrimarySurfaceId { get; init; }

    public required string BaselineCandidateId { get; init; }

    public required FidelityCandidateVector BaselineVector { get; init; }

    public IReadOnlyList<FidelityFrontierDepthSummary> Depths { get; init; } = [];

    public IReadOnlyList<FidelityFrontierCandidateEvaluation> Candidates { get; init; } = [];

    public required string SelectedCandidateId { get; init; }

    public required bool SelectedCandidateIsBaseline { get; init; }

    public FidelityFrontierCandidateEvaluation? SelectedCandidate { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

public sealed record FidelityFrontierDepthSummary
{
    public required int Depth { get; init; }

    public IReadOnlyList<string> RetainedCandidateIds { get; init; } = [];
}

public sealed record FidelityFrontierCandidateEvaluation
{
    public required string CandidateId { get; init; }

    public required string Label { get; init; }

    public required string SummaryPath { get; init; }

    public string? ParentCandidateId { get; init; }

    public required int Depth { get; init; }

    public IReadOnlyList<string> AppliedActions { get; init; } = [];

    public required FidelityCandidateVector Vector { get; init; }

    public required FidelityMeasuredLayoutPenaltyBreakdown MeasuredLayoutPenalty { get; init; }

    public required FidelityWorstRegionPenaltyBreakdown WorstRegionPenalty { get; init; }

    public required FidelityDominantBandPenaltyBreakdown DominantBandPenalty { get; init; }

    public required FidelityGuardResult GuardResult { get; init; }

    public bool RetainedAtDepth { get; init; }

    public int Rank { get; init; }

    public bool Selected { get; init; }

    public IReadOnlyList<string> RejectionReasons { get; init; } = [];
}

public sealed record FidelityCandidateVector
{
    public required double AverageDenseUguiOverallSimilarity { get; init; }

    public required double PrimaryDenseUguiSimilarity { get; init; }

    public required double AverageDenseAllBackendOverallSimilarity { get; init; }

    public required double WeightedMeasuredLayoutIssuePenalty { get; init; }

    public required double WorstRegionRecursiveScorePenalty { get; init; }

    public required double DominantBandMismatchPenalty { get; init; }
}

public sealed record FidelityMeasuredLayoutPenaltyBreakdown
{
    public int ErrorCount { get; init; }

    public int WarningCount { get; init; }

    public int InfoCount { get; init; }

    public double TotalPenalty { get; init; }

    public double ShellPenalty { get; init; }
}

public sealed record FidelityWorstRegionPenaltyBreakdown
{
    public string? SurfaceId { get; init; }

    public string? RegionLevel { get; init; }

    public double LowestRegionOverallSimilarity { get; init; }

    public string? LowestPhaseName { get; init; }

    public double LowestPhaseSimilarity { get; init; }

    public double TotalPenalty { get; init; }
}

public sealed record FidelityDominantBandPenaltyBreakdown
{
    public int PersistedMismatchCount { get; init; }

    public double AveragePenalty { get; init; }

    public IReadOnlyList<FidelityDominantBandSurfacePenalty> Surfaces { get; init; } = [];
}

public sealed record FidelityDominantBandSurfacePenalty
{
    public required string SurfaceId { get; init; }

    public required string DominantBand { get; init; }

    public required double Penalty { get; init; }
}

public sealed record FidelityGuardResult
{
    public bool Passed { get; init; }

    public bool UguiRegressionGuardPassed { get; init; }

    public bool UitkRegressionGuardPassed { get; init; }

    public bool MeasuredLayoutGuardPassed { get; init; }

    public bool ShellGuardPassed { get; init; }

    public IReadOnlyList<string> FailureReasons { get; init; } = [];
}

internal sealed record FidelityRunSummaryArtifact
{
    public string? Label { get; init; }

    public double AverageOverallSimilarityPercent { get; init; }

    public IReadOnlyList<FidelityRunSurfaceArtifact> Surfaces { get; init; } = [];
}

internal sealed record FidelityRunSurfaceArtifact
{
    public string Id { get; init; } = string.Empty;

    public string ReportPath { get; init; } = string.Empty;

    public string? MeasuredLayoutPath { get; init; }
}

internal sealed record FidelitySurfaceEvaluation(
    string SurfaceId,
    ImageSimilarityReport Report,
    MeasuredLayoutReport? MeasuredLayout);

public static class FidelityFrontierOptimizer
{
    private const double UguiRegressionTolerance = 0.50d;
    private const double UitkRegressionTolerance = 0.25d;
    private const double MeasuredLayoutPenaltyGrowthFactor = 1.10d;
    private const double ShellPenaltyGrowthFactor = 1.10d;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> ShellCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "height-collapsed-vs-preferred",
        "width-stretched-vs-preferred",
        "cross-axis-stretch-mismatch",
        "shell-padding-or-child-stack-mismatch",
        "portrait-or-status-row-shell-drift",
        "start-edge-underflow",
        "start-edge-overshift",
        "fill-underflow",
        "hug-stretched-to-fill",
        "wrap-pressure-risk",
        "clip-mismatch"
    };

    public static FidelityFrontierOptimizerSummary Optimize(FidelityFrontierOptimizerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Candidates.Count == 0)
        {
            throw new InvalidOperationException("Optimizer state must contain at least one candidate.");
        }

        var evaluations = state.Candidates
            .Select(candidate => EvaluateCandidate(candidate, state.PrimarySurfaceId))
            .OrderBy(candidate => candidate.Depth)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToList();

        var baselineCandidate = ResolveBaselineCandidate(state, evaluations);
        var baseline = evaluations.First(candidate => string.Equals(candidate.CandidateId, baselineCandidate, StringComparison.Ordinal));

        var baselineSummary = LoadSummary(baseline.SummaryPath);
        var baselineSurfaceEvaluations = baselineSummary.Surfaces
            .Where(surface => !string.IsNullOrWhiteSpace(surface.ReportPath))
            .Select(LoadSurfaceEvaluation)
            .ToList();

        evaluations = evaluations
            .Select(candidate =>
            {
                var candidateSummary = LoadSummary(candidate.SummaryPath);
                var candidateSurfaces = candidateSummary.Surfaces
                    .Where(surface => !string.IsNullOrWhiteSpace(surface.ReportPath))
                    .Select(LoadSurfaceEvaluation)
                    .ToList();
                var dominantBandPenalty = string.Equals(candidate.CandidateId, baselineCandidate, StringComparison.Ordinal)
                    ? new FidelityDominantBandPenaltyBreakdown()
                    : ComputeDominantBandPenaltyBreakdown(baselineSurfaceEvaluations, candidateSurfaces);

                return candidate with
                {
                    Vector = candidate.Vector with
                    {
                        DominantBandMismatchPenalty = dominantBandPenalty.AveragePenalty
                    },
                    DominantBandPenalty = dominantBandPenalty
                };
            })
            .ToList();

        baseline = evaluations.First(candidate => string.Equals(candidate.CandidateId, baselineCandidate, StringComparison.Ordinal));

        var depths = evaluations
            .GroupBy(candidate => candidate.Depth)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var retained = PruneToFrontier(group.ToList(), state.BeamWidth)
                    .Select(candidate => candidate.CandidateId)
                    .ToHashSet(StringComparer.Ordinal);
                return new FidelityFrontierDepthSummary
                {
                    Depth = group.Key,
                    RetainedCandidateIds = group
                        .Where(candidate => retained.Contains(candidate.CandidateId))
                        .Select(candidate => candidate.CandidateId)
                        .ToList()
                };
            })
            .ToList();

        var retainedCandidateIds = depths
            .SelectMany(depth => depth.RetainedCandidateIds)
            .ToHashSet(StringComparer.Ordinal);

        var enriched = evaluations
            .Select(candidate =>
            {
                var guardResult = BuildGuardResult(candidate, baseline, state.PrimarySurfaceId);
                var rejectionReasons = new List<string>();
                if (!guardResult.Passed)
                {
                    rejectionReasons.AddRange(guardResult.FailureReasons);
                }

                return candidate with
                {
                    RetainedAtDepth = retainedCandidateIds.Contains(candidate.CandidateId),
                    GuardResult = guardResult,
                    RejectionReasons = rejectionReasons
                };
            })
            .ToList();

        var selected = enriched
            .Where(candidate => candidate.GuardResult.Passed)
            .OrderBy(candidate => candidate, FidelityEvaluationComparer.Instance)
            .FirstOrDefault()
            ?? baseline;

        enriched = enriched
            .Select(candidate => candidate with
            {
                Rank = 0
            })
            .ToList();

        var ranks = enriched
            .OrderBy(candidate => candidate, FidelityEvaluationComparer.Instance)
            .Select((candidate, index) => new { candidate.CandidateId, Rank = index + 1 })
            .ToDictionary(entry => entry.CandidateId, entry => entry.Rank, StringComparer.Ordinal);

        enriched = enriched
            .Select(candidate => candidate with
            {
                Rank = ranks[candidate.CandidateId],
                Selected = string.Equals(candidate.CandidateId, selected.CandidateId, StringComparison.Ordinal),
                RejectionReasons = candidate.RejectionReasons.Count > 0
                    ? candidate.RejectionReasons
                    : BuildSelectionRejectionReasons(candidate, selected)
            })
            .ToList();

        return new FidelityFrontierOptimizerSummary
        {
            OptimizerMode = state.OptimizerMode,
            BeamWidth = state.BeamWidth,
            SearchDepth = state.SearchDepth,
            ExpansionBudget = state.ExpansionBudget,
            PrimarySurfaceId = state.PrimarySurfaceId,
            BaselineCandidateId = baseline.CandidateId,
            BaselineVector = baseline.Vector,
            Depths = depths,
            Candidates = enriched,
            SelectedCandidateId = selected.CandidateId,
            SelectedCandidateIsBaseline = string.Equals(selected.CandidateId, baseline.CandidateId, StringComparison.Ordinal),
            SelectedCandidate = enriched.FirstOrDefault(candidate => string.Equals(candidate.CandidateId, selected.CandidateId, StringComparison.Ordinal))
        };
    }

    public static bool Dominates(FidelityCandidateVector left, FidelityCandidateVector right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var notWorse =
            left.AverageDenseUguiOverallSimilarity >= right.AverageDenseUguiOverallSimilarity &&
            left.PrimaryDenseUguiSimilarity >= right.PrimaryDenseUguiSimilarity &&
            left.AverageDenseAllBackendOverallSimilarity >= right.AverageDenseAllBackendOverallSimilarity &&
            left.WeightedMeasuredLayoutIssuePenalty <= right.WeightedMeasuredLayoutIssuePenalty &&
            left.WorstRegionRecursiveScorePenalty <= right.WorstRegionRecursiveScorePenalty &&
            left.DominantBandMismatchPenalty <= right.DominantBandMismatchPenalty;

        var strictlyBetter =
            left.AverageDenseUguiOverallSimilarity > right.AverageDenseUguiOverallSimilarity ||
            left.PrimaryDenseUguiSimilarity > right.PrimaryDenseUguiSimilarity ||
            left.AverageDenseAllBackendOverallSimilarity > right.AverageDenseAllBackendOverallSimilarity ||
            left.WeightedMeasuredLayoutIssuePenalty < right.WeightedMeasuredLayoutIssuePenalty ||
            left.WorstRegionRecursiveScorePenalty < right.WorstRegionRecursiveScorePenalty ||
            left.DominantBandMismatchPenalty < right.DominantBandMismatchPenalty;

        return notWorse && strictlyBetter;
    }

    public static double ComputeWeightedMeasuredLayoutPenalty(IEnumerable<MeasuredLayoutIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return Math.Round(issues.Sum(issue => issue.Severity switch
        {
            "error" => 5d,
            "warning" => 2d,
            "info" => 0.5d,
            _ => 0d
        }), 4);
    }

    public static double ComputeWorstRegionRecursiveScorePenalty(ImageSimilarityRecursiveScoreNode? recursiveAnalysis)
    {
        return ComputeWorstRegionPenaltyBreakdown(recursiveAnalysis).TotalPenalty;
    }

    public static double ComputeDominantBandMismatchPenalty(
        ImageSimilaritySpatialAnalysis? baselineAnalysis,
        ImageSimilaritySpatialAnalysis? candidateAnalysis)
    {
        var breakdown = ComputeDominantBandPenaltyBreakdown(
            baselineAnalysis == null ? [] : [CreateSyntheticSurfaceEvaluation("baseline", baselineAnalysis)],
            candidateAnalysis == null ? [] : [CreateSyntheticSurfaceEvaluation("baseline", candidateAnalysis)]);
        return breakdown.AveragePenalty;
    }

    private static FidelityFrontierCandidateEvaluation EvaluateCandidate(
        FidelityFrontierCandidateState candidate,
        string primarySurfaceId)
    {
        var summary = LoadSummary(candidate.SummaryPath);
        var surfaces = summary.Surfaces
            .Where(surface => !string.IsNullOrWhiteSpace(surface.ReportPath))
            .Select(LoadSurfaceEvaluation)
            .ToList();

        var avgUgui = AverageOverall(surfaces.Where(surface => surface.SurfaceId.EndsWith("-ugui", StringComparison.OrdinalIgnoreCase)));
        var primaryUgui = surfaces
            .FirstOrDefault(surface => string.Equals(surface.SurfaceId, primarySurfaceId, StringComparison.OrdinalIgnoreCase))
            ?.Report.OverallSimilarityPercent
            ?? avgUgui;
        var avgAll = AverageOverall(surfaces);

        var allIssues = surfaces
            .SelectMany(surface => surface.MeasuredLayout?.Issues ?? [])
            .ToList();
        var measuredPenalty = new FidelityMeasuredLayoutPenaltyBreakdown
        {
            ErrorCount = allIssues.Count(issue => string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase)),
            WarningCount = allIssues.Count(issue => string.Equals(issue.Severity, "warning", StringComparison.OrdinalIgnoreCase)),
            InfoCount = allIssues.Count(issue => string.Equals(issue.Severity, "info", StringComparison.OrdinalIgnoreCase)),
            TotalPenalty = ComputeWeightedMeasuredLayoutPenalty(allIssues),
            ShellPenalty = ComputeWeightedMeasuredLayoutPenalty(allIssues.Where(issue => ShellCategories.Contains(issue.Category)))
        };
        var worstRegionPenalty = ComputeWorstRegionPenaltyBreakdown(surfaces);

        return new FidelityFrontierCandidateEvaluation
        {
            CandidateId = candidate.CandidateId,
            Label = candidate.Label,
            SummaryPath = candidate.SummaryPath,
            ParentCandidateId = candidate.ParentCandidateId,
            Depth = candidate.Depth,
            AppliedActions = candidate.AppliedActions,
            Vector = new FidelityCandidateVector
            {
                AverageDenseUguiOverallSimilarity = avgUgui,
                PrimaryDenseUguiSimilarity = Math.Round(primaryUgui, 4),
                AverageDenseAllBackendOverallSimilarity = avgAll,
                WeightedMeasuredLayoutIssuePenalty = measuredPenalty.TotalPenalty,
                WorstRegionRecursiveScorePenalty = worstRegionPenalty.TotalPenalty,
                DominantBandMismatchPenalty = 0d
            },
            MeasuredLayoutPenalty = measuredPenalty,
            WorstRegionPenalty = worstRegionPenalty,
            DominantBandPenalty = new FidelityDominantBandPenaltyBreakdown(),
            GuardResult = new FidelityGuardResult()
        };
    }

    private static FidelityRunSummaryArtifact LoadSummary(string summaryPath)
    {
        if (!File.Exists(summaryPath))
        {
            throw new FileNotFoundException($"Run summary not found: {summaryPath}", summaryPath);
        }

        return JsonSerializer.Deserialize<FidelityRunSummaryArtifact>(File.ReadAllText(summaryPath), JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize run summary '{summaryPath}'.");
    }

    private static FidelitySurfaceEvaluation LoadSurfaceEvaluation(FidelityRunSurfaceArtifact surface)
    {
        var report = JsonSerializer.Deserialize<ImageSimilarityReport>(File.ReadAllText(surface.ReportPath), JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize score report '{surface.ReportPath}'.");
        MeasuredLayoutReport? measuredLayout = null;
        if (!string.IsNullOrWhiteSpace(surface.MeasuredLayoutPath) && File.Exists(surface.MeasuredLayoutPath))
        {
            measuredLayout = JsonSerializer.Deserialize<MeasuredLayoutReport>(File.ReadAllText(surface.MeasuredLayoutPath), JsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize measured layout report '{surface.MeasuredLayoutPath}'.");
        }

        return new FidelitySurfaceEvaluation(surface.Id, report, measuredLayout);
    }

    private static FidelitySurfaceEvaluation CreateSyntheticSurfaceEvaluation(
        string surfaceId,
        ImageSimilaritySpatialAnalysis analysis)
        => new(
            surfaceId,
            new ImageSimilarityReport
            {
                Version = "1.0",
                ReferencePath = string.Empty,
                CandidatePath = string.Empty,
                Tolerance = 0,
                Metrics = new DiffMetrics(),
                PixelIdentityPercent = 0,
                DeltaSimilarityPercent = 0,
                OverallSimilarityPercent = 0,
                Analysis = analysis
            },
            null);

    private static string ResolveBaselineCandidate(
        FidelityFrontierOptimizerState state,
        IReadOnlyList<FidelityFrontierCandidateEvaluation> evaluations)
    {
        if (!string.IsNullOrWhiteSpace(state.BaselineCandidateId))
        {
            return state.BaselineCandidateId;
        }

        var explicitBaseline = evaluations.FirstOrDefault(candidate => string.Equals(candidate.CandidateId, "baseline", StringComparison.OrdinalIgnoreCase));
        if (explicitBaseline != null)
        {
            return explicitBaseline.CandidateId;
        }

        return evaluations
            .OrderBy(candidate => candidate.Depth)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .First()
            .CandidateId;
    }

    private static List<FidelityFrontierCandidateEvaluation> PruneToFrontier(
        List<FidelityFrontierCandidateEvaluation> candidates,
        int beamWidth)
    {
        if (candidates.Count <= 1)
        {
            return candidates.ToList();
        }

        var nondominated = candidates
            .Where(candidate => candidates.All(other =>
                string.Equals(candidate.CandidateId, other.CandidateId, StringComparison.Ordinal)
                || !Dominates(other.Vector, candidate.Vector)))
            .OrderBy(candidate => candidate, FidelityEvaluationComparer.Instance)
            .Take(Math.Max(1, beamWidth))
            .ToList();

        return nondominated;
    }

    private static double AverageOverall(IEnumerable<FidelitySurfaceEvaluation> surfaces)
    {
        var list = surfaces.ToList();
        if (list.Count == 0)
        {
            return 0d;
        }

        return Math.Round(list.Average(surface => surface.Report.OverallSimilarityPercent), 4);
    }

    private static FidelityWorstRegionPenaltyBreakdown ComputeWorstRegionPenaltyBreakdown(
        IReadOnlyList<FidelitySurfaceEvaluation> surfaces)
    {
        FidelityWorstRegionPenaltyBreakdown? worst = null;
        foreach (var surface in surfaces)
        {
            var candidate = ComputeWorstRegionPenaltyBreakdown(surface.Report.RecursiveAnalysis) with
            {
                SurfaceId = surface.SurfaceId
            };
            if (worst == null || candidate.TotalPenalty > worst.TotalPenalty)
            {
                worst = candidate;
            }
        }

        return worst ?? new FidelityWorstRegionPenaltyBreakdown
        {
            LowestRegionOverallSimilarity = 100,
            LowestPhaseSimilarity = 100,
            TotalPenalty = 0
        };
    }

    private static FidelityDominantBandPenaltyBreakdown ComputeDominantBandPenaltyBreakdown(
        IReadOnlyList<FidelitySurfaceEvaluation> baselineSurfaces,
        IReadOnlyList<FidelitySurfaceEvaluation> candidateSurfaces)
    {
        var penalties = new List<FidelityDominantBandSurfacePenalty>();
        var baselineById = baselineSurfaces.ToDictionary(surface => surface.SurfaceId, StringComparer.OrdinalIgnoreCase);
        foreach (var candidateSurface in candidateSurfaces)
        {
            if (!baselineById.TryGetValue(candidateSurface.SurfaceId, out var baselineSurface))
            {
                continue;
            }

            var baselineAnalysis = baselineSurface.Report.Analysis;
            var candidateAnalysis = candidateSurface.Report.Analysis;
            if (baselineAnalysis == null
                || candidateAnalysis == null
                || !string.Equals(baselineAnalysis.DominantBand, candidateAnalysis.DominantBand, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            penalties.Add(new FidelityDominantBandSurfacePenalty
            {
                SurfaceId = candidateSurface.SurfaceId,
                DominantBand = candidateAnalysis.DominantBand,
                Penalty = Math.Round(GetBandChangedPercent(candidateAnalysis, candidateAnalysis.DominantBand), 4)
            });
        }

        return new FidelityDominantBandPenaltyBreakdown
        {
            PersistedMismatchCount = penalties.Count,
            AveragePenalty = penalties.Count == 0 ? 0d : Math.Round(penalties.Average(entry => entry.Penalty), 4),
            Surfaces = penalties
        };
    }

    private static double GetBandChangedPercent(ImageSimilaritySpatialAnalysis analysis, string dominantBand)
        => dominantBand switch
        {
            "left-edge" => analysis.LeftEdgeChangedPercent,
            "right-edge" => analysis.RightEdgeChangedPercent,
            "top-edge" => analysis.TopEdgeChangedPercent,
            "bottom-edge" => analysis.BottomEdgeChangedPercent,
            "center-band" => analysis.CenterBandChangedPercent,
            _ => 0d
        };

    private static FidelityWorstRegionPenaltyBreakdown ComputeWorstRegionPenaltyBreakdown(
        ImageSimilarityRecursiveScoreNode? recursiveAnalysis)
    {
        if (recursiveAnalysis == null)
        {
            return new FidelityWorstRegionPenaltyBreakdown
            {
                LowestRegionOverallSimilarity = 100,
                LowestPhaseSimilarity = 100,
                TotalPenalty = 0
            };
        }

        var descendants = FlattenRecursiveAnalysis(recursiveAnalysis)
            .Where(node => !string.Equals(node.Level, recursiveAnalysis.Level, StringComparison.Ordinal))
            .ToList();
        if (descendants.Count == 0)
        {
            return new FidelityWorstRegionPenaltyBreakdown
            {
                LowestRegionOverallSimilarity = recursiveAnalysis.OverallSimilarityPercent,
                LowestPhaseSimilarity = recursiveAnalysis.Phases.Count == 0 ? recursiveAnalysis.OverallSimilarityPercent : recursiveAnalysis.Phases.Min(phase => phase.SimilarityPercent),
                RegionLevel = recursiveAnalysis.Level,
                LowestPhaseName = recursiveAnalysis.Phases.OrderBy(phase => phase.SimilarityPercent).FirstOrDefault()?.Phase,
                TotalPenalty = 0
            };
        }

        var lowestRegion = descendants
            .OrderBy(node => node.OverallSimilarityPercent)
            .ThenBy(node => node.Level, StringComparer.Ordinal)
            .First();
        var lowestPhase = lowestRegion.Phases
            .OrderBy(phase => phase.SimilarityPercent)
            .FirstOrDefault();
        var lowestPhaseSimilarity = lowestPhase?.SimilarityPercent ?? lowestRegion.OverallSimilarityPercent;
        var penalty = Math.Round((((100d - lowestRegion.OverallSimilarityPercent) + (100d - lowestPhaseSimilarity)) / 2d), 4);

        return new FidelityWorstRegionPenaltyBreakdown
        {
            RegionLevel = lowestRegion.Level,
            LowestRegionOverallSimilarity = Math.Round(lowestRegion.OverallSimilarityPercent, 4),
            LowestPhaseName = lowestPhase?.Phase,
            LowestPhaseSimilarity = Math.Round(lowestPhaseSimilarity, 4),
            TotalPenalty = penalty
        };
    }

    private static List<ImageSimilarityRecursiveScoreNode> FlattenRecursiveAnalysis(ImageSimilarityRecursiveScoreNode node)
    {
        var result = new List<ImageSimilarityRecursiveScoreNode> { node };
        foreach (var child in node.Children)
        {
            result.AddRange(FlattenRecursiveAnalysis(child));
        }

        return result;
    }

    private static FidelityGuardResult BuildGuardResult(
        FidelityFrontierCandidateEvaluation candidate,
        FidelityFrontierCandidateEvaluation baseline,
        string primarySurfaceId)
    {
        var candidateSummary = LoadSummary(candidate.SummaryPath);
        var baselineSummary = LoadSummary(baseline.SummaryPath);
        var reasons = new List<string>();

        var candidateReports = candidateSummary.Surfaces.ToDictionary(surface => surface.Id, LoadSurfaceEvaluation, StringComparer.OrdinalIgnoreCase);
        var baselineReports = baselineSummary.Surfaces.ToDictionary(surface => surface.Id, LoadSurfaceEvaluation, StringComparer.OrdinalIgnoreCase);

        var uguiGuardPassed = true;
        foreach (var pair in baselineReports.Where(pair =>
                     pair.Key.EndsWith("-ugui", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(pair.Key, primarySurfaceId, StringComparison.OrdinalIgnoreCase)))
        {
            if (!candidateReports.TryGetValue(pair.Key, out var candidateSurface))
            {
                uguiGuardPassed = false;
                reasons.Add($"Missing dense uGUI surface '{pair.Key}'.");
                continue;
            }

            var delta = candidateSurface.Report.OverallSimilarityPercent - pair.Value.Report.OverallSimilarityPercent;
            if (delta < -UguiRegressionTolerance)
            {
                uguiGuardPassed = false;
                reasons.Add($"Dense uGUI surface '{pair.Key}' regressed by {Math.Round(Math.Abs(delta), 4)}.");
            }
        }

        var uitkGuardPassed = true;
        foreach (var pair in baselineReports.Where(pair => pair.Key.EndsWith("-uitk", StringComparison.OrdinalIgnoreCase)))
        {
            if (!candidateReports.TryGetValue(pair.Key, out var candidateSurface))
            {
                uitkGuardPassed = false;
                reasons.Add($"Missing dense UI Toolkit surface '{pair.Key}'.");
                continue;
            }

            var delta = candidateSurface.Report.OverallSimilarityPercent - pair.Value.Report.OverallSimilarityPercent;
            if (delta < -UitkRegressionTolerance)
            {
                uitkGuardPassed = false;
                reasons.Add($"Dense UI Toolkit surface '{pair.Key}' regressed by {Math.Round(Math.Abs(delta), 4)}.");
            }
        }

        var measuredLayoutGuardPassed = baseline.MeasuredLayoutPenalty.TotalPenalty == 0d
            ? candidate.MeasuredLayoutPenalty.TotalPenalty <= 0d
            : candidate.MeasuredLayoutPenalty.TotalPenalty <= Math.Round(baseline.MeasuredLayoutPenalty.TotalPenalty * MeasuredLayoutPenaltyGrowthFactor, 4);
        if (!measuredLayoutGuardPassed)
        {
            reasons.Add("Weighted measured-layout penalty worsened beyond 10%.");
        }

        var shellGuardPassed = baseline.MeasuredLayoutPenalty.ShellPenalty == 0d
            ? candidate.MeasuredLayoutPenalty.ShellPenalty <= 0d
            : candidate.MeasuredLayoutPenalty.ShellPenalty <= Math.Round(baseline.MeasuredLayoutPenalty.ShellPenalty * ShellPenaltyGrowthFactor, 4);
        if (!shellGuardPassed)
        {
            reasons.Add("Shell-related measured-layout penalty worsened beyond the allowed tolerance.");
        }

        return new FidelityGuardResult
        {
            Passed = uguiGuardPassed && uitkGuardPassed && measuredLayoutGuardPassed && shellGuardPassed,
            UguiRegressionGuardPassed = uguiGuardPassed,
            UitkRegressionGuardPassed = uitkGuardPassed,
            MeasuredLayoutGuardPassed = measuredLayoutGuardPassed,
            ShellGuardPassed = shellGuardPassed,
            FailureReasons = reasons
        };
    }

    private static IReadOnlyList<string> BuildSelectionRejectionReasons(
        FidelityFrontierCandidateEvaluation candidate,
        FidelityFrontierCandidateEvaluation selected)
    {
        if (candidate.RejectionReasons.Count > 0 || candidate.Selected)
        {
            return candidate.RejectionReasons;
        }

        if (string.Equals(candidate.CandidateId, selected.CandidateId, StringComparison.Ordinal))
        {
            return [];
        }

        return ["Lower-ranked than the selected candidate after Pareto pruning and lexicographic tie-breaking."];
    }

    private sealed class FidelityEvaluationComparer : IComparer<FidelityFrontierCandidateEvaluation>
    {
        public static FidelityEvaluationComparer Instance { get; } = new();

        public int Compare(FidelityFrontierCandidateEvaluation? x, FidelityFrontierCandidateEvaluation? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return 1;
            }

            if (y is null)
            {
                return -1;
            }

            return CompareVectors(x.Vector, y.Vector);
        }

        private static int CompareVectors(FidelityCandidateVector left, FidelityCandidateVector right)
        {
            var comparisons = new[]
            {
                right.AverageDenseUguiOverallSimilarity.CompareTo(left.AverageDenseUguiOverallSimilarity),
                right.PrimaryDenseUguiSimilarity.CompareTo(left.PrimaryDenseUguiSimilarity),
                right.AverageDenseAllBackendOverallSimilarity.CompareTo(left.AverageDenseAllBackendOverallSimilarity),
                left.WeightedMeasuredLayoutIssuePenalty.CompareTo(right.WeightedMeasuredLayoutIssuePenalty),
                left.WorstRegionRecursiveScorePenalty.CompareTo(right.WorstRegionRecursiveScorePenalty),
                left.DominantBandMismatchPenalty.CompareTo(right.DominantBandMismatchPenalty)
            };

            return comparisons.FirstOrDefault(result => result != 0);
        }
    }
}
