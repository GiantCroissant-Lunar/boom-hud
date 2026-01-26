using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Dsl;
using BoomHud.Dsl.Figma;
using BoomHud.Gen.Avalonia;
using BoomHud.Gen.TerminalGui;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Integration;

public class EndToEndTests
{
    private readonly FigmaParser _parser = new();
    private readonly TerminalGuiGenerator _generator = new();
    private readonly GenerationOptions _options = new()
    {
        Namespace = "MyGame.Hud",
        IncludeComments = true,
        UseNullableAnnotations = true
    };

    [Fact]
    public void ParseFigmaAndGenerate_StatusBar_ProducesValidCode()
    {
        var figmaJson = """
            {
                "name": "Status Bar Design",
                "version": "1.0.0",
                "document": {
                    "id": "0:0",
                    "type": "DOCUMENT",
                    "children": [
                        {
                            "id": "0:1",
                            "type": "CANVAS",
                            "name": "Page 1",
                            "children": [
                                {
                                    "id": "1:1",
                                    "name": "Status Bar",
                                    "type": "FRAME",
                                    "layoutMode": "HORIZONTAL",
                                    "absoluteBoundingBox": { "x": 0, "y": 0, "width": 800, "height": 40 },
                                    "fills": [{ "type": "SOLID", "color": { "r": 0.13, "g": 0.13, "b": 0.13, "a": 1 } }],
                                    "children": [
                                        {
                                            "id": "1:2",
                                            "name": "Health Section",
                                            "type": "FRAME",
                                            "layoutMode": "HORIZONTAL",
                                            "itemSpacing": 8,
                                            "children": [
                                                {
                                                    "id": "1:3",
                                                    "name": "Health Label",
                                                    "type": "TEXT",
                                                    "characters": "HP: 100"
                                                }
                                            ]
                                        },
                                        {
                                            "id": "1:4",
                                            "name": "Location Label",
                                            "type": "TEXT",
                                            "characters": "Town Square"
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        // Parse Figma JSON
        var document = _parser.Parse(figmaJson);

        document.Name.Should().Be("StatusBar");
        document.Root.Type.Should().Be(ComponentType.Container);

        // Generate Terminal.Gui code
        var result = _generator.Generate(document, _options);

        result.Success.Should().BeTrue();
        result.Files.Should().HaveCount(2);

        // Check view file
        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("namespace MyGame.Hud;");
        viewFile.Content.Should().Contain("public partial class StatusBarView : Terminal.Gui.ViewBase.View");
        viewFile.Content.Should().Contain("private Label _healthLabel");
        viewFile.Content.Should().Contain("private Label _locationLabel");
    }

    [Fact]
    public void ParseFigmaAndGenerate_NestedFrames_PreservesHierarchy()
    {
        var figmaJson = """
            {
                "name": "Nested Test",
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
                                    "name": "Outer",
                                    "type": "FRAME",
                                    "children": [
                                        {
                                            "id": "1:2",
                                            "name": "Inner",
                                            "type": "FRAME",
                                            "children": [
                                                {
                                                    "id": "1:3",
                                                    "name": "Deep Label",
                                                    "type": "TEXT",
                                                    "characters": "Hello"
                                                }
                                            ]
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var document = _parser.Parse(figmaJson);
        var result = _generator.Generate(document, _options);

        result.Success.Should().BeTrue();

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("_outer");
        viewFile.Content.Should().Contain("_inner");
        viewFile.Content.Should().Contain("_deepLabel");
    }

    [Fact]
    public void ParseFigmaAndGenerate_WithDimensions_GeneratesCorrectDimCalls()
    {
        var figmaJson = """
            {
                "name": "Dimension Test",
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
                                    "name": "Fixed Panel",
                                    "type": "RECTANGLE",
                                    "absoluteBoundingBox": { "x": 0, "y": 0, "width": 200, "height": 100 }
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var document = _parser.Parse(figmaJson);
        var result = _generator.Generate(document, _options);

        result.Success.Should().BeTrue();

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("Width = Dim.Absolute(200)");
        viewFile.Content.Should().Contain("Height = Dim.Absolute(100)");
    }

    [Fact]
    public void ParseFigmaAndGenerate_WithColors_GeneratesColorScheme()
    {
        var figmaJson = """
            {
                "name": "Color Test",
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
                                            "name": "Red Label",
                                            "type": "TEXT",
                                            "characters": "Error",
                                            "style": {
                                                "fills": [{ "type": "SOLID", "color": { "r": 1, "g": 0, "b": 0, "a": 1 } }]
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

        var document = _parser.Parse(figmaJson);
        var result = _generator.Generate(document, _options);

        result.Success.Should().BeTrue();

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("Color.Red");
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsErrors()
    {
        var invalidJson = "{ not valid json }";

        var validationResult = _parser.Validate(invalidJson);

        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Avalonia_ParseFigmaAndGenerate_ProducesValidAxaml()
    {
        var avaloniaGenerator = new AvaloniaGenerator();
        var figmaJson = """
            {
                "name": "Status Bar",
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
                                    "name": "Status Bar",
                                    "type": "FRAME",
                                    "layoutMode": "HORIZONTAL",
                                    "absoluteBoundingBox": { "x": 0, "y": 0, "width": 800, "height": 40 },
                                    "fills": [{ "type": "SOLID", "color": { "r": 0.13, "g": 0.13, "b": 0.13, "a": 1 } }],
                                    "children": [
                                        {
                                            "id": "1:2",
                                            "name": "Health Label",
                                            "type": "TEXT",
                                            "characters": "HP: 100"
                                        },
                                        {
                                            "id": "1:3",
                                            "name": "Location Label",
                                            "type": "TEXT",
                                            "characters": "Town Square"
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        // Parse Figma JSON
        var document = _parser.Parse(figmaJson);
        document.Name.Should().Be("StatusBar");

        // Generate Avalonia
        var result = avaloniaGenerator.Generate(document, _options);

        result.Success.Should().BeTrue();
        result.Files.Should().HaveCount(3);

        // Check AXAML file
        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("<UserControl");
        axamlFile.Content.Should().Contain("xmlns=\"https://github.com/avaloniaui\"");
        axamlFile.Content.Should().Contain("x:Class=\"MyGame.Hud.StatusBarView\"");
        axamlFile.Content.Should().Contain("<TextBlock");
        axamlFile.Content.Should().Contain("x:Name=\"healthLabel\"");
        axamlFile.Content.Should().Contain("x:Name=\"locationLabel\"");

        // Check code-behind
        var codeBehind = result.Files.First(f => f.Path.EndsWith(".axaml.cs", StringComparison.Ordinal));
        codeBehind.Content.Should().Contain("public partial class StatusBarView : UserControl");
        codeBehind.Content.Should().Contain("InitializeComponent()");
    }

    [Fact]
    public void Avalonia_ParseFigmaAndGenerate_WithLayouts_GeneratesCorrectPanels()
    {
        var avaloniaGenerator = new AvaloniaGenerator();
        var figmaJson = """
            {
                "name": "Layout Test",
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
                                    "layoutMode": "VERTICAL",
                                    "children": [
                                        {
                                            "id": "1:2",
                                            "name": "Header",
                                            "type": "TEXT",
                                            "characters": "Title"
                                        },
                                        {
                                            "id": "1:3",
                                            "name": "Sidebar",
                                            "type": "FRAME",
                                            "absoluteBoundingBox": { "x": 0, "y": 0, "width": 200, "height": 400 }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        var document = _parser.Parse(figmaJson);
        var result = avaloniaGenerator.Generate(document, _options);

        result.Success.Should().BeTrue();

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("StackPanel");
        axamlFile.Content.Should().Contain("Width=\"200\"");
    }
}
