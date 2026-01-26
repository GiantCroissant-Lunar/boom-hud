using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Dsl.Figma;
using BoomHud.Gen.Godot;
using FluentAssertions;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public class GodotGeneratorTests
{
    private readonly GodotGenerator _generator = new();
    private readonly GenerationOptions _options = new()
    {
        Namespace = "TestNamespace",
        IncludeComments = true,
        UseNullableAnnotations = true
    };

    [Fact]
    public void Generate_WithViewModelNamespace_UsesThatNamespaceInViewFiles()
    {
        var options = _options with { ViewModelNamespace = "FantaSim.Hud.Contracts" };

        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, options);

        var viewFile = result.Files.First(f => f.Path == "TestView.cs");
        viewFile.Content.Should().Contain("using FantaSim.Hud.Contracts;");
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

        result.Files.Should().Contain(f => f.Path == "TestView.cs");
        result.Files.Should().NotContain(f => f.Path == "ITestViewModel.g.cs");
    }

    [Fact]
    public void Generate_WithCompose_EmitsComposeFile_AndUsesSlotKeyLookup()
    {
        var options = _options with
        {
            EmitCompose = true,
            EmitViewModelInterfaces = false,
            ViewModelNamespace = "FantaSim.Hud.Contracts"
        };

        var componentId = "component:Child";
        var doc = new HudDocument
        {
            Name = "Root",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Type = ComponentType.Container,
                        ComponentRefId = componentId,
                        SlotKey = "slot.child"
                    }
                ]
            },
            Components = new Dictionary<string, HudComponentDefinition>
            {
                [componentId] = new HudComponentDefinition
                {
                    Id = componentId,
                    Name = "Child",
                    Root = new ComponentNode { Type = ComponentType.Container }
                }
            }
        };

        var result = _generator.Generate(doc, options);

        result.Files.Should().Contain(f => f.Path == "RootView.Compose.g.cs");
        var composeFile = result.Files.First(f => f.Path == "RootView.Compose.g.cs");

        composeFile.Content.Should().Contain("GetNodeOrNull<ChildView>(\"slot.child\")");
        composeFile.Content.Should().Contain("resolver.Resolve<IChildViewModel>");
        composeFile.Content.Should().Contain("\"slot.child\"");
    }

    [Fact]
    public void Generate_EmbedsDriftIds_InView()
    {
        var options = _options with
        {
            ContractId = "contract:test",
            EmitViewModelInterfaces = false
        };

        var doc = new HudDocument
        {
            Name = "Root",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "RootView.cs");

        viewFile.Content.Should().Contain("public const string BoomHudSourceId = \"sha256:");
        viewFile.Content.Should().Contain("public const string BoomHudContractId = \"contract:test\";");
    }

    [Fact]
    public void Generate_SourceId_ChangesWhenDocumentChanges()
    {
        var options = _options with { EmitViewModelInterfaces = false };

        var docA = new HudDocument
        {
            Name = "Root",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode { Id = "a", Type = ComponentType.Label }
                ]
            }
        };

        var docB = new HudDocument
        {
            Name = "Root",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode { Id = "a", Type = ComponentType.Label },
                    new ComponentNode { Id = "b", Type = ComponentType.Label }
                ]
            }
        };

        var fileA = _generator.Generate(docA, options).Files.First(f => f.Path == "RootView.cs");
        var fileB = _generator.Generate(docB, options).Files.First(f => f.Path == "RootView.cs");

        var idA = ExtractConst(fileA.Content, "BoomHudSourceId");
        var idB = ExtractConst(fileB.Content, "BoomHudSourceId");

        idA.Should().StartWith("sha256:");
        idB.Should().StartWith("sha256:");
        idA.Should().NotBe(idB);
    }

    [Fact]
    public void Generate_WithNormalizedPseudoNodes_EmbedsDebugList_InView()
    {
        var options = _options with { EmitViewModelInterfaces = false };

        var doc = new HudDocument
        {
            Name = "Timeline",
            Root = new ComponentNode
            {
                Id = "timeline",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "playButton",
                        Type = ComponentType.Button,
                        InstanceOverrides = new Dictionary<string, object?>
                        {
                            [BoomHudMetadataKeys.OriginalFigmaType] = "BUTTON",
                            [BoomHudMetadataKeys.NormalizedFromPseudoType] = true
                        }
                    }
                ]
            }
        };

        var viewFile = _generator.Generate(doc, options).Files.First(f => f.Path == "TimelineView.cs");

        viewFile.Content.Should().Contain("public static readonly string[] BoomHudNormalizedPseudoNodes");
        viewFile.Content.Should().Contain("timeline/playButton|BUTTON|Button");
    }

    private static string ExtractConst(string content, string constName)
    {
        var m = Regex.Match(content, $@"public const string\s+{Regex.Escape(constName)}\s*=\s*""([^""]+)""", RegexOptions.CultureInvariant);
        m.Success.Should().BeTrue($"Expected to find const '{constName}' in generated output.");
        return m.Groups[1].Value;
    }

    [Fact]
    public void ParseFigmaPseudoTypes_WithAnnotations_GeneratesButtonAndSliderBindings()
    {
        var figmaJson = """
            {
              "name": "Timeline Design",
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
                        "name": "Timeline",
                        "type": "FRAME",
                        "children": [
                          {
                            "id": "1:2",
                            "name": "Playback Group",
                            "type": "FRAME",
                            "children": [
                              {
                                "id": "1:3",
                                "name": "Play Button",
                                "type": "BUTTON"
                              }
                            ]
                          },
                          {
                            "id": "1:4",
                            "name": "Scrubber",
                            "type": "SLIDER"
                          }
                        ]
                      }
                    ]
                  }
                ]
              }
            }
            """;

        var annotationsJson = """
            {
              "nodes": [
                {
                  "match": { "path": ["timeline", "playbackGroup", "playButton"] },
                  "set": { "type": "Button" },
                  "bindings": { "command": "PlayPauseCommand" }
                },
                {
                  "match": { "path": ["timeline", "scrubber"] },
                  "set": { "type": "Slider" },
                  "bindings": { "value": "ScrubberValue" }
                }
              ]
            }
            """;

        var parser = new FigmaParser();
        var doc = parser.Parse(figmaJson);
        var annotations = FigmaAnnotations.Parse(annotationsJson);
        doc = FigmaAnnotations.Apply(doc, annotations);

        var options = _options with
        {
            EmitViewModelInterfaces = false,
            ViewModelNamespace = "FantaSim.Hud.Contracts"
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "TimelineView.cs");

        // Button from pseudo-type BUTTON + set.type override
        viewFile.Content.Should().Contain("_playButton = new Button();");
        viewFile.Content.Should().Contain("_playButton.Pressed += () =>");
        viewFile.Content.Should().Contain("_viewModel?.PlayPauseCommand");

        // Slider from pseudo-type SLIDER + set.type override + value binding
        viewFile.Content.Should().Contain("_scrubber = new HSlider();");
        viewFile.Content.Should().Contain("_scrubber.Value = Convert.ToDouble(_viewModel.ScrubberValue);");
    }

    [Fact]
    public void Generate_MenuNodes_DoNotEmitDirectControlPropertyAssignments()
    {
        var options = _options with
        {
            EmitViewModelInterfaces = false
        };

        var doc = new HudDocument
        {
            Name = "MenuBar",
            Root = new ComponentNode
            {
                Type = ComponentType.MenuBar,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "leftGroup",
                        Type = ComponentType.Menu,
                        Layout = new LayoutSpec { Type = LayoutType.Horizontal, Width = Dimension.Fill }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "MenuBarView.cs");

        // Menu maps to PopupMenu, which is not a Control. Ensure we never emit direct Control property sets on it.
        viewFile.Content.Should().NotContain("_leftGroup.SizeFlagsHorizontal");
        viewFile.Content.Should().NotContain("_leftGroup.SizeFlagsVertical");
        viewFile.Content.Should().NotContain("_leftGroup.CustomMinimumSize");
        viewFile.Content.Should().Contain("if ((object)_leftGroup is Control c)");
    }

    [Fact]
    public void Generate_WithTscn_AttachScriptFalse_DoesNotAttachRootScript()
    {
        var options = _options with
        {
            EmitViewModelInterfaces = false,
            EmitTscn = true,
            EmitTscnAttachScript = false,
            OutputDirectory = CreateTempGodotProjectRoot()
        };

        var doc = new HudDocument
        {
            Name = "Root",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, options);
        var tscnFile = result.Files.First(f => f.Path == "RootView.tscn");
        tscnFile.Content.Should().NotContain("script = ExtResource(\"1_root_script\")");
        tscnFile.Content.Should().NotContain("[ext_resource type=\"Script\"");
    }

    [Fact]
    public void Generate_WithTscn_AttachScriptTrue_AttachesRootScript_WhenProjectRootResolvable()
    {
        var options = _options with
        {
            EmitViewModelInterfaces = false,
            EmitTscn = true,
            EmitTscnAttachScript = true,
            OutputDirectory = CreateTempGodotProjectRoot()
        };

        var doc = new HudDocument
        {
            Name = "Root",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, options);
        var tscnFile = result.Files.First(f => f.Path == "RootView.tscn");
        tscnFile.Content.Should().Contain("script = ExtResource(\"1_root_script\")");
    }

    private static string CreateTempGodotProjectRoot()
    {
        var dir = global::System.IO.Path.Combine(
            global::System.IO.Path.GetTempPath(),
            "BoomHudTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(global::System.IO.Path.Combine(dir, "project.godot"), "config_version=5\n");
        return dir;
    }
}
