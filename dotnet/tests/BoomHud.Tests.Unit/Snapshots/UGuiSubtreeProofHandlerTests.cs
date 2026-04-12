using System.Text.Json;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Generation;
using BoomHud.Cli.Handlers.Baseline;
using BoomHud.Cli.Handlers.Rules;
using BoomHud.Generators.VisualIR;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace BoomHud.Tests.Unit.Snapshots;

public sealed class UGuiSubtreeProofHandlerTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _tempDir;

    public UGuiSubtreeProofHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"boomhud-ugui-proof-{Guid.NewGuid():N}");
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
    public void BuildReport_WithRepeatedIdenticalLayouts_PassesDeterminism()
    {
        var visualDocument = CreateVisualDocument();
        var visualPath = WriteJson("visual.json", visualDocument);
        var actualLayoutA = WriteJson("actual-a.json", CreateActualLayout(width: 100, childWidth: 60));
        var actualLayoutB = WriteJson("actual-b.json", CreateActualLayout(width: 100, childWidth: 60));

        var report = UGuiSubtreeProofHandler.BuildReport(new UGuiSubtreeProofOptions
        {
            VisualIrFile = new FileInfo(visualPath),
            SubtreeStableId = "root/0",
            ActualLayoutFiles = [new FileInfo(actualLayoutA), new FileInfo(actualLayoutB)],
            Labels = ["repeat-a", "repeat-b"],
            PrintSummary = false
        });

        report.Determinism.IsSatisfied.Should().BeTrue();
        report.Determinism.UniqueCandidateCount.Should().Be(1);
        report.Determinism.RepeatedCandidateGroupCount.Should().Be(1);
        report.Determinism.StructureMatchesAcrossRuns.Should().BeTrue();
        report.LocalScoring.MaxSubtreeScoreDelta.Should().Be(0);
        report.Runs.Should().OnlyContain(run => run.SubtreeScore == report.Runs[0].SubtreeScore);
    }

    [Fact]
    public void BuildReport_WithCandidateCatalogAndRepeatedCandidateGroups_SeparatesDeterminismFromRanking()
    {
        var visualDocument = CreateVisualDocument();
        var visualPath = WriteJson("visual.json", visualDocument);
        var actualLayoutA = WriteJson("actual-a.json", CreateActualLayout(width: 100, childWidth: 60));
        var actualLayoutARepeat = WriteJson("actual-a-repeat.json", CreateActualLayout(width: 100, childWidth: 60));
        var actualLayoutB = WriteJson("actual-b.json", CreateActualLayout(width: 100, childWidth: 44, childScaleX: 1.2));
        var actualLayoutBRepeat = WriteJson("actual-b-repeat.json", CreateActualLayout(width: 100, childWidth: 44, childScaleX: 1.2));
        var buildProgramPath = WriteJson("build-program.json", new UGuiBuildProgram
        {
            DocumentName = visualDocument.DocumentName,
            BackendFamily = visualDocument.BackendFamily,
            SourceGenerationMode = visualDocument.SourceGenerationMode,
            RootStableId = visualDocument.Root.StableId,
            CandidateCatalogs =
            [
                new UGuiBuildCandidateCatalog
                {
                    StableId = "root/0",
                    SolveStage = "atom",
                    Candidates =
                    [
                        new UGuiBuildCandidate
                        {
                            CandidateId = "content-hug",
                            Label = "Content hug",
                            Action = new GeneratorRuleAction
                            {
                                Layout = new GeneratorLayoutRuleAction
                                {
                                    PreferContentWidth = true
                                }
                            }
                        },
                        new UGuiBuildCandidate
                        {
                            CandidateId = "fill-width",
                            Label = "Fill width",
                            Action = new GeneratorRuleAction
                            {
                                Layout = new GeneratorLayoutRuleAction
                                {
                                    StretchWidth = true
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
                    StableId = "root/0",
                    CandidateId = "fill-width"
                }
            ]
        });

        var report = UGuiSubtreeProofHandler.BuildReport(new UGuiSubtreeProofOptions
        {
            VisualIrFile = new FileInfo(visualPath),
            SubtreeStableId = "root/0",
            ActualLayoutFiles = [new FileInfo(actualLayoutA), new FileInfo(actualLayoutARepeat), new FileInfo(actualLayoutB), new FileInfo(actualLayoutBRepeat)],
            Labels = ["baseline-a", "baseline-b", "variant-a", "variant-b"],
            CandidateKeys = ["baseline", "baseline", "variant", "variant"],
            BuildProgramFile = new FileInfo(buildProgramPath),
            PositionTolerance = 100,
            SizeTolerance = 100,
            FontTolerance = 100,
            PrintSummary = false
        });

        report.Runs[0].SubtreeScore.Should().Be(report.Runs[1].SubtreeScore);
        report.Runs[0].RootScore.Should().Be(report.Runs[1].RootScore);
        report.Runs[0].SubtreeScore.Should().BeGreaterThan(report.Runs[2].SubtreeScore);
        report.Runs[0].RootScore.Should().BeGreaterThan(report.Runs[2].RootScore);
        report.Runs[0].CandidateKey.Should().Be("baseline");
        report.Runs[2].CandidateKey.Should().Be("variant");
        report.Determinism.IsSatisfied.Should().BeTrue();
        report.Determinism.UniqueCandidateCount.Should().Be(2);
        report.Determinism.RepeatedCandidateGroupCount.Should().Be(2);
        report.LocalScoring.SubtreeToRootScoreCorrelation.Should().BeGreaterThan(0.5);
        report.CandidateCatalog.IsSatisfied.Should().BeTrue();
        report.EmitterConsumption.IsSatisfied.Should().BeTrue();
        report.EmitterConsumption.AppliedOverrideDimensions.Should().Contain("layout");
    }

    [Fact]
    public void BuildReport_WithReferenceImage_UsesImageScoresForRootAndSubtree()
    {
        var visualDocument = CreateVisualDocument();
        var visualPath = WriteJson("visual-image.json", visualDocument);
        var actualLayoutA = WriteJson("candidate-a.layout.actual.json", CreateActualLayout(width: 100, childWidth: 60));
        var actualLayoutB = WriteJson("candidate-b.layout.actual.json", CreateActualLayout(width: 100, childWidth: 40));
        var referenceImagePath = Path.Combine(_tempDir, "reference.png");
        var candidateImageAPath = Path.Combine(_tempDir, "candidate-a.png");
        var candidateImageBPath = Path.Combine(_tempDir, "candidate-b.png");

        CreateSurfaceImage(referenceImagePath, totalWidth: 100, totalHeight: 20, filledWidth: 60);
        CreateSurfaceImage(candidateImageAPath, totalWidth: 100, totalHeight: 20, filledWidth: 60);
        CreateSurfaceImage(candidateImageBPath, totalWidth: 100, totalHeight: 20, filledWidth: 40);

        var report = UGuiSubtreeProofHandler.BuildReport(new UGuiSubtreeProofOptions
        {
            VisualIrFile = new FileInfo(visualPath),
            SubtreeStableId = "root/0",
            ActualLayoutFiles = [new FileInfo(actualLayoutA), new FileInfo(actualLayoutB)],
            Labels = ["image-a", "image-b"],
            CandidateKeys = ["baseline", "variant"],
            ReferenceImageFile = new FileInfo(referenceImagePath),
            ImageNormalizeMode = "off",
            ImageTolerance = 0,
            PositionTolerance = 100,
            SizeTolerance = 100,
            FontTolerance = 100,
            PrintSummary = false
        });

        report.Runs.Should().OnlyContain(run => run.ScoreMode == "image");
        report.Runs[0].SubtreeScore.Should().BeGreaterThan(report.Runs[1].SubtreeScore);
        report.Runs[0].RootScore.Should().BeGreaterThan(report.Runs[1].RootScore);
        report.LocalScoring.SubtreeToRootScoreCorrelation.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void BuildReport_WithCompositeSubtree_ReportsDirectChildAggregateScores()
    {
        var visualDocument = CreateCompositeVisualDocument();
        var visualPath = WriteJson("visual-composite.json", visualDocument);
        var actualLayoutA = WriteJson("composite-a.layout.actual.json", CreateCompositeActualLayout(leftChildWidth: 30, rightChildWidth: 30));
        var actualLayoutB = WriteJson("composite-b.layout.actual.json", CreateCompositeActualLayout(leftChildWidth: 30, rightChildWidth: 20));
        var referenceImagePath = Path.Combine(_tempDir, "composite-reference.png");
        var candidateImageAPath = Path.Combine(_tempDir, "composite-a.png");
        var candidateImageBPath = Path.Combine(_tempDir, "composite-b.png");

        CreateCompositeSurfaceImage(referenceImagePath, totalWidth: 100, totalHeight: 20, leftChildWidth: 30, rightChildWidth: 30);
        CreateCompositeSurfaceImage(candidateImageAPath, totalWidth: 100, totalHeight: 20, leftChildWidth: 30, rightChildWidth: 30);
        CreateCompositeSurfaceImage(candidateImageBPath, totalWidth: 100, totalHeight: 20, leftChildWidth: 30, rightChildWidth: 20);

        var report = UGuiSubtreeProofHandler.BuildReport(new UGuiSubtreeProofOptions
        {
            VisualIrFile = new FileInfo(visualPath),
            SubtreeStableId = "root/0",
            ActualLayoutFiles = [new FileInfo(actualLayoutA), new FileInfo(actualLayoutB)],
            Labels = ["composite-a", "composite-b"],
            CandidateKeys = ["baseline", "variant"],
            ReferenceImageFile = new FileInfo(referenceImagePath),
            ImageNormalizeMode = "off",
            ImageTolerance = 0,
            PositionTolerance = 100,
            SizeTolerance = 100,
            FontTolerance = 100,
            PrintSummary = false
        });

        report.Runs[0].DirectChildScores.Should().HaveCount(2);
        report.Runs[0].DirectChildAggregateScore.Should().NotBeNull();
        report.Runs[1].DirectChildAggregateScore.Should().NotBeNull();
        report.Runs[0].DirectChildAggregateScore!.Value.Should().BeGreaterThan(report.Runs[1].DirectChildAggregateScore!.Value);
        report.LocalScoring.PrimarySignal.Should().BeOneOf("direct-child-aggregate", "subtree");
        report.LocalScoring.DirectChildAggregateToRootScoreCorrelation.Should().BeGreaterThan(0.5);
        report.Runs.Should().OnlyContain(run => run.OutsideSubtreeScore.HasValue);
    }

    [Fact]
    public void BuildReport_WithHuggingLeafImageDiff_UsesActualBoundsFallbackForDirectChildScores()
    {
        var visualDocument = CreateCompositeVisualDocumentWithHuggingLeaf();
        var visualPath = WriteJson("visual-hugging.json", visualDocument);
        var actualLayoutA = WriteJson("hugging-a.layout.actual.json", CreateCompositeActualLayout(leftChildWidth: 30, rightChildWidth: 20, parentX: 10));
        var actualLayoutB = WriteJson("hugging-b.layout.actual.json", CreateCompositeActualLayout(leftChildWidth: 30, rightChildWidth: 10, rightChildX: 40, parentX: 10));
        var referenceImagePath = Path.Combine(_tempDir, "hugging-reference.png");
        var candidateImageAPath = Path.Combine(_tempDir, "hugging-a.png");
        var candidateImageBPath = Path.Combine(_tempDir, "hugging-b.png");

        CreateCompositeSurfaceImage(referenceImagePath, totalWidth: 100, totalHeight: 20, leftChildWidth: 30, rightChildWidth: 20, parentX: 10);
        CreateCompositeSurfaceImage(candidateImageAPath, totalWidth: 100, totalHeight: 20, leftChildWidth: 30, rightChildWidth: 20, parentX: 10);
        CreateCompositeSurfaceImage(candidateImageBPath, totalWidth: 100, totalHeight: 20, leftChildWidth: 30, rightChildWidth: 10, rightChildX: 40, parentX: 10);

        var report = UGuiSubtreeProofHandler.BuildReport(new UGuiSubtreeProofOptions
        {
            VisualIrFile = new FileInfo(visualPath),
            SubtreeStableId = "root/0",
            ActualLayoutFiles = [new FileInfo(actualLayoutA), new FileInfo(actualLayoutB)],
            Labels = ["hugging-a", "hugging-b"],
            CandidateKeys = ["baseline", "variant"],
            ReferenceImageFile = new FileInfo(referenceImagePath),
            ImageNormalizeMode = "off",
            ImageTolerance = 0,
            PositionTolerance = 100,
            SizeTolerance = 100,
            FontTolerance = 100,
            PrintSummary = false
        });

        report.Runs[0].DirectChildScores.Should().HaveCount(2);
        report.Runs[1].DirectChildScores.Should().HaveCount(2);
        report.Runs[0].DirectChildScores[1].StableId.Should().Be("root/0/1");
        report.Runs[0].DirectChildScores[1].Score.Should().BeGreaterThan(report.Runs[1].DirectChildScores[1].Score);
        report.Runs[0].DirectChildAggregateScore.Should().NotBeNull();
        report.Runs[1].DirectChildAggregateScore.Should().NotBeNull();
        report.Runs[0].DirectChildAggregateScore!.Value.Should().BeGreaterThan(report.Runs[1].DirectChildAggregateScore!.Value);
        report.Runs[0].SubtreeScore.Should().BeGreaterThan(report.Runs[1].SubtreeScore);
    }

    [Fact]
    public void BuildReport_WithDistinctCandidateKeysAndNoRepeats_DoesNotTreatRankingAsDeterminismDrift()
    {
        var visualDocument = CreateVisualDocument();
        var visualPath = WriteJson("visual-distinct.json", visualDocument);
        var actualLayoutA = WriteJson("distinct-a.json", CreateActualLayout(width: 100, childWidth: 60));
        var actualLayoutB = WriteJson("distinct-b.json", CreateActualLayout(width: 100, childWidth: 44, childScaleX: 1.2));

        var report = UGuiSubtreeProofHandler.BuildReport(new UGuiSubtreeProofOptions
        {
            VisualIrFile = new FileInfo(visualPath),
            SubtreeStableId = "root/0",
            ActualLayoutFiles = [new FileInfo(actualLayoutA), new FileInfo(actualLayoutB)],
            Labels = ["baseline", "variant"],
            CandidateKeys = ["baseline", "variant"],
            PositionTolerance = 0.01,
            SizeTolerance = 0.01,
            FontTolerance = 0.01,
            PrintSummary = false
        });

        report.Determinism.IsSatisfied.Should().BeFalse();
        report.Determinism.RepeatedCandidateGroupCount.Should().Be(0);
        report.Determinism.Notes.Should().Contain("No repeated candidate groups were provided");
        report.LocalScoring.IsSatisfied.Should().BeTrue();
        report.LocalScoring.SubtreeToRootScoreCorrelation.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void EvaluateLocalScoring_WhenDirectChildAggregateIsWorseThanSubtree_PrefersSubtreeSignal()
    {
        var report = UGuiSubtreeProofHandler.EvaluateLocalScoring(
        [
            CreateRunModel("baseline", rootScore: 54.82, subtreeScore: 64.60, directChildAggregateScore: 71.03, outsideSubtreeScore: 57.18),
            CreateRunModel("baseline", rootScore: 54.82, subtreeScore: 64.60, directChildAggregateScore: 71.03, outsideSubtreeScore: 57.18),
            CreateRunModel("manual", rootScore: 54.86, subtreeScore: 64.61, directChildAggregateScore: 69.02, outsideSubtreeScore: 57.22),
            CreateRunModel("extreme", rootScore: 54.73, subtreeScore: 63.05, directChildAggregateScore: 71.03, outsideSubtreeScore: 57.30)
        ]);

        report.PrimarySignal.Should().Be("subtree");
        report.IsSatisfied.Should().BeTrue();
        report.SubtreeToRootScoreCorrelation.Should().BeGreaterThan(0.5);
        report.DirectChildAggregateToRootScoreCorrelation.Should().BeLessThan(0);
    }

    private string WriteJson<T>(string fileName, T value)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
        return path;
    }

    private static UGuiSubtreeProofHandler.RunModel CreateRunModel(
        string candidateKey,
        double rootScore,
        double subtreeScore,
        double? directChildAggregateScore,
        double? outsideSubtreeScore)
        => new(
            new UGuiSubtreeProofRun
            {
                Label = candidateKey,
                CandidateKey = candidateKey,
                ActualLayoutPath = $"{candidateKey}.layout.actual.json",
                BackendFamily = "ugui",
                RootStableId = "root",
                ScoreMode = "image",
                SubtreeLocalPath = "root/0",
                SubtreeNodeCount = 2,
                SubtreeIssueCount = 0,
                RootScore = rootScore,
                SubtreeScore = subtreeScore,
                DirectChildAggregateScore = directChildAggregateScore,
                OutsideSubtreeScore = outsideSubtreeScore,
                SubtreeShell = new UGuiShellGeometrySnapshot
                {
                    LocalPath = "root/0",
                    ActualX = 0,
                    ActualY = 0,
                    ActualWidth = 60,
                    ActualHeight = 20,
                    ExpectedX = 0,
                    ExpectedY = 0,
                    ExpectedWidth = 60,
                    ExpectedHeight = 20
                }
            },
            new Dictionary<string, MeasuredLayoutComparison>(StringComparer.Ordinal));

    private static VisualDocument CreateVisualDocument()
        => new()
        {
            DocumentName = "PartyStatusStrip",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "root",
                SourceNodeId = "root",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                SemanticClass = "surface",
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container,
                    LayoutType = LayoutType.Horizontal,
                    Width = Dimension.Pixels(100),
                    Height = Dimension.Pixels(20)
                },
                EdgeContract = new EdgeContract
                {
                    Participation = LayoutParticipation.NormalFlow,
                    WidthSizing = AxisSizing.Fixed,
                    HeightSizing = AxisSizing.Fixed,
                    HorizontalPin = EdgePin.Start,
                    VerticalPin = EdgePin.Start,
                    OverflowX = OverflowBehavior.Visible,
                    OverflowY = OverflowBehavior.Visible,
                    WrapPressure = WrapPressurePolicy.Allow
                },
                Children =
                [
                    new VisualNode
                    {
                        StableId = "root/0",
                        SourceId = "member-card",
                        SourceNodeId = "member-card",
                        Kind = VisualNodeKind.Text,
                        SourceType = ComponentType.Label,
                        SemanticClass = "component",
                        Typography = new TypographyContract
                        {
                            SemanticClass = "component",
                            ResolvedFontSize = 12,
                            WrapText = false
                        },
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Label,
                            Width = Dimension.Pixels(60),
                            Height = Dimension.Pixels(20)
                        },
                        EdgeContract = new EdgeContract
                        {
                            Participation = LayoutParticipation.NormalFlow,
                            WidthSizing = AxisSizing.Fixed,
                            HeightSizing = AxisSizing.Fixed,
                            HorizontalPin = EdgePin.Start,
                            VerticalPin = EdgePin.Start,
                            OverflowX = OverflowBehavior.Visible,
                            OverflowY = OverflowBehavior.Visible,
                            WrapPressure = WrapPressurePolicy.Allow
                        }
                    }
                ]
            }
        };

    private static ActualLayoutSnapshot CreateActualLayout(double width, double childWidth, double childScaleX = 1d)
        => new()
        {
            Version = "1.0",
            BackendFamily = "ugui",
            TargetName = "PartyStatusStripRoot",
            Root = new ActualLayoutNode
            {
                LocalPath = "root",
                Name = "PartyStatusStripRoot",
                NodeType = "RectTransform",
                X = 0,
                Y = 0,
                Width = width,
                Height = 20,
                Children =
                [
                    new ActualLayoutNode
                    {
                        LocalPath = "root/0",
                        Name = "MemberCard",
                        NodeType = "Text",
                        X = 0,
                        Y = 0,
                        Width = childWidth,
                        Height = 20,
                        ScaleX = childScaleX,
                        ScaleY = 1,
                        FontSize = 12
                    }
                ]
            }
        };

    private static VisualDocument CreateCompositeVisualDocument()
        => new()
        {
            DocumentName = "CompositeSurface",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "root",
                SourceNodeId = "root",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                SemanticClass = "surface",
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container,
                    LayoutType = LayoutType.Horizontal,
                    Width = Dimension.Pixels(100),
                    Height = Dimension.Pixels(20)
                },
                EdgeContract = new EdgeContract
                {
                    Participation = LayoutParticipation.NormalFlow,
                    WidthSizing = AxisSizing.Fixed,
                    HeightSizing = AxisSizing.Fixed,
                    HorizontalPin = EdgePin.Start,
                    VerticalPin = EdgePin.Start,
                    OverflowX = OverflowBehavior.Visible,
                    OverflowY = OverflowBehavior.Visible,
                    WrapPressure = WrapPressurePolicy.Allow
                },
                Children =
                [
                    new VisualNode
                    {
                        StableId = "root/0",
                        SourceId = "member-card",
                        SourceNodeId = "member-card",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        SemanticClass = "component",
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container,
                            LayoutType = LayoutType.Horizontal,
                            Width = Dimension.Pixels(60),
                            Height = Dimension.Pixels(20)
                        },
                        EdgeContract = new EdgeContract
                        {
                            Participation = LayoutParticipation.NormalFlow,
                            WidthSizing = AxisSizing.Fixed,
                            HeightSizing = AxisSizing.Fixed,
                            HorizontalPin = EdgePin.Start,
                            VerticalPin = EdgePin.Start,
                            OverflowX = OverflowBehavior.Visible,
                            OverflowY = OverflowBehavior.Visible,
                            WrapPressure = WrapPressurePolicy.Allow
                        },
                        Children =
                        [
                            new VisualNode
                            {
                                StableId = "root/0/0",
                                SourceId = "left",
                                SourceNodeId = "left",
                                Kind = VisualNodeKind.Container,
                                SourceType = ComponentType.Panel,
                                SemanticClass = "motif",
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Panel,
                                    Width = Dimension.Pixels(30),
                                    Height = Dimension.Pixels(20)
                                },
                                EdgeContract = new EdgeContract
                                {
                                    Participation = LayoutParticipation.NormalFlow,
                                    WidthSizing = AxisSizing.Fixed,
                                    HeightSizing = AxisSizing.Fixed,
                                    HorizontalPin = EdgePin.Start,
                                    VerticalPin = EdgePin.Start,
                                    OverflowX = OverflowBehavior.Visible,
                                    OverflowY = OverflowBehavior.Visible,
                                    WrapPressure = WrapPressurePolicy.Allow
                                }
                            },
                            new VisualNode
                            {
                                StableId = "root/0/1",
                                SourceId = "right",
                                SourceNodeId = "right",
                                Kind = VisualNodeKind.Container,
                                SourceType = ComponentType.Panel,
                                SemanticClass = "motif",
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Panel,
                                    Width = Dimension.Pixels(30),
                                    Height = Dimension.Pixels(20)
                                },
                                EdgeContract = new EdgeContract
                                {
                                    Participation = LayoutParticipation.NormalFlow,
                                    WidthSizing = AxisSizing.Fixed,
                                    HeightSizing = AxisSizing.Fixed,
                                    HorizontalPin = EdgePin.Start,
                                    VerticalPin = EdgePin.Start,
                                    OverflowX = OverflowBehavior.Visible,
                                    OverflowY = OverflowBehavior.Visible,
                                    WrapPressure = WrapPressurePolicy.Allow
                                }
                            }
                        ]
                    }
                ]
            }
        };

    private static VisualDocument CreateCompositeVisualDocumentWithHuggingLeaf()
        => new()
        {
            DocumentName = "CompositeSurface",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "root",
                SourceNodeId = "root",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                SemanticClass = "surface",
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container,
                    LayoutType = LayoutType.Horizontal,
                    Width = Dimension.Pixels(100),
                    Height = Dimension.Pixels(20)
                },
                EdgeContract = new EdgeContract
                {
                    Participation = LayoutParticipation.NormalFlow,
                    WidthSizing = AxisSizing.Fixed,
                    HeightSizing = AxisSizing.Fixed,
                    HorizontalPin = EdgePin.Start,
                    VerticalPin = EdgePin.Start,
                    OverflowX = OverflowBehavior.Visible,
                    OverflowY = OverflowBehavior.Visible,
                    WrapPressure = WrapPressurePolicy.Allow
                },
                Children =
                [
                    new VisualNode
                    {
                        StableId = "root/0",
                        SourceId = "member-card",
                        SourceNodeId = "member-card",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        SemanticClass = "component",
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container,
                            LayoutType = LayoutType.Horizontal,
                            Width = Dimension.Pixels(60),
                            Height = Dimension.Pixels(20)
                        },
                        EdgeContract = new EdgeContract
                        {
                            Participation = LayoutParticipation.NormalFlow,
                            WidthSizing = AxisSizing.Fixed,
                            HeightSizing = AxisSizing.Fixed,
                            HorizontalPin = EdgePin.Start,
                            VerticalPin = EdgePin.Start,
                            OverflowX = OverflowBehavior.Visible,
                            OverflowY = OverflowBehavior.Visible,
                            WrapPressure = WrapPressurePolicy.Allow
                        },
                        Children =
                        [
                            new VisualNode
                            {
                                StableId = "root/0/0",
                                SourceId = "left",
                                SourceNodeId = "left",
                                Kind = VisualNodeKind.Container,
                                SourceType = ComponentType.Panel,
                                SemanticClass = "motif",
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Panel,
                                    Width = Dimension.Pixels(30),
                                    Height = Dimension.Pixels(20)
                                },
                                EdgeContract = new EdgeContract
                                {
                                    Participation = LayoutParticipation.NormalFlow,
                                    WidthSizing = AxisSizing.Fixed,
                                    HeightSizing = AxisSizing.Fixed,
                                    HorizontalPin = EdgePin.Start,
                                    VerticalPin = EdgePin.Start,
                                    OverflowX = OverflowBehavior.Visible,
                                    OverflowY = OverflowBehavior.Visible,
                                    WrapPressure = WrapPressurePolicy.Allow
                                }
                            },
                            new VisualNode
                            {
                                StableId = "root/0/1",
                                SourceId = "right",
                                SourceNodeId = "right",
                                Kind = VisualNodeKind.Text,
                                SourceType = ComponentType.Label,
                                SemanticClass = "motif",
                                Typography = new TypographyContract
                                {
                                    SemanticClass = "motif",
                                    ResolvedFontSize = 12,
                                    WrapText = false
                                },
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Label,
                                    Width = null,
                                    Height = null
                                },
                                EdgeContract = new EdgeContract
                                {
                                    Participation = LayoutParticipation.NormalFlow,
                                    WidthSizing = AxisSizing.Hug,
                                    HeightSizing = AxisSizing.Hug,
                                    HorizontalPin = EdgePin.Start,
                                    VerticalPin = EdgePin.Start,
                                    OverflowX = OverflowBehavior.Visible,
                                    OverflowY = OverflowBehavior.Visible,
                                    WrapPressure = WrapPressurePolicy.Allow
                                }
                            }
                        ]
                    }
                ]
            }
        };

    private static ActualLayoutSnapshot CreateCompositeActualLayout(double leftChildWidth, double rightChildWidth, double rightChildX = 30, double parentX = 0)
        => new()
        {
            Version = "1.0",
            BackendFamily = "ugui",
            TargetName = "CompositeSurfaceRoot",
            Root = new ActualLayoutNode
            {
                LocalPath = "root",
                Name = "CompositeSurfaceRoot",
                NodeType = "RectTransform",
                X = 0,
                Y = 0,
                Width = 100,
                Height = 20,
                Children =
                [
                    new ActualLayoutNode
                    {
                        LocalPath = "root/0",
                        Name = "MemberCard",
                        NodeType = "Image",
                        X = parentX,
                        Y = 0,
                        Width = 60,
                        Height = 20,
                        Children =
                        [
                            new ActualLayoutNode
                            {
                                LocalPath = "root/0/0",
                                Name = "Left",
                                NodeType = "Image",
                                X = 0,
                                Y = 0,
                                Width = leftChildWidth,
                                Height = 20
                            },
                            new ActualLayoutNode
                            {
                                LocalPath = "root/0/1",
                                Name = "Right",
                                NodeType = "Image",
                                X = rightChildX,
                                Y = 0,
                                Width = rightChildWidth,
                                Height = 20
                            }
                        ]
                    }
                ]
            }
        };

    private static void CreateSurfaceImage(string path, int totalWidth, int totalHeight, int filledWidth)
    {
        using var image = new Image<Rgba32>(totalWidth, totalHeight, new Rgba32(255, 255, 255, 255));
        for (var y = 0; y < totalHeight; y++)
        {
            for (var x = 0; x < filledWidth; x++)
            {
                image[x, y] = new Rgba32(0, 0, 0, 255);
            }
        }

        image.SaveAsPng(path);
    }

    private static void CreateCompositeSurfaceImage(string path, int totalWidth, int totalHeight, int leftChildWidth, int rightChildWidth, int rightChildX = 30, int parentX = 0)
    {
        using var image = new Image<Rgba32>(totalWidth, totalHeight, new Rgba32(255, 255, 255, 255));
        for (var y = 0; y < totalHeight; y++)
        {
            for (var x = parentX; x < parentX + leftChildWidth; x++)
            {
                image[x, y] = new Rgba32(0, 0, 0, 255);
            }

            for (var x = parentX + rightChildX; x < parentX + rightChildX + rightChildWidth; x++)
            {
                image[x, y] = new Rgba32(128, 128, 128, 255);
            }
        }

        image.SaveAsPng(path);
    }
}
