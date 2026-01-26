// Token Registry for BoomHud
// Loads and provides access to design tokens from tokens.ir.json

namespace BoomHud.Abstractions.Tokens;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Design token registry loaded from tokens.ir.json.
/// Provides lookup by category and name (e.g., "colors.debug-bg").
/// </summary>
public sealed class TokenRegistry
{
    private readonly Dictionary<string, ColorToken> _colors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SpacingToken> _spacing = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TypographyToken> _typography = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RadiusToken> _radii = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ShadowToken> _shadows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OpacityToken> _opacity = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Source file path for diagnostics.
    /// </summary>
    public string? SourcePath { get; private init; }

    /// <summary>
    /// Registry version.
    /// </summary>
    public string Version { get; private init; } = "1.0";

    /// <summary>
    /// All color tokens.
    /// </summary>
    public IReadOnlyDictionary<string, ColorToken> Colors => _colors;

    /// <summary>
    /// All spacing tokens.
    /// </summary>
    public IReadOnlyDictionary<string, SpacingToken> Spacing => _spacing;

    /// <summary>
    /// All typography tokens.
    /// </summary>
    public IReadOnlyDictionary<string, TypographyToken> Typography => _typography;

    /// <summary>
    /// All radius tokens.
    /// </summary>
    public IReadOnlyDictionary<string, RadiusToken> Radii => _radii;

    /// <summary>
    /// All shadow tokens.
    /// </summary>
    public IReadOnlyDictionary<string, ShadowToken> Shadows => _shadows;

    /// <summary>
    /// All opacity tokens.
    /// </summary>
    public IReadOnlyDictionary<string, OpacityToken> Opacity => _opacity;

    /// <summary>
    /// Creates an empty registry (for testing or when no tokens file is provided).
    /// </summary>
    public static TokenRegistry Empty => new();

    private TokenRegistry() { }

