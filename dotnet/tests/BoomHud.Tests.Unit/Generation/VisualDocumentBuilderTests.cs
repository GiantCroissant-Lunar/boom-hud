using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Generators;
using BoomHud.Generators.VisualIR;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public sealed class VisualDocumentBuilderTests
{
    [Fact]
    public void Build_RepeatedRuns_ProduceByteIdenticalJson()
    {
        var preparedDocument = CreatePreparedSyntheticDocument();

        var first = VisualDocumentBuilder.Build(preparedDocument, new GenerationOptions { EmitVisualIrArtifact = true }, "react");
        var second = VisualDocumentBuilder.Build(preparedDocument, new GenerationOptions { EmitVisualIrArtifact = true }, "react");

        GenerationDocumentPreprocessor.ToJson(first).Should().Be(GenerationDocumentPreprocessor.ToJson(second));
    }

    [Fact]
    public void Prepare_PostSynthesisVisualDocument_PreservesSyntheticRefsAndComponents()
    {
        var prepared = GenerationDocumentPreprocessor.Prepare(CreateSyntheticSourceDocument(), new GenerationOptions { EmitVisualIrArtifact = true }, "react");

        prepared.VisualDocument.Should().NotBeNull();
        prepared.SyntheticComponentization.Should().NotBeNull();
        prepared.VisualDocument!.Components.Should().ContainSingle();

        var componentId = prepared.Document.Components.Single().Key;
        prepared.VisualDocument.Components[0].Id.Should().Be(componentId);
        prepared.VisualDocument.Root.Children.Should().HaveCount(2);
        prepared.VisualDocument.Root.Children.Should().OnlyContain(node => node.ComponentRefId == componentId);
        prepared.VisualDocument.Root.Children.Select(static node => node.StableId).Should().ContainInOrder("root/0", "root/1");
    }

    [Fact]
    public void Build_DerivesExpectedEdgeAndMetricContracts()
    {
        var document = new HudDocument
        {
            Name = "VisualHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    ClipContent = true,
                    Width = Dimension.Fill,
                    Height = Dimension.Pixels(120)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "titleOne",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 12,
                            LineHeight = 1.5,
                            LetterSpacing = 1
                        },
                        InstanceOverrides = new Dictionary<string, object?>
                        {
                            [BoomHudMetadataKeys.PencilTextGrowth] = "fixed-width"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "titleTwo",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 12,
                            LineHeight = 1.5,
                            LetterSpacing = 1
                        },
                        InstanceOverrides = new Dictionary<string, object?>
                        {
                            [BoomHudMetadataKeys.PencilTextGrowth] = "fixed-width"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "icon",
                        Type = ComponentType.Icon,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(18),
                            Height = Dimension.Pixels(18),
                            IsAbsolutePositioned = true,
                            Left = Dimension.Pixels(14),
                            Top = Dimension.Pixels(6)
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Lucide",
                            FontSize = 18
                        }
                    }
                ]
            }
        };

        var visual = VisualDocumentBuilder.Build(document, new GenerationOptions(), "unity");

        visual.BackendFamily.Should().Be("unity");
        visual.Root.EdgeContract.OverflowX.Should().Be(OverflowBehavior.Clip);
        visual.Root.EdgeContract.OverflowY.Should().Be(OverflowBehavior.Clip);
        visual.Root.EdgeContract.WidthSizing.Should().Be(AxisSizing.Fill);
        visual.Root.EdgeContract.HeightSizing.Should().Be(AxisSizing.Fixed);

        var titleOne = visual.Root.Children[0];
        var titleTwo = visual.Root.Children[1];
        var icon = visual.Root.Children[2];

        titleOne.Kind.Should().Be(VisualNodeKind.Text);
        titleOne.Typography.Should().NotBeNull();
        titleOne.Typography!.SemanticClass.Should().Be("pixel-text");
        titleOne.EdgeContract.WrapPressure.Should().Be(WrapPressurePolicy.Tight);
        titleOne.MetricProfileId.Should().Be(titleTwo.MetricProfileId);

        icon.Kind.Should().Be(VisualNodeKind.Icon);
        icon.Icon.Should().NotBeNull();
        icon.Icon!.SemanticClass.Should().Be("icon-glyph");
        icon.EdgeContract.Participation.Should().Be(LayoutParticipation.Overlay);

        visual.MetricProfiles.Should().HaveCount(2);
        visual.MetricProfiles.Select(static profile => profile.Id).Should().ContainInOrder("metric-1", "metric-2");
    }

    [Fact]
    public void Build_WhenPolicyAppliesAbsoluteOffsets_ReflectsThemInVisualBox()
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

        var options = new GenerationOptions
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
                            Backend = "ugui",
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
        };

        var visual = VisualDocumentBuilder.Build(document, options, "ugui");
        var label = visual.Root.Children[0].Children[0];

        label.EdgeContract.Participation.Should().Be(LayoutParticipation.Overlay);
        label.Box.IsAbsolutePositioned.Should().BeTrue();
        label.Box.Left.Should().Be(Dimension.Pixels(12));
        label.Box.Top.Should().Be(Dimension.Pixels(4));
    }

    [Fact]
    public void Build_WhenParentUsesAbsoluteLayout_ChildUsesOverlayContract()
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
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Absolute,
                            Width = Dimension.Pixels(364),
                            Height = Dimension.Pixels(22)
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "label",
                                Type = ComponentType.Label,
                                Properties = new Dictionary<string, BindableValue<object?>>
                                {
                                    ["Text"] = "HEALTH 81%"
                                },
                                InstanceOverrides = new Dictionary<string, object?>
                                {
                                    [BoomHudMetadataKeys.PencilLeft] = 12d,
                                    [BoomHudMetadataKeys.PencilTop] = 4d
                                }
                            }
                        ]
                    }
                ]
            }
        };

        var visual = VisualDocumentBuilder.Build(document, new GenerationOptions(), "ugui");
        var label = visual.Root.Children[0].Children[0];

        label.EdgeContract.Participation.Should().Be(LayoutParticipation.Overlay);
        label.Box.IsAbsolutePositioned.Should().BeTrue();
        label.Box.Left.Should().Be(Dimension.Pixels(12));
        label.Box.Top.Should().Be(Dimension.Pixels(4));
    }

    private static HudDocument CreatePreparedSyntheticDocument()
        => GenerationDocumentPreprocessor.Prepare(CreateSyntheticSourceDocument(), new GenerationOptions(), "react").Document;

    private static HudDocument CreateSyntheticSourceDocument()
    {
        return new HudDocument
        {
            Name = "QuestHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    CreateStaticCard("card-alpha", "title-alpha", "icon-alpha", 12, 32),
                    CreateStaticCard("card-bravo", "title-bravo", "icon-bravo", 312, 64)
                ]
            }
        };
    }

    private static ComponentNode CreateStaticCard(string cardId, string titleId, string iconId, double left, double top)
    {
        return new ComponentNode
        {
            Id = cardId,
            Type = ComponentType.Container,
            Layout = new LayoutSpec
            {
                Type = LayoutType.Horizontal,
                Gap = new Spacing(8),
                Padding = new Spacing(6),
                Width = Dimension.Pixels(220),
                Height = Dimension.Pixels(60)
            },
            Style = new StyleSpec
            {
                Background = Color.Parse("#101820"),
                Border = new BorderSpec { Style = BorderStyle.Solid, Color = Color.Parse("#F5E6A8"), Width = 1 }
            },
            InstanceOverrides = new Dictionary<string, object?>
            {
                [BoomHudMetadataKeys.OriginalPencilId] = cardId,
                [BoomHudMetadataKeys.PencilLeft] = left,
                [BoomHudMetadataKeys.PencilTop] = top
            },
            Children =
            [
                new ComponentNode
                {
                    Id = titleId,
                    Type = ComponentType.Label,
                    Properties = new Dictionary<string, BindableValue<object?>>
                    {
                        ["Text"] = "QUEST"
                    },
                    Style = new StyleSpec
                    {
                        FontFamily = "Press Start 2P",
                        FontSize = 12
                    }
                },
                new ComponentNode
                {
                    Id = iconId,
                    Type = ComponentType.Icon,
                    Properties = new Dictionary<string, BindableValue<object?>>
                    {
                        ["Text"] = "shield"
                    },
                    Style = new StyleSpec
                    {
                        FontFamily = "Lucide",
                        FontSize = 18
                    }
                }
            ]
        };
    }
}
