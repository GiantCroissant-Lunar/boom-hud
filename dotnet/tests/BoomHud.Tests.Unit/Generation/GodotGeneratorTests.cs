using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;
using BoomHud.Dsl.Figma;
using BoomHud.Gen.Godot;
using BoomHud.Gen.TerminalGui;
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

    [Fact]
    public void Generate_WithMotion_EmitsGodotMotionAttachmentClass()
    {
        var options = _options with
        {
            EmitViewModelInterfaces = false,
            Motion = new MotionDocument
            {
                Name = "RootMotion",
                FramesPerSecond = 30,
                Clips =
                [
                    new MotionClip
                    {
                        Id = "intro",
                        Name = "Intro",
                        DurationFrames = 45,
                        Tracks =
                        [
                            new MotionTrack
                            {
                                Id = "labelFade",
                                TargetId = "label",
                                Channels =
                                [
                                    new MotionChannel
                                    {
                                        Property = MotionProperty.Opacity,
                                        Keyframes =
                                        [
                                            new MotionKeyframe { Frame = 0, Value = MotionValue.FromNumber(0) },
                                            new MotionKeyframe { Frame = 15, Value = MotionValue.FromNumber(1), Easing = MotionEasing.EaseOut }
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
            Name = "Root",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode { Id = "label", Type = ComponentType.Label }
                ]
            }
        };

        var result = _generator.Generate(doc, options);

        result.Files.Should().Contain(f => f.Path == "RootMotion.g.cs");
        var motionFile = result.Files.First(f => f.Path == "RootMotion.g.cs");
        motionFile.Content.Should().Contain("public static class RootMotion");
        motionFile.Content.Should().Contain("player.AddAnimation(\"intro\", BuildIntro());");
        motionFile.Content.Should().Contain("new NodePath(\"../label:modulate:a\")");
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

    [Fact]
    public void Generate_WithTscn_EmitsLayoutAndStyleAsSceneProperties()
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
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec { Type = LayoutType.Vertical },
                Children =
                [
                    new ComponentNode
                    {
                        Type = ComponentType.Label,
                        Id = "title",
                        Layout = new LayoutSpec { Width = Dimension.Fill, Height = Dimension.Fill },
                        Style = new StyleSpec { Foreground = new Color(244, 246, 248) }
                    },
                    new ComponentNode
                    {
                        Type = ComponentType.ScrollView,
                        Id = "side",
                        Layout = new LayoutSpec { Width = Dimension.Pixels(380), Gap = new Spacing(6) }
                    }
                ]
            }
        };

        var tscn = _generator.Generate(doc, options).Files.First(f => f.Path == "RootView.tscn").Content;

        // Layout/style is baked into the scene so a script-less .tscn renders like a script-backed one.
        tscn.Should().Contain("size_flags_horizontal = 3");          // fill width -> ExpandFill
        tscn.Should().Contain("size_flags_vertical = 3");            // fill height -> ExpandFill
        tscn.Should().Contain("theme_override_colors/font_color = Color(");
        tscn.Should().Contain("custom_minimum_size = Vector2(380");  // pixel width
        tscn.Should().Contain("theme_override_constants/separation = 6");
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

    [Fact]
    public void Generate_WithCylinderSpatial_EmitsNode3DHostAndQuadFaces()
    {
        var options = _options with
        {
            EmitTscn = true,
            EmitViewModelInterfaces = false,
            OutputDirectory = CreateTempGodotProjectRoot()
        };

        var doc = new HudDocument
        {
            Name = "Tunnel",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Spatial = new SpatialSpec
                {
                    Shape = SpatialShape.Cylinder,
                    Radius = 2.0,
                    AngularSpacingDeg = 45.0,
                    Facing = SpatialFacing.Inward
                },
                Children =
                [
                    new ComponentNode { Id = "card1", Type = ComponentType.Label, Properties = new Dictionary<string, BindableValue<object?>> { ["text"] = "Card 1" } },
                    new ComponentNode { Id = "card2", Type = ComponentType.Label, Properties = new Dictionary<string, BindableValue<object?>> { ["text"] = "Card 2" } }
                ]
            }
        };

        var result = _generator.Generate(doc, options);

        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tscnFile = result.Files.First(f => f.Path == "TunnelView.tscn");
        tscnFile.Content.Should().Contain("type=\"Node3D\"");
        tscnFile.Content.Should().Contain("type=\"MeshInstance3D\"");
        tscnFile.Content.Should().Contain("Transform3D");
        tscnFile.Content.Should().Contain("type=\"SubViewport\"");

        // Regression guard: the dead ComputeCylinderTransform runtime method was removed;
        // all cylinder math is now in the baked .tscn transform.
        var csFile = result.Files.First(f => f.Path == "TunnelView.cs");
        csFile.Content.Should().Contain("SetupSpatialFaces()");
        csFile.Content.Should().NotContain("private static Transform3D ComputeCylinderTransform");
        csFile.Content.Should().Contain("QuadMesh");
        csFile.Content.Should().Contain("ViewportTexture");
        csFile.Content.Should().Contain("StandardMaterial3D");

        // Assert depth VARIES per index (not all z=0 flat ring).
        // card1 is index 0 => z=0*DepthScale=0; card2 is index 1 => z=1*1.0=1 (non-zero).
        tscnFile.Content.Should().Contain(", 0, 0)");       // card1 at z=0
        tscnFile.Content.Should().Contain(", 1)");           // card2 at z=1 (DepthScale=1.0 default)

        // Assert Inward facing produces a non-identity basis.
        // Identity basis would be "1, 0, 0, 0, 1, 0, 0, 0, 1". Inward facing must differ.
        tscnFile.Content.Should().NotContain("Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1,");
    }

    [Fact]
    public void Generate_WithCylinderSpatialOutward_ProducesDifferentBasisFromInward()
    {
        var options = _options with
        {
            EmitTscn = true,
            EmitViewModelInterfaces = false,
            OutputDirectory = CreateTempGodotProjectRoot()
        };

        var docInward = new HudDocument
        {
            Name = "Tunnel",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Spatial = new SpatialSpec
                {
                    Shape = SpatialShape.Cylinder,
                    Radius = 2.0,
                    AngularSpacingDeg = 45.0,
                    Facing = SpatialFacing.Inward
                },
                Children =
                [
                    new ComponentNode { Id = "card1", Type = ComponentType.Label }
                ]
            }
        };

        var docOutward = new HudDocument
        {
            Name = "Tunnel",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Spatial = new SpatialSpec
                {
                    Shape = SpatialShape.Cylinder,
                    Radius = 2.0,
                    AngularSpacingDeg = 45.0,
                    Facing = SpatialFacing.Outward
                },
                Children =
                [
                    new ComponentNode { Id = "card1", Type = ComponentType.Label }
                ]
            }
        };

        var resultInward = _generator.Generate(docInward, options);
        var resultOutward = _generator.Generate(docOutward, options);

        var tscnInward = resultInward.Files.First(f => f.Path == "TunnelView.tscn");
        var tscnOutward = resultOutward.Files.First(f => f.Path == "TunnelView.tscn");

        // Both should contain Transform3D with a non-identity basis.
        tscnInward.Content.Should().Contain("Transform3D(");
        tscnOutward.Content.Should().Contain("Transform3D(");
        tscnInward.Content.Should().NotContain("Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1,");
        tscnOutward.Content.Should().NotContain("Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1,");

        // Inward and Outward basis vectors must differ.
        // Extract the Transform3D string from each and compare.
        var inwardTransform = ExtractTransform3D(tscnInward.Content);
        var outwardTransform = ExtractTransform3D(tscnOutward.Content);
        inwardTransform.Should().NotBeNullOrEmpty();
        outwardTransform.Should().NotBeNullOrEmpty();
        inwardTransform.Should().NotBe(outwardTransform,
            "Inward and Outward facing must produce different Transform3D basis vectors.");
    }

    [Fact]
    public void Generate_WithSpatialUnsupportedShape_FallsBackTo2DWithDiagnostic()
    {
        var options = _options with
        {
            EmitTscn = true,
            EmitViewModelInterfaces = false,
            OutputDirectory = CreateTempGodotProjectRoot()
        };

        var doc = new HudDocument
        {
            Name = "Tunnel",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Spatial = new SpatialSpec
                {
                    Shape = SpatialShape.Radial,
                    Radius = 2.0
                },
                Children =
                [
                    new ComponentNode { Id = "card1", Type = ComponentType.Label }
                ]
            }
        };

        var result = _generator.Generate(doc, options);

        result.Diagnostics.Should().Contain(d => d.Message.Contains("not supported") && d.Severity == DiagnosticSeverity.Warning);

        var tscnFile = result.Files.First(f => f.Path == "TunnelView.tscn");
        tscnFile.Content.Should().NotContain("Node3D");
        tscnFile.Content.Should().NotContain("MeshInstance3D");
    }

    [Fact]
    public void Generate_WithoutSpatial_Generates2DLayoutAsBefore()
    {
        var options = _options with
        {
            EmitTscn = true,
            EmitViewModelInterfaces = false,
            OutputDirectory = CreateTempGodotProjectRoot()
        };

        var doc = new HudDocument
        {
            Name = "Plain",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode { Id = "label1", Type = ComponentType.Label, Properties = new Dictionary<string, BindableValue<object?>> { ["text"] = "Hello" } }
                ]
            }
        };

        var result = _generator.Generate(doc, options);

        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tscnFile = result.Files.First(f => f.Path == "PlainView.tscn");
        tscnFile.Content.Should().NotContain("Node3D");
        tscnFile.Content.Should().NotContain("MeshInstance3D");
        tscnFile.Content.Should().Contain("type=\"Control\"");
        tscnFile.Content.Should().Contain("type=\"Label\"");

        var csFile = result.Files.First(f => f.Path == "PlainView.cs");
        csFile.Content.Should().NotContain("SetupSpatialFaces");
        csFile.Content.Should().NotContain("ComputeCylinderTransform");
    }

    [Fact]
    public void TerminalGuiGenerator_WithSpatial_FallsBackTo2DWithDiagnostic()
    {
        var generator = new TerminalGuiGenerator();
        var options = _options with { EmitViewModelInterfaces = false };

        var doc = new HudDocument
        {
            Name = "Tunnel",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Spatial = new SpatialSpec
                {
                    Shape = SpatialShape.Cylinder,
                    Radius = 2.0
                },
                Children =
                [
                    new ComponentNode { Id = "card1", Type = ComponentType.Label }
                ]
            }
        };

        var result = generator.Generate(doc, options);

        result.Diagnostics.Should().Contain(d => d.Message.Contains("Spatial layout is not supported") && d.Severity == DiagnosticSeverity.Warning);

        var csFile = result.Files.First(f => f.Path == "TunnelView.g.cs");
        csFile.Content.Should().NotContain("Node3D");
        csFile.Content.Should().NotContain("MeshInstance3D");
        csFile.Content.Should().NotContain("ComputeCylinderTransform");
    }

    [Fact]
    public void Generate_CSharpView_EscapesBackslashAndTab_InTextLiterals()
    {
        var options = _options with { EmitViewModelInterfaces = false };

        // Text with a tab and a backslash. The old EscapeString only handled '"' and '\n',
        // so these produced an invalid C# string literal (e.g. "val\end") in the generated view.
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
                        Id = "label",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "col\tval\\end"
                        }
                    }
                ]
            }
        };

        var view = _generator.Generate(doc, options).Files.First(f => f.Path == "RootView.cs").Content;

        // Tab -> \t, backslash -> \\ : the emitted literal must be well-formed C#.
        view.Should().Contain("col\\tval\\\\end");
        // The raw tab character must not leak into the generated source.
        view.Should().NotContain("col\tval");
    }

    [Fact]
    public void Generate_CSharpView_FormatsFloatsWithInvariantCulture_UnderCommaDecimalLocale()
    {
        var options = _options with { EmitViewModelInterfaces = false };

        var doc = new HudDocument
        {
            Name = "Root",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec { Type = LayoutType.Vertical },
                // Foreground color -> fractional Color() floats (244/255 = 0.95686...).
                Style = new StyleSpec { Foreground = new Color(244, 246, 248) },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label,
                        // Weight -> SizeFlagsStretchRatio float.
                        Layout = new LayoutSpec { Weight = 0.5 }
                    }
                ]
            }
        };

        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // A locale whose decimal separator is ',' would, before the fix, emit "0,5f" /
            // "Color(0,95686..." into the generated C#, which then fails to compile.
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var view = _generator.Generate(doc, options).Files.First(f => f.Path == "RootView.cs").Content;

            view.Should().Contain("new Color(0.9");
            view.Should().Contain("SizeFlagsStretchRatio = 0.5f");
            view.Should().NotContain("Color(0,");
            view.Should().NotContain("0,5f");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    private static string ExtractTransform3D(string tscnContent)
    {
        var m = Regex.Match(tscnContent, @"transform\s*=\s*Transform3D\([^)]+\)", RegexOptions.CultureInvariant);
        return m.Success ? m.Value : string.Empty;
    }
}
