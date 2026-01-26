// Compose Manifest for BoomHud
// Domain wrapper that maps from Generated DTOs
// Defines the schema for boom-hud.compose.json

using System.Text.Json;
using BoomHud.Abstractions.Composition.Generated;
using BoomHud.Abstractions.Diagnostics;

namespace BoomHud.Abstractions.Composition;

/// <summary>
/// A composition manifest that defines how to combine multiple design sources.
/// Loaded from boom-hud.compose.json. Domain type (immutable).
/// </summary>
public sealed record ComposeManifest
{
    /// <summary>
    /// Schema version. Currently "1.0".
    /// </summary>
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Diagnostics emitted during loading (e.g., unknown version warning).
    /// </summary>
    public IReadOnlyList<BoomHudDiagnostic> LoadDiagnostics { get; init; } = [];

    /// <summary>
    /// Known supported versions.
    /// </summary>
    private static readonly HashSet<string> SupportedVersions = ["1.0"];

    /// <summary>
    /// Name of the root component/document to use as the composition root.
    /// </summary>
    public string? Root { get; init; }

    /// <summary>
    /// List of source file paths (relative to manifest location).
    /// Order matters: first source provides default root if not specified.
    /// </summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary>
    /// Optional path to token registry file (relative to manifest).
    /// If omitted, uses standard auto-discovery (ui/tokens.ir.json).
    /// </summary>
    public string? Tokens { get; init; }

    /// <summary>
    /// Optional list of target backends to generate for.
    /// If omitted, must be specified via CLI --target.
    /// </summary>
    public IReadOnlyList<string>? Targets { get; init; }

    /// <summary>
    /// Optional output directory (relative to manifest).
    /// If omitted, uses CLI --output or current directory.
    /// </summary>
    public string? Output { get; init; }

    /// <summary>
    /// Optional namespace for generated code.
    /// If omitted, uses CLI --namespace.
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// Loads a compose manifest from a JSON file.
    /// </summary>
    public static ComposeManifest LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Compose manifest not found: {filePath}", filePath);
        }

        var json = File.ReadAllText(filePath);
        return LoadFromJson(json, filePath);
    }

    /// <summary>
    /// Loads a compose manifest from JSON string.
    /// Maps from Generated DTO to domain type.
    /// </summary>
    public static ComposeManifest LoadFromJson(string json, string? sourcePath = null)
    {
        var dto = JsonSerializer.Deserialize<ComposeManifestDto>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize compose manifest");

        var diagnostics = new List<BoomHudDiagnostic>();
        var version = dto.Version ?? "1.0";

        // Validate version
        if (!SupportedVersions.Contains(version))
        {
            diagnostics.Add(Diagnostics.UnknownSchemaVersion("compose manifest", version, sourcePath));
        }

        return new ComposeManifest
        {
            Version = version,
            Root = dto.Root,
            Sources = dto.Sources?.AsReadOnly() ?? (IReadOnlyList<string>)[],
            Tokens = dto.Tokens,
            Targets = dto.Targets?.AsReadOnly(),
            Output = dto.Output,
            Namespace = dto.Namespace,
            LoadDiagnostics = diagnostics.AsReadOnly()
        };
    }

    /// <summary>
    /// Resolves source paths relative to the manifest file location.
    /// </summary>
    public IReadOnlyList<string> ResolveSourcePaths(string manifestPath)
    {
        var manifestDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? ".";
        return Sources.Select(s => Path.GetFullPath(Path.Combine(manifestDir, s))).ToList();
    }

    /// <summary>
    /// Resolves the tokens path relative to the manifest file location.
    /// Returns null if no tokens path is specified.
    /// </summary>
    public string? ResolveTokensPath(string manifestPath)
    {
        if (string.IsNullOrEmpty(Tokens))
            return null;

        var manifestDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? ".";
        return Path.GetFullPath(Path.Combine(manifestDir, Tokens));
    }

    /// <summary>
    /// Resolves the output path relative to the manifest file location.
    /// Returns null if no output path is specified.
    /// </summary>
    public string? ResolveOutputPath(string manifestPath)
    {
        if (string.IsNullOrEmpty(Output))
            return null;

        var manifestDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? ".";
        return Path.GetFullPath(Path.Combine(manifestDir, Output));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
