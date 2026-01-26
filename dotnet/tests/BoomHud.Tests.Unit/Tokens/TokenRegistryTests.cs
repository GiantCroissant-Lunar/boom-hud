using BoomHud.Abstractions.Diagnostics;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Tokens;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Tokens;

/// <summary>
/// Tests for token registry and token resolution.
/// </summary>
public class TokenRegistryTests
{
    [Fact]
    public void LoadFromJson_ValidTokens_LoadsColors()
    {
        // Arrange
        var json = """
        {
          "version": "1.0",
          "colors": {
            "debug-bg": { "value": "#1a1a1a" },
            "debug-text": { "value": "#00ff00" }
          }
        }
        """;

        // Act
        var registry = TokenRegistry.LoadFromJson(json);

        // Assert
        registry.Colors.Should().HaveCount(2);
        registry.Colors["debug-bg"].Value.Should().Be("#1a1a1a");
        registry.Colors["debug-text"].Value.Should().Be("#00ff00");
    }

    [Fact]
    public void LoadFromJson_ValidTokens_LoadsSpacing()
    {
        // Arrange
        var json = """
        {
          "version": "1.0",
          "spacing": {
            "xs": { "value": 2 },
            "sm": { "value": 4 },
            "md": { "value": 8 }
          }
        }
        """;

        // Act
        var registry = TokenRegistry.LoadFromJson(json);

        // Assert
        registry.Spacing.Should().HaveCount(3);
        registry.Spacing["xs"].Value.Should().Be(2);
        registry.Spacing["md"].Value.Should().Be(8);
    }

    [Fact]
    public void TryResolve_ExistingColorToken_ReturnsValue()
    {
        // Arrange
        var json = """
        {
          "colors": {
            "debug-bg": { "value": "#1a1a1a" }
          }
        }
        """;
        var registry = TokenRegistry.LoadFromJson(json);

        // Act
        var result = registry.TryResolve("colors.debug-bg");

        // Assert
        result.Should().NotBeNull();
        result!.Category.Should().Be(TokenCategory.Color);
        result.AsString.Should().Be("#1a1a1a");
    }

