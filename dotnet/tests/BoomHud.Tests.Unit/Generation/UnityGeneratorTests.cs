using System.Text.Json;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Gen.Unity;
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
        ussFile.Content.Should().Contain("letter-spacing: 1px;");
        ussFile.Content.Should().Contain("-unity-font-style: bold;");
        ussFile.Content.Should().Contain("opacity: 0.8;");
        ussFile.Content.Should().Contain("border-top-left-radius: 6px;");

        var controllerFile = result.Files.First(f => f.Path == "StyledHudView.gen.cs");
        controllerFile.Content.Should().Contain("ApplyFontFamily(TitleLabel, \"Press Start 2P\", 18f);");
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
                                    Width = Dimension.Pixels(20),
                                    Height = Dimension.Pixels(20)
                                },
                                InstanceOverrides = new Dictionary<string, object?>
                                {
                                    [BoomHudMetadataKeys.PencilLeft] = 12d,
                                    [BoomHudMetadataKeys.PencilTop] = 16d
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
        controllerFile.Content.Should().Contain("ApplyIconLabelStyle(Icon, 16f, 16f);");
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
        controllerFile.Content.Should().Contain("ApplyTextLabelStyle(Name);");
        controllerFile.Content.Should().Contain("label.style.overflow = Overflow.Visible;");
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
        controllerFile.Content.Should().Contain("ApplyIconLabelStyle(ClassIcon, 32f, 32f);");
        controllerFile.Content.Should().Contain("ApplyFontFamily(ClassIcon, \"lucide\", 32f);");
        controllerFile.Content.Should().Contain("private static bool TryLoadSdfFontAsset(string familyName, out FontAsset fontAsset)");
        controllerFile.Content.Should().Contain("var iconSize = Mathf.Max(1f, Mathf.Min(boxWidth, boxHeight));");
        controllerFile.Content.Should().Contain("label.style.alignItems = Align.Center;");
        controllerFile.Content.Should().Contain("label.style.justifyContent = Justify.Center;");
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
}