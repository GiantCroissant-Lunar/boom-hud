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
}
