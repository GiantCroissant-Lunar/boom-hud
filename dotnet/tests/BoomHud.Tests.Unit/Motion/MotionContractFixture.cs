using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;

namespace BoomHud.Tests.Unit.Motion;

internal static class MotionContractFixture
{
    public static (HudDocument Document, MotionDocument Motion) CreatePartyHud()
    {
        var document = new HudDocument
        {
            Name = "PartyHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Gap = new Spacing(12)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "char1",
                        Type = ComponentType.Container,
                        ComponentRefId = "char-portrait",
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical,
                            Gap = new Spacing(8)
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "char1/name",
                                Type = ComponentType.Label,
                                Properties = new Dictionary<string, BindableValue<object?>>
                                {
                                    ["text"] = "Unknown"
                                }
                            },
                            new ComponentNode
                            {
                                Id = "char1/attackButton",
                                Type = ComponentType.Container,
                                ComponentRefId = "action-button",
                                Layout = new LayoutSpec
                                {
                                    Type = LayoutType.Horizontal,
                                    Gap = new Spacing(4)
                                },
                                Children =
                                [
                                    new ComponentNode
                                    {
                                        Id = "char1/attackButton/icon",
                                        Type = ComponentType.Icon,
                                        Properties = new Dictionary<string, BindableValue<object?>>
                                        {
                                            ["text"] = "shield"
                                        }
                                    },
                                    new ComponentNode
                                    {
                                        Id = "char1/attackButton/caption",
                                        Type = ComponentType.Label,
                                        Properties = new Dictionary<string, BindableValue<object?>>
                                        {
                                            ["text"] = "Attack"
                                        }
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

        var motion = new MotionDocument
        {
            Name = "PartyHudMotion",
            FramesPerSecond = 30,
            Clips =
            [
                new MotionClip
                {
                    Id = "intro",
                    Name = "Intro",
                    DurationFrames = 20,
                    Tracks =
                    [
                        new MotionTrack
                        {
                            Id = "rootTrack",
                            TargetId = "root",
                            TargetKind = MotionTargetKind.Root,
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Opacity,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromNumber(0.25) },
                                        new MotionKeyframe { Frame = 10, Value = MotionValue.FromNumber(1) }
                                    ]
                                }
                            ]
                        },
                        new MotionTrack
                        {
                            Id = "portraitTrack",
                            TargetId = "char1",
                            TargetKind = MotionTargetKind.Component,
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.PositionX,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromNumber(0) },
                                        new MotionKeyframe { Frame = 12, Value = MotionValue.FromNumber(18), Easing = MotionEasing.EaseOut }
                                    ]
                                }
                            ]
                        },
                        new MotionTrack
                        {
                            Id = "nameTrack",
                            TargetId = "char1/name",
                            TargetKind = MotionTargetKind.Element,
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Text,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromText("Ready"), Easing = MotionEasing.Step },
                                        new MotionKeyframe { Frame = 8, Value = MotionValue.FromText("Aelric"), Easing = MotionEasing.Step }
                                    ]
                                }
                            ]
                        },
                        new MotionTrack
                        {
                            Id = "attackButtonTrack",
                            TargetId = "char1/attackButton",
                            TargetKind = MotionTargetKind.Component,
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Opacity,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromNumber(0.5) },
                                        new MotionKeyframe { Frame = 12, Value = MotionValue.FromNumber(1), Easing = MotionEasing.EaseOut }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        return (document, motion);
    }
}
