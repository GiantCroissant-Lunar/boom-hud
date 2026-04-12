using System.Text.Json;
using BoomHud.Abstractions.Generation;
using BoomHud.Cli.Handlers.Rules;
using BoomHud.Generators;
using BoomHud.Generators.VisualIR;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Snapshots;

public sealed class UGuiSubtreeCandidateSelectionHandlerTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _tempDir;

    public UGuiSubtreeCandidateSelectionHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"boomhud-ugui-selection-{Guid.NewGuid():N}");
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
    public void Execute_WithShellImprovement_SelectsCandidateAndUpdatesBuildProgram()
    {
        var proofReportPath = WriteJson("proof.json", CreateProofReport(variantPrimaryScore: 19.7025d, variantOutsideScore: 55.24d, variantRootScore: 55.20d));
        var buildProgramPath = WriteJson("build-program.json", CreateBuildProgram());
        var outPath = Path.Combine(_tempDir, "updated-build-program.json");
        var decisionPath = Path.Combine(_tempDir, "decision.json");

        var exitCode = UGuiSubtreeCandidateSelectionHandler.Execute(new UGuiSubtreeCandidateSelectionOptions
        {
            ProofReportFile = new FileInfo(proofReportPath),
            BuildProgramFile = new FileInfo(buildProgramPath),
            OutFile = new FileInfo(outPath),
            DecisionOutFile = new FileInfo(decisionPath),
            CandidateIdMappings = ["baseline=membera-baseline-shell"],
            PrintSummary = false
        });

        exitCode.Should().Be(0);

        var updatedBuildProgram = JsonSerializer.Deserialize<UGuiBuildProgram>(File.ReadAllText(outPath));
        updatedBuildProgram.Should().NotBeNull();
        updatedBuildProgram!.AcceptedCandidates.Should().ContainSingle(selection =>
            selection.StableId == "root/1/0"
            && selection.CandidateId == "membera-tight-shell");

        var decision = JsonSerializer.Deserialize<UGuiSubtreeCandidateSelectionDecision>(File.ReadAllText(decisionPath));
        decision.Should().NotBeNull();
        decision!.ChangedSelection.Should().BeTrue();
        decision.SelectedCandidateId.Should().Be("membera-tight-shell");
    }

    [Fact]
    public void Execute_WhenVariantDropsPrimaryTooFar_KeepsBaselineSelection()
    {
        var proofReportPath = WriteJson("proof-regress.json", CreateProofReport(variantPrimaryScore: 18.90d, variantOutsideScore: 55.24d, variantRootScore: 55.20d));
        var buildProgramPath = WriteJson("build-program-regress.json", CreateBuildProgram());
        var outPath = Path.Combine(_tempDir, "updated-build-program-regress.json");

        var exitCode = UGuiSubtreeCandidateSelectionHandler.Execute(new UGuiSubtreeCandidateSelectionOptions
        {
            ProofReportFile = new FileInfo(proofReportPath),
            BuildProgramFile = new FileInfo(buildProgramPath),
            OutFile = new FileInfo(outPath),
            CandidateIdMappings = ["baseline=membera-baseline-shell"],
            PrintSummary = false
        });

        exitCode.Should().Be(0);

        var updatedBuildProgram = JsonSerializer.Deserialize<UGuiBuildProgram>(File.ReadAllText(outPath));
        updatedBuildProgram.Should().NotBeNull();
        updatedBuildProgram!.AcceptedCandidates.Should().ContainSingle(selection =>
            selection.StableId == "root/1/0"
            && selection.CandidateId == "membera-baseline-shell");
    }

    [Fact]
    public void Execute_WhenMotifVariantDriftsShell_KeepsBaselineSelection()
    {
        var proofReportPath = WriteJson("proof-shell-drift.json", CreateProofReport(
            variantPrimaryScore: 55.30d,
            variantOutsideScore: 56.90d,
            variantRootScore: 54.20d,
            subtreeSolveStage: "motif",
            variantSubtreeShellHeight: 18.94d,
            variantParentShellHeight: 216d));
        var buildProgramPath = WriteJson("build-program-shell-drift.json", CreateBuildProgram());
        var outPath = Path.Combine(_tempDir, "updated-build-program-shell-drift.json");

        var exitCode = UGuiSubtreeCandidateSelectionHandler.Execute(new UGuiSubtreeCandidateSelectionOptions
        {
            ProofReportFile = new FileInfo(proofReportPath),
            BuildProgramFile = new FileInfo(buildProgramPath),
            OutFile = new FileInfo(outPath),
            CandidateIdMappings = ["baseline=membera-baseline-shell"],
            PrintSummary = false
        });

        exitCode.Should().Be(0);

        var updatedBuildProgram = JsonSerializer.Deserialize<UGuiBuildProgram>(File.ReadAllText(outPath));
        updatedBuildProgram.Should().NotBeNull();
        updatedBuildProgram!.AcceptedCandidates.Should().ContainSingle(selection =>
            selection.StableId == "root/1/0"
            && selection.CandidateId == "membera-baseline-shell");
    }

    [Fact]
    public void Execute_WhenBaselineIsAlreadyShellCollapsed_UsesExpectedShellForMotifGate()
    {
        var proofReportPath = WriteJson("proof-expected-shell-drift.json", CreateProofReport(
            variantPrimaryScore: 55.30d,
            variantOutsideScore: 56.90d,
            variantRootScore: 54.20d,
            subtreeSolveStage: "motif",
            baselineSubtreeShellHeight: 236d,
            baselineParentShellHeight: 236d,
            variantSubtreeShellHeight: 236d,
            variantParentShellHeight: 236d,
            expectedSubtreeShellHeight: 216d,
            expectedParentShellHeight: 216d));
        var buildProgramPath = WriteJson("build-program-expected-shell-drift.json", CreateBuildProgram());
        var outPath = Path.Combine(_tempDir, "updated-build-program-expected-shell-drift.json");
        var decisionPath = Path.Combine(_tempDir, "decision-expected-shell-drift.json");

        var exitCode = UGuiSubtreeCandidateSelectionHandler.Execute(new UGuiSubtreeCandidateSelectionOptions
        {
            ProofReportFile = new FileInfo(proofReportPath),
            BuildProgramFile = new FileInfo(buildProgramPath),
            OutFile = new FileInfo(outPath),
            DecisionOutFile = new FileInfo(decisionPath),
            CandidateIdMappings = ["baseline=membera-baseline-shell"],
            PrintSummary = false
        });

        exitCode.Should().Be(0);

        var updatedBuildProgram = JsonSerializer.Deserialize<UGuiBuildProgram>(File.ReadAllText(outPath));
        updatedBuildProgram.Should().NotBeNull();
        updatedBuildProgram!.AcceptedCandidates.Should().ContainSingle(selection =>
            selection.StableId == "root/1/0"
            && selection.CandidateId == "membera-baseline-shell");

        var decision = JsonSerializer.Deserialize<UGuiSubtreeCandidateSelectionDecision>(File.ReadAllText(decisionPath));
        decision.Should().NotBeNull();
        decision!.Candidates.Should().ContainSingle(candidate => candidate.CandidateId == "membera-tight-shell")
            .Which.PassesShellGate.Should().BeFalse();
    }

    [Fact]
    public void Execute_WhenSurfaceVariantDriftsShell_AllowsSelection()
    {
        var proofReportPath = WriteJson("proof-surface-shell.json", CreateProofReport(
            variantPrimaryScore: 19.7025d,
            variantOutsideScore: 55.24d,
            variantRootScore: 55.20d,
            subtreeSolveStage: "surface",
            variantSubtreeShellHeight: 248d,
            variantParentShellHeight: 320d));
        var buildProgramPath = WriteJson("build-program-surface-shell.json", CreateBuildProgram());
        var outPath = Path.Combine(_tempDir, "updated-build-program-surface-shell.json");

        var exitCode = UGuiSubtreeCandidateSelectionHandler.Execute(new UGuiSubtreeCandidateSelectionOptions
        {
            ProofReportFile = new FileInfo(proofReportPath),
            BuildProgramFile = new FileInfo(buildProgramPath),
            OutFile = new FileInfo(outPath),
            CandidateIdMappings = ["baseline=membera-baseline-shell"],
            PrintSummary = false
        });

        exitCode.Should().Be(0);

        var updatedBuildProgram = JsonSerializer.Deserialize<UGuiBuildProgram>(File.ReadAllText(outPath));
        updatedBuildProgram.Should().NotBeNull();
        updatedBuildProgram!.AcceptedCandidates.Should().ContainSingle(selection =>
            selection.StableId == "root/1/0"
            && selection.CandidateId == "membera-tight-shell");
    }

    private string WriteJson<T>(string fileName, T value)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
        return path;
    }

    private static UGuiSubtreeProofReport CreateProofReport(
        double variantPrimaryScore,
        double variantOutsideScore,
        double variantRootScore,
        string subtreeSolveStage = "surface",
        double baselineSubtreeShellHeight = 236d,
        double baselineParentShellHeight = 320d,
        double variantSubtreeShellHeight = 248d,
        double variantParentShellHeight = 320d,
        double? expectedSubtreeShellHeight = null,
        double? expectedParentShellHeight = null)
        => new()
        {
            Version = "1.0",
            DocumentName = "PartyStatusStrip",
            BackendFamily = "ugui",
            SubtreeStableId = "root/1/0",
            SubtreeSolveStage = subtreeSolveStage,
            Runs =
            [
                new UGuiSubtreeProofRun
                {
                    Label = "baseline-a",
                    CandidateKey = "baseline",
                    ActualLayoutPath = "baseline-a.layout.actual.json",
                    BackendFamily = "ugui",
                    RootStableId = "root",
                    ScoreMode = "image",
                    SubtreeLocalPath = "root/1/0",
                    SubtreeNodeCount = 22,
                    SubtreeIssueCount = 0,
                    RootScore = 54.82d,
                    SubtreeScore = 21.06d,
                    DirectChildAggregateScore = 19.7025d,
                    OutsideSubtreeScore = 54.86d,
                    SubtreeShell = new UGuiShellGeometrySnapshot
                    {
                        LocalPath = "root/1/0",
                        ActualX = 0,
                        ActualY = 0,
                        ActualWidth = 400,
                        ActualHeight = baselineSubtreeShellHeight,
                        ExpectedX = 0,
                        ExpectedY = 0,
                        ExpectedWidth = 400,
                        ExpectedHeight = expectedSubtreeShellHeight ?? baselineSubtreeShellHeight
                    },
                    ParentShell = new UGuiShellGeometrySnapshot
                    {
                        LocalPath = "root/1",
                        ActualX = 24,
                        ActualY = 80,
                        ActualWidth = 1232,
                        ActualHeight = baselineParentShellHeight,
                        ExpectedX = 24,
                        ExpectedY = 80,
                        ExpectedWidth = 1232,
                        ExpectedHeight = expectedParentShellHeight ?? baselineParentShellHeight
                    }
                },
                new UGuiSubtreeProofRun
                {
                    Label = "baseline-b",
                    CandidateKey = "baseline",
                    ActualLayoutPath = "baseline-b.layout.actual.json",
                    BackendFamily = "ugui",
                    RootStableId = "root",
                    ScoreMode = "image",
                    SubtreeLocalPath = "root/1/0",
                    SubtreeNodeCount = 22,
                    SubtreeIssueCount = 0,
                    RootScore = 54.82d,
                    SubtreeScore = 21.06d,
                    DirectChildAggregateScore = 19.7025d,
                    OutsideSubtreeScore = 54.86d,
                    SubtreeShell = new UGuiShellGeometrySnapshot
                    {
                        LocalPath = "root/1/0",
                        ActualX = 0,
                        ActualY = 0,
                        ActualWidth = 400,
                        ActualHeight = baselineSubtreeShellHeight,
                        ExpectedX = 0,
                        ExpectedY = 0,
                        ExpectedWidth = 400,
                        ExpectedHeight = expectedSubtreeShellHeight ?? baselineSubtreeShellHeight
                    },
                    ParentShell = new UGuiShellGeometrySnapshot
                    {
                        LocalPath = "root/1",
                        ActualX = 24,
                        ActualY = 80,
                        ActualWidth = 1232,
                        ActualHeight = baselineParentShellHeight,
                        ExpectedX = 24,
                        ExpectedY = 80,
                        ExpectedWidth = 1232,
                        ExpectedHeight = expectedParentShellHeight ?? baselineParentShellHeight
                    }
                },
                new UGuiSubtreeProofRun
                {
                    Label = "tight-shell",
                    CandidateKey = "membera-tight-shell",
                    ActualLayoutPath = "tight-shell.layout.actual.json",
                    BackendFamily = "ugui",
                    RootStableId = "root",
                    ScoreMode = "image",
                    SubtreeLocalPath = "root/1/0",
                    SubtreeNodeCount = 22,
                    SubtreeIssueCount = 9,
                    RootScore = variantRootScore,
                    SubtreeScore = 20.60d,
                    DirectChildAggregateScore = variantPrimaryScore,
                    OutsideSubtreeScore = variantOutsideScore,
                    SubtreeShell = new UGuiShellGeometrySnapshot
                    {
                        LocalPath = "root/1/0",
                        ActualX = 0,
                        ActualY = 0,
                        ActualWidth = 400,
                        ActualHeight = variantSubtreeShellHeight,
                        ExpectedX = 0,
                        ExpectedY = 0,
                        ExpectedWidth = 400,
                        ExpectedHeight = expectedSubtreeShellHeight ?? baselineSubtreeShellHeight
                    },
                    ParentShell = new UGuiShellGeometrySnapshot
                    {
                        LocalPath = "root/1",
                        ActualX = 24,
                        ActualY = 80,
                        ActualWidth = 1232,
                        ActualHeight = variantParentShellHeight,
                        ExpectedX = 24,
                        ExpectedY = 80,
                        ExpectedWidth = 1232,
                        ExpectedHeight = expectedParentShellHeight ?? baselineParentShellHeight
                    }
                }
            ],
            Determinism = new UGuiDeterminismProof
            {
                IsSatisfied = true,
                RunCount = 3,
                UniqueCandidateCount = 2,
                RepeatedCandidateGroupCount = 1,
                StructureMatchesAcrossRuns = true,
                MaxPositionDelta = 0,
                MaxSizeDelta = 0,
                MaxFontDelta = 0,
                MaxSubtreeScoreDelta = 0,
                Notes = "Repeated captures agree at the subtree level within the configured layout tolerances."
            },
            LocalScoring = new UGuiLocalScoringProof
            {
                IsSatisfied = true,
                MaxSubtreeScoreDelta = 0.46d,
                MaxDirectChildAggregateScoreDelta = 0d,
                PrimarySignal = "direct-child-aggregate",
                SubtreeToRootScoreCorrelation = -1d,
                DirectChildAggregateToRootScoreCorrelation = null,
                OutsideSubtreeToRootScoreCorrelation = 1d,
                Notes = "Direct child motif scores stayed stable while outside-subtree fidelity improved, which points to a shell or placement change rather than a motif regression."
            },
            CandidateCatalog = new UGuiCandidateCatalogProof
            {
                IsSatisfied = true,
                CandidateCount = 2,
                HasAcceptedCandidate = true,
                CandidateIds = ["membera-baseline-shell", "membera-tight-shell"],
                AcceptedCandidateId = "membera-baseline-shell",
                Notes = "Candidate space is small, explicit, and carries a selected subtree realization."
            },
            EmitterConsumption = new UGuiEmitterConsumptionProof
            {
                IsSatisfied = true,
                ConsumedCandidateId = "membera-baseline-shell",
                StableId = "root/1/0",
                AppliedOverrideDimensions = ["layout"],
                Notes = "The experimental uGUI emitter can now consume the accepted subtree candidate by stable id."
            }
        };

    private static UGuiBuildProgram CreateBuildProgram()
        => new()
        {
            DocumentName = "PartyStatusStrip",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            RootStableId = "root",
            CandidateCatalogs =
            [
                new UGuiBuildCandidateCatalog
                {
                    StableId = "root/1/0",
                    SolveStage = "surface",
                    Candidates =
                    [
                        new UGuiBuildCandidate
                        {
                            CandidateId = "membera-baseline-shell",
                            Label = "Baseline shell",
                            Action = new GeneratorRuleAction
                            {
                                Layout = new GeneratorLayoutRuleAction
                                {
                                    PaddingDelta = 0
                                }
                            }
                        },
                        new UGuiBuildCandidate
                        {
                            CandidateId = "membera-tight-shell",
                            Label = "Tight shell",
                            Action = new GeneratorRuleAction
                            {
                                Layout = new GeneratorLayoutRuleAction
                                {
                                    PaddingDelta = -12
                                }
                            }
                        }
                    ]
                }
            ],
            AcceptedCandidates =
            [
                new UGuiBuildSelection
                {
                    StableId = "root/1/0",
                    CandidateId = "membera-baseline-shell"
                }
            ]
        };
}
