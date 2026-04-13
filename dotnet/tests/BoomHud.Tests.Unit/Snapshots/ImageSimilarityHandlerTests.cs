using System.Text.Json;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Cli.Handlers.Baseline;
using BoomHud.Generators;
using BoomHud.Generators.VisualIR;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace BoomHud.Tests.Unit.Snapshots;

public sealed class ImageSimilarityHandlerTests : IDisposable
{
    private readonly string _tempDir;

    public ImageSimilarityHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"boomhud-image-score-{Guid.NewGuid():N}");
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
    public void Execute_WithIdenticalImages_ReportsHundredPercentSimilarity()
    {
        var referencePath = Path.Combine(_tempDir, "reference.png");
        var candidatePath = Path.Combine(_tempDir, "candidate.png");
        var reportPath = Path.Combine(_tempDir, "report.json");

        CreatePng(referencePath, 16, 16, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 16, 16, new Rgba32(255, 255, 255, 255));

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(0);

        var report = LoadReport(reportPath);
        report.PixelIdentityPercent.Should().Be(100);
        report.DeltaSimilarityPercent.Should().Be(100);
        report.OverallSimilarityPercent.Should().Be(100);
        report.Metrics.ChangedPixels.Should().Be(0);
        report.Findings.Should().BeEmpty();
        report.Analysis.Should().NotBeNull();
    }

    [Fact]
    public void Execute_WithDifferentImages_WritesReportAndDiff()
    {
        var referencePath = Path.Combine(_tempDir, "reference.png");
        var candidatePath = Path.Combine(_tempDir, "candidate.png");
        var reportPath = Path.Combine(_tempDir, "report.json");
        var diffPath = Path.Combine(_tempDir, "diff.png");

        CreatePng(referencePath, 16, 16, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 16, 16, new Rgba32(0, 0, 0, 255));

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            DiffFile = new FileInfo(diffPath),
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(0);
        File.Exists(diffPath).Should().BeTrue();

        var report = LoadReport(reportPath);
        report.Metrics.ChangedPercent.Should().BeGreaterThan(0);
        report.OverallSimilarityPercent.Should().BeLessThan(100);
        report.DiffPath.Should().Be(diffPath);
        report.Findings.Should().NotBeEmpty();
    }

    [Fact]
    public void Execute_WithNormalizeStretch_MakesMismatchedSolidImagesComparable()
    {
        var referencePath = Path.Combine(_tempDir, "reference.png");
        var candidatePath = Path.Combine(_tempDir, "candidate.png");
        var reportPath = Path.Combine(_tempDir, "report-normalized.json");

        CreatePng(referencePath, 16, 16, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 8, 8, new Rgba32(255, 255, 255, 255));

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            NormalizeMode = "stretch",
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(0);

        var report = LoadReport(reportPath);
        report.Normalization.Should().NotBeNull();
        report.Normalization!.Mode.Should().Be("stretch");
        report.Metrics.DimensionsMatch.Should().BeTrue();
        report.OverallSimilarityPercent.Should().Be(100);
        report.Findings.Should().NotContain(f => f.Category == "dimension-mismatch");
    }

    [Fact]
    public void Execute_WithFailBelow_ReturnsThresholdExitCode()
    {
        var referencePath = Path.Combine(_tempDir, "reference-threshold.png");
        var candidatePath = Path.Combine(_tempDir, "candidate-threshold.png");
        var reportPath = Path.Combine(_tempDir, "report-threshold.json");

        CreatePng(referencePath, 16, 16, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 16, 16, new Rgba32(0, 0, 0, 255));

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            FailBelowOverallPercent = 95,
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(2);

        var report = LoadReport(reportPath);
        report.FailBelowOverallPercent.Should().Be(95);
        report.PassedThreshold.Should().BeFalse();
    }

    [Fact]
    public void Execute_WithDimensionMismatch_ReportsDimensionFinding()
    {
        var referencePath = Path.Combine(_tempDir, "reference-dimension.png");
        var candidatePath = Path.Combine(_tempDir, "candidate-dimension.png");
        var reportPath = Path.Combine(_tempDir, "report-dimension.json");

        CreatePng(referencePath, 16, 16, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 8, 8, new Rgba32(255, 255, 255, 255));

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(0);

        var report = LoadReport(reportPath);
        report.Findings.Should().Contain(f => f.Category == "dimension-mismatch");
    }

    [Fact]
    public void Execute_WithRightEdgeDrift_ReportsEdgeFinding()
    {
        var referencePath = Path.Combine(_tempDir, "reference-right-edge.png");
        var candidatePath = Path.Combine(_tempDir, "candidate-right-edge.png");
        var reportPath = Path.Combine(_tempDir, "report-right-edge.json");

        CreatePng(referencePath, 20, 20, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 20, 20, new Rgba32(255, 255, 255, 255));

        using (var candidate = Image.Load<Rgba32>(candidatePath))
        {
            for (var y = 0; y < candidate.Height; y++)
            {
                for (var x = candidate.Width - 2; x < candidate.Width; x++)
                {
                    candidate[x, y] = new Rgba32(0, 0, 0, 255);
                }
            }

            candidate.SaveAsPng(candidatePath);
        }

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(0);

        var report = LoadReport(reportPath);
        report.Findings.Should().Contain(f => f.Category == "edge-alignment-mismatch" && f.Region == "right-edge");
    }

    [Fact]
    public void Execute_WithLocalizedQuadrantDrift_BuildsRecursivePhaseAnalysis()
    {
        var referencePath = Path.Combine(_tempDir, "reference-recursive.png");
        var candidatePath = Path.Combine(_tempDir, "candidate-recursive.png");
        var reportPath = Path.Combine(_tempDir, "report-recursive.json");

        CreatePng(referencePath, 128, 128, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 128, 128, new Rgba32(255, 255, 255, 255));

        using (var candidate = Image.Load<Rgba32>(candidatePath))
        {
            for (var y = 64; y < 96; y++)
            {
                for (var x = 64; x < 96; x++)
                {
                    candidate[x, y] = new Rgba32(0, 0, 0, 255);
                }
            }

            candidate.SaveAsPng(candidatePath);
        }

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(0);

        var report = LoadReport(reportPath);
        report.RecursiveAnalysis.Should().NotBeNull();
        report.RecursiveAnalysis!.Level.Should().Be("screen/frame");
        report.RecursiveAnalysis.Phases.Should().HaveCount(5);
        report.RecursiveAnalysis.Phases.Select(phase => phase.Phase).Should().Equal(
            "structural-match",
            "outer-frame-match",
            "inner-layout-match",
            "text-icon-metrics",
            "polish-offsets");
        report.RecursiveAnalysis.Children.Should().HaveCount(4);
        report.RecursiveAnalysis.Children.Should().Contain(child => child.Level == "panel" && child.Children.Count == 4);
    }

    [Fact]
    public void Execute_WithOneBadInnerQuadrant_LocalizesLowerRecursiveScore()
    {
        var referencePath = Path.Combine(_tempDir, "reference-localized.png");
        var candidatePath = Path.Combine(_tempDir, "candidate-localized.png");
        var reportPath = Path.Combine(_tempDir, "report-localized.json");

        CreatePng(referencePath, 128, 128, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 128, 128, new Rgba32(255, 255, 255, 255));

        using (var candidate = Image.Load<Rgba32>(candidatePath))
        {
            for (var y = 96; y < 128; y++)
            {
                for (var x = 96; x < 128; x++)
                {
                    candidate[x, y] = new Rgba32(0, 0, 0, 255);
                }
            }

            candidate.SaveAsPng(candidatePath);
        }

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(0);

        var report = LoadReport(reportPath);
        var children = report.RecursiveAnalysis!.Children.OrderBy(child => child.Bounds.Y).ThenBy(child => child.Bounds.X).ToList();
        children.Should().HaveCount(4);
        children.Last().OverallSimilarityPercent.Should().BeLessThan(children.First().OverallSimilarityPercent);
        children.Last().Children.Should().Contain(grandChild => grandChild.OverallSimilarityPercent < 100);
    }

    [Fact]
    public void Execute_RepeatedRuns_ProduceDeterministicRecursiveAnalysis()
    {
        var referencePath = Path.Combine(_tempDir, "reference-deterministic.png");
        var candidatePath = Path.Combine(_tempDir, "candidate-deterministic.png");
        var reportPathOne = Path.Combine(_tempDir, "report-deterministic-1.json");
        var reportPathTwo = Path.Combine(_tempDir, "report-deterministic-2.json");

        CreatePng(referencePath, 128, 128, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 128, 128, new Rgba32(255, 255, 255, 255));

        using (var candidate = Image.Load<Rgba32>(candidatePath))
        {
            for (var y = 32; y < 64; y++)
            {
                for (var x = 80; x < 112; x++)
                {
                    candidate[x, y] = new Rgba32(0, 0, 0, 255);
                }
            }

            candidate.SaveAsPng(candidatePath);
        }

        ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPathOne),
            PrintSummary = false,
            Tolerance = 0
        }).Should().Be(0);

        ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPathTwo),
            PrintSummary = false,
            Tolerance = 0
        }).Should().Be(0);

        var first = LoadReport(reportPathOne);
        var second = LoadReport(reportPathTwo);

        JsonSerializer.Serialize(first.RecursiveAnalysis).Should().Be(
            JsonSerializer.Serialize(second.RecursiveAnalysis));
    }

    [Fact]
    public void Execute_WithVisualIr_EmitsVisualRefinementArtifactFromRecursiveScoreTree()
    {
        var referencePath = Path.Combine(_tempDir, "reference-visual-refinement.png");
        var candidatePath = Path.Combine(_tempDir, "candidate-visual-refinement.png");
        var reportPath = Path.Combine(_tempDir, "report-visual-refinement.json");
        var visualIrPath = Path.Combine(_tempDir, "QuestHud.visual-ir.json");
        var refinementPath = Path.Combine(_tempDir, "QuestHud.visual-refinement.json");

        CreatePng(referencePath, 128, 128, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 128, 128, new Rgba32(255, 255, 255, 255));

        using (var candidate = Image.Load<Rgba32>(candidatePath))
        {
            for (var y = 64; y < 96; y++)
            {
                for (var x = 64; x < 96; x++)
                {
                    candidate[x, y] = new Rgba32(0, 0, 0, 255);
                }
            }

            candidate.SaveAsPng(candidatePath);
        }

        var visual = VisualDocumentBuilder.Build(
            new HudDocument
            {
                Name = "QuestHud",
                Root = new ComponentNode
                {
                    Id = "root",
                    Type = ComponentType.Container,
                    Children =
                    [
                        new ComponentNode
                        {
                            Id = "title",
                            Type = ComponentType.Label,
                            Properties = new Dictionary<string, BindableValue<object?>>
                            {
                                ["Text"] = "QUEST"
                            }
                        }
                    ]
                }
            },
            new GenerationOptions(),
            "unity");

        File.WriteAllText(visualIrPath, GenerationDocumentPreprocessor.ToJson(visual));

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            VisualIrFile = new FileInfo(visualIrPath),
            VisualRefinementOutFile = new FileInfo(refinementPath),
            PrintSummary = false,
            Tolerance = 0,
            VisualRefinementIterationBudget = 2
        });

        exitCode.Should().Be(0);
        File.Exists(refinementPath).Should().BeTrue();

        var refinement = JsonSerializer.Deserialize<VisualRefinementSummary>(File.ReadAllText(refinementPath));
        refinement.Should().NotBeNull();
        refinement!.IterationBudget.Should().Be(2);
        refinement.ScoreTree.Should().NotBeNull();
        refinement.ScoreTree!.Level.Should().Be("screen/frame");
        refinement.Actions.Should().NotBeEmpty();
        refinement.Actions.Should().OnlyContain(action => !string.IsNullOrWhiteSpace(action.ReasonPhase));
    }

    [Fact]
    public void Execute_WithActualLayout_EmitsMeasuredLayoutArtifact()
    {
        var referencePath = Path.Combine(_tempDir, "reference-measured-layout.png");
        var candidatePath = Path.Combine(_tempDir, "candidate-measured-layout.png");
        var reportPath = Path.Combine(_tempDir, "report-measured-layout.json");
        var visualIrPath = Path.Combine(_tempDir, "QuestSidebar.visual-ir.json");
        var actualLayoutPath = Path.Combine(_tempDir, "QuestSidebar.layout.actual.json");
        var measuredLayoutPath = Path.Combine(_tempDir, "QuestSidebar.measured-layout.json");

        CreatePng(referencePath, 64, 64, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 64, 64, new Rgba32(255, 255, 255, 255));

        var visual = VisualDocumentBuilder.Build(
            new HudDocument
            {
                Name = "QuestSidebar",
                Root = new ComponentNode
                {
                    Id = "QuestSidebar",
                    Type = ComponentType.Container,
                    Layout = new LayoutSpec
                    {
                        Padding = new Spacing(12)
                    },
                    Children =
                    [
                        new ComponentNode
                        {
                            Id = "Title",
                            Type = ComponentType.Label,
                            Properties = new Dictionary<string, BindableValue<object?>>
                            {
                                ["Text"] = "QUEST"
                            },
                            Style = new StyleSpec
                            {
                                FontSize = 14
                            }
                        }
                    ]
                }
            },
            new GenerationOptions(),
            "ugui");

        File.WriteAllText(visualIrPath, GenerationDocumentPreprocessor.ToJson(visual));
        File.WriteAllText(
            actualLayoutPath,
            JsonSerializer.Serialize(
                new ActualLayoutSnapshot
                {
                    Version = "1.0",
                    BackendFamily = "ugui",
                    CaptureId = "quest-sidebar-ugui",
                    TargetName = "QuestSidebar",
                    Root = new ActualLayoutNode
                    {
                        LocalPath = "root",
                        Name = "QuestSidebar",
                        NodeType = "RectTransform",
                        X = 0,
                        Y = 0,
                        Width = 220,
                        Height = 100,
                        Children =
                        [
                            new ActualLayoutNode
                            {
                                LocalPath = "root/0",
                                Name = "Title",
                                NodeType = "Text",
                                X = 0,
                                Y = 8,
                                Width = 196,
                                Height = 20,
                                PreferredWidth = 240,
                                PreferredHeight = 20,
                                FontSize = 12,
                                WrapText = false,
                                ClipContent = false,
                                Text = "QUEST"
                            }
                        ]
                    }
                }));

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            VisualIrFile = new FileInfo(visualIrPath),
            ActualLayoutFile = new FileInfo(actualLayoutPath),
            MeasuredLayoutOutFile = new FileInfo(measuredLayoutPath),
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(0);
        File.Exists(measuredLayoutPath).Should().BeTrue();

        var measured = JsonSerializer.Deserialize<MeasuredLayoutReport>(File.ReadAllText(measuredLayoutPath));
        measured.Should().NotBeNull();
        measured!.Issues.Should().Contain(issue => issue.Category == "start-edge-underflow");
        measured.Issues.Should().Contain(issue => issue.Category == "wrap-pressure-risk");
        measured.Issues.Should().Contain(issue => issue.Category == "font-size-drift");
    }

    [Fact]
    public void BuildMeasuredLayoutReport_ExpandsComponentRefChildrenBeforeComparing()
    {
        var visual = new VisualDocument
        {
            DocumentName = "QuestSidebar",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "QuestSidebar",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container
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
                        SourceId = "Row0",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        ComponentRefId = "synthetic:row",
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container
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
            },
            Components =
            [
                new VisualComponentDefinition
                {
                    Id = "synthetic:row",
                    Name = "SyntheticRow",
                    Root = new VisualNode
                    {
                        StableId = "component:synthetic:row",
                        SourceId = "RowTemplate",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container
                        },
                        EdgeContract = new EdgeContract
                        {
                            Participation = LayoutParticipation.NormalFlow,
                            WidthSizing = AxisSizing.Fill,
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
                                StableId = "component:synthetic:row/0",
                                SourceId = "Cell0",
                                Kind = VisualNodeKind.Text,
                                SourceType = ComponentType.Label,
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Label
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
                                Typography = new TypographyContract
                                {
                                    SemanticClass = "pixel-text",
                                    ResolvedFontSize = 12,
                                    WrapText = false
                                }
                            }
                        ]
                    }
                }
            ]
        };

        var actualLayout = new ActualLayoutSnapshot
        {
            Version = "1.0",
            BackendFamily = "ugui",
            CaptureId = "quest-sidebar-ugui",
            TargetName = "QuestSidebarRoot",
            Root = new ActualLayoutNode
            {
                LocalPath = "root",
                Name = "QuestSidebarRoot",
                NodeType = "Image",
                X = 0,
                Y = 0,
                Width = 220,
                Height = 100,
                Children =
                [
                    new ActualLayoutNode
                    {
                        LocalPath = "root/0",
                        Name = "Row0",
                        NodeType = "Image",
                        X = 8,
                        Y = 8,
                        Width = 204,
                        Height = 24,
                        Children =
                        [
                            new ActualLayoutNode
                            {
                                LocalPath = "root/0/0",
                                Name = "Cell0",
                                NodeType = "Text",
                                X = 0,
                                Y = 0,
                                Width = 32,
                                Height = 12,
                                PreferredWidth = 32,
                                PreferredHeight = 12,
                                FontSize = 12
                            }
                        ]
                    }
                ]
            }
        };

        var report = ImageSimilarityHandler.BuildMeasuredLayoutReport(visual, actualLayout);
        var rowComparison = report.Comparisons.Single(comparison => comparison.LocalPath == "root/0");

        rowComparison.ExpectedWidthSizing.Should().Be(AxisSizing.Fill);
        rowComparison.ExpectedChildCount.Should().Be(1);
        report.Issues.Should().NotContain(issue => issue.Category == "child-structure-mismatch" && issue.LocalPath == "root/0");
    }

    [Fact]
    public void BuildMeasuredLayoutReport_UsesSiblingWidthsWhenEstimatingHorizontalFillSpace()
    {
        var visual = new VisualDocument
        {
            DocumentName = "ObjectiveRow",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "ObjectiveRow",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container,
                    LayoutType = LayoutType.Horizontal,
                    Gap = new Spacing(10)
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
                        SourceId = "IconShell",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container,
                            Width = Dimension.Pixels(44),
                            Height = Dimension.Pixels(44)
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
                        StableId = "root/1",
                        SourceId = "ObjectiveText",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container
                        },
                        EdgeContract = new EdgeContract
                        {
                            Participation = LayoutParticipation.NormalFlow,
                            WidthSizing = AxisSizing.Fill,
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

        var actualLayout = new ActualLayoutSnapshot
        {
            Version = "1.0",
            BackendFamily = "ugui",
            CaptureId = "objective-row",
            TargetName = "ObjectiveRowRoot",
            Root = new ActualLayoutNode
            {
                LocalPath = "root",
                Name = "ObjectiveRowRoot",
                NodeType = "Image",
                X = 0,
                Y = 0,
                Width = 364,
                Height = 44,
                Children =
                [
                    new ActualLayoutNode
                    {
                        LocalPath = "root/0",
                        Name = "IconShell",
                        NodeType = "Image",
                        X = 0,
                        Y = 0,
                        Width = 44,
                        Height = 44
                    },
                    new ActualLayoutNode
                    {
                        LocalPath = "root/1",
                        Name = "ObjectiveText",
                        NodeType = "Image",
                        X = 54,
                        Y = 0,
                        Width = 310,
                        Height = 44
                    }
                ]
            }
        };

        var report = ImageSimilarityHandler.BuildMeasuredLayoutReport(visual, actualLayout);
        var textComparison = report.Comparisons.Single(comparison => comparison.LocalPath == "root/1");

        textComparison.ExpectedStartInsetX.Should().Be(54);
        textComparison.ExpectedAvailableWidth.Should().Be(310);
        report.Issues.Should().NotContain(issue => issue.Category == "fill-underflow" && issue.LocalPath == "root/1");
        report.Issues.Should().NotContain(issue => issue.Category == "start-edge-overshift" && issue.LocalPath == "root/1");
    }

    [Fact]
    public void BuildMeasuredLayoutReport_IgnoresSyntheticBorderChromeChildren()
    {
        var visual = new VisualDocument
        {
            DocumentName = "QuestSidebar",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "QuestSidebar",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container
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
                        SourceId = "Body",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container
                        },
                        EdgeContract = new EdgeContract
                        {
                            Participation = LayoutParticipation.NormalFlow,
                            WidthSizing = AxisSizing.Fill,
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

        var actualLayout = new ActualLayoutSnapshot
        {
            Version = "1.0",
            BackendFamily = "ugui",
            CaptureId = "quest-sidebar-ugui",
            TargetName = "QuestSidebarRoot",
            Root = new ActualLayoutNode
            {
                LocalPath = "root",
                Name = "QuestSidebarRoot",
                NodeType = "Image",
                X = 0,
                Y = 0,
                Width = 220,
                Height = 100,
                Children =
                [
                    new ActualLayoutNode
                    {
                        LocalPath = "root/0",
                        Name = "__Border",
                        NodeType = "RectTransform",
                        X = 0,
                        Y = 0,
                        Width = 220,
                        Height = 100,
                        Children =
                        [
                            new ActualLayoutNode
                            {
                                LocalPath = "root/0/0",
                                Name = "Top",
                                NodeType = "Image",
                                X = 0,
                                Y = 0,
                                Width = 220,
                                Height = 2
                            }
                        ]
                    },
                    new ActualLayoutNode
                    {
                        LocalPath = "root/1",
                        Name = "Body",
                        NodeType = "Image",
                        X = 8,
                        Y = 8,
                        Width = 204,
                        Height = 24
                    }
                ]
            }
        };

        var report = ImageSimilarityHandler.BuildMeasuredLayoutReport(visual, actualLayout);

        report.Comparisons.Should().ContainSingle(comparison => comparison.LocalPath == "root/0" && comparison.ActualName == "Body");
        report.Issues.Should().NotContain(issue => issue.Category == "child-structure-mismatch" && issue.LocalPath == "root");
        report.Comparisons.Should().NotContain(comparison => comparison.ActualName == "__Border" || comparison.ActualName == "Top");
    }

    [Fact]
    public void BuildMeasuredLayoutReport_DoesNotFlagOvershiftWhenVisualIrCarriesAbsoluteOffsets()
    {
        var visual = new VisualDocument
        {
            DocumentName = "QuestSidebar",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "QuestSidebar",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container
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
                        SourceId = "HealthBar",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container
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
                                SourceId = "HealthText",
                                Kind = VisualNodeKind.Text,
                                SourceType = ComponentType.Label,
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Label,
                                    Left = Dimension.Pixels(12),
                                    Top = Dimension.Pixels(4),
                                    IsAbsolutePositioned = true
                                },
                                EdgeContract = new EdgeContract
                                {
                                    Participation = LayoutParticipation.Overlay,
                                    WidthSizing = AxisSizing.Hug,
                                    HeightSizing = AxisSizing.Hug,
                                    HorizontalPin = EdgePin.Start,
                                    VerticalPin = EdgePin.Start,
                                    OverflowX = OverflowBehavior.Visible,
                                    OverflowY = OverflowBehavior.Visible,
                                    WrapPressure = WrapPressurePolicy.Allow
                                },
                                Typography = new TypographyContract
                                {
                                    SemanticClass = "pixel-text",
                                    ResolvedFontSize = 8,
                                    WrapText = false
                                }
                            }
                        ]
                    }
                ]
            }
        };

        var actualLayout = new ActualLayoutSnapshot
        {
            Version = "1.0",
            BackendFamily = "ugui",
            CaptureId = "quest-sidebar-ugui",
            TargetName = "QuestSidebarRoot",
            Root = new ActualLayoutNode
            {
                LocalPath = "root",
                Name = "QuestSidebarRoot",
                NodeType = "Image",
                X = 0,
                Y = 0,
                Width = 388,
                Height = 164,
                Children =
                [
                    new ActualLayoutNode
                    {
                        LocalPath = "root/0",
                        Name = "HealthBar",
                        NodeType = "Image",
                        X = 12,
                        Y = 31,
                        Width = 364,
                        Height = 22,
                        Children =
                        [
                            new ActualLayoutNode
                            {
                                LocalPath = "root/0/0",
                                Name = "HealthText",
                                NodeType = "Text",
                                X = 12,
                                Y = 4,
                                Width = 100,
                                Height = 100,
                                PreferredWidth = 80,
                                PreferredHeight = 8,
                                FontSize = 8,
                                WrapText = false
                            }
                        ]
                    }
                ]
            }
        };

        var report = ImageSimilarityHandler.BuildMeasuredLayoutReport(visual, actualLayout);

        report.Issues.Should().NotContain(issue => issue.Category == "start-edge-overshift" && issue.LocalPath == "root/0/0");
        report.Comparisons.Single(comparison => comparison.LocalPath == "root/0/0").ExpectedStartInsetX.Should().Be(12);
        report.Comparisons.Single(comparison => comparison.LocalPath == "root/0/0").ExpectedStartInsetY.Should().Be(4);
    }

    [Fact]
    public void BuildMeasuredLayoutReport_PromotesShellOverflowIntoMeasuredIssues()
    {
        var visual = new VisualDocument
        {
            DocumentName = "PartyStrip",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "PartyStrip",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container
                },
                EdgeContract = new EdgeContract
                {
                    Participation = LayoutParticipation.NormalFlow,
                    WidthSizing = AxisSizing.Fill,
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
                        SourceId = "MemberA",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container
                        },
                        EdgeContract = new EdgeContract
                        {
                            Participation = LayoutParticipation.NormalFlow,
                            WidthSizing = AxisSizing.Fixed,
                            HeightSizing = AxisSizing.Fill,
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
                                SourceId = "HeroRow",
                                Kind = VisualNodeKind.Container,
                                SourceType = ComponentType.Container,
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Container
                                },
                                EdgeContract = new EdgeContract
                                {
                                    Participation = LayoutParticipation.NormalFlow,
                                    WidthSizing = AxisSizing.Fill,
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

        var actualLayout = new ActualLayoutSnapshot
        {
            Version = "1.0",
            BackendFamily = "ugui",
            Root = new ActualLayoutNode
            {
                LocalPath = "root",
                Name = "PartyStrip",
                NodeType = "Image",
                X = 0,
                Y = 0,
                Width = 1280,
                Height = 320,
                Children =
                [
                    new ActualLayoutNode
                    {
                        LocalPath = "root/0",
                        Name = "MemberA",
                        NodeType = "Image",
                        X = 0,
                        Y = 0,
                        Width = 400,
                        Height = 216,
                        PreferredWidth = 400,
                        PreferredHeight = 236,
                        Children =
                        [
                            new ActualLayoutNode
                            {
                                LocalPath = "root/0/0",
                                Name = "HeroRow",
                                NodeType = "Image",
                                X = 12,
                                Y = 12,
                                Width = 376,
                                Height = 68,
                                PreferredWidth = 250,
                                PreferredHeight = 76
                            }
                        ]
                    }
                ]
            }
        };

        var report = ImageSimilarityHandler.BuildMeasuredLayoutReport(visual, actualLayout);

        report.Issues.Should().Contain(issue => issue.Category == "height-collapsed-vs-preferred" && issue.LocalPath == "root/0");
        report.Issues.Should().Contain(issue => issue.Category == "portrait-or-status-row-shell-drift" && issue.LocalPath == "root/0/0");
        report.Issues.Should().Contain(issue => issue.Category == "start-edge-overshift" && issue.LocalPath == "root/0/0");
        report.Issues.Should().NotContain(issue => issue.Category == "width-stretched-vs-preferred" && issue.LocalPath == "root/0/0");
        report.Issues.Should().NotContain(issue => issue.Category == "shell-padding-or-child-stack-mismatch" && issue.LocalPath == "root/0/0");
    }

    [Fact]
    public void BuildMeasuredLayoutReport_FlagsScaledShellRealization()
    {
        var visual = new VisualDocument
        {
            DocumentName = "PartyStrip",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "PartyStrip",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container
                },
                EdgeContract = new EdgeContract
                {
                    Participation = LayoutParticipation.NormalFlow,
                    WidthSizing = AxisSizing.Fill,
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
                        SourceId = "MemberA",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container
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

        var actualLayout = new ActualLayoutSnapshot
        {
            Version = "1.0",
            BackendFamily = "ugui",
            Root = new ActualLayoutNode
            {
                LocalPath = "root",
                Name = "PartyStripRoot",
                NodeType = "Image",
                X = 0,
                Y = 0,
                Width = 1280,
                Height = 320,
                ScaleX = 0.66,
                ScaleY = 0.66,
                Children =
                [
                    new ActualLayoutNode
                    {
                        LocalPath = "root/0",
                        Name = "MemberA",
                        NodeType = "Image",
                        X = 0,
                        Y = 0,
                        Width = 400,
                        Height = 236
                    }
                ]
            }
        };

        var report = ImageSimilarityHandler.BuildMeasuredLayoutReport(visual, actualLayout);

        report.Issues.Should().Contain(issue => issue.Category == "realization-scale-mismatch" && issue.LocalPath == "root");
        report.Comparisons.Single(comparison => comparison.LocalPath == "root").ActualScaleX.Should().Be(0.66);
        report.Comparisons.Single(comparison => comparison.LocalPath == "root").ActualScaleY.Should().Be(0.66);
    }

    [Fact]
    public void ConvertRecursiveAnalysis_MapsBoundsIntoDeterministicRegionIds()
    {
        var converted = ImageSimilarityHandler.ConvertRecursiveAnalysis(new ImageSimilarityRecursiveScoreNode
        {
            Level = "panel",
            Bounds = new ImageSimilarityBounds
            {
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40
            },
            OverallSimilarityPercent = 75,
            Phases =
            [
                new ImageSimilarityPhaseScore
                {
                    Phase = "inner-layout-match",
                    SimilarityPercent = 61
                }
            ]
        });

        converted.Should().NotBeNull();
        converted!.RegionId.Should().Be("panel@10,20,30x40");
        converted.Phases.Should().ContainSingle(phase => phase.Phase == "inner-layout-match" && phase.SimilarityPercent == 61);
    }

    private static void CreatePng(string path, int width, int height, Rgba32 color)
    {
        using var image = new Image<Rgba32>(width, height, color);
        image.SaveAsPng(path);
    }

    private static ImageSimilarityReport LoadReport(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ImageSimilarityReport>(json)!;
    }
}
