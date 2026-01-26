// BoomHud Diagnostic Codes
// Standardized error/warning codes for consistent tooling integration

namespace BoomHud.Abstractions.Diagnostics;

/// <summary>
/// Diagnostic severity levels.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>Informational message.</summary>
    Info,
    /// <summary>Warning - generation continues.</summary>
    Warning,
    /// <summary>Error - generation fails.</summary>
    Error
}

/// <summary>
/// A diagnostic message with structured information for tooling.
/// </summary>
public sealed record BoomHudDiagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    string? SourceFile = null,
    string? NodeId = null,
    int? Line = null,
    int? Column = null)
{
    /// <summary>
    /// Formats the diagnostic for console output.
    /// Format: [CODE] severity: message (at source:line)
    /// </summary>
    public override string ToString()
    {
        var location = FormatLocation();
        return $"[{Code}] {Severity.ToString().ToLowerInvariant()}: {Message}{location}";
    }

    private string FormatLocation()
    {
        if (string.IsNullOrEmpty(SourceFile) && string.IsNullOrEmpty(NodeId))
            return "";

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(SourceFile))
        {
            var loc = SourceFile;
            if (Line.HasValue)
            {
                loc += $":{Line}";
                if (Column.HasValue)
                    loc += $":{Column}";
            }
            parts.Add(loc);
        }
        if (!string.IsNullOrEmpty(NodeId))
            parts.Add($"node: {NodeId}");

        return $" (at {string.Join(", ", parts)})";
    }
}

/// <summary>
/// Well-known BoomHud diagnostic codes.
/// </summary>
/// <remarks>
/// Code ranges:
/// - BH0100-BH0109: Component/document collisions and identity errors
/// - BH0110-BH0119: Style collisions
/// - BH0120-BH0129: Naming convention warnings (lint)
/// - BH0102-BH0109: Token resolution errors (legacy placement, kept for compatibility)
/// - BH0300-BH0399: Binding errors
/// - BH0400-BH0499: Generation errors
/// - BH0500-BH0599: Layout warnings (lint, future)
/// </remarks>
public static class DiagnosticCodes
{
    // === Collision Errors (BH010x) ===

    /// <summary>
    /// BH0100: Component name collision.
    /// Two components have the same ID after normalization.
    /// </summary>
    public const string ComponentCollision = "BH0100";

    /// <summary>
    /// BH0101: Token name collision.
    /// Two tokens have the same qualified name (e.g., "colors.primary" defined twice).
    /// </summary>
    public const string TokenCollision = "BH0101";

    // === Token Errors (BH0102-BH0109) ===
    // Note: These are in the 01xx range for historical reasons; kept for compatibility.

    /// <summary>
    /// BH0102: Unresolved token reference.
    /// A token reference like "$token(colors.missing)" could not be resolved.
    /// </summary>
    public const string UnresolvedTokenRef = "BH0102";

    /// <summary>
    /// BH0103: Deprecated token used.
    /// A token marked as deprecated is being referenced.
    /// </summary>
    public const string DeprecatedToken = "BH0103";

    /// <summary>
    /// BH0104: Inline token warning.
    /// A literal value is used where a token reference is preferred.
    /// </summary>
    public const string InlineTokenWarning = "BH0104";

    /// <summary>
    /// BH0105: Token category mismatch.
    /// A token is used in a context that expects a different category (e.g., color token used as spacing).
    /// </summary>
    public const string TokenCategoryMismatch = "BH0105";

    // === Binding Errors (BH03xx) ===

    /// <summary>
    /// BH0300: Invalid binding expression.
    /// A binding expression could not be parsed.
    /// </summary>
    public const string InvalidBinding = "BH0300";

    /// <summary>
    /// BH0301: Binding target not found.
    /// The target path in a binding does not exist.
    /// </summary>
    public const string BindingTargetNotFound = "BH0301";

    // === Style/Composition Errors (BH011x) ===

    /// <summary>
    /// BH0110: Style collision.
    /// Two styles have the same name across different sources (first-wins, warning).
    /// </summary>
    public const string StyleCollision = "BH0110";

    /// <summary>
    /// BH0111: Root not found.
    /// The specified root component was not found in any input document.
    /// </summary>
    public const string RootNotFound = "BH0111";

    // === Generation Errors (BH04xx) ===

    /// <summary>
    /// BH0400: Unsupported feature for backend.
    /// The requested feature is not supported by the target backend.
    /// </summary>
    public const string UnsupportedFeature = "BH0400";

    /// <summary>
    /// BH0401: Invalid IR structure.
    /// The IR document has an invalid structure.
    /// </summary>
    public const string InvalidIrStructure = "BH0401";
}

/// <summary>
/// Factory methods for creating common diagnostics.
/// </summary>
public static class Diagnostics
{
    /// <summary>
    /// Creates a BH0100 component collision error.
    /// </summary>
    public static BoomHudDiagnostic ComponentCollision(string componentId, string? sourceFile = null)
        => new(
            DiagnosticCodes.ComponentCollision,
            DiagnosticSeverity.Error,
            $"Component ID '{componentId}' is defined multiple times",
            sourceFile);

    /// <summary>
    /// Creates a BH0101 token collision error.
    /// </summary>
    public static BoomHudDiagnostic TokenCollision(string tokenRef, string source1, string source2)
        => new(
            DiagnosticCodes.TokenCollision,
            DiagnosticSeverity.Error,
            $"Token '{tokenRef}' is defined in multiple sources: {source1}, {source2}");

    /// <summary>
    /// Creates a BH0102 unresolved token reference error.
    /// </summary>
    public static BoomHudDiagnostic UnresolvedTokenRef(string tokenRef, string? sourceFile = null, string? nodeId = null)
        => new(
            DiagnosticCodes.UnresolvedTokenRef,
            DiagnosticSeverity.Error,
            $"Unresolved token reference: '{tokenRef}'",
            sourceFile,
            nodeId);

    /// <summary>
    /// Creates a BH0103 deprecated token warning.
    /// </summary>
    public static BoomHudDiagnostic DeprecatedToken(string tokenRef, string? sourceFile = null, string? nodeId = null)
        => new(
            DiagnosticCodes.DeprecatedToken,
            DiagnosticSeverity.Warning,
            $"Token '{tokenRef}' is deprecated",
            sourceFile,
            nodeId);

    /// <summary>
    /// Creates a BH0104 inline token warning.
    /// </summary>
    public static BoomHudDiagnostic InlineTokenWarning(string value, string fieldName, string? sourceFile = null, string? nodeId = null)
        => new(
            DiagnosticCodes.InlineTokenWarning,
            DiagnosticSeverity.Warning,
            $"Inline value '{value}' used for {fieldName}; consider using a token reference",
            sourceFile,
            nodeId);

    /// <summary>
    /// Creates a BH0400 unsupported feature error.
    /// </summary>
    public static BoomHudDiagnostic UnsupportedFeature(string feature, string backend)
        => new(
            DiagnosticCodes.UnsupportedFeature,
            DiagnosticSeverity.Error,
            $"Feature '{feature}' is not supported by backend '{backend}'");
}
