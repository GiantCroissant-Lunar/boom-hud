using BoomHud.Abstractions.IR;
using BoomHud.Dsl;
using BoomHud.Dsl.Figma;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Dsl;

public class FigmaParserTests
{
    private readonly FigmaParser _parser = new();

    [Fact]
    public void Parse_MinimalFigmaFile_ReturnsHudDocument()
    {
        var json = """
            {
                "name": "Test File",
                "version": "1.0.0",
                "document": {
                    "id": "0:0",
                    "name": "Document",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "name": "Page 1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:2",
                                    "name": "Status Bar",
                                    "type": "FRAME"
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Should().NotBeNull();
        doc.Name.Should().Be("StatusBar");
        doc.Root.Type.Should().Be(ComponentType.Container);
    }

    [Fact]
    public void Parse_FrameWithChildren_CreatesNestedComponents()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Main Frame",
                                    "type": "FRAME",
                                    "children": [
                                        {
                                            "id": "1:2",
                                            "name": "Header",
                                            "type": "FRAME"
                                        },
                                        {
                                            "id": "1:3",
                                            "name": "Content",
                                            "type": "FRAME"
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Children.Should().HaveCount(2);
        doc.Root.Children[0].Id.Should().Be("header");
        doc.Root.Children[1].Id.Should().Be("content");
    }

    [Fact]
    public void Parse_TextNode_CreatesLabelWithText()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Frame",
                                    "type": "FRAME",
                                    "children": [
                                        {
                                            "id": "1:2",
                                            "name": "Title",
                                            "type": "TEXT",
                                            "characters": "Hello World"
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        var textNode = doc.Root.Children[0];
        textNode.Type.Should().Be(ComponentType.Label);
        textNode.Properties.Should().ContainKey("text");
        textNode.Properties["text"].Value.Should().Be("Hello World");
    }

    [Fact]
    public void Parse_NodeWithBoundingBox_ExtractsWidthAndHeight()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Frame",
                                    "type": "FRAME",
                                    "absoluteBoundingBox": {
                                        "x": 0,
                                        "y": 0,
                                        "width": 400,
                                        "height": 300
                                    }
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Layout.Should().NotBeNull();
        doc.Root.Layout!.Width.Should().NotBeNull();
        doc.Root.Layout.Width!.Value.Value.Should().Be(400);
        doc.Root.Layout.Height!.Value.Value.Should().Be(300);
    }

    [Fact]
    public void Parse_NodeWithAutoLayout_ExtractsLayoutType()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "HStack",
                                    "type": "FRAME",
                                    "layoutMode": "HORIZONTAL",
                                    "itemSpacing": 8,
                                    "paddingLeft": 16,
                                    "paddingRight": 16,
                                    "paddingTop": 8,
                                    "paddingBottom": 8
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Layout.Should().NotBeNull();
        doc.Root.Layout!.Type.Should().Be(LayoutType.Horizontal);
        doc.Root.Layout.Gap!.Value.Top.Should().Be(8); // Gap is uniform spacing
        doc.Root.Layout.Padding!.Value.Left.Should().Be(16);
        doc.Root.Layout.Padding!.Value.Top.Should().Be(8);
    }

    [Fact]
    public void Parse_NodeWithFills_ExtractsBackgroundColor()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Panel",
                                    "type": "RECTANGLE",
                                    "fills": [
                                        {
                                            "type": "SOLID",
                                            "visible": true,
                                            "color": { "r": 1, "g": 0, "b": 0, "a": 1 }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Style.Should().NotBeNull();
        doc.Root.Style!.Background.Should().NotBeNull();
        doc.Root.Style.Background!.Value.R.Should().Be(255);
        doc.Root.Style.Background!.Value.G.Should().Be(0);
        doc.Root.Style.Background!.Value.B.Should().Be(0);
    }

    [Fact]
    public void Parse_NodeWithStrokes_ExtractsBorder()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Panel",
                                    "type": "RECTANGLE",
                                    "strokes": [
                                        {
                                            "type": "SOLID",
                                            "visible": true,
                                            "color": { "r": 0, "g": 0, "b": 0, "a": 1 }
                                        }
                                    ],
                                    "strokeWeight": 2
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Style.Should().NotBeNull();
        doc.Root.Style!.Border.Should().NotBeNull();
        doc.Root.Style.Border!.Width.Should().Be(2);
    }

    [Fact]
    public void Parse_NodeWithCornerRadius_ExtractsBorderRadius()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Panel",
                                    "type": "RECTANGLE",
                                    "cornerRadius": 8
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Style.Should().NotBeNull();
        doc.Root.Style!.BorderRadius.Should().Be(8);
    }

    [Fact]
    public void Parse_TextNodeWithStyle_ExtractsFontProperties()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Frame",
                                    "type": "FRAME",
                                    "children": [
                                        {
                                            "id": "1:2",
                                            "name": "Title",
                                            "type": "TEXT",
                                            "characters": "Bold Title",
                                            "style": {
                                                "fontSize": 24,
                                                "fontWeight": 700,
                                                "italic": false,
                                                "fills": [
                                                    {
                                                        "type": "SOLID",
                                                        "color": { "r": 1, "g": 1, "b": 1, "a": 1 }
                                                    }
                                                ]
                                            }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        var textNode = doc.Root.Children[0];
        textNode.Style.Should().NotBeNull();
        textNode.Style!.FontSize.Should().Be(24);
        textNode.Style.FontWeight.Should().Be(FontWeight.Bold);
        textNode.Style.Foreground.Should().NotBeNull();
    }

    [Fact]
    public void Parse_HiddenNode_IsExcluded()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Frame",
                                    "type": "FRAME",
                                    "children": [
                                        {
                                            "id": "1:2",
                                            "name": "Visible",
                                            "type": "TEXT",
                                            "visible": true
                                        },
                                        {
                                            "id": "1:3",
                                            "name": "Hidden",
                                            "type": "TEXT",
                                            "visible": false
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Children.Should().HaveCount(1);
        doc.Root.Children[0].Id.Should().Be("visible");
    }

    [Fact]
    public void ParseNode_ByNodeId_ReturnsSpecificNode()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Frame One",
                                    "type": "FRAME"
                                },
                                {
                                    "id": "1:2",
                                    "name": "Frame Two",
                                    "type": "FRAME"
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.ParseNode(json, "1:2");

        doc.Name.Should().Be("FrameTwo");
    }

    [Fact]
    public void Validate_ValidJson_ReturnsOk()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT"
                }
            }
            """;

        var result = _parser.Validate(json);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsError()
    {
        var json = "{ invalid json }";

        var result = _parser.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_MissingDocument_ReturnsError()
    {
        var json = """
            {
                "name": "Test"
            }
            """;

        var result = _parser.Validate(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("document"));
    }

    [Fact]
    public void Parse_ComponentNode_MapsToContainer()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Button Component",
                                    "type": "COMPONENT"
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Type.Should().Be(ComponentType.Container);
    }

    [Fact]
    public void Parse_VectorNode_MapsToIcon()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Frame",
                                    "type": "FRAME",
                                    "children": [
                                        {
                                            "id": "1:2",
                                            "name": "Icon",
                                            "type": "VECTOR"
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Children[0].Type.Should().Be(ComponentType.Icon);
    }

    [Fact]
    public void Parse_RectangleNode_MapsToPanel()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Background",
                                    "type": "RECTANGLE"
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Type.Should().Be(ComponentType.Panel);
    }

    [Fact]
    public void Parse_SanitizesNameToPascalCase()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "my-status_bar component",
                                    "type": "FRAME"
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Name.Should().Be("MyStatusBarComponent");
    }

