using BoomHud.Abstractions.IR;

namespace BoomHud.Dsl.Pencil;

/// <summary>
/// Parser for Pencil.dev .pen files.
/// Converts .pen JSON format to BoomHud IR.
/// </summary>
public interface IPenParser
{
    /// <summary>
    /// Parses a HUD document from a .pen file.
    /// </summary>
    /// <param name="filePath">Path to the .pen file.</param>
    /// <returns>The parsed HUD document.</returns>
    HudDocument ParseFile(string filePath);

    /// <summary>
    /// Parses a HUD document from .pen JSON content.
    /// </summary>
    /// <param name="json">.pen JSON content.</param>
    /// <returns>The parsed HUD document.</returns>
    HudDocument Parse(string json);

    /// <summary>
    /// Parses a HUD document with warnings output.
    /// </summary>
    /// <param name="json">.pen JSON content.</param>
    /// <param name="warnings">Any warnings generated during parsing.</param>
    /// <returns>The parsed HUD document.</returns>
    HudDocument Parse(string json, out IReadOnlyList<string> warnings);

    /// <summary>
    /// Validates .pen JSON content.
    /// </summary>
    /// <param name="json">.pen JSON content to validate.</param>
    /// <returns>Validation result with any errors.</returns>
    ValidationResult Validate(string json);
}

/// <summary>
/// Implementation of IPenParser.
/// </summary>
public sealed class PenParser : IPenParser
{
    public HudDocument ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Pen file not found: {filePath}", filePath);
        }

        var json = File.ReadAllText(filePath);
        return Parse(json);
    }

    public HudDocument Parse(string json)
    {
        return Parse(json, out _);
    }

    public HudDocument Parse(string json, out IReadOnlyList<string> warnings)
    {
        var penDto = PenDto.FromJson(json)
            ?? throw new InvalidOperationException("Failed to parse .pen JSON");

        var converter = new PenToIrConverter(penDto);
        var document = converter.Convert();
        
        warnings = converter.Warnings;
        return document;
    }

    public ValidationResult Validate(string json)
    {
        try
        {
            var penDto = PenDto.FromJson(json);

            if (penDto == null)
            {
                return ValidationResult.Fail(new ValidationError { Message = "Failed to parse JSON" });
            }

            if (penDto.Nodes == null || penDto.Nodes.Count == 0)
            {
                return ValidationResult.Fail(new ValidationError { Message = "No nodes defined in .pen file" });
            }

            // Check for required node properties
            var errors = new List<ValidationError>();
            ValidateNodes(penDto.Nodes, errors, "nodes");

            if (errors.Count > 0)
            {
                return ValidationResult.Fail(errors.ToArray());
            }

            return ValidationResult.Ok();
        }
        catch (System.Text.Json.JsonException ex)
        {
            return ValidationResult.Fail(new ValidationError
            {
                Message = $"Invalid JSON: {ex.Message}",
                Line = (int?)ex.LineNumber,
                Column = (int?)ex.BytePositionInLine
            });
        }
    }

    private static void ValidateNodes(List<PenNodeDto> nodes, List<ValidationError> errors, string path)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var nodePath = $"{path}[{i}]";

            if (string.IsNullOrEmpty(node.Id))
            {
                errors.Add(new ValidationError { Message = $"Node missing 'id' at {nodePath}", Path = nodePath });
            }

            if (string.IsNullOrEmpty(node.Type))
            {
                errors.Add(new ValidationError { Message = $"Node missing 'type' at {nodePath}", Path = nodePath });
            }

            if (node.Children != null && node.Children.Count > 0)
            {
                ValidateNodes(node.Children, errors, $"{nodePath}.children");
            }
        }
    }
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
