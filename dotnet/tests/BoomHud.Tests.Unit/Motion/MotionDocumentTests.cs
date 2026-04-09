using BoomHud.Abstractions.Diagnostics;
using BoomHud.Abstractions.Motion;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Motion;

public sealed class MotionDocumentTests
{
    [Fact]
    public void LoadFromJson_MapsSchemaBackedMotionDocument()
    {
        const string json = """
        {
          "$schema": "https://boom-hud.dev/schemas/motion.schema.json",
          "version": "1.0",
          "name": "HudMotion",
          "framesPerSecond": 30,
          "defaultSequenceId": "introSequence",
          "clips": [
            {
              "id": "intro",
              "name": "Intro",
              "startFrame": 0,
              "durationFrames": 20,
              "tracks": [
                {
                  "id": "charPortrait",
                  "targetId": "char-portrait",
                  "targetKind": "element",
                  "channels": [
                    {
                      "property": "opacity",
                      "keyframes": [
                        {
                          "frame": 0,
                          "value": { "kind": "number", "number": 0 },
                          "easing": "linear"
                        },
                        {
                          "frame": 20,
                          "value": { "kind": "number", "number": 1 },
                          "easing": "easeOut"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
          ],
          "sequences": [
            {
              "id": "introSequence",
              "name": "Intro Sequence",
              "items": [
                {
                  "clipId": "intro",
                  "startFrame": 0,
                  "fillMode": "holdEnd"
                }
              ]
            }
          ]
        }
        """;

        var document = MotionDocument.LoadFromJson(json);

        document.Name.Should().Be("HudMotion");
        document.FramesPerSecond.Should().Be(30);
        document.Clips.Should().ContainSingle();
        document.Clips[0].Tracks[0].TargetId.Should().Be("char-portrait");
        document.Clips[0].Tracks[0].Channels[0].Property.Should().Be(MotionProperty.Opacity);
        document.Clips[0].Tracks[0].Channels[0].Keyframes[1].Value.Kind.Should().Be(MotionValueKind.Number);
        document.Clips[0].Tracks[0].Channels[0].Keyframes[1].Value.Number.Should().Be(1);
        document.DefaultSequenceId.Should().Be("introSequence");
        document.Sequences.Should().ContainSingle();
        document.Sequences[0].Items.Should().ContainSingle();
        document.Sequences[0].Items[0].FillMode.Should().Be(MotionSequenceFillMode.HoldEnd);
        document.LoadDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromJson_AllowsUnknownVersionWithWarning()
    {
        const string json = """
        {
          "version": "2.0",
          "name": "HudMotion",
          "clips": [
            {
              "id": "intro",
              "name": "Intro",
              "durationFrames": 1,
              "tracks": [
                {
                  "id": "root",
                  "targetId": "root",
                  "channels": [
                    {
                      "property": "visibility",
                      "keyframes": [
                        {
                          "frame": 0,
                          "value": { "kind": "boolean", "boolean": true }
                        }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var document = MotionDocument.LoadFromJson(json, "motion.ir.json");

        document.Version.Should().Be("2.0");
        document.LoadDiagnostics.Should().ContainSingle();
        document.LoadDiagnostics[0].Code.Should().Be(DiagnosticCodes.UnknownSchemaVersion);
        document.LoadDiagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ToJson_RoundTripsTaggedValues()
    {
        var document = new MotionDocument
        {
            Name = "HudMotion",
            FramesPerSecond = 60,
            DefaultSequenceId = "introSequence",
            Clips =
            [
                new MotionClip
                {
                    Id = "intro",
                    Name = "Intro",
                    StartFrame = 0,
                    DurationFrames = 12,
                    Tracks =
                    [
                        new MotionTrack
                        {
                            Id = "badge",
                            TargetId = "status-badge",
                            TargetKind = MotionTargetKind.Component,
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.PositionY,
                                    Keyframes =
                                    [
                                        new MotionKeyframe
                                        {
                                            Frame = 0,
                                            Value = MotionValue.FromNumber(-12),
                                            Easing = MotionEasing.EaseOut
                                        }
                                    ]
                                },
                                new MotionChannel
                                {
                                    Property = MotionProperty.Visibility,
                                    Keyframes =
                                    [
                                        new MotionKeyframe
                                        {
                                            Frame = 6,
                                            Value = MotionValue.FromBoolean(true),
                                            Easing = MotionEasing.Step
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ],
            Sequences =
            [
                new MotionSequence
                {
                    Id = "introSequence",
                    Name = "Intro Sequence",
                    Items =
                    [
                        new MotionSequenceItem
                        {
                            ClipId = "intro",
                            StartFrame = 0,
                            FillMode = MotionSequenceFillMode.HoldBoth
                        }
                    ]
                }
            ]
        };

        var json = document.ToJson();
        var roundTripped = MotionDocument.LoadFromJson(json);

        roundTripped.FramesPerSecond.Should().Be(60);
        roundTripped.DefaultSequenceId.Should().Be("introSequence");
        roundTripped.Clips[0].Tracks[0].TargetKind.Should().Be(MotionTargetKind.Component);
        roundTripped.Clips[0].Tracks[0].Channels[0].Keyframes[0].Value.Kind.Should().Be(MotionValueKind.Number);
        roundTripped.Clips[0].Tracks[0].Channels[0].Keyframes[0].Value.Number.Should().Be(-12);
        roundTripped.Clips[0].Tracks[0].Channels[1].Keyframes[0].Value.Kind.Should().Be(MotionValueKind.Boolean);
        roundTripped.Clips[0].Tracks[0].Channels[1].Keyframes[0].Value.Boolean.Should().BeTrue();
        roundTripped.Sequences.Should().ContainSingle();
        roundTripped.Sequences[0].Items[0].FillMode.Should().Be(MotionSequenceFillMode.HoldBoth);
    }

    [Fact]
    public void LoadFromJson_LegacyVectorValue_EmitsPortableContractWarning()
    {
        const string json = """
        {
          "version": "1.0",
          "name": "HudMotion",
          "clips": [
            {
              "id": "intro",
              "name": "Intro",
              "durationFrames": 12,
              "tracks": [
                {
                  "id": "badge",
                  "targetId": "status-badge",
                  "targetKind": "component",
                  "channels": [
                    {
                      "property": "positionY",
                      "keyframes": [
                        {
                          "frame": 0,
                          "value": { "kind": "vector", "vector": [0, -12] }
                        }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var document = MotionDocument.LoadFromJson(json, "motion.ir.json");

        document.Clips[0].Tracks[0].Channels[0].Keyframes[0].Value.Kind.Should().Be(MotionValueKind.Vector);
        document.LoadDiagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.NonPortableMotionValue);
    }

    [Fact]
    public void ToJson_LegacyVectorValue_ThrowsPortableContractError()
    {
        var document = new MotionDocument
        {
            Name = "HudMotion",
            Clips =
            [
                new MotionClip
                {
                    Id = "intro",
                    Name = "Intro",
                    DurationFrames = 12,
                    Tracks =
                    [
                        new MotionTrack
                        {
                            Id = "badge",
                            TargetId = "status-badge",
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.PositionY,
                                    Keyframes =
                                    [
                                        new MotionKeyframe
                                        {
                                            Frame = 0,
                                            Value = MotionValue.FromVector(0, -12)
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var action = () => document.ToJson();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-portable keyframe values*");
    }
}
