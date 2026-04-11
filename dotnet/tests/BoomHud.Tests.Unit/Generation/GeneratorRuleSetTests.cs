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
                  "offsetX": 3,
                  "offsetY": -2
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
        rule.Action.Layout.OffsetX.Should().Be(3);
        rule.Action.Layout.OffsetY.Should().Be(-2);
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
}
