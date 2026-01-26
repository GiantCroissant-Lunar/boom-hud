// Token Registry for BoomHud
// Loads and provides access to design tokens from tokens.ir.json
// Domain wrapper that maps from Generated DTOs to domain types

namespace BoomHud.Abstractions.Tokens;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoomHud.Abstractions.Diagnostics;
using BoomHud.Abstractions.Tokens.Generated;

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
    /// Diagnostics emitted during loading (e.g., unknown version warning).
    /// </summary>
    public IReadOnlyList<BoomHudDiagnostic> LoadDiagnostics { get; private init; } = [];

    /// <summary>
    /// Known supported versions.
    /// </summary>
    private static readonly HashSet<string> SupportedVersions = ["1.0"];

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
    /// Maps from Generated DTOs to domain types.
    /// </summary>
    public static TokenRegistry LoadFromJson(string json, string? sourcePath = null)
    {
        var dto = JsonSerializer.Deserialize<TokenRegistryDto>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize token registry");

        var diagnostics = new List<BoomHudDiagnostic>();
        var version = dto.Version ?? "1.0";

        // Validate version
        if (!SupportedVersions.Contains(version))
        {
            diagnostics.Add(Diagnostics.UnknownSchemaVersion("token registry", version, sourcePath));
        }

        var registry = new TokenRegistry
        {
            SourcePath = sourcePath,
            Version = version,
            LoadDiagnostics = diagnostics.AsReadOnly()
        };

        // Map colors: DTO → domain
        if (dto.Colors != null)
        {
            foreach (var (name, tokenDto) in dto.Colors)
            {
                registry._colors[name] = new ColorToken(
                    tokenDto.Value,
                    tokenDto.Description,
                    tokenDto.Deprecated,
                    tokenDto.Aliases?.AsReadOnly());
            }
        }

        // Map spacing: DTO → domain
        if (dto.Spacing != null)
        {
            foreach (var (name, tokenDto) in dto.Spacing)
            {
                registry._spacing[name] = new SpacingToken(
                    tokenDto.Value,
                    tokenDto.Description,
                    tokenDto.Deprecated);
            }
        }

        // Map typography: DTO → domain
        if (dto.Typography != null)
        {
            foreach (var (name, tokenDto) in dto.Typography)
            {
                registry._typography[name] = new TypographyToken(
                    tokenDto.FontFamily,
                    tokenDto.FontSize,
                    ParseFontWeight(tokenDto.FontWeight),
                    tokenDto.LineHeight,
                    tokenDto.LetterSpacing,
                    tokenDto.Description,
                    tokenDto.Deprecated);
            }
        }

        // Map radii: DTO → domain
        if (dto.Radii != null)
        {
            foreach (var (name, tokenDto) in dto.Radii)
            {
                registry._radii[name] = new RadiusToken(
                    tokenDto.Value,
                    tokenDto.Description,
                    tokenDto.Deprecated);
            }
        }

        // Map shadows: DTO → domain
        if (dto.Shadows != null)
        {
            foreach (var (name, tokenDto) in dto.Shadows)
            {
                registry._shadows[name] = new ShadowToken(
                    tokenDto.ValueAsString,
                    tokenDto.Description,
                    tokenDto.Deprecated);
            }
        }

        // Map opacity: DTO → domain
        if (dto.Opacity != null)
        {
            foreach (var (name, tokenDto) in dto.Opacity)
            {
                registry._opacity[name] = new OpacityToken(
                    tokenDto.Value,
                    tokenDto.Description,
                    tokenDto.Deprecated);
            }
        }

        return registry;
    }

    private static object? ParseFontWeight(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind == JsonValueKind.Null)
            return null;

        return element.Value.ValueKind switch
        {
            JsonValueKind.Number => element.Value.GetDouble(),
            JsonValueKind.String => element.Value.GetString(),
            _ => element.Value.GetRawText()
        };
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
            "spacing" => _spacing.TryGetValue(name, out var s) ? new TokenValue(tokenRef, s.Value.ToString(CultureInfo.InvariantCulture), TokenCategory.Spacing, s.Deprecated) : null,
            "typography" => _typography.TryGetValue(name, out var t) ? new TokenValue(tokenRef, t, TokenCategory.Typography, t.Deprecated) : null,
            "radii" => _radii.TryGetValue(name, out var r) ? new TokenValue(tokenRef, r.Value.ToString(CultureInfo.InvariantCulture), TokenCategory.Radius, r.Deprecated) : null,
            "shadows" => _shadows.TryGetValue(name, out var sh) ? new TokenValue(tokenRef, sh.Value, TokenCategory.Shadow, sh.Deprecated) : null,
            "opacity" => _opacity.TryGetValue(name, out var o) ? new TokenValue(tokenRef, o.Value.ToString(CultureInfo.InvariantCulture), TokenCategory.Opacity, o.Deprecated) : null,
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

#region Domain Types

/// <summary>
/// Color token (domain type).
/// </summary>
public sealed record ColorToken(
    string Value,
    string? Description = null,
    bool Deprecated = false,
    IReadOnlyList<string>? Aliases = null);

/// <summary>
/// Spacing token (domain type).
/// </summary>
public sealed record SpacingToken(
    double Value,
    string? Description = null,
    bool Deprecated = false);

/// <summary>
/// Typography token (domain type).
/// </summary>
public sealed record TypographyToken(
    string? FontFamily = null,
    double? FontSize = null,
    object? FontWeight = null,
    double? LineHeight = null,
    double? LetterSpacing = null,
    string? Description = null,
    bool Deprecated = false);

/// <summary>
/// Radius token (domain type).
/// </summary>
public sealed record RadiusToken(
    double Value,
    string? Description = null,
    bool Deprecated = false);

/// <summary>
/// Shadow token (domain type).
/// </summary>
public sealed record ShadowToken(
    string Value,
    string? Description = null,
    bool Deprecated = false);

/// <summary>
/// Opacity token (domain type).
/// </summary>
public sealed record OpacityToken(
    double Value,
    string? Description = null,
    bool Deprecated = false);

#endregion
