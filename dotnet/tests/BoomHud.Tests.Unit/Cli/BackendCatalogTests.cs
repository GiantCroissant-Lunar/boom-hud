using BoomHud.Cli.Backends;
using BoomHud.Gen.Godot;
using BoomHud.Gen.React;
using BoomHud.Gen.TerminalGui;
using BoomHud.Gen.Remotion;
using BoomHud.Gen.Unity;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Cli;

public sealed class BackendCatalogTests
{
    [Fact]
    public void ResolveTargets_DefaultsToTerminalGui()
    {
        var targets = BackendCatalog.ResolveTargets(null);

        targets.Should().Equal(["TerminalGui"]);
    }

    [Fact]
    public void ResolveTargets_SupportsCommaSeparatedManifestTargets()
    {
        var targets = BackendCatalog.ResolveTargets("godot,terminalgui,react,remotion,unity");

        targets.Should().Equal(["Godot", "TerminalGui", "React", "Remotion", "Unity"]);
    }

    [Fact]
    public void ResolveTargets_AllExpandsToRegisteredBackends()
    {
        var targets = BackendCatalog.ResolveTargets("all");

        targets.Should().Equal(BackendCatalog.RegisteredBackendNames);
    }

    [Fact]
    public void CreateGenerator_ReturnsRegisteredBackendGenerator()
    {
        BackendCatalog.CreateGenerator("Godot").Should().BeOfType<GodotGenerator>();
        BackendCatalog.CreateGenerator("React").Should().BeOfType<ReactGenerator>();
        BackendCatalog.CreateGenerator("Remotion").Should().BeOfType<RemotionGenerator>();
        BackendCatalog.CreateGenerator("TerminalGui").Should().BeOfType<TerminalGuiGenerator>();
        BackendCatalog.CreateGenerator("Unity").Should().BeOfType<UnityGenerator>();
    }

    [Fact]
    public void CreateGenerator_UnknownBackend_Throws()
    {
        var act = () => BackendCatalog.CreateGenerator("Nope");

        act.Should().Throw<ArgumentException>()
            .WithMessage("Unknown backend: Nope");
    }
}