    [Fact]
    public void TryResolve_NonExistentToken_ReturnsNull()
    {
        // Arrange
        var json = """
        {
          "colors": {
            "debug-bg": { "value": "#1a1a1a" }
          }
        }
        """;
        var registry = TokenRegistry.LoadFromJson(json);

        // Act
        var result = registry.TryResolve("colors.nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryResolve_InvalidTokenRef_ReturnsNull()
    {
        // Arrange
        var registry = TokenRegistry.Empty;

        // Act & Assert
        registry.TryResolve("").Should().BeNull();
        registry.TryResolve("invalid").Should().BeNull();
        registry.TryResolve("no-dot-category").Should().BeNull();
    }

    [Fact]
    public void Contains_ExistingToken_ReturnsTrue()
    {
        // Arrange
        var json = """
        {
          "colors": {
            "primary": { "value": "#007bff" }
          }
        }
        """;
        var registry = TokenRegistry.LoadFromJson(json);

        // Act & Assert
        registry.Contains("colors.primary").Should().BeTrue();
        registry.Contains("colors.missing").Should().BeFalse();
    }

    [Fact]
    public void GetAllTokenNames_ReturnsAllTokens()
    {
        // Arrange
        var json = """
        {
          "colors": { "bg": { "value": "#000" } },
          "spacing": { "sm": { "value": 4 } }
        }
        """;
        var registry = TokenRegistry.LoadFromJson(json);

        // Act
        var names = registry.GetAllTokenNames().ToList();

        // Assert
        names.Should().Contain("colors.bg");
        names.Should().Contain("spacing.sm");
    }
}

/// <summary>
/// Tests for BH0102 (unresolved token reference) diagnostic behavior.
/// Golden tests to ensure error messages are actionable.
/// </summary>
public class BH0102UnresolvedTokenTests
{
    [Fact]
    public void UnresolvedTokenRef_HasCorrectErrorCode()
    {
        // Act
        var diagnostic = Diagnostics.UnresolvedTokenRef("colors.missing", "test.pen", "button-1");

        // Assert
        diagnostic.Code.Should().Be("BH0102");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void UnresolvedTokenRef_IncludesTokenId()
    {
        // Act
        var diagnostic = Diagnostics.UnresolvedTokenRef("colors.missing-color", "test.pen", "button-1");

        // Assert
        diagnostic.Message.Should().Contain("colors.missing-color");
    }

    [Fact]
    public void UnresolvedTokenRef_IncludesSourceFile()
    {
        // Act
        var diagnostic = Diagnostics.UnresolvedTokenRef("colors.x", "path/to/file.pen", "node1");

        // Assert
        diagnostic.SourceFile.Should().Be("path/to/file.pen");
        diagnostic.ToString().Should().Contain("path/to/file.pen");
    }

    [Fact]
    public void UnresolvedTokenRef_IncludesNodeId()
    {
        // Act
        var diagnostic = Diagnostics.UnresolvedTokenRef("spacing.invalid", "test.pen", "submit-button");

        // Assert
        diagnostic.NodeId.Should().Be("submit-button");
        diagnostic.ToString().Should().Contain("submit-button");
    }

    [Fact]
    public void UnresolvedTokenRef_FormatsCorrectlyForConsole()
    {
        // Act
        var diagnostic = Diagnostics.UnresolvedTokenRef("colors.missing", "ui/overlay.pen", "health-bar");

        // Assert
        var output = diagnostic.ToString();
        
        // Should be machine-parseable format
        output.Should().StartWith("[BH0102]");
        output.Should().Contain("error:");
        output.Should().Contain("colors.missing");
        output.Should().Contain("ui/overlay.pen");
        output.Should().Contain("health-bar");
    }
}

/// <summary>
/// Tests for BH0103 (deprecated token) diagnostic behavior.
/// </summary>
public class BH0103DeprecatedTokenTests
{
    [Fact]
    public void TryResolve_DeprecatedToken_ReturnsDeprecatedFlag()
    {
        // Arrange
        var json = """
        {
          "colors": {
            "old-bg": { "value": "#1a1a1a", "deprecated": true },
            "new-bg": { "value": "#2a2a2a", "deprecated": false }
          }
        }
        """;
        var registry = TokenRegistry.LoadFromJson(json);

        // Act
        var oldToken = registry.TryResolve("colors.old-bg");
        var newToken = registry.TryResolve("colors.new-bg");

        // Assert
        oldToken.Should().NotBeNull();
        oldToken!.Deprecated.Should().BeTrue();
        
        newToken.Should().NotBeNull();
        newToken!.Deprecated.Should().BeFalse();
    }

    [Fact]
    public void DeprecatedToken_Diagnostic_HasCorrectCode()
    {
        // Act
        var diagnostic = Diagnostics.DeprecatedToken("colors.old-bg", "test.pen", "node1");

        // Assert
        diagnostic.Code.Should().Be("BH0103");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.Message.Should().Contain("deprecated");
        diagnostic.Message.Should().Contain("colors.old-bg");
    }
}

/// <summary>
/// Tests for BH0104 (inline token warning) diagnostic behavior.
/// </summary>
public class BH0104InlineTokenTests
{
    [Fact]
    public void InlineTokenWarning_HasCorrectCode()
    {
        // Act
        var diagnostic = Diagnostics.InlineTokenWarning("#ff0000", "Background", "test.pen", "node1");

        // Assert
        diagnostic.Code.Should().Be("BH0104");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void InlineTokenWarning_IncludesValueAndField()
    {
        // Act
        var diagnostic = Diagnostics.InlineTokenWarning("#123456", "Foreground", "test.pen", "btn");

        // Assert
        diagnostic.Message.Should().Contain("#123456");
        diagnostic.Message.Should().Contain("Foreground");
    }
}
