namespace BoomHud.Generators;

/// <summary>
/// Shared emission helpers for backend generators. Centralizes string-escaping so a fix
/// applies to every backend at once (the divergent per-generator copies are what allowed
/// the Godot <c>.tscn</c>/C# escaping bug to exist).
/// </summary>
/// <remarks>
/// Target-specific escapers that are genuinely different stay in their own backends:
/// React emits single-quoted TypeScript literals, and XML/XAML attribute escaping differs
/// from C# string escaping.
/// </remarks>
public static class GeneratorEmit
{
    /// <summary>
    /// Escapes a string for embedding inside a C# (or Godot <c>.tscn</c>) double-quoted
    /// string literal. Does NOT add the surrounding quotes. Backslash is escaped first.
    /// </summary>
    public static string EscapeCSharpString(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\n", "\\n")
        .Replace("\r", "\\r")
        .Replace("\t", "\\t");

    /// <summary>
    /// Returns a complete double-quoted C# string literal for <paramref name="value"/>,
    /// with its contents escaped by <see cref="EscapeCSharpString"/>.
    /// </summary>
    public static string CSharpStringLiteral(string value) => "\"" + EscapeCSharpString(value) + "\"";
}
