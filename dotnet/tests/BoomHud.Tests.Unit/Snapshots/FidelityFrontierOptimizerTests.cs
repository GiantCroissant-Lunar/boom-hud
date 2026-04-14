using System.Text.Json;
using BoomHud.Abstractions.Snapshots;
using BoomHud.Cli.Handlers.Baseline;
using BoomHud.Cli.Handlers.Rules;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Snapshots;

public sealed class FidelityFrontierOptimizerTests : IDisposable
{
    private readonly string _tempDir;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
    private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FidelityFrontierOptimizerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"boomhud-frontier-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Dominates_RequiresParetoImprovement()
    {
        var left = new FidelityCandidateVector
        {
            AverageDenseUguiOverallSimilarity = 90,
            PrimaryDenseUguiSimilarity = 91,
            AverageDenseAllBackendOverallSimilarity = 89,
            WeightedMeasuredLayoutIssuePenalty = 2,
            WorstRegionRecursiveScorePenalty = 4,
            DominantBandMismatchPenalty = 3
        };
        var dominated = new FidelityCandidateVector
        {
            AverageDenseUguiOverallSimilarity = 88,
            PrimaryDenseUguiSimilarity = 90,
            AverageDenseAllBackendOverallSimilarity = 88,
            WeightedMeasuredLayoutIssuePenalty = 3,
            WorstRegionRecursiveScorePenalty = 4,
            DominantBandMismatchPenalty = 3
        };
        var tradeoff = dominated with
        {
            DominantBandMismatchPenalty = 1
        };

        FidelityFrontierOptimizer.Dominates(left, dominated).Should().BeTrue();
        FidelityFrontierOptimizer.Dominates(left, tradeoff).Should().BeFalse();
    }

    [Fact]
    public void ComputeWeightedMeasuredLayoutPenalty_UsesConfiguredSeverityWeights()
    {
        var issues = new[]
        {
            new MeasuredLayoutIssue
            {
                Category = "clip-mismatch",
                Severity = "error",
                LocalPath = "root/0",
                Summary = "error"
            },
            new MeasuredLayoutIssue
            {
                Category = "wrap-pressure-risk",
                Severity = "warning",
                LocalPath = "root/1",
                Summary = "warning"
            },
            new MeasuredLayoutIssue
            {
                Category = "font-size-drift",
                Severity = "info",
                LocalPath = "root/2",
                Summary = "info"
            }
        };

        FidelityFrontierOptimizer.ComputeWeightedMeasuredLayoutPenalty(issues).Should().Be(7.5);
    }

    [Fact]
    public void ComputeDominantBandMismatchPenalty_OnlyCountsPersistedDominantBand()
    {
        var baseline = CreateSpatialAnalysis("right-edge", 12);
        var persisted = CreateSpatialAnalysis("right-edge", 9);
        var shifted = CreateSpatialAnalysis("center-band", 18);

        FidelityFrontierOptimizer.ComputeDominantBandMismatchPenalty(baseline, persisted).Should().Be(9);
        FidelityFrontierOptimizer.ComputeDominantBandMismatchPenalty(baseline, shifted).Should().Be(0);
    }

    [Fact]
    public void Optimize_UsesConfiguredPrimarySurfaceForVectorAndPrunesFrontier()
    {
        var baselineSummary = WriteCandidateArtifacts(
            "baseline",
            [
                CreateSurface("custom-primary-ugui", 72, recursivePenalty: 8),
                CreateSurface("secondary-ugui", 88, recursivePenalty: 7),
                CreateSurface("secondary-uitk", 84, recursivePenalty: 6)
            ]);
        var strongCandidateSummary = WriteCandidateArtifacts(
            "candidate-strong",
            [
                CreateSurface("custom-primary-ugui", 79, dominantBand: "right-edge", dominantPenalty: 5, recursivePenalty: 6),
                CreateSurface("secondary-ugui", 89, dominantBand: "right-edge", dominantPenalty: 4, recursivePenalty: 5),
                CreateSurface("secondary-uitk", 85, recursivePenalty: 4)
            ]);
        var weakCandidateSummary = WriteCandidateArtifacts(
            "candidate-weak",
            [
                CreateSurface("custom-primary-ugui", 70, dominantBand: "right-edge", dominantPenalty: 7, recursivePenalty: 12),
                CreateSurface("secondary-ugui", 84, dominantBand: "right-edge", dominantPenalty: 8, recursivePenalty: 11),
                CreateSurface("secondary-uitk", 83, recursivePenalty: 10)
            ]);

        var summary = FidelityFrontierOptimizer.Optimize(new FidelityFrontierOptimizerState
        {
            PrimarySurfaceId = "custom-primary-ugui",
            BaselineCandidateId = "baseline",
            BeamWidth = 1,
            Candidates =
            [
                CreateCandidateState("baseline", baselineSummary, depth: 0),
                CreateCandidateState("candidate-strong", strongCandidateSummary, depth: 1),
                CreateCandidateState("candidate-weak", weakCandidateSummary, depth: 1)
            ]
        });

        summary.BaselineVector.PrimaryDenseUguiSimilarity.Should().Be(72);
        summary.BaselineVector.DominantBandMismatchPenalty.Should().Be(0);
        summary.SelectedCandidateId.Should().Be("candidate-strong");
        summary.Candidates.Single(candidate => candidate.CandidateId == "candidate-strong").Rank.Should().Be(1);
        var retainedDepth = summary.Depths.Should().ContainSingle(depth => depth.Depth == 1).Subject;
        retainedDepth.RetainedCandidateIds.Should().Equal("candidate-strong");
    }

