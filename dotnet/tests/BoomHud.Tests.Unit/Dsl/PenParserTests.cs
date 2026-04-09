using System.IO;
using System.Text.Json;
using BoomHud.Abstractions.IR;
using BoomHud.Dsl.Pencil;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Dsl;

public class PenParserTests
{
    private readonly PenParser _parser = new();

    private static string GetRepoFilePath(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repo file '{relativePath}'.");
    }

    [Fact]
    public void Parse_MinimalPenFile_ReturnsHudDocument()
    {
        var json = """
            {
                "$schema": "https://boom-hud.dev/schemas/pencil.schema.json",
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "name": "TestComponent"
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        doc.Should().NotBeNull();
        doc.Name.Should().Be("TestComponent");
        doc.Root.Type.Should().Be(ComponentType.Container);
        doc.Root.Id.Should().Be("root");
    }

    [Fact]
    public void Parse_FrameWithChildren_CreatesNestedComponents()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "name": "MainFrame",
                        "children": [
                            {
                                "id": "header",
                                "type": "frame",
                                "name": "Header"
                            },
                            {
                                "id": "content",
                                "type": "frame",
                                "name": "Content"
                            }
                        ]
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Children.Should().HaveCount(2);
        doc.Root.Children[0].Id.Should().Be("header");
        doc.Root.Children[1].Id.Should().Be("content");
    }

    [Fact]
    public void Parse_TextNode_MapsToLabel()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "children": [
                            {
                                "id": "label",
                                "type": "text",
                                "name": "TitleLabel",
                                "content": "Hello World"
                            }
                        ]
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Children.Should().HaveCount(1);
        var label = doc.Root.Children[0];
        label.Type.Should().Be(ComponentType.Label);
        label.Properties.Should().ContainKey("Text");
        label.Properties["Text"].Value.Should().Be("Hello World");
    }

    [Fact]
    public void Parse_InlineBinding_CreatesBindingSpec()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "children": [
                            {
                                "id": "fps",
                                "type": "text",
                                "content": "60",
                                "bindings": {
                                    "content": "Fps"
                                }
                            }
                        ]
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        var textNode = doc.Root.Children[0];
        textNode.Bindings.Should().HaveCount(1);
        textNode.Bindings[0].Property.Should().Be("Text");
        textNode.Bindings[0].Path.Should().Be("Fps");
        textNode.Bindings[0].Mode.Should().Be(BindingMode.OneWay);
    }

    [Fact]
    public void Parse_ComplexBinding_ExtractsFormatAndMode()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "children": [
                            {
                                "id": "fps",
                                "type": "text",
                                "content": "60",
                                "bindings": {
                                    "content": {
                                        "$bind": "Fps",
                                        "format": "{0:F0}",
                                        "mode": "oneWay"
                                    }
                                }
                            }
                        ]
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        var textNode = doc.Root.Children[0];
        textNode.Bindings.Should().HaveCount(1);
        textNode.Bindings[0].Property.Should().Be("Text");
        textNode.Bindings[0].Path.Should().Be("Fps");
        textNode.Bindings[0].Format.Should().Be("{0:F0}");
        textNode.Bindings[0].Mode.Should().Be(BindingMode.OneWay);
    }

    [Fact]
    public void Parse_StyleFillBindingOnTextNode_NormalizesToForegroundAndPreservesMap()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "text",
                        "content": "60",
                        "bindings": {
                            "style.fill": {
                                "$bind": "FpsStatus",
                                "map": {
                                    "good": { "$ref": "tokens.colors.good" },
                                    "warning": "#ffaa00"
                                },
                                "fallback": "#00ff00"
                            }
                        }
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        var textNode = doc.Root;
        textNode.Bindings.Should().HaveCount(1);
        textNode.Bindings[0].Property.Should().Be("style.foreground");
        textNode.Bindings[0].Path.Should().Be("FpsStatus");
        textNode.Bindings[0].Fallback.Should().Be("#00ff00");

        textNode.Bindings[0].ConverterParameter.Should().BeOfType<JsonElement>();
        var map = (JsonElement)textNode.Bindings[0].ConverterParameter!;
        map.GetProperty("good").GetProperty("$ref").GetString().Should().Be("tokens.colors.good");
        map.GetProperty("warning").GetString().Should().Be("#ffaa00");
    }

    [Fact]
    public void Parse_ImageFill_PreservesBackgroundImageInIr()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "style": {
                            "fill": {
                                "type": "image",
                                "enabled": true,
                                "url": "./images/viewport.png",
                                "mode": "fill"
                            }
                        }
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json, out var warnings);

        doc.Root.Style.Should().NotBeNull();
        doc.Root.Style!.BackgroundImage.Should().NotBeNull();
        doc.Root.Style.BackgroundImage!.Url.Should().Be("./images/viewport.png");
        doc.Root.Style.BackgroundImage.Mode.Should().Be(BackgroundImageMode.Fill);
        warnings.Should().NotContain(w => w.Contains("does not emit background images yet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_ClipTrue_PreservesClipMetadata()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "clip": true
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.InstanceOverrides.Should().ContainKey(BoomHudMetadataKeys.PencilClip);
        doc.Root.InstanceOverrides[BoomHudMetadataKeys.PencilClip].Should().Be(true);
    }

    [Fact]
    public void Parse_LayoutMode_MapsToLayoutType()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "layout": {
                            "mode": "horizontal",
                            "gap": 8,
                            "padding": { "top": 4, "right": 8, "bottom": 4, "left": 8 }
                        }
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Layout.Should().NotBeNull();
        doc.Root.Layout!.Type.Should().Be(LayoutType.Horizontal);
        doc.Root.Layout.Gap.Should().NotBeNull();
        doc.Root.Layout.Padding.Should().NotBeNull();
        doc.Root.Layout.Padding!.Value.Top.Should().Be(4);
        doc.Root.Layout.Padding!.Value.Right.Should().Be(8);
    }

    [Fact]
    public void Parse_FrameWithoutLayout_DefaultsToHorizontalLayout()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "children": [
                            {
                                "id": "left",
                                "type": "frame",
                                "width": 100,
                                "height": 50
                            },
                            {
                                "id": "right",
                                "type": "frame",
                                "width": 200,
                                "height": 50
                            }
                        ]
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Layout.Should().NotBeNull();
        doc.Root.Layout!.Type.Should().Be(LayoutType.Horizontal);
    }

    [Fact]
    public void Parse_GroupWithoutLayout_DefaultsToAbsoluteLayout()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "group",
                        "children": [
                            {
                                "id": "child",
                                "type": "frame",
                                "x": 10,
                                "y": 20,
                                "width": 100,
                                "height": 50
                            }
                        ]
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Layout.Should().NotBeNull();
        doc.Root.Layout!.Type.Should().Be(LayoutType.Absolute);
    }

    [Fact]
    public void Parse_FrameWithCanvasCoordinates_PreservesExplicitFlowLayout()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "x": 32,
                        "y": 48,
                        "layout": {
                            "mode": "vertical",
                            "gap": 8
                        },
                        "children": [
                            {
                                "id": "child",
                                "type": "text",
                                "content": "Hello"
                            }
                        ]
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Layout.Should().NotBeNull();
        doc.Root.Layout!.Type.Should().Be(LayoutType.Vertical);
        doc.Root.Layout.Gap.Should().NotBeNull();
        doc.Root.Layout.Gap!.Value.Top.Should().Be(8);
    }

    [Fact]
    public void Parse_FrameWithoutExplicitLayout_DoesNotBecomeAbsoluteFromCanvasCoordinates()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "x": 10,
                        "y": 20,
                        "children": [
                            {
                                "id": "left",
                                "type": "frame",
                                "width": 100,
                                "height": 50
                            },
                            {
                                "id": "right",
                                "type": "frame",
                                "width": 200,
                                "height": 50
                            }
                        ]
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Layout.Should().NotBeNull();
        doc.Root.Layout!.Type.Should().Be(LayoutType.Horizontal);
    }

    [Fact]
    public void Parse_StyleWithColors_ParsesCorrectly()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "style": {
                            "background": "#ff0000",
                            "opacity": 0.8
                        }
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Style.Should().NotBeNull();
        doc.Root.Style!.Background.Should().NotBeNull();
        doc.Root.Style.Background!.Value.R.Should().Be(255);
        doc.Root.Style.Background!.Value.G.Should().Be(0);
        doc.Root.Style.Background!.Value.B.Should().Be(0);
        doc.Root.Style.Opacity.Should().Be(0.8);
    }

    [Fact]
    public void Parse_PenSpecificStyleAliases_MapToIrStyle()
    {
        var json = """
            {
                "version": "1.0",
                "name": "AliasPanel",
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "style": {
                            "fill": "#112233",
                            "stroke": "#445566",
                            "strokeWidth": 2
                        },
                        "children": [
                            {
                                "id": "label",
                                "type": "text",
                                "style": {
                                    "fill": "#abcdef"
                                }
                            }
                        ]
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        doc.Name.Should().Be("AliasPanel");
        doc.Metadata.Should().NotBeNull();
        doc.Metadata!.Version.Should().Be("1.0");
        doc.Root.Style.Should().NotBeNull();
        doc.Root.Style!.Background.Should().Be(new Color(0x11, 0x22, 0x33));
        doc.Root.Style.Border.Should().NotBeNull();
        doc.Root.Style.Border!.Color.Should().Be(new Color(0x44, 0x55, 0x66));
        doc.Root.Style.Border.Width.Should().Be(2);

        var label = doc.Root.Children[0];
        label.Style.Should().NotBeNull();
        label.Style!.Foreground.Should().Be(new Color(0xAB, 0xCD, 0xEF));
    }

    [Fact]
    public void Parse_TextTypography_PreservesFontFamilyAndLetterSpacing()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "children": [
                            {
                                "id": "label",
                                "type": "text",
                                "content": "Mage",
                                "fontFamily": "Press Start 2P",
                                "fontSize": 8,
                                "letterSpacing": 1
                            }
                        ]
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);
        var label = doc.Root.Children[0];