    /// <summary>
    /// Loads a token registry from a JSON file.
    /// </summary>
    public static TokenRegistry LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Token registry file not found: {filePath}", filePath);
        }

        var json = File.ReadAllText(filePath);
        return LoadFromJson(json, filePath);
    }

    /// <summary>
    /// Loads a token registry from JSON string.
    /// </summary>
    public static TokenRegistry LoadFromJson(string json, string? sourcePath = null)
    {
        var dto = JsonSerializer.Deserialize<TokenRegistryDto>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize token registry");

        var registry = new TokenRegistry
        {
            SourcePath = sourcePath,
            Version = dto.Version ?? "1.0"
        };

        // Load colors
        if (dto.Colors != null)
        {
            foreach (var (name, token) in dto.Colors)
            {
                registry._colors[name] = token;
            }
        }

        // Load spacing
        if (dto.Spacing != null)
        {
            foreach (var (name, token) in dto.Spacing)
            {
                registry._spacing[name] = token;
            }
        }

        // Load typography
        if (dto.Typography != null)
        {
            foreach (var (name, token) in dto.Typography)
            {
                registry._typography[name] = token;
            }
        }

        // Load radii
        if (dto.Radii != null)
        {
            foreach (var (name, token) in dto.Radii)
            {
                registry._radii[name] = token;
            }
        }

        // Load shadows
        if (dto.Shadows != null)
        {
            foreach (var (name, token) in dto.Shadows)
            {
                registry._shadows[name] = token;
            }
        }

        // Load opacity
        if (dto.Opacity != null)
        {
            foreach (var (name, token) in dto.Opacity)
            {
                registry._opacity[name] = token;
            }
        }

        return registry;
    }

    /// <summary>
    /// Tries to resolve a token reference like "colors.debug-bg" or "spacing.md".
    /// Returns the resolved value or null if not found.
    /// </summary>
    public TokenValue? TryResolve(string tokenRef)
    {
        if (string.IsNullOrEmpty(tokenRef))
            return null;

        // Parse "category.name" format
        var dotIndex = tokenRef.IndexOf('.');
        if (dotIndex <= 0 || dotIndex == tokenRef.Length - 1)
            return null;

        var category = tokenRef[..dotIndex];
        var name = tokenRef[(dotIndex + 1)..];

        return category.ToLowerInvariant() switch
        {
            "colors" => _colors.TryGetValue(name, out var c) ? new TokenValue(tokenRef, c.Value, TokenCategory.Color, c.Deprecated) : null,
            "spacing" => _spacing.TryGetValue(name, out var s) ? new TokenValue(tokenRef, s.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), TokenCategory.Spacing, s.Deprecated) : null,
            "typography" => _typography.TryGetValue(name, out var t) ? new TokenValue(tokenRef, t, TokenCategory.Typography, t.Deprecated) : null,
            "radii" => _radii.TryGetValue(name, out var r) ? new TokenValue(tokenRef, r.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), TokenCategory.Radius, r.Deprecated) : null,
            "shadows" => _shadows.TryGetValue(name, out var sh) ? new TokenValue(tokenRef, sh.ValueAsString, TokenCategory.Shadow, sh.Deprecated) : null,
            "opacity" => _opacity.TryGetValue(name, out var o) ? new TokenValue(tokenRef, o.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), TokenCategory.Opacity, o.Deprecated) : null,
            _ => null
        };
    }

    /// <summary>
    /// Checks if a token reference exists.
    /// </summary>
    public bool Contains(string tokenRef) => TryResolve(tokenRef) != null;

    /// <summary>
    /// Gets all token names in a flattened format (e.g., "colors.debug-bg").
    /// </summary>
    public IEnumerable<string> GetAllTokenNames()
    {
        foreach (var name in _colors.Keys) yield return $"colors.{name}";
        foreach (var name in _spacing.Keys) yield return $"spacing.{name}";
        foreach (var name in _typography.Keys) yield return $"typography.{name}";
        foreach (var name in _radii.Keys) yield return $"radii.{name}";
        foreach (var name in _shadows.Keys) yield return $"shadows.{name}";
        foreach (var name in _opacity.Keys) yield return $"opacity.{name}";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

/// <summary>
/// Resolved token value with category information.
/// </summary>
public sealed record TokenValue(string TokenRef, object Value, TokenCategory Category, bool Deprecated = false)
{
    /// <summary>
    /// Gets the value as a string (for colors, shadows).
    /// </summary>
    public string? AsString => Value as string;

    /// <summary>
    /// Gets the value as a number (for spacing, radii, opacity).
    /// </summary>
    public double? AsNumber => Value switch
    {
        double d => d,
        string s when double.TryParse(s, out var n) => n,
        _ => null
    };

    /// <summary>
    /// Gets the value as typography (for typography tokens).
    /// </summary>
    public TypographyToken? AsTypography => Value as TypographyToken;
}

/// <summary>
/// Token category for type-safe resolution.
/// </summary>
public enum TokenCategory
{
    Color,
    Spacing,
    Typography,
    Radius,
    Shadow,
    Opacity
}

#region Token DTOs

internal sealed class TokenRegistryDto
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("colors")]
    public Dictionary<string, ColorToken>? Colors { get; set; }

    [JsonPropertyName("spacing")]
    public Dictionary<string, SpacingToken>? Spacing { get; set; }

    [JsonPropertyName("typography")]
    public Dictionary<string, TypographyToken>? Typography { get; set; }

    [JsonPropertyName("radii")]
    public Dictionary<string, RadiusToken>? Radii { get; set; }

    [JsonPropertyName("shadows")]
    public Dictionary<string, ShadowToken>? Shadows { get; set; }

    [JsonPropertyName("opacity")]
    public Dictionary<string, OpacityToken>? Opacity { get; set; }
}

public sealed class ColorToken
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; set; }

    [JsonPropertyName("aliases")]
    public List<string>? Aliases { get; set; }
}

public sealed class SpacingToken
{
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; set; }
}

public sealed class TypographyToken
{
    [JsonPropertyName("fontFamily")]
    public string? FontFamily { get; set; }

    [JsonPropertyName("fontSize")]
    public double? FontSize { get; set; }

    [JsonPropertyName("fontWeight")]
    public object? FontWeight { get; set; } // Can be number or string

    [JsonPropertyName("lineHeight")]
    public double? LineHeight { get; set; }

    [JsonPropertyName("letterSpacing")]
    public double? LetterSpacing { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; set; }
}

public sealed class RadiusToken
{
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; set; }
}

public sealed class ShadowToken
{
    /// <summary>
    /// Shadow value - can be a CSS-like string or a structured object.
    /// Use JsonElement to handle both cases.
    /// </summary>
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }

    /// <summary>
    /// Gets the shadow value as a string (serialized if object).
    /// </summary>
    public string ValueAsString =>
        Value.ValueKind == JsonValueKind.String
            ? Value.GetString() ?? ""
            : Value.GetRawText();

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; set; }
}

public sealed class OpacityToken
{
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; set; }
}

#endregion
