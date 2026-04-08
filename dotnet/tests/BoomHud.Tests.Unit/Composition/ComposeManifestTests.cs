using BoomHud.Abstractions.Composition;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Composition;

public class ComposeManifestTests
{
    [Fact]
    public void LoadFromJson_ValidManifest_LoadsAllProperties()
    {
        // Arrange
        var json = """
        {
          "version": "1.0",
          "root": "MainHud",
          "sources": [
            "figma/main-hud.figma.json",
            "pencil/debug-overlay.pen"
          ],
          "tokens": "../tokens.ir.json",
          "targets": ["godot", "terminalgui"],
          "output": "generated",
          "namespace": "MyGame.Hud"
        }
        """;

        // Act
        var manifest = ComposeManifest.LoadFromJson(json);

        // Assert
        manifest.Version.Should().Be("1.0");
        manifest.Root.Should().Be("MainHud");
        manifest.Sources.Should().HaveCount(2);
        manifest.Sources[0].Should().Be("figma/main-hud.figma.json");
        manifest.Sources[1].Should().Be("pencil/debug-overlay.pen");
        manifest.Tokens.Should().Be("../tokens.ir.json");
        manifest.Targets.Should().BeEquivalentTo(["godot", "terminalgui"]);
        manifest.Output.Should().Be("generated");
        manifest.Namespace.Should().Be("MyGame.Hud");
    }

    [Fact]
    public void LoadFromJson_MinimalManifest_UsesDefaults()
    {
        // Arrange
        var json = """
        {
          "sources": ["input.pen"]
        }
        """;

        // Act
        var manifest = ComposeManifest.LoadFromJson(json);

        // Assert
        manifest.Version.Should().Be("1.0");
        manifest.Root.Should().BeNull();
        manifest.Sources.Should().HaveCount(1);
        manifest.Tokens.Should().BeNull();
        manifest.Targets.Should().BeNull();
        manifest.Output.Should().BeNull();
        manifest.Namespace.Should().BeNull();
    }

    [Fact]
    public void ResolveSourcePaths_RelativePaths_ResolvesCorrectly()
    {
        // Arrange
        var json = """
        {
          "sources": [
            "figma/main.figma.json",
            "../shared/components.pen"
          ]
        }
        """;
        var manifest = ComposeManifest.LoadFromJson(json);

        // Act - simulate manifest at /project/ui/boom-hud.compose.json
        var manifestPath = Path.Combine(Path.GetTempPath(), "project", "ui", "boom-hud.compose.json");
        var resolved = manifest.ResolveSourcePaths(manifestPath);

        // Assert
        resolved.Should().HaveCount(2);
        resolved[0].Should().EndWith(Path.Combine("project", "ui", "figma", "main.figma.json"));
        resolved[1].Should().EndWith(Path.Combine("project", "shared", "components.pen"));
    }

    [Fact]
    public void ResolveTokensPath_RelativePath_ResolvesCorrectly()
    {
        // Arrange
        var json = """
        {
          "sources": ["input.pen"],
          "tokens": "../tokens.ir.json"
        }
        """;
        var manifest = ComposeManifest.LoadFromJson(json);

        // Act
        var manifestPath = Path.Combine(Path.GetTempPath(), "project", "ui", "boom-hud.compose.json");
        var resolved = manifest.ResolveTokensPath(manifestPath);

        // Assert
        resolved.Should().EndWith(Path.Combine("project", "tokens.ir.json"));
    }

    [Fact]
    public void ResolveTokensPath_NoTokens_ReturnsNull()
    {
        // Arrange
        var json = """
        {
          "sources": ["input.pen"]
        }
        """;
        var manifest = ComposeManifest.LoadFromJson(json);

        // Act
        var manifestPath = Path.Combine(Path.GetTempPath(), "boom-hud.compose.json");
        var resolved = manifest.ResolveTokensPath(manifestPath);

        // Assert
        resolved.Should().BeNull();
    }

    [Fact]
    public void ResolveOutputPath_RelativePath_ResolvesCorrectly()
    {
        // Arrange
        var json = """
        {
          "sources": ["input.pen"],
          "output": "generated"
        }
        """;
        var manifest = ComposeManifest.LoadFromJson(json);

        // Act
        var manifestPath = Path.Combine(Path.GetTempPath(), "project", "ui", "boom-hud.compose.json");
        var resolved = manifest.ResolveOutputPath(manifestPath);

        // Assert
        resolved.Should().EndWith(Path.Combine("project", "ui", "generated"));
    }

    [Fact]
    public void LoadFromJson_WithComments_IgnoresComments()
    {
        // Arrange
        var json = """
        {
          // This is a comment
          "sources": ["input.pen"],
          "root": "MainHud"
        }
        """;

        // Act
        var manifest = ComposeManifest.LoadFromJson(json);

        // Assert
        manifest.Sources.Should().HaveCount(1);
        manifest.Root.Should().Be("MainHud");
    }

    [Fact]
    public void LoadFromJson_WithTrailingCommas_ParsesCorrectly()
    {
        // Arrange
        var json = """
        {
          "sources": [
            "a.pen",
            "b.pen",
          ],
          "root": "MainHud",
        }
        """;

        // Act
        var manifest = ComposeManifest.LoadFromJson(json);

        // Assert
        manifest.Sources.Should().HaveCount(2);
    }
}
