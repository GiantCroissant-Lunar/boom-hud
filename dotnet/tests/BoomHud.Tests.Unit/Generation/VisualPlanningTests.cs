using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Generators;
using BoomHud.Generators.VisualIR;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public sealed class VisualPlanningTests
{
    [Fact]
    public void Synthesize_RepeatedSameShapeCardsWithDifferentTextIconAndValue_DoesNotYetLiftParametricFamily()
    {
        var document = new HudDocument
        {
            Name = "QuestHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    CreateQuestCard("card-a", "QUEST", "shield", 35),
                    CreateQuestCard("card-b", "BONUS", "sparkles", 72)
                ]
            }
        };

        var visual = VisualDocumentBuilder.Build(document, new GenerationOptions(), "react");

        var synthesized = VisualSynthesisPlanner.Synthesize(visual);

        synthesized.Summary.ChosenFamilyCount.Should().Be(0);
        synthesized.Summary.RewrittenOccurrenceCount.Should().Be(0);
        synthesized.Document.Components.Should().BeEmpty();
    }

    [Fact]
    public void Synthesize_UnsupportedDifferences_BlockParametricSynthesis()
    {
        var document = new HudDocument
        {
            Name = "QuestHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    CreateQuestCard("card-a", "QUEST", "shield", 35),
                    CreateQuestCard("card-b", "QUEST", "shield", 35, gap: 12)
                ]
            }
        };

        var visual = VisualDocumentBuilder.Build(document, new GenerationOptions(), "react");
        var synthesized = VisualSynthesisPlanner.Synthesize(visual);

        synthesized.Summary.ChosenFamilyCount.Should().Be(0);
        synthesized.Document.Components.Should().BeEmpty();
    }

    [Fact]
    public void RefinementPlan_OrdersWeakestPhasesDeterministically()
    {
        var document = new VisualDocument
        {
            DocumentName = "QuestHud",
            BackendFamily = "unity",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox { SourceType = ComponentType.Container },
                EdgeContract = new EdgeContract
                {
                    Participation = LayoutParticipation.NormalFlow,
                    WidthSizing = AxisSizing.Fill,
                    HeightSizing = AxisSizing.Fill,
                    HorizontalPin = EdgePin.Start,
                    VerticalPin = EdgePin.Start,
                    OverflowX = OverflowBehavior.Visible,
                    OverflowY = OverflowBehavior.Visible,
                    WrapPressure = WrapPressurePolicy.Allow
                }
            }
        };

        var scoreTree = new RecursiveFidelityScoreNode
        {
            Level = "panel",
            RegionId = "root",
            OverallSimilarityPercent = 76,
            Phases =
            [
                new RecursiveFidelityPhaseScore { Phase = "inner-layout-match", SimilarityPercent = 52 },
                new RecursiveFidelityPhaseScore { Phase = "text-icon-metrics", SimilarityPercent = 49 },
                new RecursiveFidelityPhaseScore { Phase = "polish-offsets", SimilarityPercent = 65 }
            ]
        };

        var summary = VisualRefinementPlanner.Plan(document, scoreTree, iterationBudget: 2);

        summary.IterationCount.Should().Be(2);
        summary.Actions.Select(static action => action.ActionType).Should().ContainInOrder("metric-profile-adjustment", "edge-contract-adjustment");
        summary.Actions.Select(static action => action.ReasonPhase).Should().ContainInOrder("text-icon-metrics", "inner-layout-match");
    }

    [Fact]
    public void RefinementPlan_WithoutScoreTree_IsImmediatelyConverged()
    {
        var document = new VisualDocument
        {
            DocumentName = "QuestHud",
            BackendFamily = "unity",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox { SourceType = ComponentType.Container },
                EdgeContract = new EdgeContract
                {
                    Participation = LayoutParticipation.NormalFlow,
                    WidthSizing = AxisSizing.Fill,
                    HeightSizing = AxisSizing.Fill,
                    HorizontalPin = EdgePin.Start,
                    VerticalPin = EdgePin.Start,
                    OverflowX = OverflowBehavior.Visible,
                    OverflowY = OverflowBehavior.Visible,
                    WrapPressure = WrapPressurePolicy.Allow
                }
            }
        };

        var summary = VisualRefinementPlanner.Plan(document, null, iterationBudget: 3);

        summary.Converged.Should().BeTrue();
        summary.IterationCount.Should().Be(0);
        summary.Actions.Should().BeEmpty();
    }

    [Fact]
    public void RefinementPlan_WithMeasuredShellIssues_PrioritizesShellActionsBeforeMetricAdjustments()
    {
        var document = new VisualDocument
        {
            DocumentName = "QuestHud",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox { SourceType = ComponentType.Container },
                EdgeContract = new EdgeContract
                {
                    Participation = LayoutParticipation.NormalFlow,
                    WidthSizing = AxisSizing.Fill,
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
                        StableId = "root/1/0",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        Box = new VisualBox { SourceType = ComponentType.Container },
                        EdgeContract = new EdgeContract
                        {
                            Participation = LayoutParticipation.NormalFlow,
                            WidthSizing = AxisSizing.Fill,
                            HeightSizing = AxisSizing.Fill,
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

        var scoreTree = new RecursiveFidelityScoreNode
        {
            Level = "panel",
            RegionId = "root",
            OverallSimilarityPercent = 70,
            Phases =
            [
                new RecursiveFidelityPhaseScore { Phase = "text-icon-metrics", SimilarityPercent = 45 }
            ]
        };

        var summary = VisualRefinementPlanner.Plan(
            document,
            scoreTree,
            iterationBudget: 2,
            measuredIssues:
            [
                new VisualMeasuredIssue
                {
                    Category = "shell-padding-or-child-stack-mismatch",
                    Severity = "warning",
                    LocalPath = "root/1/0",
                    Summary = "Shell overflow",
                    SuggestedAction = "Tighten shell padding."
                }
            ]);

        summary.IterationCount.Should().Be(2);
        summary.Actions[0].ActionType.Should().Be("tighten-shell-padding");
        summary.Actions[0].TriggerIssueCategory.Should().Be("shell-padding-or-child-stack-mismatch");
        summary.Actions[0].TriggerIssueLocalPath.Should().Be("root/1/0");
        summary.Actions[1].ActionType.Should().Be("metric-profile-adjustment");
    }

    [Fact]
    public void UGuiBuildProgramPlanner_ProducesReplayableDeterministicStepsAndCheckpoints()
    {
        var document = new VisualDocument
        {
            DocumentName = "PartyStatusStrip",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container,
                    LayoutType = LayoutType.Vertical
                },
                EdgeContract = CreateEdgeContract(),
                Children =
                [
                    new VisualNode
                    {
                        StableId = "root/header",
                        Kind = VisualNodeKind.Text,
                        SourceType = ComponentType.Label,
                        MetricProfileId = "metric:header",
                        Box = new VisualBox { SourceType = ComponentType.Label },
                        EdgeContract = CreateEdgeContract()
                    },
                    new VisualNode
                    {
                        StableId = "root/card",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container,
                            LayoutType = LayoutType.Horizontal
                        },
                        EdgeContract = CreateEdgeContract(),
                        Children =
                        [
                            new VisualNode
                            {
                                StableId = "root/card/icon",
                                Kind = VisualNodeKind.Icon,
                                SourceType = ComponentType.Icon,
                                MetricProfileId = "metric:icon",
                                Box = new VisualBox { SourceType = ComponentType.Icon },
                                EdgeContract = CreateEdgeContract()
                            }
                        ]
                    }
                ]
            },
            MetricProfiles =
            [
                new MetricProfileDefinition
                {
                    Id = "metric:header",
                    BackendFamily = "ugui",
                    SemanticClass = "header",
                    Text = new TextMetricProfile
                    {
                        WrapText = false
                    }
                },
                new MetricProfileDefinition
                {
                    Id = "metric:icon",
                    BackendFamily = "ugui",
                    SemanticClass = "icon",
                    Icon = new IconMetricProfile
                    {
                        BaselineOffset = 0,
                        OpticalCentering = true,
                        SizeMode = "fit-box"
                    }
                }
            ]
        };

        var program = UGuiBuildProgramPlanner.Plan(document);

        program.DocumentName.Should().Be("PartyStatusStrip");
        program.BackendFamily.Should().Be("ugui");
        program.RootStableId.Should().Be("root");
        program.Steps.Should().NotBeEmpty();
        program.Steps.Select(static step => step.Order).Should().ContainInOrder(Enumerable.Range(1, program.Steps.Count));
        program.Steps.Should().Contain(step => step.StableId == "root" && step.ActionType == "create-node");
        program.Steps.Should().Contain(step => step.StableId == "root/header" && step.ActionType == "bind-metric-profile");
        program.Steps.Should().Contain(step => step.StableId == "root/card/icon" && step.ActionType == "seal-subtree");
        program.Checkpoints.Should().Contain(checkpoint => checkpoint.StableId == "root/card/icon" && checkpoint.SolveStage == "atom");
        program.Checkpoints.Should().Contain(checkpoint => checkpoint.StableId == "root/card" && checkpoint.SolveStage == "motif");
        program.Checkpoints.Should().Contain(checkpoint => checkpoint.StableId == "root" && checkpoint.SolveStage == "motif");
    }

    [Fact]
    public void Prepare_ForUGui_CreatesUGuiBuildProgram()
    {
        var document = new HudDocument
        {
            Name = "PartyStatusStrip",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "header",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "Header"
                        }
                    }
                ]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions(), "ugui");

        prepared.UGuiBuildProgram.Should().NotBeNull();
        prepared.UGuiBuildProgram!.DocumentName.Should().Be("PartyStatusStrip");
        prepared.UGuiBuildProgram.BackendFamily.Should().Be("ugui");
    }

    private static EdgeContract CreateEdgeContract()
        => new()
        {
            Participation = LayoutParticipation.NormalFlow,
            WidthSizing = AxisSizing.Fill,
            HeightSizing = AxisSizing.Hug,
            HorizontalPin = EdgePin.Start,
            VerticalPin = EdgePin.Start,
            OverflowX = OverflowBehavior.Visible,
            OverflowY = OverflowBehavior.Visible,
            WrapPressure = WrapPressurePolicy.Allow
        };

    private static ComponentNode CreateQuestCard(string id, string text, string icon, int value, int gap = 8)
    {
        return new ComponentNode
        {
            Id = id,
            Type = ComponentType.Container,
            Layout = new LayoutSpec
            {
                Type = LayoutType.Horizontal,
                Gap = new Spacing(gap),
                Padding = new Spacing(6),
                Width = Dimension.Pixels(220),
                Height = Dimension.Pixels(60)
            },
            Children =
            [
                new ComponentNode
                {
                    Id = id + "-title",
                    Type = ComponentType.Label,
                    Properties = new Dictionary<string, BindableValue<object?>>
                    {
                        ["Text"] = text
                    }
                },
                new ComponentNode
                {
                    Id = id + "-icon",
                    Type = ComponentType.Icon,
                    Properties = new Dictionary<string, BindableValue<object?>>
                    {
                        ["Text"] = icon
                    }
                },
                new ComponentNode
                {
                    Id = id + "-progress",
                    Type = ComponentType.ProgressBar,
                    Properties = new Dictionary<string, BindableValue<object?>>
                    {
                        ["Value"] = value
                    }
                }
            ]
        };
    }
}