    [Fact]
    public void Optimize_WhenCandidateViolatesRegressionGuard_FallsBackToBaseline()
    {
        var baselineSummary = WriteCandidateArtifacts(
            "baseline-guard",
            [
                CreateSurface("the-alters-crafting-ugui", 70, recursivePenalty: 8),
                CreateSurface("quest-sidebar-ugui", 82, recursivePenalty: 7),
                CreateSurface("quest-sidebar-uitk", 87, recursivePenalty: 6)
            ]);
        var candidateSummary = WriteCandidateArtifacts(
            "candidate-guard",
            [
                CreateSurface("the-alters-crafting-ugui", 75, recursivePenalty: 5),
                CreateSurface("quest-sidebar-ugui", 81.3, recursivePenalty: 7),
                CreateSurface("quest-sidebar-uitk", 87, recursivePenalty: 6)
            ]);

        var summary = FidelityFrontierOptimizer.Optimize(new FidelityFrontierOptimizerState
        {
            PrimarySurfaceId = "the-alters-crafting-ugui",
            BaselineCandidateId = "baseline",
            Candidates =
            [
                CreateCandidateState("baseline", baselineSummary, depth: 0),
                CreateCandidateState("candidate", candidateSummary, depth: 1)
            ]
        });

        summary.SelectedCandidateId.Should().Be("baseline");
        summary.SelectedCandidateIsBaseline.Should().BeTrue();
        summary.Candidates.Single(candidate => candidate.CandidateId == "candidate").GuardResult.Passed.Should().BeFalse();
        summary.Candidates.Single(candidate => candidate.CandidateId == "candidate").RejectionReasons.Should()
            .Contain(reason => reason.Contains("Dense uGUI surface 'quest-sidebar-ugui' regressed by", StringComparison.Ordinal));
    }

    [Fact]
    public void Summary_ToJson_RoundTripsOptimizerArtifact()
    {
        var baselineSummary = WriteCandidateArtifacts(
            "baseline-json",
            [
                CreateSurface("the-alters-crafting-ugui", 70, recursivePenalty: 8)
            ]);

        var summary = FidelityFrontierOptimizer.Optimize(new FidelityFrontierOptimizerState
        {
            PrimarySurfaceId = "the-alters-crafting-ugui",
            BaselineCandidateId = "baseline",
            Candidates =
            [
                CreateCandidateState("baseline", baselineSummary, depth: 0)
            ]
        });

        var roundTrip = JsonSerializer.Deserialize<FidelityFrontierOptimizerSummary>(summary.ToJson(), CaseInsensitiveJsonOptions);

        roundTrip.Should().NotBeNull();
        roundTrip!.SelectedCandidateId.Should().Be("baseline");
        roundTrip.BaselineVector.PrimaryDenseUguiSimilarity.Should().Be(70);
        roundTrip.Candidates.Single().Rank.Should().Be(1);
    }

    private static FidelityFrontierCandidateState CreateCandidateState(string id, string summaryPath, int depth)
        => new()
        {
            CandidateId = id,
            Label = id,
            SummaryPath = summaryPath,
            Depth = depth,
            ParentCandidateId = depth == 0 ? null : "baseline",
            AppliedActions = depth == 0 ? [] : [$"action-{id}"]
        };

