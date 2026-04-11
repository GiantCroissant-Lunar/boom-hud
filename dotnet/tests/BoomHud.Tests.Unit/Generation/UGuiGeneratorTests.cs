using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;
using BoomHud.Gen.UGui;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public sealed class UGuiGeneratorTests
{
    private readonly UGuiGenerator _generator = new();
    private readonly GenerationOptions _options = new()
    {
        Namespace = "TestNamespace",
        IncludeComments = true,
        UseNullableAnnotations = true
    };

    [Fact]
    public void Generate_MinimalDocument_ProducesUGuiArtifacts()
    {
        var doc = new HudDocument
        {
            Name = "TestComponent",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        result.Success.Should().BeTrue();
        result.Files.Should().Contain(f => f.Path == "TestComponentView.ugui.cs");
        result.Files.Should().Contain(f => f.Path == "ITestComponentViewModel.g.cs");

        var viewFile = result.Files.First(f => f.Path == "TestComponentView.ugui.cs");
        viewFile.Content.Should().Contain("using UnityEngine.UI;");
        viewFile.Content.Should().Contain("public sealed class TestComponentView");
        viewFile.Content.Should().Contain("public RectTransform Root { get; }");
        viewFile.Content.Should().Contain("Root = CreateRect(\"TestComponentRoot\", parent);");
    }

    [Fact]
    public void Generate_WithBindings_EmitsViewModelInterfaceAndRefreshAssignments()
    {
        var doc = new HudDocument
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
                        Id = "statusLabel",
                        Type = ComponentType.Label,
                        Bindings = [new BindingSpec { Property = "text", Path = "Player.HealthText" }],
                        Visible = BindableValue<bool>.Bind("Player.IsVisible")
                    },
                    new ComponentNode
                    {
                        Id = "actionButton",
                        Type = ComponentType.Button,
                        Bindings = [new BindingSpec { Property = "text", Path = "Player.ActionLabel" }],
                        Enabled = BindableValue<bool>.Bind("Player.CanAct")
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path == "StatusHudView.ugui.cs");
        viewFile.Content.Should().Contain("public Text StatusLabel { get; }");
        viewFile.Content.Should().Contain("public Button ActionButton { get; }");
        viewFile.Content.Should().Contain("StatusLabel.text = AsString(_viewModel.PlayerHealthText);");
        viewFile.Content.Should().Contain("StatusLabel.gameObject.SetActive(AsBool(_viewModel.PlayerIsVisible, true));");
        viewFile.Content.Should().Contain("SetButtonText(ActionButton, AsString(_viewModel.PlayerActionLabel));");
        viewFile.Content.Should().Contain("ApplyEnabled(ActionButton, AsBool(_viewModel.PlayerCanAct, true));");

        var interfaceFile = result.Files.First(f => f.Path == "IStatusHudViewModel.g.cs");
        interfaceFile.Content.Should().Contain("object? PlayerHealthText { get; }");
        interfaceFile.Content.Should().Contain("object? PlayerIsVisible { get; }");
        interfaceFile.Content.Should().Contain("object? PlayerActionLabel { get; }");
        interfaceFile.Content.Should().Contain("object? PlayerCanAct { get; }");
    }

    [Fact]
    public void Generate_ComponentRef_ComposesReferencedUGuiView()
    {
        var badge = new HudComponentDefinition
        {
            Id = "badge",
            Name = "Badge",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "label",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "READY"
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
        {
            Name = "StatusHud",
            Components = new Dictionary<string, HudComponentDefinition>
            {
                ["badge"] = badge
            },
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "readyBadge",
                        Type = ComponentType.Container,
                        ComponentRefId = "badge"
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path == "StatusHudView.ugui.cs");
        viewFile.Content.Should().Contain("var readyBadgeView = new BadgeView(Root);");
        viewFile.Content.Should().Contain("ReadyBadge = readyBadgeView.Root;");
    }

    [Fact]
    public void Generate_NumericIds_AreSanitizedIntoValidIdentifiers()
    {
        var doc = new HudDocument
        {
            Name = "MinimapHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "00",
                        Type = ComponentType.Container
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "MinimapHudView.ugui.cs");

        viewFile.Content.Should().Contain("public RectTransform Node00 { get; }");
        viewFile.Content.Should().Contain("Node00 = CreateRect(\"Node00\", Root);");
    }

    [Fact]
    public void Generate_IconComponent_NormalizesTokenText()
    {
        var doc = new HudDocument
        {
            Name = "IconHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "classIcon",
                        Type = ComponentType.Icon,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "shield"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "IconHudView.ugui.cs");

        viewFile.Content.Should().Contain("ClassIcon.text = ResolveIconText(\"shield\");");
        viewFile.Content.Should().Contain("\"shield\"=>\"\\uE158\"");
        viewFile.Content.Should().Contain("ApplyStyle(ClassIcon, fg: null, bg: null, fontFamily: null, fontSize: 16");
        viewFile.Content.Should().Contain("treatAsIcon: true");
    }

    [Fact]
    public void Generate_LeafNodes_DoNotEmitLayoutGroups_AndBordersAreApplied()
    {
        var doc = new HudDocument
        {
            Name = "MiniMapTile",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Width = Dimension.Pixels(24),
                    Height = Dimension.Pixels(24)
                },
                Style = new StyleSpec
                {
                    Background = Color.Black,
                    Border = new BorderSpec
                    {
                        Style = BorderStyle.Solid,
                        Color = Color.White,
                        Width = 2
                    }
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "icon",
                        Type = ComponentType.Icon,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical,
                            Width = Dimension.Pixels(24),
                            Height = Dimension.Pixels(24)
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "flame"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "MiniMapTileView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyBorder(component.gameObject,ParseColor(borderColor,Color.white),borderWidth.Value);");
        viewFile.Content.Should().Contain("ApplyStyle(Root, fg: null, bg: \"#000000\", fontFamily: null, fontSize: null, borderColor: \"#FFFFFF\", borderWidth: 2f, treatAsIcon: false);");
        viewFile.Content.Should().NotContain("ApplyVerticalLayout(RectOf(Icon)");
    }

    [Fact]
    public void Generate_FixedWidthLabel_WithLineHeight_EmitsTextMetrics()
    {
        var doc = new HudDocument
        {
            Name = "WrappedHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "body",
                        Type = ComponentType.Label,
                        InstanceOverrides = new Dictionary<string, object?>
                        {
                            [BoomHudMetadataKeys.PencilTextGrowth] = "fixed-width"
                        },
                        Style = new StyleSpec
                        {
                            FontSize = 14,
                            LineHeight = 1.4
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "Wrapped copy"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "WrappedHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyTextMetrics(Body, lineSpacing: 1.4f, wrapText: true);");
        viewFile.Content.Should().Contain("text.lineSpacing=lineSpacing.Value;");
        viewFile.Content.Should().Contain("text.horizontalOverflow=wrapText?HorizontalWrapMode.Wrap:HorizontalWrapMode.Overflow;");
    }

    [Fact]
    public void Generate_AutoHeightLayoutRoot_UsesContentSizeFitter()
    {
        var doc = new HudDocument
        {
            Name = "AutoHeightHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Width = Dimension.Pixels(130)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "Ready"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "AutoHeightHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyContentSizeFit(Root, horizontal: false, vertical: true);");
    }

    [Fact]
    public void Generate_TextStackChildrenWithContentHug_PropagatesVerticalHugToContainer()
    {
        var doc = new HudDocument
        {
            Name = "ObjectiveHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "iconShell",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical,
                            Width = Dimension.Pixels(44),
                            Height = Dimension.Pixels(44)
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "icon",
                                Type = ComponentType.Icon,
                                Layout = new LayoutSpec
                                {
                                    Width = Dimension.Pixels(20),
                                    Height = Dimension.Pixels(20)
                                }
                            }
                        ]
                    },
                    new ComponentNode
                    {
                        Id = "body",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "title",
                                Type = ComponentType.Label,
                                Properties = new Dictionary<string, BindableValue<object?>>
                                {
                                    ["text"] = "Recover the key"
                                }
                            },
                            new ComponentNode
                            {
                                Id = "hint",
                                Type = ComponentType.Label,
                                Properties = new Dictionary<string, BindableValue<object?>>
                                {
                                    ["text"] = "Search the chapel."
                                }
                            }
                        ]
                    }
                ]
            }
        };

        var ruleSet = new GeneratorRuleSet
        {
            Rules =
            [
                new GeneratorRule
                {
                    Name = "stacked-line-hug",
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "ugui",
                        DocumentName = "ObjectiveHud",
                        SemanticClass = "stacked-text-line"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Layout = new GeneratorLayoutRuleAction
                        {
                            PreferContentHeight = true
                        }
                    }
                }
            ]
        };

        var result = _generator.Generate(doc, _options with { RuleSet = ruleSet });
        var viewFile = result.Files.First(f => f.Path == "ObjectiveHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyLayoutSizing(RectOf(Body), ignoreLayout: false, preferredWidth: null, preferredHeight: null, flexibleWidth: 1f, flexibleHeight: null);");
        viewFile.Content.Should().Contain("ApplyContentSizeFit(RectOf(Body), horizontal: false, vertical: true);");
    }

    [Fact]
    public void Generate_LayoutAlignmentPreset_EmitsUguiLayoutGroupAlignment()
    {
        var doc = new HudDocument
        {
            Name = "AlignedHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Gap = Spacing.Uniform(6),
                    Padding = Spacing.Uniform(4)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "icon",
                        Type = ComponentType.Icon,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(20),
                            Height = Dimension.Pixels(20)
                        }
                    }
                ]
            }
        };

        var ruleSet = new GeneratorRuleSet
        {
            Rules =
            [
                new GeneratorRule
                {
                    Name = "center-layout",
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "ugui",
                        DocumentName = "AlignedHud",
                        NodeId = "root"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Layout = new GeneratorLayoutRuleAction
                        {
                            FlexAlignmentPreset = "center"
                        }
                    }
                }
            ]
        };

        var result = _generator.Generate(doc, _options with { RuleSet = ruleSet });
        var viewFile = result.Files.First(f => f.Path == "AlignedHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyVerticalLayout(Root, 6f, 4, 4, 4, 4, \"center\");");
        viewFile.Content.Should().Contain("ApplyLayoutAlignment(group,alignmentPreset);");
    }

    [Fact]
    public void Generate_EmitsPrefabBindingApi_ForExistingHierarchy()
    {
        var doc = new HudDocument
        {
            Name = "PrefabHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "Ready"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "PrefabHudView.ugui.cs");

        viewFile.Content.Should().Contain("public static PrefabHudView Bind(RectTransform root, IPrefabHudViewModel? viewModel = null) => new(root, viewModel);");
        viewFile.Content.Should().Contain("private PrefabHudView(RectTransform root, IPrefabHudViewModel? viewModel)");
        viewFile.Content.Should().Contain("Title = RequireComponent<Text>(Root, \"Title\");");
        viewFile.Content.Should().Contain("private static RectTransform RequireRect(Transform root,string path)");
        viewFile.Content.Should().Contain("private static T RequireComponent<T>(Transform root,string path) where T : Component");
    }

    [Fact]
    public void Generate_WithMotion_EmitsSharedTimelineArtifactsForUGui()
    {
        var doc = new HudDocument
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
                        Type = ComponentType.Label
                    }
                ]
            }
        };

        var motion = new MotionDocument
        {
            Name = "StatusHudMotion",
            FramesPerSecond = 30,
            DefaultSequenceId = "introSequence",
            Clips =
            [
                new MotionClip
                {
                    Id = "intro",
                    Name = "Intro",
                    DurationFrames = 24,
                    Tracks =
                    [
                        new MotionTrack
                        {
                            Id = "rootTrack",
                            TargetId = "root",
                            TargetKind = MotionTargetKind.Root,
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Opacity,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromNumber(0) },
                                        new MotionKeyframe { Frame = 12, Value = MotionValue.FromNumber(1), Easing = MotionEasing.EaseOut }
                                    ]
                                }
                            ]
                        },
                        new MotionTrack
                        {
                            Id = "titleTrack",
                            TargetId = "title",
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Text,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromText("BOOT") },
                                        new MotionKeyframe { Frame = 18, Value = MotionValue.FromText("READY") }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ],
            Sequences =
            [
                new MotionSequence
                {
                    Id = "introSequence",
                    Name = "Intro Sequence",
                    Items =
                    [
                        new MotionSequenceItem
                        {
                            ClipId = "intro",
                            StartFrame = 0,
                            FillMode = MotionSequenceFillMode.HoldEnd
                        }
                    ]
                }
            ]
        };

        var result = _generator.Generate(doc, _options with { Motion = motion });

        result.Success.Should().BeTrue();
        result.Files.Should().Contain(f => f.Path == "StatusHudMotion.gen.cs");
        result.Files.Should().Contain(f => f.Path == "StatusHudMotionHost.gen.cs");

        var motionFile = result.Files.First(f => f.Path == "StatusHudMotion.gen.cs");
        motionFile.Content.Should().Contain("public const int FramesPerSecond = 30;");
        motionFile.Content.Should().Contain("public const string DefaultSequenceId = \"introSequence\";");
        motionFile.Content.Should().Contain("public static TimelineSequenceClip[] GetSequenceItems(string sequenceId)");
        motionFile.Content.Should().Contain("ApplyOpacity(view.Root, EvaluateNumber(localFrame, s_IntroRootTrackOpacity, 1f));");
        motionFile.Content.Should().Contain("ApplyText(view.Title, EvaluateString(localFrame, s_IntroTitleTrackText, string.Empty));");
        motionFile.Content.Should().Contain("var canvasGroup = target.GetComponent<CanvasGroup>();");
        motionFile.Content.Should().Contain("canvasGroup = target.gameObject.AddComponent<CanvasGroup>();");
        motionFile.Content.Should().Contain("var state = BoomHudMotionRectState.Capture(rectTransform);");

        var hostFile = result.Files.First(f => f.Path == "StatusHudMotionHost.gen.cs");
        hostFile.Content.Should().Contain("public class StatusHudMotionHost : BoomHudUguiMotionHost");
        hostFile.Content.Should().NotContain("OnMotionApplied(");
        hostFile.Content.Should().Contain("if (root.childCount == 1 && root.GetChild(0) is RectTransform childRoot && TryBindExisting(childRoot, out boundView))");
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generate_WithMotionRuleSet_AppliesMotionPoliciesForUGui()
    {
        var doc = new HudDocument
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
                        Type = ComponentType.Label
                    }
                ]
            }
        };

        var motion = new MotionDocument
        {
            Name = "StatusHudMotion",
            FramesPerSecond = 30,
            DefaultSequenceId = "introSequence",
            Clips =
            [
                new MotionClip
                {
                    Id = "intro",
                    Name = "Intro",
                    DurationFrames = 60,
                    Tracks =
                    [
                        new MotionTrack
                        {
                            Id = "rootTrack",
                            TargetId = "root",
                            TargetKind = MotionTargetKind.Root,
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Opacity,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromNumber(0), Easing = MotionEasing.EaseOut },
                                        new MotionKeyframe { Frame = 12, Value = MotionValue.FromNumber(1), Easing = MotionEasing.EaseOut }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ],
            Sequences =
            [
                new MotionSequence
                {
                    Id = "introSequence",
                    Name = "Intro Sequence",
                    Items =
                    [
                        new MotionSequenceItem
                        {
                            ClipId = "intro",
                            StartFrame = 0,
                            FillMode = MotionSequenceFillMode.HoldEnd
                        }
                    ]
                }
            ]
        };

        var result = _generator.Generate(doc, _options with
        {
            Motion = motion,
            RuleSet = new GeneratorRuleSet
            {
                Rules =
                [
                    new GeneratorRule
                    {
                        Phase = GeneratorRulePhase.Motion,
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            DocumentName = "StatusHud",
                            ClipId = "intro"
                        },
                        Template = new GeneratorActionTemplate
                        {
                            Kind = "durationQuantization",
                            NumberValue = 16
                        }
                    },
                    new GeneratorRule
                    {
                        Phase = GeneratorRulePhase.Motion,
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            DocumentName = "StatusHud",
                            SequenceId = "introSequence"
                        },
                        Template = new GeneratorActionTemplate
                        {
                            Kind = "fillModePolicy",
                            Parameters = new Dictionary<string, string>
                            {
                                ["fillMode"] = "HoldBoth"
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Phase = GeneratorRulePhase.Motion,
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            DocumentName = "StatusHud",
                            TrackId = "rootTrack",
                            MotionProperty = MotionProperty.Opacity
                        },
                        Template = new GeneratorActionTemplate
                        {
                            Kind = "easingRemap",
                            Parameters = new Dictionary<string, string>
                            {
                                ["easing"] = "Linear"
                            }
                        }
                    }
                ]
            }
        });

        var motionFile = result.Files.First(f => f.Path == "StatusHudMotion.gen.cs").Content;
        motionFile.Should().Contain("public const string DefaultSequenceId = \"introSequence\";");
        motionFile.Should().Contain("FillMode = TimelineSequenceFillMode.HoldBoth");
        motionFile.Should().Contain("=> 64,");
        motionFile.Should().Contain("EaseMode.Linear");
    }

    [Fact]
    public void Generate_WithRuleSet_RemapsLabelToInputField()
    {
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
                            Backend = "ugui",
                            NodeId = "title",
                            ComponentType = ComponentType.Label
                        },
                        Action = new GeneratorRuleAction
                        {
                            ControlType = "InputField"
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
        {
            Name = "RuleHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "Ready"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "RuleHudView.ugui.cs");

        viewFile.Content.Should().Contain("public InputField Title { get; }");
        viewFile.Content.Should().Contain("Title = CreateInput(\"Title\", Root, false);");
        viewFile.Content.Should().Contain("Title.text = \"Ready\";");
    }

    [Fact]
    public void Generate_WithRuleSet_OverridesTextIconAndLayoutPolicies()
    {
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
                            Backend = "ugui",
                            NodeId = "body"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Text = new GeneratorTextRuleAction
                            {
                                WrapText = true,
                                LineHeight = 1.6,
                                FontSize = 18
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            NodeId = "classIcon"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Icon = new GeneratorIconRuleAction
                            {
                                BaselineOffset = 1.5,
                                OpticalCentering = false,
                                SizeMode = "match-height",
                                FontSize = 20
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            NodeId = "root"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                Gap = 9,
                                Padding = 7
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            NodeId = "badge"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                ForceAbsolutePositioning = true,
                                OffsetX = 3,
                                OffsetY = 4
                            }
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
        {
            Name = "PolicyHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Gap = new Spacing(2),
                    Padding = new Spacing(1)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "body",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            FontSize = 14
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "Wrapped copy"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "classIcon",
                        Type = ComponentType.Icon,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(24),
                            Height = Dimension.Pixels(24)
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "shield"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "badge",
                        Type = ComponentType.Container
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "PolicyHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyStyle(Body, fg: null, bg: null, fontFamily: null, fontSize: 18");
        viewFile.Content.Should().Contain("ApplyTextMetrics(Body, lineSpacing: 1.6f, wrapText: true);");
        viewFile.Content.Should().Contain("ApplyIconMetrics(ClassIcon, boxWidth: 24f, boxHeight: 24f, baselineOffset: 1.5f, opticalCentering: false, sizeMode: \"match-height\", explicitFontSize: 20f);");
        viewFile.Content.Should().Contain("ApplyVerticalLayout(Root, 9f, 7, 7, 7, 7);");
        viewFile.Content.Should().Contain("ConfigureRect(RectOf(Badge), width: null, height: null, left: 3f, top: 4f, absolute: true);");
    }

    [Fact]
    public void Generate_WithRuleSet_AnchorPivotAndRectTransformMode_EmitsRectTransformOverrides()
    {
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
                            Backend = "ugui",
                            NodeId = "badge"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                AnchorPreset = "top-center",
                                PivotPreset = "center",
                                RectTransformMode = "stretch-parent"
                            }
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
        {
            Name = "AnchorHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "badge",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(40),
                            Height = Dimension.Pixels(20)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "AnchorHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyRectAnchorPreset(RectOf(Badge), \"top-center\");");
        viewFile.Content.Should().Contain("ApplyRectPivotPreset(RectOf(Badge), \"center\");");
        viewFile.Content.Should().Contain("ApplyRectTransformMode(RectOf(Badge), \"stretch-parent\");");
        viewFile.Content.Should().Contain("private static void ApplyRectAnchorPreset(RectTransform rect,string preset)");
        viewFile.Content.Should().Contain("private static void ApplyRectPivotPreset(RectTransform rect,string preset)");
        viewFile.Content.Should().Contain("private static void ApplyRectTransformMode(RectTransform rect,string mode)");
    }

    [Fact]
    public void Generate_WithRuleSet_LayoutDeltasAndEdgeInsetPolicy_EmitsAdjustedLayoutOverrides()
    {
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
                            Backend = "ugui",
                            NodeId = "root"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                GapDelta = 2,
                                PaddingDelta = 3
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            NodeId = "badge"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                ForceAbsolutePositioning = true,
                                OffsetXDelta = 3,
                                OffsetYDelta = -2,
                                EdgeInsetPolicy = "match-parent"
                            }
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
        {
            Name = "DeltaHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Gap = new Spacing(4),
                    Padding = new Spacing(1)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "badge",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Absolute,
                            Left = Dimension.Pixels(5),
                            Top = Dimension.Pixels(10),
                            Width = Dimension.Pixels(40),
                            Height = Dimension.Pixels(20)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "DeltaHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyVerticalLayout(Root, 6f, 4, 4, 4, 4);");
        viewFile.Content.Should().Contain("ConfigureRect(RectOf(Badge), width: 40f, height: 20f, left: 8f, top: 8f, absolute: true);");
        viewFile.Content.Should().Contain("ApplyEdgeInsetPolicy(RectOf(Badge), \"match-parent\");");
        viewFile.Content.Should().Contain("private static void ApplyEdgeInsetPolicy(RectTransform rect,string policy)");
    }

    [Fact]
    public void Generate_WithRuleSet_PreferredSizeDeltas_AdjustsLayoutSizing()
    {
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
                            Backend = "ugui",
                            NodeId = "card"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                PreferredWidthDelta = -20,
                                PreferredHeightDelta = 15
                            }
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
        {
            Name = "PreferredSizeHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "card",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical,
                            Width = Dimension.Pixels(120),
                            Height = Dimension.Pixels(80)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "PreferredSizeHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyLayoutSizing(RectOf(Card), ignoreLayout: false, preferredWidth: 100f, preferredHeight: 95f, flexibleWidth: null, flexibleHeight: null);");
    }
}
