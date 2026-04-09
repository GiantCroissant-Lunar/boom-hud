using BoomHud.Abstractions.IR;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.IR;

public sealed class MotionSpecTests
{
    [Fact]
    public void MotionDocument_CanDescribeClipTrackAndKeyframes()
    {
        var document = new MotionDocument
        {
            Name = "HudMotion",
            FramesPerSecond = 30,
            Clips =
            [
                new MotionClip
                {
                    Id = "intro",
                    Name = "Intro",
                    StartFrame = 0,
                    DurationFrames = 20,
                    Tracks =
                    [
                        new MotionTrack
                        {
                            Id = "charPortrait",
                            TargetId = "char-portrait",
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Opacity,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromNumber(0), Easing = MotionEasing.Linear },
                                        new MotionKeyframe { Frame = 20, Value = MotionValue.FromNumber(1), Easing = MotionEasing.EaseOut }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        document.Name.Should().Be("HudMotion");
        document.Clips.Should().ContainSingle();
        document.Clips[0].Tracks[0].TargetId.Should().Be("char-portrait");
        document.Clips[0].Tracks[0].Channels[0].Property.Should().Be(MotionProperty.Opacity);
        document.Clips[0].Tracks[0].Channels[0].Keyframes[1].Value.Number.Should().Be(1);
    }

    [Fact]
    public void MotionValue_CanRepresentVectorAndBoolean()
    {
        var vector = MotionValue.FromVector(10, 20, 30);
        var boolean = MotionValue.FromBoolean(true);

        vector.Vector.Should().Equal([10, 20, 30]);
        boolean.Boolean.Should().BeTrue();
    }
}
