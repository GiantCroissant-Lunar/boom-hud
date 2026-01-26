using System.Text.Json;
using BoomHud.Abstractions.Snapshots;
using Xunit;

namespace BoomHud.Tests.Unit.Snapshots;

public class SnapshotStatesManifestTests
{
    [Fact]
    public void LoadFromJson_ValidManifest_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
              "version": "1.0",
              "viewport": {
                "width": 1920,
                "height": 1080,
                "scale": 2.0
              },
              "states": [
                {
                  "name": "Default",
                  "description": "Initial state"
                },
                {
                  "name": "Active",
                  "vm": {
                    "IsActive": true
                  }
                }
              ]
            }
            """;

        // Act
        var manifest = SnapshotStatesManifest.LoadFromJson(json);

        // Assert
        Assert.Equal("1.0", manifest.Version);
        Assert.Equal(1920, manifest.Viewport.Width);
        Assert.Equal(1080, manifest.Viewport.Height);
        Assert.Equal(2.0, manifest.Viewport.Scale);
        Assert.Equal(2, manifest.States.Count);

        Assert.Equal("Default", manifest.States[0].Name);
        Assert.Equal("Initial state", manifest.States[0].Description);
        Assert.Null(manifest.States[0].Vm);

        Assert.Equal("Active", manifest.States[1].Name);
        Assert.NotNull(manifest.States[1].Vm);
    }

    [Fact]
    public void LoadFromJson_MinimalManifest_UsesDefaults()
    {
        // Arrange
        var json = """
            {
              "states": [
                { "name": "Default" }
              ]
            }
            """;

        // Act
        var manifest = SnapshotStatesManifest.LoadFromJson(json);

        // Assert
        Assert.Equal("1.0", manifest.Version);
        Assert.Equal(1280, manifest.Viewport.Width);
        Assert.Equal(720, manifest.Viewport.Height);
        Assert.Equal(1.0, manifest.Viewport.Scale);
        Assert.Single(manifest.States);
    }

    [Fact]
    public void LoadFromJson_WithNestedVm_PreservesJsonElement()
    {
        // Arrange
        var json = """
            {
              "states": [
                {
                  "name": "DebugOn",
                  "vm": {
                    "Debug": {
                      "Enabled": true,
                      "Fps": 60,
                      "Message": "Test"
                    }
                  }
                }
              ]
            }
            """;

        // Act
        var manifest = SnapshotStatesManifest.LoadFromJson(json);

        // Assert
        Assert.Single(manifest.States);
        var state = manifest.States[0];
        Assert.NotNull(state.Vm);

        // Verify VM structure is preserved
        var vmElement = state.Vm!.Value;
        Assert.True(vmElement.TryGetProperty("Debug", out var debugElement));
        Assert.True(debugElement.TryGetProperty("Enabled", out var enabledElement));
        Assert.True(enabledElement.GetBoolean());
        Assert.True(debugElement.TryGetProperty("Fps", out var fpsElement));
        Assert.Equal(60, fpsElement.GetInt32());
    }

    [Fact]
    public void LoadFromJson_WithComments_ParsesCorrectly()
    {
        // Arrange - JSON with comments (allowed by our options)
        var json = """
            {
              // Version comment
              "version": "1.0",
              "states": [
                { "name": "Default" } // Trailing comma allowed
              ]
            }
            """;

        // Act
        var manifest = SnapshotStatesManifest.LoadFromJson(json);

        // Assert
        Assert.Equal("1.0", manifest.Version);
        Assert.Single(manifest.States);
    }

    [Fact]
    public void ViewportConfig_DefaultValues_AreReasonable()
    {
        // Arrange
        var viewport = new ViewportConfig();

        // Assert - 720p defaults
        Assert.Equal(1280, viewport.Width);
        Assert.Equal(720, viewport.Height);
        Assert.Equal(1.0, viewport.Scale);
    }

    [Fact]
    public void SnapshotOutputManifest_ToJson_ProducesValidJson()
    {
        // Arrange
        var manifest = new SnapshotOutputManifest
        {
            Target = "godot",
            Viewport = new ViewportConfig { Width = 1920, Height = 1080 },
            ToolVersion = "1.0.0",
            InputHashes = new Dictionary<string, string>
            {
                ["states"] = "abc123"
            },
            Snapshots = new[]
            {
                new SnapshotFileInfo { State = "Default", Path = "000_Default.png", Sha256 = "def456" }
            }
        };

        // Act
        var json = manifest.ToJson();

        // Assert
        Assert.Contains("\"target\": \"godot\"", json);
        Assert.Contains("\"toolVersion\": \"1.0.0\"", json);
        Assert.Contains("\"width\": 1920", json);
        Assert.Contains("\"abc123\"", json);
        Assert.Contains("\"000_Default.png\"", json);

        // Verify it's valid JSON
        var parsed = JsonDocument.Parse(json);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void SnapshotDefaults_DefaultValues_AreReasonable()
    {
        // Arrange
        var defaults = new SnapshotDefaults();

        // Assert - reasonable defaults for determinism
        Assert.Equal(2, defaults.WaitFrames);
        Assert.Null(defaults.Background); // null means use renderer default
    }

    [Fact]
    public void LoadFromJson_WithDefaults_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
              "version": "1.0",
              "viewport": { "width": 1920, "height": 1080 },
              "defaults": {
                "waitFrames": 5,
                "background": "#ff0000"
              },
              "states": [
                { "name": "Default" }
              ]
            }
            """;

        // Act
        var manifest = SnapshotStatesManifest.LoadFromJson(json);

        // Assert
        Assert.NotNull(manifest.Defaults);
        Assert.Equal(5, manifest.Defaults.WaitFrames);
        Assert.Equal("#ff0000", manifest.Defaults.Background);
    }

    [Fact]
    public void LoadFromJson_WithoutDefaults_UsesDefaultValues()
    {
        // Arrange
        var json = """
            {
              "states": [
                { "name": "Default" }
              ]
            }
            """;

        // Act
        var manifest = SnapshotStatesManifest.LoadFromJson(json);

        // Assert - uses default SnapshotDefaults values
        Assert.NotNull(manifest.Defaults);
        Assert.Equal(2, manifest.Defaults.WaitFrames);
        Assert.Null(manifest.Defaults.Background); // null is valid default
    }

    [Fact]
    public void LoadFromJson_WithPerStateWaitFrames_OverridesDefault()
    {
        // Arrange
        var json = """
            {
              "defaults": { "waitFrames": 2 },
              "states": [
                { "name": "Default" },
                { "name": "Complex", "waitFrames": 10 }
              ]
            }
            """;

        // Act
        var manifest = SnapshotStatesManifest.LoadFromJson(json);

        // Assert
        Assert.Equal(2, manifest.States.Count);

        // First state uses defaults (no override)
        Assert.Null(manifest.States[0].WaitFrames);

        // Second state has explicit override
        Assert.Equal(10, manifest.States[1].WaitFrames);
    }

    [Fact]
    public void SnapshotState_WaitFrames_NullByDefault()
    {
        // Arrange
        var json = """
            {
              "states": [
                { "name": "Test" }
              ]
            }
            """;

        // Act
        var manifest = SnapshotStatesManifest.LoadFromJson(json);

        // Assert - WaitFrames is null when not explicitly set
        Assert.Null(manifest.States[0].WaitFrames);
    }
}
