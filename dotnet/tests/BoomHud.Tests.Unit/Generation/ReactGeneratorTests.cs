using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Gen.React;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public sealed class ReactGeneratorTests
{
    private readonly ReactGenerator _generator = new();
    private readonly GenerationOptions _options = new() { EmitViewModelInterfaces = true };

    [Fact]
    public void Generate_MinimalDocument_ProducesTsxAndContract()
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

        var tsx = result.Files.First(file => file.Path == "StatusHudView.tsx").Content;
        tsx.Should().Contain("import React from 'react';");
        tsx.Should().Contain("export function StatusHudView(props: StatusHudViewModel): React.JSX.Element");
        tsx.Should().Contain("motionTargets?: Record<string");
        tsx.Should().Contain("className='boomhud-node boomhud-container'");
    }

    [Fact]
    public void Generate_WithBindings_EmitsPropsAndBoundExpressions()
    {
        var document = new HudDocument
        {
            Name = "Hud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "healthLabel",
                        Type = ComponentType.Label,
                        Bindings =
                        [
                            new BindingSpec { Property = "text", Path = "Player.HealthText", Format = "{0} HP" }
                        ]
                    },
                    new ComponentNode
                    {
                        Id = "healthBar",
                        Type = ComponentType.ProgressBar,
                        Bindings =
                        [
                            new BindingSpec { Property = "value", Path = "Player.HealthPercent" }
                        ],
                        Visible = BindableValue<bool>.Bind("Player.ShowHud")
                    }
                ]
            }
        };

        var result = _generator.Generate(document, _options);
        var tsx = result.Files.First(file => file.Path == "HudView.tsx").Content;

        tsx.Should().Contain("playerHealthText?: unknown;");
        tsx.Should().Contain("playerHealthPercent?: unknown;");
        tsx.Should().Contain("playerShowHud?: unknown;");
        tsx.Should().Contain("getMotionStyle(props.motionTargets, 'healthLabel')");
        tsx.Should().Contain("formatValue(props.playerHealthText, '{0} HP', '')");
        tsx.Should().Contain("width: clampPercent(props.playerHealthPercent)");
        tsx.Should().Contain("asBool(props.playerShowHud)");
    }

    [Fact]
    public void Generate_WithComponentReference_ImportsReferencedView()
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
        tsx.Should().Contain("<ActionButtonView motionTargets={props.motionTargets} />");
    }
}
