using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;
using BoomHud.Gen.Remotion;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public sealed class RemotionGeneratorTests
{
    private readonly RemotionGenerator _generator = new();
    private readonly GenerationOptions _options = new() { EmitViewModelInterfaces = true };

    [Fact]
    public void Generate_MinimalDocument_ProducesReactArtifacts()
    {
        var document = new HudDocument
        {
            Name = "StatusHud",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(document, _options);

        result.Success.Should().BeTrue();
        result.Files.Should().Contain(file => file.Path == "StatusHudView.tsx");
        result.Files.Should().Contain(file => file.Path == "IStatusHudViewModel.g.ts");
        result.Files.Should().NotContain(file => file.Path == "StatusHudMotionComposition.tsx");
    }

    [Fact]
    public void Generate_WithComponentReference_ImportsComponentViews()
    {
        var actionButton = new HudComponentDefinition
        {
            Id = "action-button",
            Name = "ActionButton",
            Root = new ComponentNode { Id = "actionButtonRoot", Type = ComponentType.Button }
        };

        var document = new HudDocument
        {
            Name = "Hud",
            Components = new Dictionary<string, HudComponentDefinition> { ["action-button"] = actionButton },
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode { Id = "primaryAction", Type = ComponentType.Container, ComponentRefId = "action-button" }
                ]
            }
        };

        var result = _generator.Generate(document, _options);
        var tsx = result.Files.First(file => file.Path == "HudView.tsx").Content;

        tsx.Should().Contain("import { ActionButtonView } from './ActionButtonView';");
    }

    [Fact]
    public void Generate_WithMotion_EmitsMotionComposition()
    {
        var options = _options with
        {
            Motion = new MotionDocument
            {
                Name = "StatusHudMotion",
                FramesPerSecond = 30,
                Clips =
                [
                    new MotionClip
                    {
                        Id = "intro",
                        Name = "Intro",
                        DurationFrames = 24,
                        Tracks =
                        [
                            new MotionTrack
                            {
                                Id = "statusFade",
                                TargetId = "statusLabel",
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
                            }
                        ]
                    }
                ]
            }
        };

        var document = new HudDocument
        {
            Name = "StatusHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "statusLabel",
                        Type = ComponentType.Label
                    }
                ]
            }
        };

        var result = _generator.Generate(document, options);

        result.Files.Should().Contain(file => file.Path == "StatusHudMotionComposition.tsx");
        var composition = result.Files.First(file => file.Path == "StatusHudMotionComposition.tsx").Content;

        composition.Should().Contain("from './motion';");
        composition.Should().Contain("import { StatusHudView, type StatusHudViewModel } from './StatusHudView';");
        composition.Should().Contain("const motionDocument = parseMotionDocument(`");
        composition.Should().Contain("const StatusHudMotionSequenceDefault: MotionSequence = [");
        composition.Should().Contain("export const StatusHudMotionSequences = [");
        composition.Should().Contain("export const StatusHudDefaultMotionSequence = StatusHudMotionSequenceDefault;");
        composition.Should().Contain("export const StatusHudDefaultMotionDurationInFrames = StatusHudMotionSequenceDurationInFramesDefault;");
        composition.Should().Contain("export const StatusHudMotionFramesPerSecond = motionDocument.framesPerSecond;");
        composition.Should().Contain("export const StatusHudFramesPerSecond = motionDocument.framesPerSecond;");
        composition.Should().Contain("export const StatusHudMotionSequence = StatusHudDefaultMotionSequence;");
        composition.Should().Contain("export const StatusHudMotionDurationInFrames = StatusHudDefaultMotionDurationInFrames;");
        composition.Should().Contain("from={0}");
        composition.Should().Contain("durationInFrames={StatusHudDefaultMotionDurationInFrames}");
        composition.Should().Contain("export const StatusHudMotionComposition = (viewModel: StatusHudViewModel): React.JSX.Element =>");
        composition.Should().Contain("<MotionScene");
    }
}
