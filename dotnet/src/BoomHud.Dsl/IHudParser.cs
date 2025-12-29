using BoomHud.Abstractions.IR;

namespace BoomHud.Dsl;

/// <summary>
/// Parser for Figma JSON files exported from Figma REST API.
/// </summary>
public interface IFigmaParser
{
    /// <summary>
    /// Parses a HUD document from a Figma JSON file.
    /// </summary>
    /// <param name="filePath">Path to the Figma JSON file.</param>
    /// <returns>The parsed HUD document.</returns>
    HudDocument ParseFile(string filePath);

    /// <summary>
    /// Parses a HUD document from Figma JSON content.
    /// </summary>
    /// <param name="json">Figma JSON content.</param>
    /// <returns>The parsed HUD document.</returns>
    HudDocument Parse(string json);

    /// <summary>
    /// Parses a specific node/frame from Figma JSON.
    /// </summary>
    /// <param name="json">Figma JSON content.</param>
    /// <param name="nodeId">The node ID to extract (e.g., "1:2").</param>
    /// <returns>The parsed HUD document for that node.</returns>
    HudDocument ParseNode(string json, string nodeId);

    /// <summary>
    /// Validates Figma JSON content.
    /// </summary>
    /// <param name="json">Figma JSON content to validate.</param>
    /// <returns>Validation result with any errors.</returns>
    ValidationResult Validate(string json);
}

/// <summary>
/// Result of DSL validation.
/// </summary>
public sealed record ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];

    public static ValidationResult Ok() => new() { IsValid = true };
    public static ValidationResult Fail(params ValidationError[] errors) => new() { IsValid = false, Errors = errors };
}

/// <summary>
/// A validation error.
/// </summary>
public sealed record ValidationError
{
    public required string Message { get; init; }
    public string? Path { get; init; }
    public int? Line { get; init; }
    public int? Column { get; init; }
}
