using System.Text.Json;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Dsl.Pencil;
using BoomHud.Gen.Pencil;
using BoomHud.Generators.VisualIR;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public sealed class PencilGeneratorTests
{
    private readonly PencilGenerator _generator = new();
    private readonly PenParser _parser = new();
    private readonly GenerationOptions _options = new() { EmitViewModelInterfaces = true };

    [Fact]
    public void Generate_MinimalDocument_ProducesPenFileThatParses()
    {
        var document = new HudDocument
        {
            Name = "StatusHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Width = Dimension.Pixels(320),
                    Height = Dimension.Pixels(120),
                    Gap = new Spacing(8),
                    Padding = new Spacing(12)
                },
                Style = new StyleSpec
                {
                    Background = Color.Parse("#111111")
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "STATUS"
                        },
                        Style = new StyleSpec
                        {
                            Foreground = Color.White,
                            FontFamily = "Press Start 2P",
                            FontSize = 12
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(document, _options);

        result.Success.Should().BeTrue();
        result.Files.Should().ContainSingle(file => file.Path == "StatusHud.pen");

        var pen = result.Files.Single(file => file.Path == "StatusHud.pen").Content;
        pen.Should().Contain("\"version\": \"2.10\"");
        pen.Should().Contain("\"layout\": \"vertical\"");
        pen.Should().Contain("\"content\": \"STATUS\"");
        pen.Should().NotContain("\"padding\": 0");
        pen.Should().NotContain("\"alignItems\": \"start\"");
        pen.Should().NotContain("\"justifyContent\": \"start\"");
        pen.Should().NotContain("\"fontWeight\": \"normal\"");

        var parsed = _parser.Parse(pen);
        parsed.Name.Should().Be("StatusHud");
        parsed.Root.Layout!.Type.Should().Be(LayoutType.Vertical);
        parsed.Root.Layout.Width.Should().Be(Dimension.Pixels(320));
        parsed.Root.Layout.Height.Should().Be(Dimension.Pixels(120));
        parsed.Root.Children.Should().HaveCount(1);
        parsed.Root.Children[0].Properties["Text"].Value.Should().Be("STATUS");
        var titleStyle = parsed.Root.Children[0].Style;
        titleStyle.Should().NotBeNull();
        titleStyle!.FontFamily.Should().Be("Press Start 2P");
        titleStyle.FontSize.Should().Be(12);
    }

    [Fact]
    public void Generate_WithReusableComponentReference_UsesPenFriendlyReusableIds()
    {
        var badge = new HudComponentDefinition
        {
            Id = "synthetic:badge",
            Name = "Badge",
            Root = new ComponentNode
            {
                Id = "badgeRoot",
                Type = ComponentType.Container,
                Layout = new LayoutSpec { Type = LayoutType.Horizontal, Gap = new Spacing(6) },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "badgeLabel",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "READY"
                        }
                    }
                ]
            }
        };

        var document = new HudDocument
        {
            Name = "Hud",
            Components = new Dictionary<string, HudComponentDefinition> { ["synthetic:badge"] = badge },
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "primaryBadge",
                        Type = ComponentType.Container,
                        ComponentRefId = "synthetic:badge",
                        Layout = new LayoutSpec { Width = Dimension.Pixels(120), Height = Dimension.Pixels(32) }
                    }
                ]
            }
        };

        var result = _generator.Generate(document, _options);
        var pen = result.Files.Single(file => file.Path == "Hud.pen").Content;

        pen.Should().Contain("\"reusable\": true");
        pen.Should().Contain("\"id\": \"synthetic_badge\"");
        pen.Should().Contain("\"ref\": \"synthetic_badge\"");
        pen.Should().NotContain("synthetic:badge");

        var parsed = JsonDocument.Parse(pen);
        parsed.RootElement.GetProperty("children").GetArrayLength().Should().Be(2);

        var roundTripped = _parser.Parse(pen);
        roundTripped.Components.Should().ContainKey("synthetic_badge");
        roundTripped.Root.Children.Should().ContainSingle();
        roundTripped.Root.Children[0].ComponentRefId.Should().Be("synthetic_badge");
    }

    [Fact]
    public void Generate_WithPencilLayoutRule_UsesResolvedVisualOffsetsInPenOutput()
    {
        var document = new HudDocument
        {
            Name = "QuestSidebar",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "bar",
                        Type = ComponentType.Container,
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "label",
                                Type = ComponentType.Label,
                                Properties = new Dictionary<string, BindableValue<object?>>
                                {
                                    ["Text"] = "HEALTH 81%"
                                }
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(document, _options with
        {
            RuleSet = new GeneratorRuleSet
            {
                Rules =
                [
                    new GeneratorRule
                    {
                        Name = "resource bar text overlay",
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "pencil",
                            NodeId = "label"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                PositionMode = "absolute",
                                OffsetX = 12,
                                OffsetY = 4
                            }
                        }
                    }
                ]
            }
        });

        var pen = result.Files.Single(file => file.Path == "QuestSidebar.pen").Content;
        using var parsedJson = JsonDocument.Parse(pen);
        var label = parsedJson.RootElement
            .GetProperty("children")[0]
            .GetProperty("children")[0]
            .GetProperty("children")[0];

        label.GetProperty("x").GetDouble().Should().Be(12);
        label.GetProperty("y").GetDouble().Should().Be(4);
        label.GetProperty("content").GetString().Should().Be("HEALTH 81%");
    }

    [Fact]
    public void Generate_WithPencilTextRule_UsesResolvedVisualTypographyInPenOutput()
    {
        var document = new HudDocument
        {
            Name = "StatusHud",
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
                            ["Text"] = "STATUS"
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 12
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(document, _options with
        {
            RuleSet = new GeneratorRuleSet
            {
                Rules =
                [
                    new GeneratorRule
                    {
                        Name = "pencil title typography",
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "pencil",
                            NodeId = "title"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Text = new GeneratorTextRuleAction
                            {
                                FontFamily = "Arcade Classic",
                                FontSize = 18,
                                LetterSpacing = 1.5,
                                TextGrowth = "fixed-width"
                            }
                        }
                    }
                ]
            }
        });

        var pen = result.Files.Single(file => file.Path == "StatusHud.pen").Content;
        pen.Should().Contain("\"fontFamily\": \"Arcade Classic\"");
        pen.Should().Contain("\"fontSize\": 18");
        pen.Should().Contain("\"letterSpacing\": 1.5");
        pen.Should().Contain("\"textGrowth\": \"fixed-width\"");

        var roundTripped = _parser.Parse(pen);
        var label = roundTripped.Root.Children.Single();
        label.Style!.FontFamily.Should().Be("Arcade Classic");
        label.Style.FontSize.Should().Be(18);
        label.Style.LetterSpacing.Should().Be(1.5);
        label.InstanceOverrides[BoomHudMetadataKeys.PencilTextGrowth].Should().Be("fixed-width");
    }

    [Fact]
    public void PencilPatchPlanBuilder_WithMetricAction_ProducesConcretePenPropertySuggestions()
    {
        var visual = new VisualDocument
        {
            DocumentName = "QuestHud",
            BackendFamily = "pencil",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "QH01",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox { SourceType = ComponentType.Container },
                EdgeContract = CreateEdgeContract(),
                Children =
                [
                    new VisualNode
                    {
                        StableId = "root/0",
                        SourceId = "QH02",
                        Kind = VisualNodeKind.Text,
                        SourceType = ComponentType.Label,
                        SemanticClass = "heading-label",
                        Box = new VisualBox { SourceType = ComponentType.Label },
                        EdgeContract = CreateEdgeContract(),
                        Typography = new TypographyContract
                        {
                            SemanticClass = "heading-label",
                            ResolvedFontFamily = "Press Start 2P",
                            ResolvedFontSize = 14,
                            ResolvedLetterSpacing = 1,
                            WrapText = false,
                            TextGrowth = "fixed-width"
                        }
                    }
                ]
            }
        };

        var refinement = new VisualRefinementSummary
        {
            IterationBudget = 1,
            IterationCount = 1,
            Converged = false,
            Actions =
            [
                new VisualRefinementAction
                {
                    Iteration = 1,
                    TargetStableId = "root/0",
                    TargetSemanticClass = "heading-label",
                    ReasonPhase = "text-icon-metrics",
                    ActionType = "metric-profile-adjustment",
                    Description = "Tune typography."
                }
            ]
        };

        var plan = PencilPatchPlanBuilder.Build(visual, refinement);

        plan.Should().NotBeNull();
        plan!.ActionCount.Should().Be(1);
        plan.Steps[0].TargetPenId.Should().Be("QH02");
        plan.Steps[0].RequiresStructuralRewrite.Should().BeFalse();
        plan.Steps[0].SuggestedProperties.Should().Contain("fontFamily", "Press Start 2P");
        plan.Steps[0].SuggestedProperties.Should().Contain("fontSize", 14d);
        plan.Steps[0].SuggestedProperties.Should().Contain("letterSpacing", 1d);
        plan.Steps[0].SuggestedProperties.Should().Contain("textGrowth", "fixed-width");
    }

    [Fact]
    public void PencilPatchPlanBuilder_PrefersSourceNodeIdOverDisplaySourceIdForPenTargets()
    {
        var visual = new VisualDocument
        {
            DocumentName = "QuestHud",
            BackendFamily = "pencil",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "QuestHud",
                SourceNodeId = "QH01",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox { SourceType = ComponentType.Container },
                EdgeContract = CreateEdgeContract()
            }
        };

        var refinement = new VisualRefinementSummary
        {
            IterationBudget = 1,
            IterationCount = 1,
            Converged = false,
            Actions =
            [
                new VisualRefinementAction
                {
                    Iteration = 1,
                    TargetStableId = "root",
                    ReasonPhase = "outer-frame-match",
                    ActionType = "edge-contract-adjustment",
                    Description = "Tune shell."
                }
            ]
        };

        var plan = PencilPatchPlanBuilder.Build(visual, refinement);

        plan.Should().NotBeNull();
        plan!.Steps[0].TargetPenId.Should().Be("QH01");
    }

    [Fact]
    public void PencilPatchPlanBuilder_WithStructuralAction_MarksManualRewrite()
    {
        var visual = new VisualDocument
        {
            DocumentName = "QuestHud",
            BackendFamily = "pencil",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "QH01",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox { SourceType = ComponentType.Container },
                EdgeContract = CreateEdgeContract()
            }
        };

        var refinement = new VisualRefinementSummary
        {
            IterationBudget = 1,
            IterationCount = 1,
            Converged = false,
            Actions =
            [
                new VisualRefinementAction
                {
                    Iteration = 1,
                    TargetStableId = "root",
                    ReasonPhase = "structural-match",
                    ActionType = "panel-motif-split",
                    Description = "Split the panel motif."
                }
            ]
        };

        var plan = PencilPatchPlanBuilder.Build(visual, refinement);

        plan.Should().NotBeNull();
        plan!.Steps[0].RequiresStructuralRewrite.Should().BeTrue();
        plan.Steps[0].SuggestedProperties.Should().BeEmpty();
    }

    [Fact]
    public void PencilPatchPlanBuilder_BuildsDeterministicPropertiesFromVisualNode()
    {
        var visual = new VisualDocument
        {
            DocumentName = "QuestHud",
            BackendFamily = "pencil",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "QuestHud",
                SourceNodeId = "QH01",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container,
                    Width = Dimension.Pixels(1920),
                    Height = Dimension.Pixels(1080),
                    Left = Dimension.Pixels(0),
                    Top = Dimension.Pixels(0),
                    Padding = new Spacing(0),
                    LayoutType = LayoutType.Absolute
                },
                EdgeContract = CreateEdgeContract()
            }
        };

        var refinement = new VisualRefinementSummary
        {
            IterationBudget = 1,
            IterationCount = 1,
            Converged = false,
            Actions =
            [
                new VisualRefinementAction
                {
                    Iteration = 1,
                    TargetStableId = "root",
                    ReasonPhase = "outer-frame-match",
                    ActionType = "edge-contract-adjustment",
                    Description = "Tune shell."
                }
            ]
        };

        var plan = PencilPatchPlanBuilder.Build(visual, refinement);

        plan.Should().NotBeNull();
        plan!.Steps.Should().ContainSingle();
        plan.Steps[0].TargetPenId.Should().Be("QH01");
        plan.Steps[0].SuggestedProperties.Should().Contain("width", 1920d);
        plan.Steps[0].SuggestedProperties.Should().Contain("height", 1080d);
        plan.Steps[0].SuggestedProperties.Should().Contain("layout", "none");
    }

    [Fact]
    public void PencilPatchScriptBuilder_WithDeterministicAndManualSteps_EmitsEditableScript()
    {
        var plan = new PencilPatchPlan
        {
            DocumentName = "QuestHud",
            TargetFormat = "pen",
            ActionCount = 2,
            Steps =
            [
                new PencilPatchPlanStep
                {
                    Order = 1,
                    TargetStableId = "root/0",
                    TargetPenId = "QH02",
                    ReasonPhase = "text-icon-metrics",
                    ActionType = "metric-profile-adjustment",
                    Description = "Tune typography.",
                    RequiresStructuralRewrite = false,
                    SuggestedProperties = new Dictionary<string, object?>
                    {
                        ["fontFamily"] = "Press Start 2P",
                        ["fontSize"] = 14d,
                        ["textGrowth"] = "fixed-width"
                    }
                },
                new PencilPatchPlanStep
                {
                    Order = 2,
                    TargetStableId = "root/1",
                    TargetPenId = "QH03",
                    ReasonPhase = "structural-match",
                    ActionType = "panel-motif-split",
                    Description = "Split the panel motif.",
                    RequiresStructuralRewrite = true
                }
            ]
        };

        var script = PencilPatchScriptBuilder.Build(plan);

        script.Should().NotBeNull();
        script.Should().Contain("U(\"QH02\", {\"fontFamily\": \"Press Start 2P\", \"fontSize\": 14, \"textGrowth\": \"fixed-width\"})");
        script.Should().Contain("// MANUAL: inspect 'QH03' and rewrite structure by hand.");
    }

    [Fact]
    public void PencilBatchOpsBuilder_WithDeterministicAndManualSteps_EmitsExecutableUpdatesOnly()
    {
        var plan = new PencilPatchPlan
        {
            DocumentName = "QuestHud",
            TargetFormat = "pen",
            ActionCount = 2,
            Steps =
            [
                new PencilPatchPlanStep
                {
                    Order = 1,
                    TargetStableId = "root/0",
                    TargetPenId = "QH02",
                    ReasonPhase = "text-icon-metrics",
                    ActionType = "metric-profile-adjustment",
                    Description = "Tune typography.",
                    RequiresStructuralRewrite = false,
                    SuggestedProperties = new Dictionary<string, object?>
                    {
                        ["fontFamily"] = "Press Start 2P",
                        ["fontSize"] = 14d,
                        ["textGrowth"] = "fixed-width"
                    }
                },
                new PencilPatchPlanStep
                {
                    Order = 2,
                    TargetStableId = "root/1",
                    TargetPenId = "QH03",
                    ReasonPhase = "structural-match",
                    ActionType = "panel-motif-split",
                    Description = "Split the panel motif.",
                    RequiresStructuralRewrite = true
                }
            ]
        };

        var batchOps = PencilBatchOpsBuilder.Build(plan);

        batchOps.Should().NotBeNull();
        batchOps.Should().Be("U(\"QH02\", {\"fontFamily\": \"Press Start 2P\", \"fontSize\": 14, \"textGrowth\": \"fixed-width\"})" + Environment.NewLine);
    }

    [Fact]
    public void PencilBatchOpsBuilder_WithOnlyManualSteps_ReturnsNull()
    {
        var plan = new PencilPatchPlan
        {
            DocumentName = "QuestHud",
            TargetFormat = "pen",
            ActionCount = 1,
            Steps =
            [
                new PencilPatchPlanStep
                {
                    Order = 1,
                    TargetStableId = "root/1",
                    TargetPenId = "QH03",
                    ReasonPhase = "structural-match",
                    ActionType = "panel-motif-split",
                    Description = "Split the panel motif.",
                    RequiresStructuralRewrite = true
                }
            ]
        };

        var batchOps = PencilBatchOpsBuilder.Build(plan);

        batchOps.Should().BeNull();
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
}
