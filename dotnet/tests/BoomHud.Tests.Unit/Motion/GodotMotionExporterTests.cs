using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;
using BoomHud.Gen.Godot;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Motion;

public sealed class GodotMotionExporterTests
{
    private readonly GenerationOptions _options = new()
    {
        Namespace = "Generated.Hud",
        IncludeComments = true,
        UseNullableAnnotations = true
    };

    [Fact]
    public void Generate_ExportsAnimationPlayerCodeForSupportedTracks()
    {
        var document = new HudDocument
        {
            Name = "DebugOverlay",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "header",
                        Type = ComponentType.Container,
                        Children =
                        [
                            new ComponentNode { Id = "version", Type = ComponentType.Label }
                        ]
                    },
                    new ComponentNode
                    {
                        Id = "fps-row",
                        Type = ComponentType.Container,
                        Children =
                        [
                            new ComponentNode { Id = "fps-value", Type = ComponentType.Label }
                        ]
                    }
                ]
            }
        };

        var motion = new MotionDocument
        {
            Name = "DebugOverlayMotion",
            FramesPerSecond = 30,
            Clips =
            [
                new MotionClip
                {
                    Id = "intro",
                    Name = "Intro",
                    DurationFrames = 60,
                    Tracks =
                    [
                        new MotionTrack
                        {
                            Id = "rootFade",
                            TargetId = "root",
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
                                }
                            ]
                        },
                        new MotionTrack
                        {
                            Id = "fpsFlash",
                            TargetId = "fps-value",
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Opacity,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 18, Value = MotionValue.FromNumber(0.25) },
                                        new MotionKeyframe { Frame = 30, Value = MotionValue.FromNumber(1) }
                                    ]
                                }
                            ]
                        },
                        new MotionTrack
                        {
                            Id = "versionText",
                            TargetId = "version",
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Text,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromText("BOOT"), Easing = MotionEasing.Step },
                                        new MotionKeyframe { Frame = 24, Value = MotionValue.FromText("READY"), Easing = MotionEasing.Step }
                                    ]
                                },
                                new MotionChannel
                                {
                                    Property = MotionProperty.Color,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromText("#7dd3fc") },
                                        new MotionKeyframe { Frame = 24, Value = MotionValue.FromText("#f8fafc") }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var result = GodotMotionExporter.Generate(document, motion, _options);

        result.Success.Should().BeTrue();
        result.Files.Should().ContainSingle();

        var file = result.Files[0];
        file.Path.Should().Be("DebugOverlayMotion.g.cs");
        file.Content.Should().Contain("public static class DebugOverlayMotion");
        file.Content.Should().Contain("AnimationPlayer");
        file.Content.Should().Contain("player.AddAnimation(\"intro\", BuildIntro());");
        file.Content.Should().Contain("new NodePath(\"..:modulate:a\")");
        file.Content.Should().Contain("new NodePath(\"../fps-row/fps-value:modulate:a\")");
        file.Content.Should().Contain("new NodePath(\"../header/version:text\")");
        file.Content.Should().Contain("Color.FromString(\"#7dd3fc\", Colors.White)");
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generate_UnsupportedPropertyOrMissingTarget_EmitsWarningsAndSkipsTrack()
    {
        var document = new HudDocument
        {
            Name = "DebugOverlay",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children = [new ComponentNode { Id = "label", Type = ComponentType.Label }]
            }
        };

        var motion = new MotionDocument
        {
            Name = "DebugOverlayMotion",
            Clips =
            [
                new MotionClip
                {
                    Id = "intro",
                    Name = "Intro",
                    DurationFrames = 30,
                    Tracks =
                    [
                        new MotionTrack
                        {
                            Id = "unsupported",
                            TargetId = "label",
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.PositionZ,
                                    Keyframes = [new MotionKeyframe { Frame = 0, Value = MotionValue.FromNumber(42) }]
                                }
                            ]
                        },
                        new MotionTrack
                        {
                            Id = "missing",
                            TargetId = "does-not-exist",
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Opacity,
                                    Keyframes = [new MotionKeyframe { Frame = 0, Value = MotionValue.FromNumber(1) }]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var result = GodotMotionExporter.Generate(document, motion, _options);

        result.Success.Should().BeTrue();
        result.Diagnostics.Should().HaveCount(2);
        result.Diagnostics.Should().Contain(d => d.Code == "BHG2001");
        result.Diagnostics.Should().Contain(d => d.Code == "BHG2002");
        result.Files[0].Content.Should().NotContain("does-not-exist");
        result.Files[0].Content.Should().NotContain("position:z");
    }
}
