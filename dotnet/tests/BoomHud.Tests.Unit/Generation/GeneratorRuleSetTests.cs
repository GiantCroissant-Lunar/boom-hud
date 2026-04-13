using System.Collections.Generic;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;
using BoomHud.Generators;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public sealed class GeneratorRuleSetTests
{
    [Fact]
    public void LoadFromJson_RoundTripsTypedRuleActions()
    {
        var json = """
        {
          "version": "1.0",
          "rules": [
            {
              "name": "body text",
              "phase": "text",
              "cost": 2.5,
              "preconditions": [
                {
                  "key": "finding.text",
                  "value": "present"
                }
              ],
              "effects": [
                {
                  "key": "text.wrap.adjusted",
                  "value": "true"
                }
              ],
              "selector": {
                "backend": "unity",
                "documentName": "QuestSidebar",
                "nodeId": "body",
                "sourceNodeId": "QSB56",
                "componentType": "label",
                "fontFamily": "Press Start 2P",
                "textGrowth": "fixed-width",
                "semanticClass": "pixel-text",
                "sizeBand": "xsmall",
                "metadataKey": "boomhud:pencilTextGrowth",
                "metadataValue": "fixed-width"
              },
              "action": {
                "controlType": "TextField",
                "text": {
                  "lineHeight": 1.4,
                  "wrapText": true,
                  "fontFamily": "Press Start 2P",
                  "fontSize": 15,
                  "letterSpacing": 0.5,
                  "textGrowth": "fixed-width"
                },
                "icon": {
                  "baselineOffset": 1.5,
                  "opticalCentering": false,
                  "sizeMode": "match-height",
                  "fontSize": 18
                },
                "layout": {
                  "forceAbsolutePositioning": true,
                  "stretchWidth": true,
                  "preferContentHeight": true,
                  "preferredWidthDelta": -24,
                  "preferredHeightDelta": 12,
                  "edgeAlignment": "end",
                  "gap": 6,
                  "padding": 8,
                  "paddingTop": 10,
                  "paddingLeftDelta": 3,
                  "offsetX": 3,
                  "offsetY": -2,
                  "insetRight": 14,
                  "insetBottomDelta": -5
                }
              }
            }
          ]
        }
        """;

        var ruleSet = GeneratorRuleSet.LoadFromJson(json);
        var roundTrip = GeneratorRuleSet.LoadFromJson(ruleSet.ToJson());
        var rule = roundTrip.Rules.Should().ContainSingle().Subject;

        roundTrip.Version.Should().Be("1.0");
        rule.Phase.Should().Be(GeneratorRulePhase.Text);
        rule.Cost.Should().Be(2.5);
        rule.Preconditions.Should().ContainSingle();
        rule.Preconditions[0].Key.Should().Be("finding.text");
        rule.Effects.Should().ContainSingle();
        rule.Effects[0].Key.Should().Be("text.wrap.adjusted");
        rule.Selector.ComponentType.Should().Be(ComponentType.Label);
        rule.Selector.SourceNodeId.Should().Be("QSB56");
        rule.Selector.FontFamily.Should().Be("Press Start 2P");
        rule.Selector.TextGrowth.Should().Be("fixed-width");
        rule.Selector.SemanticClass.Should().Be("pixel-text");
        rule.Selector.SizeBand.Should().Be("xsmall");
        rule.Action.ControlType.Should().Be("TextField");
        rule.Action.Text!.LineHeight.Should().Be(1.4);
        rule.Action.Text.WrapText.Should().BeTrue();
        rule.Action.Text.FontSize.Should().Be(15);
        rule.Action.Text.LetterSpacing.Should().Be(0.5);
        rule.Action.Icon!.BaselineOffset.Should().Be(1.5);
        rule.Action.Icon.OpticalCentering.Should().BeFalse();
        rule.Action.Icon.FontSize.Should().Be(18);
        rule.Action.Layout!.ForceAbsolutePositioning.Should().BeTrue();
        rule.Action.Layout.StretchWidth.Should().BeTrue();
        rule.Action.Layout.PreferContentHeight.Should().BeTrue();
        rule.Action.Layout.PreferredWidthDelta.Should().Be(-24);
        rule.Action.Layout.PreferredHeightDelta.Should().Be(12);
        rule.Action.Layout.Gap.Should().Be(6);
        rule.Action.Layout.Padding.Should().Be(8);
        rule.Action.Layout.PaddingTop.Should().Be(10);
        rule.Action.Layout.PaddingLeftDelta.Should().Be(3);
        rule.Action.Layout.OffsetX.Should().Be(3);
        rule.Action.Layout.OffsetY.Should().Be(-2);
        rule.Action.Layout.InsetRight.Should().Be(14);
        rule.Action.Layout.InsetBottomDelta.Should().Be(-5);
    }

    [Fact]
    public void Resolve_MoreSpecificRuleWinsOverLaterGeneralRule()
    {
        var ruleSet = new GeneratorRuleSet
        {
            Rules =
            [
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "unity",
                        ComponentType = ComponentType.Label
                    },
                    Action = new GeneratorRuleAction
                    {
                        Text = new GeneratorTextRuleAction
                        {
                            WrapText = false
                        }
                    }
                },
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "unity",
                        NodeId = "body"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Text = new GeneratorTextRuleAction
                        {
                            WrapText = true
                        }
                    }
                },
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "unity",
                        ComponentType = ComponentType.Label
                    },
                    Action = new GeneratorRuleAction
                    {
                        Text = new GeneratorTextRuleAction
                        {
                            WrapText = false
                        }
                    }
                }
            ]
        };

        var node = new ComponentNode
        {
            Id = "body",
            Type = ComponentType.Label
        };

        var resolver = new RuleResolver(ruleSet, "unity");
        var policy = resolver.Resolve("QuestSidebar", node);

        policy.Text.WrapText.Should().BeTrue();
    }

    [Fact]
    public void Resolve_LaterEqualSpecificityRuleWins()
    {
        var ruleSet = new GeneratorRuleSet
        {
            Rules =
            [
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "ugui",
                        MetadataKey = BoomHudMetadataKeys.PencilTextGrowth,
                        MetadataValue = "fixed-width"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Text = new GeneratorTextRuleAction
                        {
                            LineHeight = 1.2
                        }
                    }
                },
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "ugui",
                        MetadataKey = BoomHudMetadataKeys.PencilTextGrowth,
                        MetadataValue = "fixed-width"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Text = new GeneratorTextRuleAction
                        {
                            LineHeight = 1.6
                        }
                    }
                }
            ]
        };

        var node = new ComponentNode
        {
            Id = "body",
            Type = ComponentType.Label,
            InstanceOverrides = new Dictionary<string, object?>
            {
                [BoomHudMetadataKeys.PencilTextGrowth] = "fixed-width"
            }
        };

        var resolver = new RuleResolver(ruleSet, "ugui");
        var policy = resolver.Resolve("QuestSidebar", node);

        policy.Text.LineHeight.Should().Be(1.6);
    }

    [Fact]
    public void Resolve_SourceNodeIdMatchesOriginalPencilIdMetadata()
    {
        var ruleSet = new GeneratorRuleSet
        {
            Rules =
            [
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "unity",
                        SourceNodeId = "QSB56"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Text = new GeneratorTextRuleAction
                        {
                            WrapText = true
                        }
                    }
                }
            ]
        };

        var node = new ComponentNode
        {
            Id = "ObjectiveHint2",
            Type = ComponentType.Label,
            InstanceOverrides = new Dictionary<string, object?>
            {
                [BoomHudMetadataKeys.OriginalPencilId] = "QSB56"
            }
        };

        var resolver = new RuleResolver(ruleSet, "unity");
        var policy = resolver.Resolve("QuestSidebar", node);

        policy.Text.WrapText.Should().BeTrue();
    }

    [Fact]
    public void Resolve_SemanticAndStyleSelectorsMatchPixelTextAndIconShells()
    {
        var ruleSet = new GeneratorRuleSet
        {
            Rules =
            [
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "remotion",
                        SemanticClass = "pixel-text",
                        FontFamily = "Press Start 2P",
                        TextGrowth = "fixed-width",
                        SizeBand = "small"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Text = new GeneratorTextRuleAction
                        {
                            FontSizeDelta = 1,
                            LetterSpacing = 0.25
                        }
                    }
                },
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "remotion",
                        SemanticClass = "icon-shell",
                        SizeBand = "xlarge"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Layout = new GeneratorLayoutRuleAction
                        {
                            PreferredWidthDelta = -2,
                            PreferredHeightDelta = -2
                        }
                    }
                }
            ]
        };

        var pixelTextNode = new ComponentNode
        {
            Id = "title",
            Type = ComponentType.Label,
            Style = new StyleSpec
            {
                FontFamily = "Press Start 2P",
                FontSize = 10
            },
            InstanceOverrides = new Dictionary<string, object?>
            {
                [BoomHudMetadataKeys.PencilTextGrowth] = "fixed-width"
            }
        };

        var iconShellNode = new ComponentNode
        {
            Id = "shell",
            Type = ComponentType.Container,
            Layout = new LayoutSpec
            {
                Width = Dimension.Pixels(44),
                Height = Dimension.Pixels(44)
            },
            Children =
            [
                new ComponentNode
                {
                    Id = "icon",
                    Type = ComponentType.Icon,
                    Style = new StyleSpec
                    {
                        FontFamily = "lucide"
                    },
                    Layout = new LayoutSpec
                    {
                        Width = Dimension.Pixels(20),
                        Height = Dimension.Pixels(20)
                    }
                }
            ]
        };

        var resolver = new RuleResolver(ruleSet, "remotion");
        var pixelPolicy = resolver.Resolve("QuestSidebar", pixelTextNode);
        var shellPolicy = resolver.Resolve("QuestSidebar", iconShellNode);

        pixelPolicy.Text.FontSize.Should().BeNull();
        pixelPolicy.Text.FontSizeDelta.Should().Be(1);
        pixelPolicy.Text.LetterSpacing.Should().Be(0.25);
        TextPolicyService.ResolveFontSize(pixelTextNode, pixelTextNode.Layout?.Width ?? pixelTextNode.Style?.Width, pixelTextNode.Layout?.Height ?? pixelTextNode.Style?.Height, pixelPolicy).Should().Be(11);
        shellPolicy.Layout.PreferredWidthDelta.Should().Be(-2);
        shellPolicy.Layout.PreferredHeightDelta.Should().Be(-2);
    }

    [Fact]
    public void Resolve_FontDeltasApplyRelativeToSourceMetrics()
    {
        var ruleSet = new GeneratorRuleSet
        {
            Rules =
            [
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "remotion",
                        NodeId = "body"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Text = new GeneratorTextRuleAction
                        {
                            FontSize = 12,
                            LetterSpacing = 0.2
                        }
                    }
                },
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "remotion",
                        NodeId = "body"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Text = new GeneratorTextRuleAction
                        {
                            FontSizeDelta = 2,
                            LetterSpacingDelta = 0.1
                        },
                        Icon = new GeneratorIconRuleAction
                        {
                            FontSizeDelta = -1
                        }
                    }
                }
            ]
        };

        var node = new ComponentNode
        {
            Id = "body",
            Type = ComponentType.Icon,
            Style = new StyleSpec
            {
                FontSize = 10,
                LetterSpacing = 0.05
            },
            Layout = new LayoutSpec
            {
                Width = Dimension.Pixels(20),
                Height = Dimension.Pixels(18)
            }
        };

        var resolver = new RuleResolver(ruleSet, "remotion");
        var policy = resolver.Resolve("QuestSidebar", node);

        policy.Text.FontSize.Should().Be(12);
        policy.Text.FontSizeDelta.Should().Be(2);
        policy.Text.LetterSpacing.Should().Be(0.2);
        policy.Text.LetterSpacingDelta.Should().Be(0.1);
        policy.Icon.FontSize.Should().BeNull();
        policy.Icon.FontSizeDelta.Should().Be(-1);
        TextPolicyService.ResolveFontSize(node, node.Layout?.Width, node.Layout?.Height, policy).Should().Be(14);
        TextPolicyService.ResolveLetterSpacing(node, policy).Should().BeApproximately(0.3, 0.0001);
        IconPolicyService.ResolveFontSize(node, node.Layout?.Width, node.Layout?.Height, policy).Should().Be(17);
    }

    [Fact]
    public void Resolve_StructuralSemanticRolesMatchHeadingAndStackedTextPatterns()
    {
        var ruleSet = new GeneratorRuleSet
        {
            Rules =
            [
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "remotion",
                        SemanticClass = "heading-label"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Layout = new GeneratorLayoutRuleAction
                        {
                            PreferContentHeight = true
                        }
                    }
                },
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "remotion",
                        SemanticClass = "stacked-text-line"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Layout = new GeneratorLayoutRuleAction
                        {
                            PreferContentHeight = true
                        }
                    }
                },
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "remotion",
                        SemanticClass = "stacked-text-group"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Layout = new GeneratorLayoutRuleAction
                        {
                            GapDelta = -3
                        }
                    }
                }
            ]
        };

        var headingLabel = new ComponentNode
        {
            Id = "SectionHeading",
            Type = ComponentType.Label,
            Style = new StyleSpec
            {
                FontFamily = "Press Start 2P",
                FontSize = 10
            }
        };

        var contentPanel = new ComponentNode
        {
            Id = "SectionBody",
            Type = ComponentType.Container,
            Layout = new LayoutSpec
            {
                Type = LayoutType.Absolute
            }
        };

        var headingParent = new ComponentNode
        {
            Id = "SectionCard",
            Type = ComponentType.Container,
            Layout = new LayoutSpec
            {
                Type = LayoutType.Vertical,
                Gap = Spacing.Uniform(10)
            },
            Children =
            [
                headingLabel,
                contentPanel
            ]
        };

        var stackedTitle = new ComponentNode
        {
            Id = "ObjectiveTitle",
            Type = ComponentType.Label,
            Style = new StyleSpec
            {
                FontFamily = "Press Start 2P",
                FontSize = 9
            }
        };

        var stackedHint = new ComponentNode
        {
            Id = "ObjectiveHint",
            Type = ComponentType.Label,
            Style = new StyleSpec
            {
                FontFamily = "Press Start 2P",
                FontSize = 8
            }
        };

        var stackedParent = new ComponentNode
        {
            Id = "ObjectiveText",
            Type = ComponentType.Container,
            Layout = new LayoutSpec
            {
                Type = LayoutType.Vertical,
                Gap = Spacing.Uniform(6)
            },
            Children =
            [
                stackedTitle,
                stackedHint
            ]
        };

        var resolver = new RuleResolver(ruleSet, "remotion");
        var headingPolicy = resolver.Resolve("QuestSidebar", headingLabel, new RuleSelectionContext(headingParent, null, 0));
        var stackedLinePolicy = resolver.Resolve("QuestSidebar", stackedTitle, new RuleSelectionContext(stackedParent, null, 0));
        var stackedGroupPolicy = resolver.Resolve("QuestSidebar", stackedParent);

        headingPolicy.Layout.PreferContentHeight.Should().BeTrue();
        stackedLinePolicy.Layout.PreferContentHeight.Should().BeTrue();
        stackedGroupPolicy.Layout.GapDelta.Should().Be(-3);
    }

    [Fact]
    public void ResolveFlexibleSize_PreferContentHeightSuppressesDefaultFlexGrowth()
    {
        var baseline = LayoutPolicyService.ResolveFlexibleSize(
            null,
            "height",
            LayoutType.Vertical,
            isFlexibleContainer: true,
            new ResolvedGeneratorPolicy());

        var policy = new ResolvedGeneratorPolicy
        {
            Layout = new ResolvedGeneratorLayoutPolicy
            {
                PreferContentHeight = true
            }
        };

        var resolved = LayoutPolicyService.ResolveFlexibleSize(
            null,
            "height",
            LayoutType.Vertical,
            isFlexibleContainer: true,
            policy);

        baseline.Should().Be(1);
        resolved.Should().BeNull();
    }

    [Fact]
    public void LoadFromJson_RoundTripsMotionSelectorsAndTemplateMetadata()
    {
        var json = """
        {
          "version": "1.0",
          "rules": [
            {
              "name": "timeline quantize",
              "phase": "motion",
              "cost": 1.5,
              "preconditions": [
                {
                  "key": "motion.enabled",
                  "value": "true"
                }
              ],
              "effects": [
                {
                  "key": "motion.sequence.normalized",
                  "value": "true"
                }
              ],
              "selector": {
                "backend": "unity",
                "documentName": "PartyHud",
                "clipId": "intro",
                "trackId": "rootTrack",
                "targetId": "root",
                "motionProperty": "opacity",
                "sequenceId": "introSequence"
              },
              "template": {
                "kind": "durationQuantization",
                "numberValue": 8
              },
              "action": {
                "motion": {
                  "targetResolutionPolicy": "error"
                }
              }
            }
          ]
        }
        """;

        var ruleSet = GeneratorRuleSet.LoadFromJson(json);
        var roundTrip = GeneratorRuleSet.LoadFromJson(ruleSet.ToJson());
        var rule = roundTrip.Rules.Should().ContainSingle().Subject;

        rule.Template.Should().NotBeNull();
        rule.Template!.Kind.Should().Be("durationQuantization");
        rule.Template.NumberValue.Should().Be(8);
        rule.Selector.ClipId.Should().Be("intro");
        rule.Selector.TrackId.Should().Be("rootTrack");
        rule.Selector.TargetId.Should().Be("root");
        rule.Selector.MotionProperty.Should().Be(MotionProperty.Opacity);
        rule.Selector.SequenceId.Should().Be("introSequence");
        rule.Action.Motion!.TargetResolutionPolicy.Should().Be("error");
    }

    [Fact]
    public void ResolveMotion_CompilesTemplatesAndHonorsSpecificSelectors()
    {
        var ruleSet = new GeneratorRuleSet
        {
            Rules =
            [
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "unity",
                        ClipId = "intro"
                    },
                    Template = new GeneratorActionTemplate
                    {
                        Kind = "durationQuantization",
                        NumberValue = 12
                    }
                },
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "unity",
                        ClipId = "intro",
                        TrackId = "attackButtonTrack",
                        MotionProperty = MotionProperty.Opacity
                    },
                    Template = new GeneratorActionTemplate
                    {
                        Kind = "easingRemap",
                        Parameters = new Dictionary<string, string>
                        {
                            ["easing"] = "linear"
                        }
                    }
                }
            ]
        };

        var resolver = new RuleResolver(ruleSet, "unity");
        var policy = resolver.ResolveMotion("PartyHud", new MotionRuleContext
        {
            ClipId = "intro",
            TrackId = "attackButtonTrack",
            MotionProperty = MotionProperty.Opacity
        });

        policy.DurationQuantizationFrames.Should().Be(12);
        policy.EasingRemapTo.Should().Be(MotionEasing.Linear);
    }

    [Fact]
    public void LoadFromJson_RoundTripsMetricProfiles()
    {
        var json = """
        {
          "version": "1.0",
          "metricProfiles": [
            {
              "name": "ugui pixel small font bump",
              "selector": {
                "backend": "ugui",
                "componentType": "label",
                "semanticClass": "pixel-text",
                "sizeBand": "small"
              },
              "template": {
                "kind": "fontSizeDelta",
                "numberValue": 1
              },
              "action": {
                "text": {
                  "letterSpacingDelta": 0.5
                }
              }
            }
          ]
        }
        """;

        var ruleSet = GeneratorRuleSet.LoadFromJson(json);
        var roundTrip = GeneratorRuleSet.LoadFromJson(ruleSet.ToJson());
        var profile = roundTrip.MetricProfiles.Should().ContainSingle().Subject;

        profile.Name.Should().Be("ugui pixel small font bump");
        profile.Selector.Backend.Should().Be("ugui");
        profile.Selector.ComponentType.Should().Be(ComponentType.Label);
        profile.Selector.SemanticClass.Should().Be("pixel-text");
        profile.Selector.SizeBand.Should().Be("small");
        profile.Template.Should().NotBeNull();
        profile.Template!.Kind.Should().Be("fontSizeDelta");
        profile.Template.NumberValue.Should().Be(1);
        profile.Action.Text!.LetterSpacingDelta.Should().Be(0.5);
    }

    [Fact]
    public void Resolve_AppliesMetricProfilesBeforeRules()
    {
        var ruleSet = new GeneratorRuleSet
        {
            MetricProfiles =
            [
                new GeneratorMetricProfile
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "ugui",
                        ComponentType = ComponentType.Label,
                        SemanticClass = "pixel-text",
                        SizeBand = "small"
                    },
                    Template = new GeneratorActionTemplate
                    {
                        Kind = "fontSizeDelta",
                        NumberValue = 1
                    },
                    Action = new GeneratorRuleAction
                    {
                        Text = new GeneratorTextRuleAction
                        {
                            LetterSpacingDelta = 0.5
                        }
                    }
                }
            ],
            Rules =
            [
                new GeneratorRule
                {
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "ugui",
                        NodeId = "body"
                    },
                    Template = new GeneratorActionTemplate
                    {
                        Kind = "fontSizeDelta",
                        NumberValue = 2
                    }
                }
            ]
        };

        var node = new ComponentNode
        {
            Id = "body",
            Type = ComponentType.Label,
            Style = new StyleSpec
            {
                FontSize = 9
            },
            InstanceOverrides = new Dictionary<string, object?>
            {
                [BoomHudMetadataKeys.PencilTextGrowth] = "fixed-width"
            }
        };

        var resolver = new RuleResolver(ruleSet, "ugui");
        var policy = resolver.Resolve("QuestSidebar", node);

        policy.Text.FontSizeDelta.Should().Be(3);
        policy.Text.LetterSpacingDelta.Should().Be(0.5);
    }
}
