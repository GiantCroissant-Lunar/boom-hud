using System.IO;
using System.Text.Json;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Dsl.Pencil;
using BoomHud.Gen.Godot;
using BoomHud.Gen.TerminalGui;
using BoomHud.Gen.Unity;
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

      private readonly GenerationOptions _unityOptions = new()
      {
        Namespace = "Generated.Hud",
        IncludeComments = true,
        UseNullableAnnotations = true
      };

    [Fact]
    public void ParseAndGenerate_DebugOverlay_TerminalGui_ProducesValidCode()
    {
        // Arrange - Minimal debug overlay .pen file
        var penJson = """
            {
              "$schema": "../../schemas/json/pencil.schema.json",
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
    public void ParseAndGenerate_PenSpecificFields_Unity_ProducesValidUiToolkitArtifacts()
    {
        var penJson = """
            {
              "version": "1.0",
              "name": "DebugOverlay",
              "nodes": [
                {
                  "id": "debug-panel",
                  "type": "frame",
                  "layout": {
                    "mode": "vertical",
                    "width": "hug",
                    "padding": { "vertical": 8, "horizontal": 12 },
                    "position": "absolute"
                  },
                  "style": {
                    "fill": { "$ref": "tokens.colors.debug-bg" },
                    "stroke": "#445566",
                    "strokeWidth": 1,
                    "cornerRadius": 4
                  },
                  "children": [
                    {
                      "id": "separator",
                      "type": "rectangle",
                      "name": "Separator",
                      "layout": {
                        "width": "fill",
                        "height": { "type": "fixed", "value": 1 }
                      },
                      "style": {
                        "fill": "#888888",
                        "opacity": 0.3
                      }
                    },
                    {
                      "id": "fps-value",
                      "type": "text",
                      "name": "FpsValue",
                      "content": "60",
                      "style": {
                        "fill": "#00ff00",
                        "fontWeight": "bold"
                      },
                      "bindings": {
                        "content": {
                          "$bind": "Fps",
                          "format": "{0:F0}"
                        }
                      }
                    }
                  ]
                }
              ]
            }
            """;

        var document = _parser.Parse(penJson);
        var generator = new UnityGenerator();
        var result = generator.Generate(document, _unityOptions);

        document.Name.Should().Be("DebugOverlay");
        document.Root.Layout!.Type.Should().Be(LayoutType.Absolute);
        document.Root.Style!.Border.Should().NotBeNull();
        document.Root.Style.Border!.Width.Should().Be(1);
        document.Root.Children[0].Layout!.Height.Should().Be(Dimension.Pixels(1));
        document.Root.Children[1].Style!.Foreground.Should().Be(Color.Green);

        result.Success.Should().BeTrue("Unity generation should succeed from .pen input");
        result.Files.Should().Contain(f => f.Path == "DebugOverlayView.uxml");
        result.Files.Should().Contain(f => f.Path == "DebugOverlayView.uss");
        result.Files.Should().Contain(f => f.Path == "DebugOverlayView.gen.cs");

        var controllerFile = result.Files.First(f => f.Path == "DebugOverlayView.gen.cs");
        controllerFile.Content.Should().Contain("public Label FpsValue { get; }");
        controllerFile.Content.Should().Contain("FpsValue = Root.Q<Label>(\"FpsValue\")");
        controllerFile.Content.Should().Contain("FpsValue.text = AsString(_viewModel.Fps);");

        var ussFile = result.Files.First(f => f.Path == "DebugOverlayView.uss");
        ussFile.Content.Should().Contain("border-left-width: 1px;");
        ussFile.Content.Should().Contain("height: 1px;");
        ussFile.Content.Should().Contain("opacity: 0.3;");
    }

    [Fact]
    public void ParseAndGenerate_PenInlineTokens_Unity_ResolvesColorsAndAbsoluteRoot()
    {
        var penJson = """
            {
              "version": "1.0",
              "name": "DebugOverlay",
              "tokens": {
                "colors": {
                  "debug-bg": "rgba(0, 0, 0, 0.85)",
                  "debug-text": "#00ff00",
                  "debug-muted": "#888888"
                }
              },
              "nodes": [
                {
                  "id": "root",
                  "type": "frame",
                  "name": "DebugOverlay",
                  "layout": {
                    "mode": "vertical",
                    "position": "absolute",
                    "width": "hug",
                    "padding": { "top": 8, "right": 12, "bottom": 8, "left": 12 }
                  },
                  "style": {
                    "background": { "$ref": "tokens.colors.debug-bg" }
                  },
                  "children": [
                    {
                      "id": "fps-row",
                      "type": "frame",
                      "name": "FpsRow",
                      "layout": { "mode": "horizontal", "width": "fill" },
                      "children": [
                        {
                          "id": "fps-label",
                          "type": "text",
                          "name": "FpsLabel",
                          "content": "FPS",
                          "style": { "fill": { "$ref": "tokens.colors.debug-muted" } }
                        },
                        {
                          "id": "fps-value",
                          "type": "text",
                          "name": "FpsValue",
                          "content": "60",
                          "style": { "fill": { "$ref": "tokens.colors.debug-text" } }
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var document = _parser.Parse(penJson);
        var generator = new UnityGenerator();
        var result = generator.Generate(document, _unityOptions);

        result.Success.Should().BeTrue();
        document.Root.Layout!.Type.Should().Be(LayoutType.Vertical);
        document.Root.Style!.Background.Should().Be(new Color(0, 0, 0, 217));
        document.Root.Children[0].Children[0].Style!.Foreground.Should().Be(new Color(136, 136, 136));
        document.Root.Children[0].Children[1].Style!.Foreground.Should().Be(Color.Green);

        var ussFile = result.Files.First(f => f.Path == "DebugOverlayView.uss");
        ussFile.Content.Should().Contain("background-color: #000000D9;");
        ussFile.Content.Should().Contain("color: #888888;");
        ussFile.Content.Should().Contain("color: #00FF00;");
        ussFile.Content.Should().Contain("position: absolute;");
        ussFile.Content.Should().Contain("left: 0px;");
        ussFile.Content.Should().Contain("top: 0px;");
        ussFile.Content.Should().Contain("flex-direction: column;");
    }

    [Fact]
    public void ParseAndGenerate_DebugOverlayPen_Unity_ResolvesRgbaBackgroundAndKeepsFlowLayout()
    {
        var penPath = GetRepoFilePath(@"ui\sources\pencil\debug-overlay.pen");
        var penJson = File.ReadAllText(penPath);

        var document = _parser.Parse(penJson);
        var generator = new UnityGenerator();
        var result = generator.Generate(document, _unityOptions);

        result.Success.Should().BeTrue();
        document.Root.Layout!.Type.Should().Be(LayoutType.Vertical);
        document.Root.Style!.Background.Should().Be(new Color(0, 0, 0, 217));
        document.Root.Style.BackgroundToken.Should().Be("colors.debug-bg");

        var ussFile = result.Files.First(f => f.Path == "DebugOverlayView.uss");
        ussFile.Content.Should().Contain("background-color: #000000D9;");
        ussFile.Content.Should().Contain(".boomhud-fps-row {");
        ussFile.Content.Should().Contain("width: auto;");
        ussFile.Content.Should().Contain("justify-content: flex-start;");
        ussFile.Content.Should().NotContain("justify-content: space-between;");
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
        
        var contentBinding = document.Root.Bindings.FirstOrDefault(b => b.Property == "Text");
        contentBinding.Should().NotBeNull();
        contentBinding!.Path.Should().Be("Message");
        contentBinding.Mode.Should().Be(BindingMode.TwoWay);

        var foregroundBinding = document.Root.Bindings.FirstOrDefault(b => b.Property == "style.foreground");
        foregroundBinding.Should().NotBeNull();
        foregroundBinding!.Path.Should().Be("ErrorColor");
    }

    [Fact]
    public void ParseAndGenerate_StyleFillBindingMap_NormalizesAndPreservesBindingMetadata()
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
                    "style.fill": {
                      "$bind": "Severity",
                      "map": {
                        "error": { "$ref": "tokens.colors.error" },
                        "warning": "#ffaa00"
                      },
                      "fallback": { "$ref": "tokens.colors.default" }
                    }
                  }
                }
              ]
            }
            """;

        // Act
        var document = _parser.Parse(penJson);

        // Assert
        var foregroundBinding = document.Root.Bindings.Single();
        foregroundBinding.Property.Should().Be("style.foreground");
        foregroundBinding.Path.Should().Be("Severity");

        foregroundBinding.ConverterParameter.Should().BeOfType<JsonElement>();
        var map = (JsonElement)foregroundBinding.ConverterParameter!;
        map.GetProperty("error").GetProperty("$ref").GetString().Should().Be("tokens.colors.error");
        map.GetProperty("warning").GetString().Should().Be("#ffaa00");

        foregroundBinding.Fallback.Should().BeOfType<JsonElement>();
        var fallback = (JsonElement)foregroundBinding.Fallback!;
        fallback.GetProperty("$ref").GetString().Should().Be("tokens.colors.default");
    }

    [Fact]
    public void ParseAndGenerate_StyleFillBinding_Unity_EmitsRuntimeColorRefresh()
    {
        var penJson = """
            {
              "name": "StatusOverlay",
              "nodes": [
                {
                  "id": "status-label",
                  "type": "text",
                  "name": "StatusLabel",
                  "content": "OK",
                  "style": {
                    "fill": "#00FF00"
                  },
                  "bindings": {
                    "style.fill": {
                      "$bind": "StatusTone",
                      "map": {
                        "good": "#00FF00",
                        "warning": "#FFAA00",
                        "critical": "#FF4444"
                      },
                      "fallback": "#00FF00"
                    }
                  }
                }
              ]
            }
            """;

        var document = _parser.Parse(penJson);
        var generator = new UnityGenerator();
        var result = generator.Generate(document, _unityOptions);

        result.Success.Should().BeTrue();

        var controllerFile = result.Files.First(f => f.Path == "StatusOverlayView.gen.cs");
        controllerFile.Content.Should().Contain("Root.style.color = ParseStyleColor(ResolveMappedStyleValue(_viewModel.StatusTone, \"#00FF00\", \"good\", \"#00FF00\", \"warning\", \"#FFAA00\", \"critical\", \"#FF4444\"), \"#00FF00\");");
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

      [Fact]
      public void ParseAndGenerate_RawPencilSample_Unity_EmitsComponentArtifacts_AndAbsolutePlacement()
      {
        var samplePath = GetRepoFilePath(Path.Combine("samples", "pencil", "raw-hud-components.pen"));
        var penJson = File.ReadAllText(samplePath);

        var document = _parser.Parse(penJson);
        var generator = new UnityGenerator();
        var result = generator.Generate(document, _unityOptions);

        result.Success.Should().BeTrue();
        result.Files.Should().Contain(f => f.Path == "ExploreHudView.uxml");
        result.Files.Should().Contain(f => f.Path == "ExploreHudView.uss");
        result.Files.Should().Contain(f => f.Path == "ActionButtonView.uxml");
        result.Files.Should().Contain(f => f.Path == "CharPortraitView.uxml");

        var rootUxml = result.Files.First(f => f.Path == "ExploreHudView.uxml");
        rootUxml.Content.Should().Contain("name=\"C1name\"");
        rootUxml.Content.Should().Contain("name=\"AttackButton\"");

        var rootUss = result.Files.First(f => f.Path == "ExploreHudView.uss");
        rootUss.Content.Should().Contain("left: 444px;");
        rootUss.Content.Should().Contain("top: 12px;");
        rootUss.Content.Should().Contain("left: 16px;");
        rootUss.Content.Should().Contain("top: 620px;");

        var componentUxml = result.Files.First(f => f.Path == "CharPortraitView.uxml");
        componentUxml.Content.Should().Contain("name=\"AttackButton\"");
      }

      [Fact]
      public void Parse_RealFullPen_ContentFrame_DefaultsToHorizontalLayout()
      {
        var samplePath = @"C:\Users\User\project-ultima-magic\ultima-magic\docs\assets\hud\full.pen";
        var penJson = File.ReadAllText(samplePath);

        var document = _parser.Parse(penJson);
        var generator = new UnityGenerator();
        var result = generator.Generate(document, _unityOptions);

        var content = document.Root.Children
            .Single(child => child.Id == "HUD")
            .Children.Single(child => child.Id == "Content");

        content.Layout.Should().NotBeNull();
        content.Layout!.Type.Should().Be(LayoutType.Horizontal);

        var rootUss = result.Files.First(f => f.Path == "ExploreHudView.uss");
        rootUss.Content.Should().Contain(".boomhud-content");
        rootUss.Content.Should().Contain("flex-direction: row;");
      }

      [Fact]
      public void Parse_RealFullPen_ComponentRootsWithCanvasCoordinates_PreserveFlowLayouts()
      {
        var samplePath = @"C:\Users\User\project-ultima-magic\ultima-magic\docs\assets\hud\full.pen";
        var penJson = File.ReadAllText(samplePath);

        var document = _parser.Parse(penJson);

        document.Components.Should().ContainKey("minimap");
        document.Components.Should().ContainKey("charPortrait");
        document.Components["minimap"].Root.Layout.Should().NotBeNull();
        document.Components["minimap"].Root.Layout!.Type.Should().Be(LayoutType.Vertical);
        document.Components["charPortrait"].Root.Layout.Should().NotBeNull();
        document.Components["charPortrait"].Root.Layout!.Type.Should().Be(LayoutType.Vertical);
      }
}