    private string WriteCandidateArtifacts(string name, IReadOnlyList<TestSurfaceDefinition> surfaces)
    {
        var candidateDir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(candidateDir);

        var summary = new
        {
            label = name,
            averageOverallSimilarityPercent = Math.Round(surfaces.Average(surface => surface.OverallSimilarity), 4),
            surfaces = surfaces.Select(surface =>
            {
                var reportPath = Path.Combine(candidateDir, $"{surface.Id}.report.json");
                File.WriteAllText(reportPath, JsonSerializer.Serialize(CreateReport(surface), JsonOptions));

                string? measuredLayoutPath = null;
                if (surface.MeasuredIssues.Count > 0)
                {
                    measuredLayoutPath = Path.Combine(candidateDir, $"{surface.Id}.measured.json");
                    File.WriteAllText(measuredLayoutPath, JsonSerializer.Serialize(CreateMeasuredLayoutReport(surface), JsonOptions));
                }

                return new
                {
                    id = surface.Id,
                    reportPath,
                    measuredLayoutPath
                };
            }).ToList()
        };

        var summaryPath = Path.Combine(candidateDir, "summary.json");
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, JsonOptions));
        return summaryPath;
    }

    private static ImageSimilarityReport CreateReport(TestSurfaceDefinition surface)
        => new()
        {
            Version = "1.0",
            ReferencePath = $"{surface.Id}.reference.png",
            CandidatePath = $"{surface.Id}.candidate.png",
            Tolerance = 8,
            Metrics = new DiffMetrics(),
            PixelIdentityPercent = surface.OverallSimilarity,
            DeltaSimilarityPercent = surface.OverallSimilarity,
            OverallSimilarityPercent = surface.OverallSimilarity,
            Analysis = CreateSpatialAnalysis(surface.DominantBand, surface.DominantPenalty),
            RecursiveAnalysis = CreateRecursiveAnalysis(surface.RecursivePenalty),
            Findings = []
        };

    private static MeasuredLayoutReport CreateMeasuredLayoutReport(TestSurfaceDefinition surface)
        => new()
        {
            Version = "1.0",
            DocumentName = surface.Id,
            BackendFamily = surface.Id.EndsWith("-ugui", StringComparison.OrdinalIgnoreCase) ? "ugui" : "unity",
            ExpectedRootStableId = "root",
            ActualRootName = "root",
            Comparisons = [],
            Issues = surface.MeasuredIssues,
            SourceSemanticSummaries = []
        };

    private static ImageSimilarityRecursiveScoreNode CreateRecursiveAnalysis(double penalty)
    {
        var lowestSimilarity = 100d - penalty;
        return new ImageSimilarityRecursiveScoreNode
        {
            Level = "screen/frame",
            Bounds = new ImageSimilarityBounds
            {
                X = 0,
                Y = 0,
                Width = 100,
                Height = 100
            },
            OverallSimilarityPercent = 100,
            Children =
            [
                new ImageSimilarityRecursiveScoreNode
                {
                    Level = "panel",
                    Bounds = new ImageSimilarityBounds
                    {
                        X = 0,
                        Y = 0,
                        Width = 50,
                        Height = 50
                    },
                    OverallSimilarityPercent = lowestSimilarity,
                    Phases =
                    [
                        new ImageSimilarityPhaseScore
                        {
                            Phase = "text-icon-metrics",
                            SimilarityPercent = lowestSimilarity
                        }
                    ]
                }
            ]
        };
    }

    private static ImageSimilaritySpatialAnalysis CreateSpatialAnalysis(string dominantBand, double dominantPenalty)
        => new()
        {
            BaselineOpaquePercent = 50,
            CandidateOpaquePercent = 50,
            OpaqueCoverageDeltaPercent = 0,
            LeftEdgeChangedPercent = dominantBand == "left-edge" ? dominantPenalty : 1,
            RightEdgeChangedPercent = dominantBand == "right-edge" ? dominantPenalty : 1,
            TopEdgeChangedPercent = dominantBand == "top-edge" ? dominantPenalty : 1,
            BottomEdgeChangedPercent = dominantBand == "bottom-edge" ? dominantPenalty : 1,
            CenterBandChangedPercent = dominantBand == "center-band" ? dominantPenalty : 1,
            LeftThirdChangedPercent = 1,
            CenterThirdChangedPercent = 1,
            RightThirdChangedPercent = 1,
            TopThirdChangedPercent = 1,
            MiddleThirdChangedPercent = 1,
            BottomThirdChangedPercent = 1,
            DominantHorizontalRegion = dominantBand.Contains("left", StringComparison.Ordinal) ? "left-third" : "center-third",
            DominantVerticalRegion = dominantBand.Contains("top", StringComparison.Ordinal) ? "top-third" : "middle-third",
            DominantBand = dominantBand
        };

    private static TestSurfaceDefinition CreateSurface(
        string id,
        double overallSimilarity,
        string dominantBand = "center-band",
        double dominantPenalty = 2,
        double recursivePenalty = 5,
        IReadOnlyList<MeasuredLayoutIssue>? measuredIssues = null)
        => new(
            id,
            overallSimilarity,
            dominantBand,
            dominantPenalty,
            recursivePenalty,
            measuredIssues ?? []);

    private sealed record TestSurfaceDefinition(
        string Id,
        double OverallSimilarity,
        string DominantBand,
        double DominantPenalty,
        double RecursivePenalty,
        IReadOnlyList<MeasuredLayoutIssue> MeasuredIssues);
}