    [Fact]
    public void Parse_SanitizesIdToCamelCase()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Frame",
                                    "type": "FRAME",
                                    "children": [
                                        {
                                            "id": "1:2",
                                            "name": "Health Bar",
                                            "type": "RECTANGLE"
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Children[0].Id.Should().Be("healthBar");
    }

    [Fact]
    public void Parse_WithOpacity_ExtractsOpacity()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Panel",
                                    "type": "RECTANGLE",
                                    "opacity": 0.5
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Style.Should().NotBeNull();
        doc.Root.Style!.Opacity.Should().BeApproximately(0.5f, 0.01f);
    }

    [Fact]
    public void Parse_VerticalLayout_ExtractsCorrectType()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "VStack",
                                    "type": "FRAME",
                                    "layoutMode": "VERTICAL"
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Layout!.Type.Should().Be(LayoutType.Vertical);
    }

    [Fact]
    public void Parse_AlignmentProperties_ExtractsCorrectly()
    {
        var json = """
            {
                "name": "Test",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Frame",
                                    "type": "FRAME",
                                    "layoutMode": "HORIZONTAL",
                                    "primaryAxisAlignItems": "CENTER",
                                    "counterAxisAlignItems": "MAX"
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var doc = _parser.Parse(json);

        doc.Root.Layout!.Justify.Should().Be(Justification.Center);
        doc.Root.Layout.Align.Should().Be(BoomHud.Abstractions.IR.Alignment.End);
    }
}
