using BoomHud.Abstractions.Generation;
using BoomHud.Gen.UGui;
using BoomHud.Gen.React;
using BoomHud.Gen.Unity;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Motion;

public sealed class GeneratorMotionContractTests
{
    private readonly ReactGenerator _reactGenerator = new();
    private readonly UGuiGenerator _uguiGenerator = new();
    private readonly UnityGenerator _unityGenerator = new();
    private readonly GenerationOptions _reactOptions = new()
    {
        EmitViewModelInterfaces = true,
        IncludeComments = true,
        UseNullableAnnotations = true
    };

    private readonly GenerationOptions _unityOptions = new()
    {
        Namespace = "Generated.Hud",
        IncludeComments = true,
        UseNullableAnnotations = true
    };

    [Fact]
    public void SharedFixture_KeepsReactAndUnityMotionTargetsAligned()
    {
        var (document, motion) = MotionContractFixture.CreatePartyHud();

        var reactResult = _reactGenerator.Generate(document, _reactOptions);
        var uguiResult = _uguiGenerator.Generate(document, _unityOptions with { Namespace = "Generated.Hud.UGui", Motion = motion });
        var unityResult = _unityGenerator.Generate(document, _unityOptions with { Motion = motion });

        reactResult.Success.Should().BeTrue();
        uguiResult.Success.Should().BeTrue();
        unityResult.Success.Should().BeTrue();

        var partyHudTsx = reactResult.Files.First(file => file.Path == "PartyHudView.tsx").Content;

        partyHudTsx.Should().NotContain("import { CharPortraitView } from './CharPortraitView';");
        partyHudTsx.Should().Contain("data-boomhud-id={resolveMotionId(props.motionScope, 'char1')}");
        partyHudTsx.Should().Contain("data-boomhud-id={resolveMotionId(props.motionScope, 'char1/name')}");
        partyHudTsx.Should().Contain("data-boomhud-id={resolveMotionId(props.motionScope, 'char1/attackButton')}");
        partyHudTsx.Should().Contain("data-boomhud-id={resolveMotionId(props.motionScope, 'char1/attackButton/caption')}");

        uguiResult.Files.Should().Contain(file => file.Path == "PartyHudMotion.gen.cs");
        uguiResult.Files.Should().Contain(file => file.Path == "PartyHudMotionHost.gen.cs");

        var uguiMotion = uguiResult.Files.First(file => file.Path == "PartyHudMotion.gen.cs").Content;
        uguiMotion.Should().Contain("ApplyOpacity(view.Root, EvaluateNumber(localFrame, s_IntroRootTrackOpacity, 1f));");
        uguiMotion.Should().Contain("ApplyTranslate(view.Char1, EvaluateNumber(localFrame, s_IntroPortraitTrackPositionX, 0f), 0f);");
        uguiMotion.Should().Contain("ApplyText(view.Char1Name, EvaluateString(localFrame, s_IntroNameTrackText, string.Empty));");
        uguiMotion.Should().Contain("ApplyOpacity(view.Char1AttackButton, EvaluateNumber(localFrame, s_IntroAttackButtonTrackOpacity, 1f));");

        unityResult.Files.Should().Contain(file => file.Path == "PartyHudView.uxml");
        unityResult.Files.Should().Contain(file => file.Path == "PartyHudMotion.gen.cs");
        unityResult.Files.Should().Contain(file => file.Path == "PartyHudMotionHost.gen.cs");

        var unityMotion = unityResult.Files.First(file => file.Path == "PartyHudMotion.gen.cs").Content;
        unityMotion.Should().Contain("ApplyOpacity(view.Root, EvaluateNumber(localFrame, s_IntroRootTrackOpacity, 1f));");
        unityMotion.Should().Contain("ApplyTranslate(view.Char1, EvaluateNumber(localFrame, s_IntroPortraitTrackPositionX, 0f), 0f);");
        unityMotion.Should().Contain("ApplyText(view.Char1Name, EvaluateString(localFrame, s_IntroNameTrackText, string.Empty));");
        unityMotion.Should().Contain("ApplyOpacity(view.Char1AttackButton, EvaluateNumber(localFrame, s_IntroAttackButtonTrackOpacity, 1f));");
    }
}
