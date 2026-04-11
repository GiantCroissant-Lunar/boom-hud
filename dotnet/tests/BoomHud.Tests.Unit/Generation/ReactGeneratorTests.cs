using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Gen.React;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public sealed class ReactGeneratorTests
{
    private readonly ReactGenerator _generator = new();
    private readonly GenerationOptions _options = new() { EmitViewModelInterfaces = true };

    [Fact]
    public void Generate_MinimalDocument_ProducesTsxAndContract()
    {
        var document = new HudDocument
        {
            Name = "StatusHud",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(document, _options);

        result.Success.Should().BeTrue();
        result.Files.Should().Contain(file => file.Path == "StatusHudView.tsx");
        result.Files.Should().Contain(file => file.Path == "IStatusHudViewModel.g.ts");

        var tsx = result.Files.First(file => file.Path == "StatusHudView.tsx").Content;
        tsx.Should().Contain("import React from 'react';");
        tsx.Should().Contain("export function StatusHudView(props: StatusHudViewModel): React.JSX.Element");
        tsx.Should().Contain("motionTargets?: Record<string");
        tsx.Should().Contain("className='boomhud-node boomhud-container'");
    }

    [Fact]
    public void Generate_WithBindings_EmitsPropsAndBoundExpressions()
    {
        var document = new HudDocument
        {
            Name = "Hud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "healthLabel",
                        Type = ComponentType.Label,
                        Bindings =
                        [
                            new BindingSpec { Property = "text", Path = "Player.HealthText", Format = "{0} HP" }
                        ]
                    },
                    new ComponentNode
                    {
                        Id = "healthBar",
                        Type = ComponentType.ProgressBar,
                        Bindings =
                        [
                            new BindingSpec { Property = "value", Path = "Player.HealthPercent" }
                        ],
                        Visible = BindableValue<bool>.Bind("Player.ShowHud")
                    }
                ]
            }
        };

        var result = _generator.Generate(document, _options);
        var tsx = result.Files.First(file => file.Path == "HudView.tsx").Content;

        tsx.Should().Contain("playerHealthText?: unknown;");
        tsx.Should().Contain("playerHealthPercent?: unknown;");
        tsx.Should().Contain("playerShowHud?: unknown;");
        tsx.Should().Contain("getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'healthLabel'))");
        tsx.Should().Contain("formatValue(props.playerHealthText, '{0} HP', '')");
        tsx.Should().Contain("width: clampPercent(props.playerHealthPercent)");
        tsx.Should().Contain("asBool(props.playerShowHud)");
    }

    [Fact]
    public void Generate_WithComponentReference_ImportsReferencedView()
    {
        var actionButton = new HudComponentDefinition
        {
            Id = "action-button",
            Name = "ActionButton",
            Root = new ComponentNode { Id = "actionButtonRoot", Type = ComponentType.Button }
        };

        var document = new HudDocument
        {
            Name = "Hud",
            Components = new Dictionary<string, HudComponentDefinition> { ["action-button"] = actionButton },
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode { Id = "primaryAction", Type = ComponentType.Container, ComponentRefId = "action-button" }
                ]
            }
        };

        var result = _generator.Generate(document, _options);
        var tsx = result.Files.First(file => file.Path == "HudView.tsx").Content;

        tsx.Should().Contain("import { ActionButtonView } from './ActionButtonView';");
        tsx.Should().Contain("data-boomhud-id={resolveMotionId(props.motionScope, 'primaryAction')}");
        tsx.Should().Contain("<ActionButtonView motionTargets={props.motionTargets} motionScope={resolveMotionId(props.motionScope, 'primaryAction')} />");
    }

    [Fact]
    public void Generate_FlowChildWithAbsoluteLayout_KeepsChildInFlow()
    {
        var document = new HudDocument
        {
            Name = "Portrait",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Gap = new Spacing(8),
                    Padding = new Spacing(0)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "face",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Absolute,
                            Width = Dimension.Pixels(56),
                            Height = Dimension.Pixels(56)
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "icon",
                                Type = ComponentType.Icon,
                                Layout = new LayoutSpec
                                {
                                    Left = Dimension.Pixels(12),
                                    Top = Dimension.Pixels(12),
                                    Width = Dimension.Pixels(16),
                                    Height = Dimension.Pixels(16)
                                }
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(document, _options);
        var tsx = result.Files.First(file => file.Path == "PortraitView.tsx").Content;

        tsx.Should().Contain("gap: '8px'");
        tsx.Should().Contain("padding: '0'");
        tsx.Should().Contain("data-boomhud-id={resolveMotionId(props.motionScope, 'face')}", Exactly.Once());
        tsx.Should().Contain("width: '56px', height: '56px', position: 'relative'");
        tsx.Should().NotContain("position: 'absolute', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'face'))");
        tsx.Should().Contain("data-boomhud-id={resolveMotionId(props.motionScope, 'icon')}");
        tsx.Should().Contain("position: 'absolute', left: '12px', top: '12px'");
    }

    [Fact]
    public void Generate_LayoutClipContent_EmitsOverflowHidden()
    {
        var document = new HudDocument
        {
            Name = "ClippedHud",
            Root = new ComponentNode
            {
                Id = "viewport",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    ClipContent = true
                }
            }
        };

        var result = _generator.Generate(document, _options);
        var tsx = result.Files.First(file => file.Path == "ClippedHudView.tsx").Content;

        tsx.Should().Contain("overflow: 'hidden'");
    }

    [Fact]
    public void Generate_SpacingShorthands_EmitsCssUnits()
    {
        var document = new HudDocument
        {
            Name = "SpacingHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Gap = new Spacing(6),
                    Padding = new Spacing(0, 6),
                    Margin = new Spacing(1, 2, 3, 4)
                }
            }
        };

        var result = _generator.Generate(document, _options);
        var tsx = result.Files.First(file => file.Path == "SpacingHudView.tsx").Content;

        tsx.Should().Contain("gap: '6px'");
        tsx.Should().Contain("padding: '0 6px'");
        tsx.Should().Contain("margin: '1px 2px 3px 4px'");
    }

    [Fact]
    public void Generate_HorizontalFillChildren_UseFlexFillWidth()
    {
        var document = new HudDocument
        {
            Name = "ActionRow",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Gap = new Spacing(2)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "left",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Fill,
                            Height = Dimension.Pixels(28)
                        }
                    },
                    new ComponentNode
                    {
                        Id = "right",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Fill,
                            Height = Dimension.Pixels(28)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(document, _options);
        var tsx = result.Files.First(file => file.Path == "ActionRowView.tsx").Content;

        tsx.Should().Contain("data-boomhud-id={resolveMotionId(props.motionScope, 'left')}");
        tsx.Should().Contain("data-boomhud-id={resolveMotionId(props.motionScope, 'right')}");
        tsx.Should().Contain("flex: '1 1 0'");
    }

    [Fact]
    public void Generate_IconNode_NormalizesLucideTokens()
    {
        var document = new HudDocument
        {
            Name = "IconHud",
            Root = new ComponentNode
            {
                Id = "icon",
                Type = ComponentType.Icon,
                Style = new StyleSpec
                {
                    FontFamily = "lucide"
                },
                Properties = new Dictionary<string, BindableValue<object?>>()
                {
                    ["text"] = "shield"
                }
            }
        };

        var result = _generator.Generate(document, _options);
        var tsx = result.Files.First(file => file.Path == "IconHudView.tsx").Content;

        tsx.Should().Contain("const renderLucideIcon = (token: string): React.JSX.Element | string => {");
        tsx.Should().Contain("case 'shield': return <svg {...common}><path d='M12 3l7 3v6c0 5-3.5 8.8-7 9-3.5-.2-7-4-7-9V6l7-3Z' /></svg>;");
        tsx.Should().Contain("{renderIconContent(getMotionText(props.motionTargets, resolveMotionId(props.motionScope, 'icon')) ?? ('shield'), 'lucide')}");
    }

    [Fact]
    public void Generate_WithRemotionBackend_AliasesPressStartFontFamily()
    {
        var document = new HudDocument
        {
            Name = "FontHud",
            Root = new ComponentNode
            {
                Id = "title",
                Type = ComponentType.Label,
                Style = new StyleSpec
                {
                    FontFamily = "Press Start 2P"
                },
                Properties = new Dictionary<string, BindableValue<object?>>()
                {
                    ["text"] = "Quest"
                }
            }
        };

        var result = new ReactGenerator("remotion").Generate(document, _options);
        var tsx = result.Files.First(file => file.Path == "FontHudView.tsx").Content;

        tsx.Should().Contain("fontFamily: 'BoomHudPressStart2P'");
        tsx.Should().NotContain("fontFamily: 'Press Start 2P'");
    }

    [Fact]
    public void Generate_BackgroundImage_EmitsCssBackgroundImageStyles()
    {
        var document = new HudDocument
        {
            Name = "ViewportHud",
            Root = new ComponentNode
            {
                Id = "viewport",
                Type = ComponentType.Container,
                Style = new StyleSpec
                {
                    BackgroundImage = new BackgroundImageSpec
                    {
                        Url = "./images/viewport.png",
                        Mode = BackgroundImageMode.Fill
                    }
                }
            }
        };

        var result = _generator.Generate(document, _options);
        var tsx = result.Files.First(file => file.Path == "ViewportHudView.tsx").Content;

        tsx.Should().Contain("backgroundImage:");
        tsx.Should().Contain("./images/viewport.png");
        tsx.Should().Contain("backgroundSize: 'cover'");
        tsx.Should().Contain("backgroundPosition: 'center'");
        tsx.Should().Contain("backgroundRepeat: 'no-repeat'");
    }

    [Fact]
    public void Generate_WithRuleSet_AppliesCanonicalLayoutAndTextPolicies()
    {
        var document = new HudDocument
        {
            Name = "RuleHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label
                    }
                ]
            }
        };

        var options = _options with
        {
            RuleSet = new GeneratorRuleSet
            {
                Rules =
                [
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "react",
                            NodeId = "root"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                Gap = 10,
                                Padding = 4,
                                FlexAlignmentPreset = "center"
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "react",
                            NodeId = "title"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Text = new GeneratorTextRuleAction
                            {
                                FontFamily = "Orbitron",
                                FontSize = 18,
                                WrapText = false
                            },
                            Layout = new GeneratorLayoutRuleAction
                            {
                                PositionMode = "absolute",
                                OffsetX = 12,
                                OffsetY = 6
                            }
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(document, options);
        var tsx = result.Files.First(file => file.Path == "RuleHudView.tsx").Content;

        tsx.Should().Contain("gap: '10px'");
        tsx.Should().Contain("padding: '4px'");
        tsx.Should().Contain("alignItems: 'center'");
        tsx.Should().Contain("justifyContent: 'center'");
        tsx.Should().Contain("fontFamily: 'Orbitron'");
        tsx.Should().Contain("fontSize: '18px'");
        tsx.Should().Contain("whiteSpace: 'nowrap'");
        tsx.Should().Contain("position: 'absolute'");
        tsx.Should().Contain("left: '12px'");
        tsx.Should().Contain("top: '6px'");
    }

    [Fact]
    public void Generate_WithEdgePaddingAndInsets_AppliesPerEdgeLayoutPolicies()
    {
        var document = new HudDocument
        {
            Name = "InsetHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Padding = new Spacing(2)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "badge",
                        Type = ComponentType.Badge
                    }
                ]
            }
        };

        var options = _options with
        {
            RuleSet = new GeneratorRuleSet
            {
                Rules =
                [
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "react",
                            NodeId = "root"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                PaddingLeft = 10,
                                PaddingRightDelta = 2,
                                PaddingBottom = 6
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "react",
                            NodeId = "badge"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                PositionMode = "absolute",
                                InsetRight = 8,
                                InsetBottom = 3
                            }
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(document, options);
        var tsx = result.Files.First(file => file.Path == "InsetHudView.tsx").Content;

        tsx.Should().Contain("padding: '2px 4px 6px 10px'");
        tsx.Should().Contain("position: 'absolute'");
        tsx.Should().Contain("right: '8px'");
        tsx.Should().Contain("bottom: '3px'");
        tsx.Should().NotContain("left: '0px'");
        tsx.Should().NotContain("top: '0px'");
    }
}
