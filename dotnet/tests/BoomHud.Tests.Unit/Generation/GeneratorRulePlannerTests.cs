using System.Collections.Generic;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;
using BoomHud.Generators;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public sealed class GeneratorRulePlannerTests
{
    [Fact]
    public void CreatePlan_UsesPreconditionsEffectsAndPhaseCostOrdering()
    {
        var ruleSet = new GeneratorRuleSet
        {
            Rules =
            [
                new GeneratorRule
                {
                    Name = "layout-first",
                    Phase = GeneratorRulePhase.Layout,
                    Cost = 2,
                    Preconditions =
                    [
                        new GeneratorRuleFact
                        {
                            Key = "finding.layout",
                            Value = "present"
                        }
                    ],
                    Effects =
                    [
                        new GeneratorRuleFact
                        {
                            Key = "layout.adjusted",
                            Value = "true"
                        }
                    ],
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "unity",
                        ComponentType = ComponentType.Container
                    },
                    Action = new GeneratorRuleAction
                    {
                        Layout = new GeneratorLayoutRuleAction
                        {
                            Padding = 8
                        }
                    }
                },
                new GeneratorRule
                {
                    Name = "text-second",
                    Phase = GeneratorRulePhase.Text,
                    Cost = 1,
                    Preconditions =
                    [
                        new GeneratorRuleFact
                        {
                            Key = "layout.adjusted",
                            Value = "true"
                        }
                    ],
                    Effects =
                    [
                        new GeneratorRuleFact
                        {
                            Key = "text.adjusted",
                            Value = "true"
                        }
                    ],
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
                    Name = "blocked-motion",
                    Phase = GeneratorRulePhase.Motion,
                    Preconditions =
                    [
                        new GeneratorRuleFact
                        {
                            Key = "motion.enabled",
                            Value = "true"
                        }
                    ],
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "unity"
                    },
                    Action = new GeneratorRuleAction()
                }
            ]
        };

        var plan = GeneratorRulePlanner.CreatePlan(ruleSet, new[]
        {
            new GeneratorRuleFact
            {
                Key = "finding.layout",
                Value = "present"
            }
        }, "fixture-plan");

        plan.Name.Should().Be("fixture-plan");
        plan.AppliedRules.Should().HaveCount(2);
        plan.AppliedRules[0].Name.Should().Be("layout-first");
        plan.AppliedRules[1].Name.Should().Be("text-second");
        plan.TotalCost.Should().Be(3);
        plan.FinalFacts.Should().ContainEquivalentOf(new GeneratorRuleFact
        {
            Key = "text.adjusted",
            Value = "true"
        });
        plan.SkippedRules.Should().ContainSingle();
        plan.SkippedRules[0].Name.Should().Be("blocked-motion");
        plan.SkippedRules[0].MissingPreconditions.Should().ContainSingle();
    }

    [Fact]
    public void BuildExecutableRuleSet_OnlyIncludesAppliedRulesInPlanOrder()
    {
        var plan = new GeneratorRulePlan
        {
            AppliedRules =
            [
                new GeneratorPlannedRule
                {
                    OriginalIndex = 2,
                    Name = "text-phase",
                    Phase = GeneratorRulePhase.Text,
                    Cost = 1,
                    Specificity = 2,
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
                new GeneratorPlannedRule
                {
                    OriginalIndex = 1,
                    Name = "layout-phase",
                    Phase = GeneratorRulePhase.Layout,
                    Cost = 1,
                    Specificity = 1,
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "unity"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Layout = new GeneratorLayoutRuleAction
                        {
                            Padding = 6
                        }
                    }
                }
            ]
        };

        var executable = GeneratorRulePlanner.BuildExecutableRuleSet(plan);

        executable.Rules.Should().HaveCount(2);
        executable.Rules[0].Name.Should().Be("layout-phase");
        executable.Rules[1].Name.Should().Be("text-phase");
    }

    [Fact]
    public void CreatePlan_CompilesTemplates_AndGatesMotionActionsOnFacts()
    {
        var ruleSet = new GeneratorRuleSet
        {
            Rules =
            [
                new GeneratorRule
                {
                    Name = "layout-pass",
                    Phase = GeneratorRulePhase.Layout,
                    Cost = 1,
                    Preconditions =
                    [
                        new GeneratorRuleFact
                        {
                            Key = "finding.layout",
                            Value = "present"
                        }
                    ],
                    Effects =
                    [
                        new GeneratorRuleFact
                        {
                            Key = "layout.adjusted",
                            Value = "true"
                        }
                    ],
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "unity",
                        DocumentName = "PartyHud"
                    },
                    Template = new GeneratorActionTemplate
                    {
                        Kind = "paddingDelta",
                        NumberValue = 4
                    }
                },
                new GeneratorRule
                {
                    Name = "motion-pass",
                    Phase = GeneratorRulePhase.Motion,
                    Cost = 2,
                    Preconditions =
                    [
                        new GeneratorRuleFact
                        {
                            Key = "layout.adjusted",
                            Value = "true"
                        },
                        new GeneratorRuleFact
                        {
                            Key = "motion.enabled",
                            Value = "true"
                        }
                    ],
                    Effects =
                    [
                        new GeneratorRuleFact
                        {
                            Key = "motion.sequence.normalized",
                            Value = "true"
                        }
                    ],
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "unity",
                        ClipId = "intro",
                        MotionProperty = MotionProperty.Opacity
                    },
                    Template = new GeneratorActionTemplate
                    {
                        Kind = "durationQuantization",
                        NumberValue = 8
                    }
                }
            ]
        };

        var withoutMotion = GeneratorRulePlanner.CreatePlan(ruleSet, new[]
        {
            new GeneratorRuleFact
            {
                Key = "finding.layout",
                Value = "present"
            }
        });

        withoutMotion.AppliedRules.Should().ContainSingle();
        withoutMotion.SkippedRules.Should().ContainSingle(rule => rule.Name == "motion-pass");

        var withMotion = GeneratorRulePlanner.CreatePlan(ruleSet, new[]
        {
            new GeneratorRuleFact
            {
                Key = "finding.layout",
                Value = "present"
            },
            new GeneratorRuleFact
            {
                Key = "motion.enabled",
                Value = "true"
            }
        });

        withMotion.AppliedRules.Should().HaveCount(2);
        withMotion.AppliedRules[0].Action.Layout!.PaddingDelta.Should().Be(4);
        withMotion.AppliedRules[1].Action.Motion!.DurationQuantizationFrames.Should().Be(8);
    }

    [Fact]
    public void GetSpecificity_CountsSemanticAndStyleSelectors()
    {
        var selector = new GeneratorRuleSelector
        {
            Backend = "remotion",
            ComponentType = ComponentType.Label,
            FontFamily = "Press Start 2P",
            TextGrowth = "fixed-width",
            SemanticClass = "pixel-text",
            SizeBand = "small"
        };

        GeneratorRulePlanner.GetSpecificity(selector).Should().Be(6);
    }
}
