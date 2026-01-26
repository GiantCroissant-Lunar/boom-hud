using BoomHud.Abstractions.Composition;
using BoomHud.Abstractions.Diagnostics;
using BoomHud.Abstractions.Snapshots;
using BoomHud.Abstractions.Tokens;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Schema;

/// <summary>
/// Contract tests for schema → DTO → domain mapping.
/// Verifies defaults are applied, required fields are present, and invariants hold.
/// </summary>
public class SchemaContractTests
{
    #region TokenRegistry Contract Tests

    [Fact]
    public void TokenRegistry_EmptyJson_ReturnsEmptyCollections()
    {
        // Arrange
        var json = "{}";

        // Act
        var registry = TokenRegistry.LoadFromJson(json);

        // Assert - defaults applied
        registry.Version.Should().Be("1.0");
        registry.Colors.Should().BeEmpty();
        registry.Spacing.Should().BeEmpty();
        registry.Typography.Should().BeEmpty();
        registry.Radii.Should().BeEmpty();
        registry.Shadows.Should().BeEmpty();
        registry.Opacity.Should().BeEmpty();
    }

    [Fact]
    public void TokenRegistry_AllTokenTypes_MapsCorrectly()
    {
        // Arrange - representative JSON with all token types
        var json = """
        {
          "version": "1.0",
          "colors": {
            "primary": { "value": "#007bff", "description": "Primary color", "deprecated": false },
            "danger": { "value": "#dc3545", "aliases": ["error", "red"] }
          },
          "spacing": {
            "xs": { "value": 4, "description": "Extra small" },
            "md": { "value": 16 }
          },
          "typography": {
            "body": { "fontFamily": "Inter", "fontSize": 14, "fontWeight": 400, "lineHeight": 1.5 },
            "heading": { "fontFamily": "Inter", "fontSize": 24, "fontWeight": "bold" }
          },
          "radii": {
            "sm": { "value": 4 },
            "full": { "value": 9999, "description": "Fully rounded" }
          },
          "shadows": {
            "sm": { "value": "0 1px 2px rgba(0,0,0,0.1)" },
            "lg": { "value": "0 4px 8px rgba(0,0,0,0.2)", "deprecated": true }
          },
          "opacity": {
            "disabled": { "value": 0.5 },
            "hover": { "value": 0.8 }
          }
        }
        """;

        // Act
        var registry = TokenRegistry.LoadFromJson(json);

        // Assert - all token types mapped
        registry.Version.Should().Be("1.0");
        registry.Colors.Should().HaveCount(2);
        registry.Spacing.Should().HaveCount(2);
        registry.Typography.Should().HaveCount(2);
        registry.Radii.Should().HaveCount(2);
        registry.Shadows.Should().HaveCount(2);
        registry.Opacity.Should().HaveCount(2);

        // Assert - domain type properties
        registry.Colors["primary"].Value.Should().Be("#007bff");
        registry.Colors["primary"].Description.Should().Be("Primary color");
        registry.Colors["danger"].Aliases.Should().BeEquivalentTo(["error", "red"]);

        registry.Spacing["md"].Value.Should().Be(16);

        registry.Typography["body"].FontFamily.Should().Be("Inter");
        registry.Typography["body"].FontSize.Should().Be(14);
        registry.Typography["heading"].FontWeight.Should().Be("bold");

        registry.Shadows["lg"].Deprecated.Should().BeTrue();

        registry.Opacity["disabled"].Value.Should().Be(0.5);
    }

    [Fact]
    public void TokenRegistry_TryResolve_WorksForAllCategories()
    {
        // Arrange
        var json = """
        {
          "colors": { "bg": { "value": "#000" } },
          "spacing": { "sm": { "value": 4 } },
          "typography": { "body": { "fontSize": 14 } },
          "radii": { "md": { "value": 8 } },
          "shadows": { "sm": { "value": "0 1px 2px black" } },
          "opacity": { "half": { "value": 0.5 } }
        }
        """;
        var registry = TokenRegistry.LoadFromJson(json);

        // Act & Assert - all categories resolve
        registry.TryResolve("colors.bg").Should().NotBeNull();
        registry.TryResolve("spacing.sm").Should().NotBeNull();
        registry.TryResolve("typography.body").Should().NotBeNull();
        registry.TryResolve("radii.md").Should().NotBeNull();
        registry.TryResolve("shadows.sm").Should().NotBeNull();
        registry.TryResolve("opacity.half").Should().NotBeNull();

        // Unknown tokens
        registry.TryResolve("colors.unknown").Should().BeNull();
        registry.TryResolve("invalid.token").Should().BeNull();
    }