        label.Style.Should().NotBeNull();
        label.Style!.FontFamily.Should().Be("Press Start 2P");
        label.Style.FontSize.Should().Be(8);
        label.Style.LetterSpacing.Should().Be(1);
    }

    [Fact]
    public void Parse_PenSpecificDimensionObjects_AreConverted()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "layout": {
                            "width": { "type": "fixed", "value": 320 },
                            "height": "hug",
                            "padding": { "vertical": 4, "horizontal": 8 }
                        }
                    }
                ]
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Layout.Should().NotBeNull();
        doc.Root.Layout!.Width.Should().Be(Dimension.Pixels(320));
        doc.Root.Layout.Height.Should().Be(Dimension.Auto);
        doc.Root.Layout.Padding.Should().NotBeNull();
        doc.Root.Layout.Padding!.Value.Top.Should().Be(4);
        doc.Root.Layout.Padding!.Value.Bottom.Should().Be(4);
        doc.Root.Layout.Padding!.Value.Left.Should().Be(8);
        doc.Root.Layout.Padding!.Value.Right.Should().Be(8);
    }

    [Fact]
    public void Validate_MissingNodes_ReturnsError()
    {
        var json = """
            {
                "$schema": "https://boom-hud.dev/schemas/pencil.schema.json"
            }
            """;

        var result = _parser.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("No nodes");
    }

    [Fact]
    public void Validate_NodeWithoutId_ReturnsError()
    {
        var json = """
            {
                "nodes": [
                    {
                        "type": "frame",
                        "name": "NoIdNode"
                    }
                ]
            }
            """;

        var result = _parser.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("id"));
    }

    [Fact]
    public void Validate_NodeWithoutType_ReturnsError()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "name": "NoTypeNode"
                    }
                ]
            }
            """;

        var result = _parser.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("type"));
    }

    [Fact]
    public void Validate_ValidFile_ReturnsOk()
    {
        var json = """
            {
                "nodes": [
                    {
                        "id": "root",
                        "type": "frame",
                        "name": "ValidComponent"
                    }
                ]
            }
            """;

        var result = _parser.Validate(json);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_RawPencilExport_CollectsReusableComponents_AndExpandsRefs()
    {
        var samplePath = GetRepoFilePath(Path.Combine("samples", "pencil", "raw-hud-components.pen"));
        var json = File.ReadAllText(samplePath);

        var doc = _parser.Parse(json);

        doc.Name.Should().Be("ExploreHud");
        doc.Components.Should().ContainKey("actionButton");
        doc.Components.Should().ContainKey("charPortrait");
        doc.Components["actionButton"].Name.Should().Be("ActionButton");
        doc.Components["charPortrait"].Name.Should().Be("CharPortrait");

        var viewport = doc.Root.Children[0];
        viewport.Layout!.Type.Should().Be(LayoutType.Absolute);
        viewport.Style!.Border.Should().NotBeNull();
        viewport.Style.Border!.Width.Should().Be(12);

        var partyPanel = doc.Root.Children[1];
        partyPanel.Children.Should().HaveCount(3);

        var firstPortrait = partyPanel.Children[0];
        firstPortrait.Children.Should().Contain(child => child.Id == "c1name");
        firstPortrait.Children.First(child => child.Id == "c1name").Properties["Text"].Value.Should().Be("Aelric");
        firstPortrait.Children.Should().Contain(child => child.Id == "AttackButton");
    }
}
