using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;
using BoomHud.Gen.Unity;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Motion;

public sealed class UnityMotionExporterTests
{
    private readonly GenerationOptions _options = new()
    {
        Namespace = "Generated.Hud",
        IncludeComments = true,
        UseNullableAnnotations = true
    };

    [Fact]
    public void Generate_ExportsRuntimeMotionHelpersForSupportedTracks()
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
                        Id = "version",
                        Type = ComponentType.Label
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
                            Id = "fadeRoot",
                            TargetId = "root",
                            TargetKind = MotionTargetKind.Root,
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
                        },
                        new MotionTrack
                        {
                            Id = "versionTrack",
                            TargetId = "version",
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Text,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromText("BOOT") },
                                        new MotionKeyframe { Frame = 24, Value = MotionValue.FromText("READY") }
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

        var result = UnityMotionExporter.Generate(document, motion, _options);

        result.Success.Should().BeTrue();
        result.Files.Should().HaveCount(2);
        var file = result.Files.First(f => f.Path == "DebugOverlayMotion.gen.cs");
        var hostFile = result.Files.First(f => f.Path == "DebugOverlayMotionHost.gen.cs");
        file.Content.Should().Contain("public static class DebugOverlayMotion");
        file.Content.Should().Contain("public const int FramesPerSecond = 30;");
        file.Content.Should().Contain("public static bool TryApplyAtFrame(DebugOverlayView view, string clipId, int frame)");
        file.Content.Should().Contain("throw new ArgumentNullException(nameof(view));");
        file.Content.Should().NotContain("ArgumentNullException.ThrowIfNull");
        file.Content.Should().Contain("ApplyOpacity(view.Root, EvaluateNumber(localFrame, s_IntroFadeRootOpacity, 1f));");
        file.Content.Should().Contain("ApplyText(view.Version, EvaluateString(localFrame, s_IntroVersionTrackText, string.Empty));");
        file.Content.Should().Contain("ApplyColor(view.Version, EvaluateString(localFrame, s_IntroVersionTrackColor, string.Empty));");
        file.Content.Should().Contain("element.style.color = new StyleColor");
        hostFile.Content.Should().Contain("public partial class DebugOverlayMotionHost : BoomHudUiToolkitMotionHost");
        hostFile.Content.Should().Contain("throw new ArgumentNullException(nameof(root));");
        hostFile.Content.Should().NotContain("ArgumentNullException.ThrowIfNull");
        hostFile.Content.Should().Contain("_view = new DebugOverlayView(generatedRoot);");
        hostFile.Content.Should().Contain("return _view != null && DebugOverlayMotion.TryApplyAtTime(_view, clipId, timeSeconds);");
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
                Children = [new ComponentNode { Id = "portrait", Type = ComponentType.Image }]
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
                            TargetId = "portrait",
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.SpriteFrame,
                                    Keyframes = [new MotionKeyframe { Frame = 0, Value = MotionValue.FromText("frame-a") }]
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

        var result = UnityMotionExporter.Generate(document, motion, _options);

        result.Success.Should().BeTrue();
        result.Diagnostics.Should().HaveCount(2);
        result.Diagnostics.Should().Contain(d => d.Code == "BHU2001");
        result.Diagnostics.Should().Contain(d => d.Code == "BHU2002");
        var motionFile = result.Files.First(f => f.Path == "DebugOverlayMotion.gen.cs");
        motionFile.Content.Should().NotContain("does-not-exist");
        motionFile.Content.Should().NotContain("SpriteFrame");
    }
}
