using BoomHud.Godot.Runtime;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Runtime;

public sealed class GodotRuntimeSurfaceCatalogTests
{
    [Theory]
    [InlineData("label", null, "Label")]
    [InlineData("badge", null, "Label")]
    [InlineData("button", null, "Button")]
    [InlineData("panel", null, "PanelContainer")]
    [InlineData("progressBar", null, "ProgressBar")]
    [InlineData("list", null, "ItemList")]
    [InlineData("nodeGraph", null, "GraphEdit")]
    [InlineData("container", "vertical", "VBoxContainer")]
    [InlineData("container", "horizontal", "HBoxContainer")]
    [InlineData("container", "grid", "GridContainer")]
    public void TryGetControlType_KnownCatalogType_ReturnsGodotControl(
        string runtimeType,
        string? layoutType,
        string expectedControlType)
    {
        var found = GodotRuntimeSurfaceCatalog.TryGetControlType(runtimeType, layoutType, out var controlType);

        found.Should().BeTrue();
        controlType.Should().Be(expectedControlType);
    }

    [Fact]
    public void TryGetControlType_UnknownCatalogType_ReturnsFalse()
    {
        var found = GodotRuntimeSurfaceCatalog.TryGetControlType("godotNode3D", layoutType: null, out var controlType);

        found.Should().BeFalse();
        controlType.Should().BeEmpty();
    }

    [Fact]
    public void TryGetControlType_AllBasicCatalogTypes_AreMapped()
    {
        foreach (var componentType in GodotRuntimeSurfaceCatalog.Catalog.Components.Keys)
        {
            var found = GodotRuntimeSurfaceCatalog.TryGetControlType(componentType, layoutType: null, out var controlType);

            found.Should().BeTrue();
            controlType.Should().NotBeNullOrWhiteSpace();
        }
    }
}
