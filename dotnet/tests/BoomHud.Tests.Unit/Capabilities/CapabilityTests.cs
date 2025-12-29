using BoomHud.Gen.TerminalGui;
using BoomHud.Gen.Avalonia;
using BoomHud.Abstractions.Capabilities;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Capabilities;

public class CapabilityTests
{
    [Fact]
    public void TerminalGui_SupportsBasicComponents()
    {
        var caps = TerminalGuiCapabilities.Instance;

        caps.SupportsComponent("label").Should().BeTrue();
        caps.SupportsComponent("button").Should().BeTrue();
        caps.SupportsComponent("progressBar").Should().BeTrue();
        caps.SupportsComponent("container").Should().BeTrue();
    }

    [Fact]
    public void TerminalGui_DoesNotSupportAdvancedFeatures()
    {
        var caps = TerminalGuiCapabilities.Instance;

        caps.GetCapabilityLevel(Abstractions.Capabilities.Capabilities.Animation)
            .Should().Be(CapabilityLevel.Unsupported);
        caps.GetCapabilityLevel(Abstractions.Capabilities.Capabilities.Images)
            .Should().Be(CapabilityLevel.Unsupported);
    }

    [Fact]
    public void TerminalGui_HasNativeCellLayout()
    {
        var caps = TerminalGuiCapabilities.Instance;

        caps.GetCapabilityLevel(Abstractions.Capabilities.Capabilities.CellLayout)
            .Should().Be(CapabilityLevel.Native);
    }

    [Fact]
    public void Avalonia_SupportsAllComponents()
    {
        var caps = AvaloniaCapabilities.Instance;

        caps.SupportsComponent("label").Should().BeTrue();
        caps.SupportsComponent("image").Should().BeTrue();
        caps.SupportsComponent("splitView").Should().BeTrue();
        caps.SupportsComponent("grid").Should().BeTrue();
    }

    [Fact]
    public void Avalonia_HasNativeDataBinding()
    {
        var caps = AvaloniaCapabilities.Instance;

        caps.GetCapabilityLevel(Abstractions.Capabilities.Capabilities.DataBinding)
            .Should().Be(CapabilityLevel.Native);
        caps.GetCapabilityLevel(Abstractions.Capabilities.Capabilities.CompiledBindings)
            .Should().Be(CapabilityLevel.Native);
    }

    [Fact]
    public void Avalonia_HasNativeAnimation()
    {
        var caps = AvaloniaCapabilities.Instance;

        caps.GetCapabilityLevel(Abstractions.Capabilities.Capabilities.Animation)
            .Should().Be(CapabilityLevel.Native);
    }
}
