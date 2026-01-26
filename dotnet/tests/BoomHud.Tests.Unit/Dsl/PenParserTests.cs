using BoomHud.Abstractions.IR;
using BoomHud.Dsl.Pencil;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Dsl;

public class PenParserTests
{
    private readonly PenParser _parser = new();

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
        textNode.Bindings[0].Property.Should().Be("content");
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
        textNode.Bindings[0].Path.Should().Be("Fps");
        textNode.Bindings[0].Format.Should().Be("{0:F0}");
        textNode.Bindings[0].Mode.Should().Be(BindingMode.OneWay);
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
}
