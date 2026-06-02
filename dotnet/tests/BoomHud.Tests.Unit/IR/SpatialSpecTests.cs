using System.Text.Json;
using System.Text.Json.Serialization;
using BoomHud.Abstractions.Capabilities;
using BoomHud.Abstractions.IR;
using BoomHud.Gen.Godot;
using BoomHud.Gen.TerminalGui;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.IR;

public class SpatialSpecTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public void JsonRoundTrip_ComponentWithSpatial_ParsesIntoPopulatedSpatialSpec()
    {
        var json = """
        {
            "name": "TunnelTimeline",
            "root": {
                "type": "container",
                "spatial": {
                    "shape": "cylinder",
                    "facing": "inward",
                    "angleFrom": { "property": "angle", "path": "item.trackIndex", "mode": "oneWay" },
                    "depthFrom": { "property": "depth", "path": "item.tick", "mode": "oneWay" },
                    "radius": 2.5,
                    "angularSpacingDeg": 15.0,
                    "depthScale": 0.5,
                    "curvature": 0.0
                },
                "children": [
                    { "type": "label", "id": "clipLabel" }
                ]
            }
        }
        """;

        var doc = JsonSerializer.Deserialize<HudDocument>(json, JsonOptions);

        doc.Should().NotBeNull();
        doc!.Name.Should().Be("TunnelTimeline");
        doc.Root.Spatial.Should().NotBeNull();
        doc.Root.Spatial!.Shape.Should().Be(SpatialShape.Cylinder);
        doc.Root.Spatial.Facing.Should().Be(SpatialFacing.Inward);
        doc.Root.Spatial.AngleFrom.Should().NotBeNull();
        doc.Root.Spatial.AngleFrom!.Path.Should().Be("item.trackIndex");
        doc.Root.Spatial.DepthFrom.Should().NotBeNull();
        doc.Root.Spatial.DepthFrom!.Path.Should().Be("item.tick");
        doc.Root.Spatial.RadiusFrom.Should().BeNull();
        doc.Root.Spatial.LateralFrom.Should().BeNull();
        doc.Root.Spatial.Radius.Should().Be(2.5);
        doc.Root.Spatial.AngularSpacingDeg.Should().Be(15.0);
        doc.Root.Spatial.DepthScale.Should().Be(0.5);
        doc.Root.Spatial.Curvature.Should().Be(0.0);
    }

    [Fact]
    public void JsonRoundTrip_ComponentWithoutSpatial_SpatialIsNull()
    {
        var json = """
        {
            "name": "SimplePanel",
            "root": {
                "type": "container",
                "children": [
                    { "type": "label", "id": "title" }
                ]
            }
        }
        """;

        var doc = JsonSerializer.Deserialize<HudDocument>(json, JsonOptions);

        doc.Should().NotBeNull();
        doc!.Root.Spatial.Should().BeNull();
    }

    [Fact]
    public void JsonRoundTrip_SpatialWithDefaults_OmitsFacingDefaultsToCamera()
    {
        var json = """
        {
            "name": "DefaultsTest",
            "root": {
                "type": "container",
                "spatial": {
                    "shape": "radial"
                }
            }
        }
        """;

        var doc = JsonSerializer.Deserialize<HudDocument>(json, JsonOptions);

        doc.Should().NotBeNull();
        doc!.Root.Spatial.Should().NotBeNull();
        doc.Root.Spatial!.Shape.Should().Be(SpatialShape.Radial);
        doc.Root.Spatial.Facing.Should().Be(SpatialFacing.Camera);
        doc.Root.Spatial.Radius.Should().Be(1.0);
        doc.Root.Spatial.AngularSpacingDeg.Should().Be(30.0);
        doc.Root.Spatial.DepthScale.Should().Be(1.0);
        doc.Root.Spatial.Curvature.Should().Be(0.0);
    }

    [Fact]
    public void Capability_Godot_Spatial3D_IsNative()
    {
        var caps = GodotCapabilities.Instance;
        caps.GetCapabilityLevel(Capabilities.Spatial3D).Should().Be(CapabilityLevel.Native);
    }

    [Fact]
    public void Capability_TerminalGui_Spatial3D_IsUnsupported()
    {
        var caps = TerminalGuiCapabilities.Instance;
        caps.GetCapabilityLevel(Capabilities.Spatial3D).Should().Be(CapabilityLevel.Unsupported);
    }

    [Fact]
    public void Capability_UnknownFeature_DefaultsToUnsupported()
    {
        var caps = TerminalGuiCapabilities.Instance;
        caps.GetCapabilityLevel("nonExistentFeature").Should().Be(CapabilityLevel.Unsupported);
    }
}
