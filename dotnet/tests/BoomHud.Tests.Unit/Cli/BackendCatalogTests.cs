using BoomHud.Cli.Backends;
using BoomHud.Gen.Godot;
using BoomHud.Gen.Pencil;
using BoomHud.Gen.React;
using BoomHud.Gen.Remotion;
using BoomHud.Gen.TerminalGui;
using BoomHud.Gen.UGui;
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
        var targets = BackendCatalog.ResolveTargets("godot,terminalgui,react,pencil,remotion,unity,ugui");

        targets.Should().Equal(["Godot", "TerminalGui", "React", "Pencil", "Remotion", "Unity", "UGui"]);
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
        BackendCatalog.CreateGenerator("Pencil").Should().BeOfType<PencilGenerator>();
        BackendCatalog.CreateGenerator("React").Should().BeOfType<ReactGenerator>();
        BackendCatalog.CreateGenerator("Remotion").Should().BeOfType<RemotionGenerator>();
        BackendCatalog.CreateGenerator("TerminalGui").Should().BeOfType<TerminalGuiGenerator>();
        BackendCatalog.CreateGenerator("Unity").Should().BeOfType<UnityGenerator>();
        BackendCatalog.CreateGenerator("UGui").Should().BeOfType<UGuiGenerator>();
    }

    [Fact]
    public void CreateGenerator_UnknownBackend_Throws()
    {
        var act = () => BackendCatalog.CreateGenerator("Nope");

        act.Should().Throw<ArgumentException>()
            .WithMessage("Unknown backend: Nope");
    }
}
