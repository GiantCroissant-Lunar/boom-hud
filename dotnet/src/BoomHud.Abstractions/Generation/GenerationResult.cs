namespace BoomHud.Abstractions.Generation;

/// <summary>
/// Result of code generation.
/// </summary>
public sealed record GenerationResult
{
    /// <summary>
    /// Generated files.
    /// </summary>
    public IReadOnlyList<GeneratedFile> Files { get; init; } = [];

    /// <summary>
    /// Diagnostics produced during generation.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Whether generation was successful (no errors).
    /// </summary>
    public bool Success => !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// Creates a successful result with the given files.
    /// </summary>
    public static GenerationResult Ok(params GeneratedFile[] files) => new() { Files = files };

    /// <summary>
    /// Creates a failed result with the given error.
    /// </summary>
    public static GenerationResult Fail(string message, string? location = null) => new()
    {
        Diagnostics = [Diagnostic.Error(message, location)]
    };
}

/// <summary>
/// A generated file.
/// </summary>
public sealed record GeneratedFile
{
    /// <summary>
    /// Relative path for the generated file.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Content of the generated file.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Type of generated file.
    /// </summary>
    public GeneratedFileType Type { get; init; } = GeneratedFileType.SourceCode;
}

/// <summary>
/// Type of generated file.
/// </summary>
public enum GeneratedFileType
{
    /// <summary>
    /// C# source code (.cs).
    /// </summary>
    SourceCode,

    /// <summary>
    /// XAML/AXAML markup (.axaml, .xaml).
    /// </summary>
    Markup,

    /// <summary>
    /// Resource file.
    /// </summary>
    Resource,

    /// <summary>
    /// Other file type.
    /// </summary>
    Other
}

/// <summary>
/// A diagnostic message from generation.
/// </summary>
public sealed record Diagnostic
{
    /// <summary>
    /// Severity of the diagnostic.
    /// </summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Diagnostic message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Location in source (if applicable).
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Diagnostic code for categorization.
    /// </summary>
    public string? Code { get; init; }

    public static Diagnostic Error(string message, string? location = null, string? code = null)
        => new() { Severity = DiagnosticSeverity.Error, Message = message, Location = location, Code = code };

    public static Diagnostic Warning(string message, string? location = null, string? code = null)
        => new() { Severity = DiagnosticSeverity.Warning, Message = message, Location = location, Code = code };

    public static Diagnostic Info(string message, string? location = null, string? code = null)
        => new() { Severity = DiagnosticSeverity.Info, Message = message, Location = location, Code = code };

    public override string ToString() => Location != null
        ? $"{Severity}: {Message} at {Location}"
        : $"{Severity}: {Message}";
}

/// <summary>
/// Severity levels for diagnostics.
/// </summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}
