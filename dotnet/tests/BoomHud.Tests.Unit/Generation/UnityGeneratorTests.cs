using System;
using System.Collections.Generic;
using System.Text.Json;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;
using BoomHud.Gen.Unity;
using BoomHud.Generators;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public class UnityGeneratorTests
{
    private readonly UnityGenerator _generator = new();
    private readonly GenerationOptions _options = new()
    {
        Namespace = "TestNamespace",
        IncludeComments = true,
        UseNullableAnnotations = true
    };

    [Fact]
    public void Generate_MinimalDocument_ProducesUnityToolkitArtifacts()
    {
        var doc = new HudDocument
        {
            Name = "TestComponent",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        result.Success.Should().BeTrue();
        result.Files.Should().HaveCount(4);
        result.Files.Should().Contain(f => f.Path == "TestComponentView.uxml" && f.Type == GeneratedFileType.Markup);
        result.Files.Should().Contain(f => f.Path == "TestComponentView.uss" && f.Type == GeneratedFileType.Resource);
        result.Files.Should().Contain(f => f.Path == "TestComponentView.gen.cs" && f.Type == GeneratedFileType.SourceCode);
        result.Files.Should().Contain(f => f.Path == "ITestComponentViewModel.g.cs" && f.Type == GeneratedFileType.SourceCode);

        var uxmlFile = result.Files.First(f => f.Path == "TestComponentView.uxml");
        uxmlFile.Content.Should().Contain("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">");
        uxmlFile.Content.Should().Contain("<ui:VisualElement name=\"TestComponentRoot\" class=\"boomhud-test-component-root\" />");
    }

    [Fact]
    public void Generate_DefaultMode_DoesNotEmitVisualIrArtifact()
    {
        var doc = new HudDocument
        {
            Name = "VisualHud",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        result.Files.Should().NotContain(file => file.Path == "VisualHud.visual-ir.json");
    }

    [Fact]
    public void Generate_WhenRequested_EmitsVisualIrArtifact()
    {
        var doc = new HudDocument
        {
            Name = "VisualHud",
            Root = new ComponentNode
            {
                Id = "title",
                Type = ComponentType.Label,
                Style = new StyleSpec
                {
                    FontFamily = "Press Start 2P",
                    FontSize = 12
                }
            }
        };

        var result = _generator.Generate(doc, _options with { EmitVisualIrArtifact = true });
        var visualIr = result.Files.Single(file => file.Path == "VisualHud.visual-ir.json").Content;

        visualIr.Should().Contain("\"DocumentName\": \"VisualHud\"");
        visualIr.Should().Contain("\"BackendFamily\": \"unity\"");
    }

    [Fact]
    public void Generate_WhenRequested_EmitsVisualPlanningArtifacts()
    {
        var doc = new HudDocument
        {
            Name = "VisualHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title-a",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "QUEST"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "title-b",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "BONUS"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options with
        {
            EmitVisualSynthesisArtifact = true,
            EmitVisualRefinementArtifact = true
        });

        result.Files.Should().Contain(file => file.Path == "VisualHud.visual-synthesis.json");
        result.Files.Should().Contain(file => file.Path == "VisualHud.visual-refinement.json");
    }

    [Fact]
    public void Generate_WithMotion_EmitsUnityMotionAdapter()
    {
        var options = _options with
        {
            Motion = new MotionDocument
            {
                Name = "StatusHudMotion",
                FramesPerSecond = 30,
                Clips =
                [
                    new MotionClip
                    {
                        Id = "intro",
                        Name = "Intro",
                        DurationFrames = 48,
                        Tracks =
                        [
                            new MotionTrack
                            {
                                Id = "statusFade",
                                TargetId = "statusLabel",
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
                                    },
                                    new MotionChannel
                                    {
                                        Property = MotionProperty.Text,
                                        Keyframes =
                                        [
                                            new MotionKeyframe { Frame = 0, Value = MotionValue.FromText("BOOT") },
                                            new MotionKeyframe { Frame = 24, Value = MotionValue.FromText("READY") }
                                        ]
                                    }
                                ]
                            },
                            new MotionTrack
                            {
                                Id = "statusMove",
                                TargetId = "statusLabel",
                                Channels =
                                [
                                    new MotionChannel
                                    {
                                        Property = MotionProperty.PositionX,
                                        Keyframes =
                                        [
                                            new MotionKeyframe { Frame = 0, Value = MotionValue.FromNumber(-12) },
                                            new MotionKeyframe { Frame = 24, Value = MotionValue.FromNumber(0), Easing = MotionEasing.EaseOut }
                                        ]
                                    },
                                    new MotionChannel
                                    {
                                        Property = MotionProperty.Rotation,
                                        Keyframes =
                                        [
                                            new MotionKeyframe { Frame = 0, Value = MotionValue.FromNumber(-8) },
                                            new MotionKeyframe { Frame = 24, Value = MotionValue.FromNumber(0), Easing = MotionEasing.EaseInOut }
                                        ]
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

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
                        Type = ComponentType.Label
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);

        result.Files.Should().Contain(f => f.Path == "StatusHudMotion.gen.cs");
        result.Files.Should().Contain(f => f.Path == "StatusHudMotionHost.gen.cs");
        var motionFile = result.Files.First(f => f.Path == "StatusHudMotion.gen.cs");
        var hostFile = result.Files.First(f => f.Path == "StatusHudMotionHost.gen.cs");
        motionFile.Content.Should().Contain("public const string DefaultClipId = \"intro\";");
        motionFile.Content.Should().Contain("public static readonly string[] ClipIds =");
        motionFile.Content.Should().Contain("public static bool TryApplyAtFrame(StatusHudView view, string clipId, int frame)");
        motionFile.Content.Should().Contain("ApplyOpacity(view.StatusLabel, EvaluateNumber(localFrame, s_IntroStatusFadeOpacity, 1f));");
        motionFile.Content.Should().Contain("ApplyText(view.StatusLabel, EvaluateString(localFrame, s_IntroStatusFadeText, string.Empty));");
        motionFile.Content.Should().Contain("ApplyTranslate(view.StatusLabel, EvaluateNumber(localFrame, s_IntroStatusMovePositionX, 0f), 0f);");
        motionFile.Content.Should().Contain("ApplyRotation(view.StatusLabel, EvaluateNumber(localFrame, s_IntroStatusMoveRotation, 0f));");
        hostFile.Content.Should().Contain("public partial class StatusHudMotionHost : BoomHudUiToolkitMotionHost");
        hostFile.Content.Should().Contain("_view = new StatusHudView(generatedRoot);");
        hostFile.Content.Should().Contain("return _view != null && StatusHudMotion.TryApplyAtTime(_view, clipId, timeSeconds);");
    }

    [Fact]
    public void Generate_WithViewModelNamespace_UsesThatNamespaceInController()
    {
        var options = _options with { ViewModelNamespace = "FantaSim.Hud.Contracts" };

        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, options);

        var controllerFile = result.Files.First(f => f.Path == "TestView.gen.cs");
        controllerFile.Content.Should().Contain("using FantaSim.Hud.Contracts;");
        controllerFile.Content.Should().Contain("namespace TestNamespace");
    }

    [Fact]
    public void Generate_WithNoViewModelInterfaces_SuppressesInterfaceFile()
    {
        var options = _options with { EmitViewModelInterfaces = false };

        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, options);

        result.Files.Should().Contain(f => f.Path == "TestView.uxml");
        result.Files.Should().Contain(f => f.Path == "TestView.uss");
        result.Files.Should().Contain(f => f.Path == "TestView.gen.cs");
        result.Files.Should().NotContain(f => f.Path == "ITestViewModel.g.cs");
    }

    [Fact]
    public void Generate_ComponentRef_ComposesReferencedUnityView()
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

        var viewFile = result.Files.First(f => f.Path == "StatusHudView.gen.cs");
        viewFile.Content.Should().Contain("var readyBadgePlaceholder = Root.Q<VisualElement>(\"ReadyBadge\")");
        viewFile.Content.Should().Contain("_readyBadgeComponent = BadgeView.Attach(readyBadgePlaceholder, null);");
        viewFile.Content.Should().Contain("ReadyBadge = _readyBadgeComponent.Root;");

        var badgeFile = result.Files.First(f => f.Path == "BadgeView.gen.cs");
        badgeFile.Content.Should().Contain("public static BadgeView Attach(VisualElement placeholder, IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? componentOverrides = null)");
        badgeFile.Content.Should().Contain("private static VisualElement CreateGeneratedRoot(string? instanceName)");
    }

    [Fact]
    public void Generate_ComponentRefWithInstanceOverrides_PassesOverrideBagIntoAttachedView()
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
                        ComponentRefId = "badge",
                        InstanceOverrides = new Dictionary<string, object?>
                        {
                            [BoomHudMetadataKeys.ComponentPropertyOverrides] = new Dictionary<string, object?>
                            {
                                [ComponentInstanceOverrideSupport.ChildPath(ComponentInstanceOverrideSupport.RootPath, 0)] = new Dictionary<string, object?>
                                {
                                    ["text"] = "GO"
                                }
                            }
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "StatusHudView.gen.cs");
        var badgeFile = result.Files.First(f => f.Path == "BadgeView.gen.cs");

        viewFile.Content.Should().Contain("BadgeView.Attach(readyBadgePlaceholder, new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal)");
        badgeFile.Content.Should().Contain("private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? _componentOverrides;");
        badgeFile.Content.Should().Contain("private void ApplyInstanceOverrides()");
        badgeFile.Content.Should().Contain("TryGetComponentOverrideValue(\"$/0\", \"text\", out var componentOverrideValue0)");
    }

    [Fact]
    public void Generate_WithSyntheticExactReuse_EmitsSyntheticComponentArtifactsForUnity()
    {
        var doc = new HudDocument
        {
            Name = "QuestHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    CreateSyntheticCandidateCard("card-alpha", 12, 32),
                    CreateSyntheticCandidateCard("card-bravo", 312, 64)
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        result.Files.Should().Contain(file => file.Path == "QuestHud.synthetic-components.json");
        result.Files.Should().Contain(file => file.Path.StartsWith("Synthetic", StringComparison.Ordinal) && file.Path.EndsWith("View.uxml", StringComparison.Ordinal));
        result.Files.Should().Contain(file => file.Path.StartsWith("Synthetic", StringComparison.Ordinal) && file.Path.EndsWith("View.gen.cs", StringComparison.Ordinal));

        var viewFile = result.Files.First(f => f.Path == "QuestHudView.gen.cs");
        viewFile.Content.Should().Contain("generated component placeholder");
        viewFile.Content.Should().Contain("Attach(");
    }

    [Fact]
    public void Generate_ViewModelInterface_ContainsBindingProperties_AndControllerRefreshesBindings()
    {
        var doc = new HudDocument
        {
            Name = "StatusHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "statusLabel",
                        Type = ComponentType.Label,
                        Bindings =
                        [
                            new BindingSpec { Property = "text", Path = "Player.HealthText" }
                        ],
                        Visible = BindableValue<bool>.Bind("Player.IsAlive"),
                        Tooltip = BindableValue<string>.Bind("Player.Tooltip")
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var controllerFile = result.Files.First(f => f.Path == "StatusHudView.gen.cs");
        controllerFile.Content.Should().Contain("public Label StatusLabel { get; }");
        controllerFile.Content.Should().Contain("StatusLabel = Root.Q<Label>(\"StatusLabel\") ?? throw new InvalidOperationException");
        controllerFile.Content.Should().Contain("StatusLabel.text = AsString(_viewModel.PlayerHealthText);");
        controllerFile.Content.Should().Contain("StatusLabel.style.display = AsBool(_viewModel.PlayerIsAlive) ? DisplayStyle.Flex : DisplayStyle.None;");
        controllerFile.Content.Should().Contain("StatusLabel.tooltip = AsString(_viewModel.PlayerTooltip);");

        var viewModelFile = result.Files.First(f => f.Path == "IStatusHudViewModel.g.cs");
        viewModelFile.Content.Should().Contain("object? PlayerHealthText { get; }");
        viewModelFile.Content.Should().Contain("object? PlayerIsAlive { get; }");
        viewModelFile.Content.Should().Contain("object? PlayerTooltip { get; }");
    }

    [Fact]
    public void Generate_WithStyleAndGridLayout_EmitsUssAndFallbackDiagnostic()
    {
        var doc = new HudDocument
        {
            Name = "StyledHud",
            Root = new ComponentNode
            {
                Id = "rootPanel",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Grid,
                    Width = Dimension.Pixels(320),
                    Padding = new Spacing(8)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "titleLabel",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            Foreground = Color.Red,
                            Background = Color.Black,
                            FontFamily = "Press Start 2P",
                            FontSize = 18,
                            LineHeight = 1.5,
                            FontWeight = FontWeight.Bold,
                            LetterSpacing = 1,
                            Opacity = 0.8,
                            BorderRadius = 6
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        result.Diagnostics.Should().Contain(d => d.Code == "BHU1001");

        var ussFile = result.Files.First(f => f.Path == "StyledHudView.uss");
        ussFile.Content.Should().Contain(".boomhud-root-panel {");
        ussFile.Content.Should().Contain("flex-direction: column;");
        ussFile.Content.Should().Contain("width: 320px;");
        ussFile.Content.Should().Contain("padding-top: 8px;");
        ussFile.Content.Should().Contain(".boomhud-title-label {");
        ussFile.Content.Should().Contain("color: #FF0000;");
        ussFile.Content.Should().Contain("background-color: #000000;");
        ussFile.Content.Should().Contain("font-size: 18px;");
        ussFile.Content.Should().Contain("line-height: 27px;");
        ussFile.Content.Should().Contain("letter-spacing: 1px;");
        ussFile.Content.Should().Contain("-unity-font-style: bold;");
        ussFile.Content.Should().Contain("opacity: 0.8;");
        ussFile.Content.Should().Contain("border-top-left-radius: 6px;");

        var controllerFile = result.Files.First(f => f.Path == "StyledHudView.gen.cs");
        controllerFile.Content.Should().Contain("ApplyFontFamily(TitleLabel, \"Press Start 2P\", 18f);");
        controllerFile.Content.Should().Contain("TitleLabel.style.fontSize = 18f;");
        controllerFile.Content.Should().Contain("TitleLabel.style.letterSpacing = 1f;");
        controllerFile.Content.Should().Contain("TitleLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;");
        controllerFile.Content.Should().Contain("FontDefinition.FromSDFFont(fontAsset);");
    }

    [Fact]
    public void Generate_WithMappedForegroundBinding_EmitsRuntimeColorRefresh()
    {
        using var mapDocument = JsonDocument.Parse("""
            {
              "good": "#00FF00",
              "warning": "#FFAA00"
            }
            """);

        var doc = new HudDocument
        {
            Name = "DynamicStyleHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "statusLabel",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            Foreground = Color.Green
                        },
                        Bindings =
                        [
                            new BindingSpec
                            {
                                Property = "style.foreground",
                                Path = "Player.StatusColor",
                                Fallback = "#00FF00",
                                ConverterParameter = mapDocument.RootElement.Clone()
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var controllerFile = result.Files.First(f => f.Path == "DynamicStyleHudView.gen.cs");
        controllerFile.Content.Should().Contain("using UnityEngine;");
        controllerFile.Content.Should().Contain("StatusLabel.style.color = ParseStyleColor(ResolveMappedStyleValue(_viewModel.PlayerStatusColor, \"#00FF00\", \"good\", \"#00FF00\", \"warning\", \"#FFAA00\"), \"#00FF00\");");
        controllerFile.Content.Should().Contain("private static string? ResolveMappedStyleValue(object? value, string? fallbackValue, params string[] mappings)");
        controllerFile.Content.Should().Contain("private static Color ParseStyleColor(string? value, string? fallbackValue)");

        var viewModelFile = result.Files.First(f => f.Path == "IDynamicStyleHudViewModel.g.cs");
        viewModelFile.Content.Should().Contain("object? PlayerStatusColor { get; }");
    }

    [Fact]
    public void Generate_FillWidthInVerticalLayout_UsesStretchInsteadOfFlexGrow()
    {
        var doc = new HudDocument
        {
            Name = "VerticalFillHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Width = Dimension.Pixels(400),
                    Height = Dimension.Pixels(300)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "stretchChild",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical,
                            Width = Dimension.Fill,
                            Height = Dimension.Pixels(80)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var ussFile = result.Files.First(f => f.Path == "VerticalFillHudView.uss");

        ussFile.Content.Should().Contain(".boomhud-stretch-child {");
        ussFile.Content.Should().Contain("align-self: stretch;");
    }

    [Fact]
    public void Generate_WithParentGap_UsesChildMarginsForSpacing()
    {
        var doc = new HudDocument
        {
            Name = "GapHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Gap = new Spacing(8)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "firstChild",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical,
                            Margin = new Spacing(1, 2, 3, 4)
                        }
                    },
                    new ComponentNode
                    {
                        Id = "secondChild",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical,
                            Margin = new Spacing(1, 2, 3, 4)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var ussFile = result.Files.First(f => f.Path == "GapHudView.uss");

        ussFile.Content.Should().Contain(".boomhud-first-child {");
        ussFile.Content.Should().Contain("margin-top: 1px;");
        ussFile.Content.Should().Contain(".boomhud-second-child {");
        ussFile.Content.Should().Contain("margin-top: 9px;");
        ussFile.Content.Should().NotContain("row-gap:");
        ussFile.Content.Should().NotContain("column-gap:");
    }

    [Fact]
    public void Generate_WithParentGap_AppliesSpacingToAbsoluteChildContainersInFlow()
    {
        var doc = new HudDocument
        {
            Name = "AbsoluteGapHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Gap = new Spacing(8)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "firstChild",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Absolute,
                            Height = Dimension.Pixels(10)
                        }
                    },
                    new ComponentNode
                    {
                        Id = "secondChild",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Absolute,
                            Height = Dimension.Pixels(10)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var ussFile = result.Files.First(f => f.Path == "AbsoluteGapHudView.uss");

        ussFile.Content.Should().Contain(".boomhud-second-child {");
        ussFile.Content.Should().Contain("margin-top: 8px;");
    }

    [Fact]
    public void Generate_AbsoluteLayoutWithoutCoordinates_DoesNotRemoveNodeFromFlow()
    {
        var doc = new HudDocument
        {
            Name = "AbsoluteContainerHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Width = Dimension.Pixels(400),
                    Height = Dimension.Pixels(300)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "viewport",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Absolute,
                            Width = Dimension.Fill,
                            Height = Dimension.Fill
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "marker",
                                Type = ComponentType.Label,
                                Layout = new LayoutSpec
                                {
                                    Type = LayoutType.Absolute,
                                    Left = Dimension.Pixels(12),
                                    Top = Dimension.Pixels(16),
                                    Width = Dimension.Pixels(20),
                                    Height = Dimension.Pixels(20)
                                }
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var ussFile = result.Files.First(f => f.Path == "AbsoluteContainerHudView.uss");

        ussFile.Content.Should().Contain(".boomhud-viewport {");
        ussFile.Content.Should().NotContain(".boomhud-viewport {\r\n    position: absolute;");
        ussFile.Content.Should().Contain(".boomhud-marker {");
        ussFile.Content.Should().Contain("position: absolute;");
        ussFile.Content.Should().Contain("left: 12px;");
        ussFile.Content.Should().Contain("top: 16px;");
    }

    [Fact]
    public void Generate_AbsoluteLayoutChildInsideHorizontalParent_StaysInFlowEvenWithCoordinates()
    {
        var doc = new HudDocument
        {
            Name = "AbsoluteChildFlowHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Width = Dimension.Pixels(400),
                    Height = Dimension.Pixels(300)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "card",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Absolute,
                            Width = Dimension.Fill,
                            Height = Dimension.Fill
                        },
                        InstanceOverrides = new Dictionary<string, object?>
                        {
                            [BoomHudMetadataKeys.PencilLeft] = 24d,
                            [BoomHudMetadataKeys.PencilTop] = 18d
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var ussFile = result.Files.First(f => f.Path == "AbsoluteChildFlowHudView.uss");

        ussFile.Content.Should().Contain(".boomhud-card {");
        ussFile.Content.Should().NotContain(".boomhud-card {\r\n    position: absolute;");
        ussFile.Content.Should().NotContain(".boomhud-card {\r\n    left: 24px;");
        ussFile.Content.Should().NotContain(".boomhud-card {\r\n    top: 18px;");
    }

    [Fact]
    public void Generate_IconComponent_NormalizesTokenTextInController()
    {
        var doc = new HudDocument
        {
            Name = "IconHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "icon",
                        Type = ComponentType.Icon,
                        Properties = new Dictionary<string, BindableValue<object?>>()
                        {
                            ["text"] = "swords"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var controllerFile = result.Files.First(f => f.Path == "IconHudView.gen.cs");

        controllerFile.Content.Should().Contain("Icon.text = ResolveIconText(\"swords\", null, 16f);");
        controllerFile.Content.Should().Contain("ApplyIconLabelStyle(Icon, 16f, 16f, 0f, true, \"fit-box\", 0f);");
        controllerFile.Content.Should().Contain("return NormalizeIconText(value);");
        controllerFile.Content.Should().Contain("\"swords\" => \"⚔\"");
    }

    [Fact]
    public void Generate_IconComponent_WithLucideFont_MapsIconNameToGlyphText()
    {
        var doc = new HudDocument
        {
            Name = "LigatureIconHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "icon",
                        Type = ComponentType.Icon,
                        Style = new StyleSpec
                        {
                            FontFamily = "lucide",
                            FontSize = 24
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>()
                        {
                            ["text"] = "swords"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var controllerFile = result.Files.First(f => f.Path == "LigatureIconHudView.gen.cs");

        controllerFile.Content.Should().Contain("Icon.text = ResolveIconText(\"swords\", \"lucide\", 24f);");
        controllerFile.Content.Should().Contain("ApplyFontFamily(Icon, \"lucide\", 24f);");
        controllerFile.Content.Should().Contain("Resources.Load<FontAsset>(resourcePath);");
        controllerFile.Content.Should().Contain("[\"swords\"] = \"\\uE2B4\"");
        controllerFile.Content.Should().Contain("return string.Equals(familyName?.Trim(), \"lucide\", StringComparison.OrdinalIgnoreCase) ? NormalizeIconText(value) : value;");
    }

    [Fact]
    public void Generate_IconComponent_WithLucideAlias_MapsToBundledFallbackGlyph()
    {
        var doc = new HudDocument
        {
            Name = "LucideAliasHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "icon",
                        Type = ComponentType.Icon,
                        Style = new StyleSpec
                        {
                            FontFamily = "lucide",
                            FontSize = 24
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>()
                        {
                            ["text"] = "wand-sparkles"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var controllerFile = result.Files.First(f => f.Path == "LucideAliasHudView.gen.cs");

        controllerFile.Content.Should().Contain("Icon.text = ResolveIconText(\"wand-sparkles\", \"lucide\", 24f);");
        controllerFile.Content.Should().Contain("[\"wand-sparkles\"] = \"\\uE357\"");
    }

    [Fact]
    public void Generate_LabelComponent_AppliesNonWrappingHudLabelStyle()
    {
        var doc = new HudDocument
        {
            Name = "LabelHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "name",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>()
                        {
                            ["text"] = "Theron"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var controllerFile = result.Files.First(f => f.Path == "LabelHudView.gen.cs");

        controllerFile.Content.Should().Contain("Name.text = \"Theron\";");
        controllerFile.Content.Should().Contain("ApplyTextLabelStyle(Name, false);");
        controllerFile.Content.Should().Contain("label.style.overflow = Overflow.Visible;");
    }

    [Fact]
    public void Generate_FixedWidthPenText_EnablesWrappingForUnityLabels()
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
                        Properties = new Dictionary<string, BindableValue<object?>>()
                        {
                            ["text"] = "Wrapped copy"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var controllerFile = result.Files.First(f => f.Path == "WrappedHudView.gen.cs");

        controllerFile.Content.Should().Contain("ApplyTextLabelStyle(Body, true);");
        controllerFile.Content.Should().Contain("label.style.whiteSpace = wrapText ? WhiteSpace.Normal : WhiteSpace.NoWrap;");
    }

    [Fact]
    public void Generate_IconComponent_UsesNodeDimensionsForGeneratedIconStyle()
    {
        var doc = new HudDocument
        {
            Name = "PortraitHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "classIcon",
                        Type = ComponentType.Icon,
                        Style = new StyleSpec
                        {
                            FontFamily = "lucide"
                        },
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(32),
                            Height = Dimension.Pixels(32)
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>()
                        {
                            ["text"] = "shield"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var controllerFile = result.Files.First(f => f.Path == "PortraitHudView.gen.cs");

        controllerFile.Content.Should().Contain("ClassIcon.text = ResolveIconText(\"shield\", \"lucide\", 32f);");
        controllerFile.Content.Should().Contain("ApplyIconLabelStyle(ClassIcon, 32f, 32f, 0f, true, \"fit-box\", 0f);");
        controllerFile.Content.Should().Contain("ApplyFontFamily(ClassIcon, \"lucide\", 32f);");
        controllerFile.Content.Should().Contain("private static bool TryLoadSdfFontAsset(string familyName, out FontAsset fontAsset)");
        controllerFile.Content.Should().Contain("var iconSize = explicitFontSize > 0f ? explicitFontSize : string.Equals(sizeMode, \"match-height\", StringComparison.OrdinalIgnoreCase) ? Mathf.Max(1f, boxHeight) : Mathf.Max(1f, Mathf.Min(boxWidth, boxHeight));");
        controllerFile.Content.Should().Contain("label.style.alignItems = opticalCentering ? Align.Center : Align.FlexStart;");
        controllerFile.Content.Should().Contain("label.style.justifyContent = opticalCentering ? Justify.Center : Justify.FlexStart;");
        controllerFile.Content.Should().Contain("label.style.overflow = Overflow.Visible;");
        controllerFile.Content.Should().Contain("label.style.width = boxWidth;");
        controllerFile.Content.Should().Contain("label.style.height = boxHeight;");
    }

    [Fact]
    public void Generate_WithBorderAndOpacityBindings_EmitsRuntimeStyleRefresh()
    {
        var doc = new HudDocument
        {
            Name = "DynamicBorderHud",
            Root = new ComponentNode
            {
                Id = "panel",
                Type = ComponentType.Container,
                Style = new StyleSpec
                {
                    Border = new BorderSpec
                    {
                        Width = 1,
                        Color = Color.Green,
                        Style = BorderStyle.Solid
                    },
                    Opacity = 0.5
                },
                Bindings =
                [
                    new BindingSpec { Property = "style.borderColor", Path = "Panel.BorderTone", Fallback = "#00FF00" },
                    new BindingSpec { Property = "style.borderWidth", Path = "Panel.BorderWidth" },
                    new BindingSpec { Property = "style.opacity", Path = "Panel.Opacity" }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var controllerFile = result.Files.First(f => f.Path == "DynamicBorderHudView.gen.cs");
        controllerFile.Content.Should().Contain("Root.style.borderLeftColor = ParseStyleColor(ResolveMappedStyleValue(_viewModel.PanelBorderTone, \"#00FF00\"), \"#00FF00\");");
        controllerFile.Content.Should().Contain("Root.style.borderRightColor = ParseStyleColor(ResolveMappedStyleValue(_viewModel.PanelBorderTone, \"#00FF00\"), \"#00FF00\");");
        controllerFile.Content.Should().Contain("Root.style.borderLeftWidth = AsFloat(_viewModel.PanelBorderWidth);");
        controllerFile.Content.Should().Contain("Root.style.borderBottomWidth = AsFloat(_viewModel.PanelBorderWidth);");
        controllerFile.Content.Should().Contain("Root.style.opacity = AsFloat(_viewModel.PanelOpacity);");

        var viewModelFile = result.Files.First(f => f.Path == "IDynamicBorderHudViewModel.g.cs");
        viewModelFile.Content.Should().Contain("object? PanelBorderTone { get; }");
        viewModelFile.Content.Should().Contain("object? PanelBorderWidth { get; }");
        viewModelFile.Content.Should().Contain("object? PanelOpacity { get; }");
    }

    [Fact]
    public void Generate_WithRuleSet_RemapsLabelToTextField()
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
                            Backend = "unity",
                            NodeId = "title",
                            ComponentType = ComponentType.Label
                        },
                        Action = new GeneratorRuleAction
                        {
                            ControlType = "TextField"
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
        var uxmlFile = result.Files.First(f => f.Path == "RuleHudView.uxml");
        var controllerFile = result.Files.First(f => f.Path == "RuleHudView.gen.cs");

        uxmlFile.Content.Should().Contain("<ui:TextField name=\"Title\" class=\"boomhud-title\" />");
        controllerFile.Content.Should().Contain("public TextField Title { get; }");
        controllerFile.Content.Should().Contain("Title = Root.Q<TextField>(\"Title\") ?? throw new InvalidOperationException");
        controllerFile.Content.Should().Contain("Title.value = \"Ready\";");
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
                            Backend = "unity",
                            NodeId = "body"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Text = new GeneratorTextRuleAction
                            {
                                WrapText = true,
                                LineHeight = 1.6,
                                FontFamily = "Press Start 2P",
                                FontSize = 15,
                                LetterSpacing = 1
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "unity",
                            NodeId = "classIcon"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Icon = new GeneratorIconRuleAction
                            {
                                BaselineOffset = 2,
                                OpticalCentering = false,
                                SizeMode = "match-height",
                                FontSize = 22
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "unity",
                            NodeId = "root"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                Gap = 10,
                                Padding = 6
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "unity",
                            NodeId = "badge"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                ForceAbsolutePositioning = true,
                                OffsetX = 4,
                                OffsetY = 5
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
                            FontSize = 12
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
        var ussFile = result.Files.First(f => f.Path == "PolicyHudView.uss");
        var controllerFile = result.Files.First(f => f.Path == "PolicyHudView.gen.cs");

        ussFile.Content.Should().Contain("font-size: 15px;");
        ussFile.Content.Should().Contain("line-height: 24px;");
        ussFile.Content.Should().Contain("letter-spacing: 1px;");
        ussFile.Content.Should().Contain(".boomhud-badge {");
        ussFile.Content.Should().Contain("position: absolute;");
        ussFile.Content.Should().Contain("left: 4px;");
        ussFile.Content.Should().Contain("top: 5px;");
        ussFile.Content.Should().Contain("padding-top: 6px;");
        ussFile.Content.Should().Contain(".boomhud-class-icon {");
        ussFile.Content.Should().Contain("margin-top: 10px;");
        controllerFile.Content.Should().Contain("ApplyTextLabelStyle(Body, true);");
        controllerFile.Content.Should().Contain("ApplyFontFamily(Body, \"Press Start 2P\", 15f);");
        controllerFile.Content.Should().Contain("Body.style.fontSize = 15f;");
        controllerFile.Content.Should().Contain("Body.style.letterSpacing = 1f;");
        controllerFile.Content.Should().Contain("ApplyIconLabelStyle(ClassIcon, 24f, 24f, 2f, false, \"match-height\", 22f);");
    }

    [Fact]
    public void Generate_WithRuleSet_PositionModeAndFlexAlignmentPreset_AppliesLayoutOverrides()
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
                            Backend = "unity",
                            NodeId = "card"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                PositionMode = "relative",
                                FlexAlignmentPreset = "center"
                            }
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
        {
            Name = "PresetHud",
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
                            Type = LayoutType.Absolute,
                            Left = Dimension.Pixels(10),
                            Top = Dimension.Pixels(14),
                            Width = Dimension.Pixels(120),
                            Height = Dimension.Pixels(80)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);
        var ussFile = result.Files.First(f => f.Path == "PresetHudView.uss");

        ussFile.Content.Should().Contain(".boomhud-card {");
        ussFile.Content.Should().Contain("position: relative;");
        ussFile.Content.Should().Contain("align-items: center;");
        ussFile.Content.Should().Contain("justify-content: center;");
        ussFile.Content.Should().NotContain(".boomhud-card {\r\n    position: absolute;");
    }

    [Fact]
    public void Generate_WithRuleSet_LayoutDeltas_AppliesGapPaddingAndOffsetAdjustments()
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
                            Backend = "unity",
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
                            Backend = "unity",
                            NodeId = "badge"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                ForceAbsolutePositioning = true,
                                OffsetXDelta = 3,
                                OffsetYDelta = -2
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
                    },
                    new ComponentNode
                    {
                        Id = "detail",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical,
                            Width = Dimension.Pixels(20),
                            Height = Dimension.Pixels(10)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);
        var ussFile = result.Files.First(f => f.Path == "DeltaHudView.uss");

        ussFile.Content.Should().Contain("padding-top: 4px;");
        ussFile.Content.Should().Contain(".boomhud-badge {");
        ussFile.Content.Should().Contain("left: 8px;");
        ussFile.Content.Should().Contain("top: 8px;");
        ussFile.Content.Should().Contain(".boomhud-detail {");
        ussFile.Content.Should().Contain("margin-top: 6px;");
    }

    [Fact]
    public void Generate_WithRuleSet_PreferredSizeDeltas_AdjustsWidthAndHeightStyles()
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
                            Backend = "unity",
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
        var ussFile = result.Files.First(f => f.Path == "PreferredSizeHudView.uss");

        ussFile.Content.Should().Contain(".boomhud-card {");
        ussFile.Content.Should().Contain("width: 100px;");
        ussFile.Content.Should().Contain("height: 95px;");
    }

    private static ComponentNode CreateSyntheticCandidateCard(string cardId, double left, double top)
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
                    Id = cardId + "-title",
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
                    Id = cardId + "-icon",
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
