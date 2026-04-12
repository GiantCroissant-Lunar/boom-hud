using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Generators.VisualIR;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public sealed class VisualPlanningTests
{
    [Fact]
    public void Synthesize_RepeatedSameShapeCardsWithDifferentTextIconAndValue_LiftsOneVisualComponentFamily()
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

        var visual = VisualDocumentBuilder.Build(
            GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions(), "react").Document,
            new GenerationOptions(),
            "react");

        var synthesized = VisualSynthesisPlanner.Synthesize(visual);

        synthesized.Summary.ChosenFamilyCount.Should().Be(1);
        synthesized.Summary.RewrittenOccurrenceCount.Should().Be(2);
        synthesized.Document.Components.Should().ContainSingle();
        synthesized.Document.Root.Children.Should().OnlyContain(static child => child.ComponentRefId != null);

        var secondOverrides = synthesized.Document.Root.Children[1].PropertyOverrides;
        secondOverrides.Should().ContainKey("$/0");
        secondOverrides.Should().ContainKey("$/1");
        secondOverrides.Should().ContainKey("$/2");
        secondOverrides["$/0"].Should().Contain(new KeyValuePair<string, object?>("Text", "BONUS"));
        secondOverrides["$/1"].Should().Contain(new KeyValuePair<string, object?>("Text", "sparkles"));
        secondOverrides["$/2"].Should().Contain(new KeyValuePair<string, object?>("Value", 72));
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