    [Fact]
    public void TokenRegistry_UnknownVersion_EmitsWarning()
    {
        // Arrange
        var json = """{ "version": "2.0", "colors": { "bg": { "value": "#000" } } }""";

        // Act
        var registry = TokenRegistry.LoadFromJson(json, "test.json");

        // Assert - still loads, but emits warning
        registry.Version.Should().Be("2.0");
        registry.Colors.Should().HaveCount(1);
        registry.LoadDiagnostics.Should().ContainSingle();
        registry.LoadDiagnostics[0].Code.Should().Be(DiagnosticCodes.UnknownSchemaVersion);
        registry.LoadDiagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void TokenRegistry_KnownVersion_NoDiagnostics()
    {
        // Arrange
        var json = """{ "version": "1.0", "colors": {} }""";

        // Act
        var registry = TokenRegistry.LoadFromJson(json);

        // Assert
        registry.LoadDiagnostics.Should().BeEmpty();
    }

    #endregion

    #region ComposeManifest Contract Tests

    [Fact]
    public void ComposeManifest_EmptyJson_ThrowsOrDefaultsSources()
    {
        // Arrange - sources is required by schema but LoadFromJson should handle gracefully
        var json = "{}";

        // Act
        var manifest = ComposeManifest.LoadFromJson(json);

        // Assert - defaults applied
        manifest.Version.Should().Be("1.0");
        manifest.Sources.Should().BeEmpty();
        manifest.Root.Should().BeNull();
        manifest.Tokens.Should().BeNull();
        manifest.Targets.Should().BeNull();
        manifest.Output.Should().BeNull();
        manifest.Namespace.Should().BeNull();
    }

    [Fact]
    public void ComposeManifest_FullManifest_MapsAllFields()
    {
        // Arrange
        var json = """
        {
          "$schema": "../../schemas/compose.schema.json",
          "version": "1.0",
          "root": "GameHud",
          "sources": [
            "sources/figma/main.figma.json",
            "sources/pencil/debug.pen"
          ],
          "tokens": "tokens.ir.json",
          "targets": ["godot", "avalonia"],
          "output": "_generated",
          "namespace": "MyGame.UI"
        }
        """;

        // Act
        var manifest = ComposeManifest.LoadFromJson(json);

        // Assert
        manifest.Version.Should().Be("1.0");
        manifest.Root.Should().Be("GameHud");
        manifest.Sources.Should().HaveCount(2);
        manifest.Sources.Should().Contain("sources/figma/main.figma.json");
        manifest.Tokens.Should().Be("tokens.ir.json");
        manifest.Targets.Should().BeEquivalentTo(["godot", "avalonia"]);
        manifest.Output.Should().Be("_generated");
        manifest.Namespace.Should().Be("MyGame.UI");
    }

    [Fact]
    public void ComposeManifest_SourcesAreImmutable()
    {
        // Arrange
        var json = """{ "sources": ["a.pen", "b.pen"] }""";
        var manifest = ComposeManifest.LoadFromJson(json);

        // Assert - IReadOnlyList should not be modifiable
        manifest.Sources.Should().BeAssignableTo<IReadOnlyList<string>>();
    }

    [Fact]
    public void ComposeManifest_UnknownVersion_EmitsWarning()
    {
        // Arrange
        var json = """{ "version": "99.0", "sources": ["a.pen"] }""";

        // Act
        var manifest = ComposeManifest.LoadFromJson(json, "boom-hud.compose.json");

        // Assert
        manifest.Version.Should().Be("99.0");
        manifest.LoadDiagnostics.Should().ContainSingle();
        manifest.LoadDiagnostics[0].Code.Should().Be(DiagnosticCodes.UnknownSchemaVersion);
    }

    [Fact]
    public void ComposeManifest_KnownVersion_NoDiagnostics()
    {
        // Arrange
        var json = """{ "version": "1.0", "sources": [] }""";

        // Act
        var manifest = ComposeManifest.LoadFromJson(json);

        // Assert
        manifest.LoadDiagnostics.Should().BeEmpty();
    }

    #endregion

    #region SnapshotStatesManifest Contract Tests

    [Fact]
    public void StatesManifest_EmptyStates_ReturnsDefaults()
    {
        // Arrange
        var json = """{ "states": [] }""";

        // Act
        var manifest = SnapshotStatesManifest.LoadFromJson(json);

        // Assert - viewport defaults
        manifest.Version.Should().Be("1.0");
        manifest.Viewport.Width.Should().Be(1280);
        manifest.Viewport.Height.Should().Be(720);
        manifest.Viewport.Scale.Should().Be(1.0);

        // Assert - defaults defaults
        manifest.Defaults.WaitFrames.Should().Be(2);
        manifest.Defaults.Background.Should().BeNull();

        manifest.States.Should().BeEmpty();
    }

    [Fact]
    public void StatesManifest_FullManifest_MapsAllFields()
    {
        // Arrange
        var json = """
        {
          "$schema": "../../schemas/states.schema.json",
          "version": "1.0",
          "viewport": {
            "width": 1920,
            "height": 1080,
            "scale": 2.0
          },
          "defaults": {
            "waitFrames": 5,
            "background": "#1a1a2e"
          },
          "states": [
            {
              "name": "Idle",
              "description": "Default idle state",
              "waitFrames": 3
            },
            {
              "name": "Active",
              "vm": { "IsActive": true, "Count": 42 }
            }
          ]
        }
        """;

        // Act
        var manifest = SnapshotStatesManifest.LoadFromJson(json);

        // Assert - viewport
        manifest.Viewport.Width.Should().Be(1920);
        manifest.Viewport.Height.Should().Be(1080);
        manifest.Viewport.Scale.Should().Be(2.0);

        // Assert - defaults
        manifest.Defaults.WaitFrames.Should().Be(5);
        manifest.Defaults.Background.Should().Be("#1a1a2e");

        // Assert - states
        manifest.States.Should().HaveCount(2);

        manifest.States[0].Name.Should().Be("Idle");
        manifest.States[0].Description.Should().Be("Default idle state");
        manifest.States[0].WaitFrames.Should().Be(3);
        manifest.States[0].Vm.Should().BeNull();

        manifest.States[1].Name.Should().Be("Active");
        manifest.States[1].Vm.Should().NotBeNull();
    }

    [Fact]
    public void StatesManifest_VmJsonElement_PreservesStructure()
    {
        // Arrange
        var json = """
        {
          "states": [
            {
              "name": "Complex",
              "vm": {
                "Debug": {
                  "Enabled": true,
                  "Stats": {
                    "Fps": 60,
                    "Memory": 1024
                  }
                }
              }
            }
          ]
        }
        """;

        // Act
        var manifest = SnapshotStatesManifest.LoadFromJson(json);

        // Assert - JsonElement preserved for runtime binding
        var vm = manifest.States[0].Vm!.Value;
        vm.GetProperty("Debug").GetProperty("Enabled").GetBoolean().Should().BeTrue();
        vm.GetProperty("Debug").GetProperty("Stats").GetProperty("Fps").GetInt32().Should().Be(60);
    }

    [Fact]
    public void StatesManifest_StatesAreImmutable()
    {
        // Arrange
        var json = """{ "states": [{ "name": "A" }, { "name": "B" }] }""";
        var manifest = SnapshotStatesManifest.LoadFromJson(json);

        // Assert - IReadOnlyList should not be modifiable
        manifest.States.Should().BeAssignableTo<IReadOnlyList<SnapshotState>>();
    }

    [Fact]
    public void StatesManifest_UnknownVersion_EmitsWarning()
    {
        // Arrange
        var json = """{ "version": "3.0", "states": [{ "name": "A" }] }""";

        // Act
        var manifest = SnapshotStatesManifest.LoadFromJson(json, "debug.states.json");

        // Assert
        manifest.Version.Should().Be("3.0");
        manifest.LoadDiagnostics.Should().ContainSingle();
        manifest.LoadDiagnostics[0].Code.Should().Be(DiagnosticCodes.UnknownSchemaVersion);
        manifest.LoadDiagnostics[0].SourceFile.Should().Be("debug.states.json");
    }

    [Fact]
    public void StatesManifest_KnownVersion_NoDiagnostics()
    {
        // Arrange
        var json = """{ "version": "1.0", "states": [] }""";

        // Act
        var manifest = SnapshotStatesManifest.LoadFromJson(json);

        // Assert
        manifest.LoadDiagnostics.Should().BeEmpty();
    }

    #endregion
}
