// Snapshot States for BoomHud
// Defines the schema for *.states.json files used by snapshot generation

using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoomHud.Abstractions.Snapshots;

/// <summary>
/// A snapshot states manifest that defines viewport and VM states to render.
/// Loaded from *.states.json files.
/// </summary>
public sealed record SnapshotStatesManifest
{
    /// <summary>
    /// Schema version. Currently "1.0".
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Viewport configuration for all snapshots.
    /// </summary>
    [JsonPropertyName("viewport")]
    public ViewportConfig Viewport { get; init; } = new();

    /// <summary>
    /// States to render (in order).
    /// </summary>
    [JsonPropertyName("states")]
    public IReadOnlyList<SnapshotState> States { get; init; } = [];

    /// <summary>
    /// Default settings for all states.
    /// </summary>
    [JsonPropertyName("defaults")]
    public SnapshotDefaults Defaults { get; init; } = new();

    /// <summary>
    /// Loads a states manifest from a JSON file.
    /// </summary>
    public static SnapshotStatesManifest LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"States manifest not found: {filePath}", filePath);
        }

        var json = File.ReadAllText(filePath);
        return LoadFromJson(json);
    }

    /// <summary>
    /// Loads a states manifest from JSON string.
    /// </summary>
    public static SnapshotStatesManifest LoadFromJson(string json)
    {
        var manifest = JsonSerializer.Deserialize<SnapshotStatesManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize states manifest");

        return manifest;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

/// <summary>
/// Viewport configuration for snapshot rendering.
/// </summary>
public sealed record ViewportConfig
{
    /// <summary>
    /// Viewport width in pixels.
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; init; } = 1280;

    /// <summary>
    /// Viewport height in pixels.
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; init; } = 720;

    /// <summary>
    /// Scale factor (1 = 100%).
    /// </summary>
    [JsonPropertyName("scale")]
    public double Scale { get; init; } = 1.0;
}

/// <summary>
/// Default settings for all states.
/// </summary>
public sealed record SnapshotDefaults
{
    /// <summary>
    /// Default number of frames to wait before capture (allows layout to settle).
    /// </summary>
    [JsonPropertyName("waitFrames")]
    public int WaitFrames { get; init; } = 2;

    /// <summary>
    /// Default background color (hex, e.g., "#1a1a2e").
    /// </summary>
    [JsonPropertyName("background")]
    public string? Background { get; init; }
}

/// <summary>
/// A single snapshot state with ViewModel bindings.
/// </summary>
public sealed record SnapshotState
{
    /// <summary>
    /// State name (used for filename, e.g., "Default" → "000_Default.png").
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Optional description for documentation.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// ViewModel property values to apply before rendering.
    /// Structure matches the generated IViewModel interface hierarchy.
    /// </summary>
    [JsonPropertyName("vm")]
    public JsonElement? Vm { get; init; }

    /// <summary>
    /// Override frames to wait before capture (defaults to manifest.defaults.waitFrames).
    /// </summary>
    [JsonPropertyName("waitFrames")]
    public int? WaitFrames { get; init; }
}

/// <summary>
/// Output manifest written alongside snapshots for traceability.
/// </summary>
public sealed record SnapshotOutputManifest
{
    /// <summary>
    /// Version of the snapshot output format.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Timestamp when snapshots were generated (ISO 8601).
    /// </summary>
    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; init; } = DateTime.UtcNow.ToString("O");

    /// <summary>
    /// BoomHud CLI version used.
    /// </summary>
    [JsonPropertyName("toolVersion")]
    public string? ToolVersion { get; init; }

    /// <summary>
    /// Godot version used for rendering (if applicable).
    /// </summary>
    [JsonPropertyName("godotVersion")]
    public string? GodotVersion { get; init; }

    /// <summary>
    /// Target backend (godot, avalonia, terminalgui).
    /// </summary>
    [JsonPropertyName("target")]
    public required string Target { get; init; }

    /// <summary>
    /// Viewport configuration used.
    /// </summary>
    [JsonPropertyName("viewport")]
    public required ViewportConfig Viewport { get; init; }

    /// <summary>
    /// Runner environment info for baseline stability diagnostics.
    /// </summary>
    [JsonPropertyName("runnerInfo")]
    public RunnerInfo? RunnerInfo { get; init; }

    /// <summary>
    /// Input hashes for determinism verification.
    /// </summary>
    [JsonPropertyName("inputHashes")]
    public Dictionary<string, string> InputHashes { get; init; } = [];

    /// <summary>
    /// Generated snapshot files.
    /// </summary>
    [JsonPropertyName("snapshots")]
    public IReadOnlyList<SnapshotFileInfo> Snapshots { get; init; } = [];

    /// <summary>
    /// Serializes to JSON.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, WriteOptions);

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

/// <summary>
/// Runner environment information for baseline stability diagnostics.
/// </summary>
public sealed record RunnerInfo
{
    /// <summary>
    /// Operating system (e.g., "Windows 10.0.19045", "Ubuntu 22.04").
    /// </summary>
    [JsonPropertyName("os")]
    public string? Os { get; init; }

    /// <summary>
    /// .NET runtime version.
    /// </summary>
    [JsonPropertyName("dotnetVersion")]
    public string? DotnetVersion { get; init; }

    /// <summary>
    /// Whether running in CI environment.
    /// </summary>
    [JsonPropertyName("isCI")]
    public bool IsCI { get; init; }

    /// <summary>
    /// CI runner name/type if available.
    /// </summary>
    [JsonPropertyName("ciRunner")]
    public string? CIRunner { get; init; }

    /// <summary>
    /// Git commit hash of the snapshot runner script (if available).
    /// </summary>
    [JsonPropertyName("runnerScriptHash")]
    public string? RunnerScriptHash { get; init; }

    /// <summary>
    /// Hash of bundled font assets (if applicable).
    /// </summary>
    [JsonPropertyName("fontBundleHash")]
    public string? FontBundleHash { get; init; }

    /// <summary>
    /// Whether headless mode was used.
    /// </summary>
    [JsonPropertyName("headless")]
    public bool Headless { get; init; }
}

/// <summary>
/// Information about a generated snapshot file.
/// </summary>
public sealed record SnapshotFileInfo
{
    /// <summary>
    /// State name.
    /// </summary>
    [JsonPropertyName("state")]
    public required string State { get; init; }

    /// <summary>
    /// Relative path to the PNG file.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// SHA256 hash of the PNG file.
    /// </summary>
    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }
}
