using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Dsl.Pencil;
using BoomHud.Gen.Godot;
using BoomHud.Gen.TerminalGui;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Integration;

/// <summary>
/// End-to-end tests for the Pencil (.pen) format -> generators pipeline.
/// Phase 0A: Single .pen file compiles end-to-end.
/// </summary>
public class PencilEndToEndTests
{
    private readonly PenParser _parser = new();

    private readonly GenerationOptions _terminalGuiOptions = new()
    {
        Namespace = "Generated.Hud",
        IncludeComments = true,
        UseNullableAnnotations = true
    };

    private readonly GenerationOptions _godotOptions = new()
    {
        Namespace = "Generated.Hud",
        IncludeComments = true,
        UseNullableAnnotations = true,
        EmitTscn = true,
        EmitTscnAttachScript = true
    };

    [Fact]
    public void ParseAndGenerate_DebugOverlay_TerminalGui_ProducesValidCode()
    {
        // Arrange - Minimal debug overlay .pen file
        var penJson = """
            {
              "$schema": "../../schemas/pencil.schema.json",
              "canvas": { "units": "px", "scaleMode": "responsive", "width": 1920, "height": 1080 },
              "nodes": [
                {
                  "id": "debug-panel",
                  "type": "frame",
                  "name": "DebugOverlay",
                  "layout": {
                    "mode": "vertical",
                    "width": 300,
                    "height": "hug",
                    "padding": 16,
                    "gap": 8
                  },
                  "style": {
                    "background": "#1E1E2ECC",
                    "cornerRadius": 8
                  },
                  "children": [
                    {
                      "id": "fps-row",
                      "type": "frame",
                      "name": "FpsRow",
                      "layout": { "mode": "horizontal", "gap": 8 },
                      "children": [
                        {
                          "id": "fps-label",
                          "type": "text",
                          "name": "FpsLabel",
                          "content": "FPS:",
                          "style": { "foreground": "#CDD6F4" }
                        },
                        {
                          "id": "fps-value",
                          "type": "text",
                          "name": "FpsValue",
                          "content": "60",
                          "style": { "foreground": "#A6E3A1" },
                          "bindings": {
                            "content": { "path": "Fps", "mode": "oneWay" }
                          }
                        }
                      ]
                    },
                    {
                      "id": "seed-row",
                      "type": "frame",
                      "name": "SeedRow",
                      "layout": { "mode": "horizontal", "gap": 8 },
                      "children": [
                        {
                          "id": "seed-label",
                          "type": "text",
                          "name": "SeedLabel",
                          "content": "Seed:",
                          "style": { "foreground": "#CDD6F4" }
                        },
                        {
                          "id": "seed-value",
                          "type": "text",
                          "name": "SeedValue",
                          "content": "12345",
                          "style": { "foreground": "#89B4FA" },
                          "bindings": {
                            "content": { "path": "WorldSeed", "mode": "oneWay" }
                          }
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        // Act
        var document = _parser.Parse(penJson);
        var generator = new TerminalGuiGenerator();
        var result = generator.Generate(document, _terminalGuiOptions);

        // Assert - Document parsed correctly
        document.Name.Should().Be("DebugOverlay");
        document.Root.Type.Should().Be(ComponentType.Container);
        document.Root.Children.Should().HaveCount(2); // FpsRow, SeedRow

        // Assert - Generation succeeded
        result.Success.Should().BeTrue("Terminal.Gui generation should succeed");
        result.Files.Should().HaveCountGreaterOrEqualTo(2);

        // Assert - View file contains expected elements
        var viewFile = result.Files.FirstOrDefault(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Should().NotBeNull();
        viewFile!.Content.Should().Contain("namespace Generated.Hud;");
        viewFile.Content.Should().Contain("class DebugOverlayView");
        // Note: Generator uses hyphenated IDs from .pen, so _fps-label not _fpsLabel
        viewFile.Content.Should().Contain("fps-label");
        viewFile.Content.Should().Contain("fps-value");
        viewFile.Content.Should().Contain("seed-label");
        viewFile.Content.Should().Contain("seed-value");

        // Assert - ViewModel interface contains bound properties
        var vmFile = result.Files.FirstOrDefault(f => f.Path.Contains("ViewModel", StringComparison.Ordinal));
        vmFile.Should().NotBeNull();
        vmFile!.Content.Should().Contain("Fps");
        vmFile.Content.Should().Contain("WorldSeed");
    }

    [Fact]
    public void ParseAndGenerate_DebugOverlay_Godot_ProducesValidTscnAndCSharp()
    {
        // Arrange - Same debug overlay .pen file
        var penJson = """
            {
              "canvas": { "units": "px" },
              "nodes": [
                {
                  "id": "debug-panel",
                  "type": "frame",
                  "name": "DebugOverlay",
                  "layout": { "mode": "vertical", "width": 300, "padding": 16, "gap": 8 },
                  "style": { "background": "#1E1E2ECC" },
                  "children": [
                    {
                      "id": "fps-row",
                      "type": "frame",
                      "name": "FpsRow",
                      "layout": { "mode": "horizontal", "gap": 8 },
                      "children": [
                        {
                          "id": "fps-label",
                          "type": "text",
                          "name": "FpsLabel",
                          "content": "FPS:"
                        },
                        {
                          "id": "fps-value",
                          "type": "text",
                          "name": "FpsValue",
                          "content": "60",
                          "bindings": { "content": { "path": "Fps" } }
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        // Act
        var document = _parser.Parse(penJson);
        var generator = new GodotGenerator();
        var result = generator.Generate(document, _godotOptions);

        // Assert - Generation succeeded
        result.Success.Should().BeTrue("Godot generation should succeed");

        // Assert - C# view file exists and has expected content
        var csFile = result.Files.FirstOrDefault(f => f.Path.EndsWith("View.cs", StringComparison.Ordinal) ||
                                                       f.Path.EndsWith("DebugOverlay.cs", StringComparison.Ordinal));
        csFile.Should().NotBeNull("C# file should be generated");
        csFile!.Content.Should().Contain("namespace Generated.Hud");
        // Note: Generator uses transformed IDs from .pen
        csFile.Content.Should().Contain("fpslabel");
        csFile.Content.Should().Contain("fpsvalue");

        // Assert - TSCN scene file exists
        var tscnFile = result.Files.FirstOrDefault(f => f.Path.EndsWith(".tscn", StringComparison.Ordinal));
        tscnFile.Should().NotBeNull("TSCN scene file should be generated");
        tscnFile!.Content.Should().Contain("[gd_scene");
        tscnFile.Content.Should().Contain("fps-label"); // Uses hyphenated names from .pen IDs
    }

    [Fact]
    public void ParseAndGenerate_TokenRefs_ResolvedAsTokenNames()
    {
        // Arrange - .pen file with token references
        var penJson = """
            {
              "nodes": [
                {
                  "id": "panel",
                  "type": "frame",
                  "name": "TokenPanel",
                  "layout": { "mode": "vertical" },
                  "style": {
                    "background": { "$ref": "tokens.colors.primary" },
                    "foreground": "$text-primary"
                  }
                }
              ]
            }
            """;

        // Act
        var document = _parser.Parse(penJson);

        // Assert - Token refs are captured as token names
        document.Root.Style.Should().NotBeNull();
        document.Root.Style!.BackgroundToken.Should().Be("colors.primary");
        document.Root.Style.ForegroundToken.Should().Be("text-primary");
        
        // Actual color values should be null since they're token refs
        document.Root.Style.Background.Should().BeNull();
        document.Root.Style.Foreground.Should().BeNull();
    }

    [Fact]
    public void ParseAndGenerate_InlineBindings_ConvertToBindingSpec()
    {
        // Arrange
        var penJson = """
            {
              "nodes": [
                {
                  "id": "label",
                  "type": "text",
                  "name": "BoundLabel",
                  "content": "Hello",
                  "bindings": {
                    "content": { "path": "Message", "mode": "twoWay" },
                    "style.foreground": "ErrorColor"
                  }
                }
              ]
            }
            """;

        // Act
        var document = _parser.Parse(penJson);

        // Assert
        document.Root.Bindings.Should().NotBeEmpty();
        
        var contentBinding = document.Root.Bindings.FirstOrDefault(b => b.Property == "content");
        contentBinding.Should().NotBeNull();
        contentBinding!.Path.Should().Be("Message");
        contentBinding.Mode.Should().Be(BindingMode.TwoWay);

        var foregroundBinding = document.Root.Bindings.FirstOrDefault(b => b.Property == "style.foreground");
        foregroundBinding.Should().NotBeNull();
        foregroundBinding!.Path.Should().Be("ErrorColor");
    }

    [Fact]
    public void ParseAndGenerate_LayoutModes_MapCorrectly()
    {
        // Arrange
        var penJson = """
            {
              "nodes": [
                {
                  "id": "hbox",
                  "type": "frame",
                  "name": "HorizontalBox",
                  "layout": { "mode": "horizontal", "alignment": "center", "gap": 8 },
                  "children": [
                    {
                      "id": "vbox",
                      "type": "frame",
                      "name": "VerticalBox",
                      "layout": { "mode": "vertical", "justify": "space-between" }
                    }
                  ]
                }
              ]
            }
            """;

        // Act
        var document = _parser.Parse(penJson);

        // Assert - Horizontal layout
        document.Root.Layout.Should().NotBeNull();
        document.Root.Layout!.Type.Should().Be(LayoutType.Horizontal);
        document.Root.Layout.Gap.Should().NotBeNull();
        document.Root.Layout.Gap!.Value.Top.Should().Be(8); // Gap is stored as Spacing
        document.Root.Layout.Align.Should().Be(Alignment.Center);

        // Assert - Vertical layout (child)
        var vbox = document.Root.Children[0];
        vbox.Should().NotBeNull();
        vbox!.Layout!.Type.Should().Be(LayoutType.Vertical);
        vbox.Layout.Justify.Should().Be(Justification.SpaceBetween);
    }
}
