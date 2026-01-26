// Multi-Source Composer for BoomHud
// Merges multiple HudDocuments into a single composed document

using BoomHud.Abstractions.Diagnostics;

namespace BoomHud.Abstractions.IR;

/// <summary>
/// Source identity for tracking where components/tokens originated.
/// Used for diagnostics when collisions occur.
/// </summary>
public sealed record SourceIdentity(
    string FilePath,
    string? NodeId = null,
    int? Line = null,
    int? Column = null)
{
    public override string ToString() => NodeId != null ? $"{FilePath} (node: {NodeId})" : FilePath;
}

/// <summary>
/// A document paired with its source identity for composition.
/// </summary>
public sealed record SourcedDocument(HudDocument Document, SourceIdentity Source);

/// <summary>
/// Result of multi-source composition.
/// </summary>
public sealed record CompositionResult(
    HudDocument? Document,
    IReadOnlyList<BoomHudDiagnostic> Diagnostics)
{
    public bool Success => Document != null && !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}

/// <summary>
/// Composes multiple HudDocuments into a single document.
/// Detects and reports collisions (BH0100 for components).
/// </summary>
public static class MultiSourceComposer
{
    /// <summary>
    /// Composes multiple documents into one.
    /// </summary>
    /// <param name="sources">Documents with their source identities.</param>
    /// <param name="rootComponentName">Optional: name of the component to use as root. If null, first document's root is used.</param>
    /// <returns>Composition result with merged document or diagnostics.</returns>
    public static CompositionResult Compose(IReadOnlyList<SourcedDocument> sources, string? rootComponentName = null)
    {
        if (sources.Count == 0)
        {
            return new CompositionResult(null, [new BoomHudDiagnostic(
                "BH0099",
                DiagnosticSeverity.Error,
                "No input documents to compose")]);
        }

        if (sources.Count == 1)
        {
            // Single source - no composition needed
            return new CompositionResult(sources[0].Document, []);
        }

        var diagnostics = new List<BoomHudDiagnostic>();

        // Track component definitions and their sources for collision detection
        var componentSources = new Dictionary<string, (HudComponentDefinition Def, SourceIdentity Source)>(StringComparer.OrdinalIgnoreCase);
        var mergedComponents = new Dictionary<string, HudComponentDefinition>(StringComparer.OrdinalIgnoreCase);

        // Track styles and their sources
        var styleSources = new Dictionary<string, (StyleSpec Style, SourceIdentity Source)>(StringComparer.OrdinalIgnoreCase);
        var mergedStyles = new Dictionary<string, StyleSpec>(StringComparer.OrdinalIgnoreCase);

        // Process each source document
        foreach (var (doc, source) in sources)
        {
            // Merge component definitions
            foreach (var (name, compDef) in doc.Components)
            {
                if (componentSources.TryGetValue(name, out var existing))
                {
                    // BH0100: Component name collision
                    diagnostics.Add(new BoomHudDiagnostic(
                        DiagnosticCodes.ComponentCollision,
                        DiagnosticSeverity.Error,
                        $"Component '{name}' defined in both '{existing.Source}' and '{source}'",
                        source.FilePath,
                        name));
                }
                else
                {
                    componentSources[name] = (compDef, source);
                    mergedComponents[name] = compDef;
                }
            }

            // Merge styles (same collision logic)
            foreach (var (name, style) in doc.Styles)
            {
                if (styleSources.TryGetValue(name, out var existing))
                {
                    // BH0110: Style collision - warn only (first wins)
                    diagnostics.Add(new BoomHudDiagnostic(
                        DiagnosticCodes.StyleCollision,
                        DiagnosticSeverity.Warning,
                        $"Style '{name}' collision: using '{existing.Source}' (winner), ignoring '{source}' (loser). Consider defining styles inside components or in a single theme file.",
                        source.FilePath,
                        name));
                }
                else
                {
                    styleSources[name] = (style, source);
                    mergedStyles[name] = style;
                }
            }
        }

        // If there are component collisions, fail early
        if (diagnostics.Any(d => d.Code == DiagnosticCodes.ComponentCollision))
        {
            return new CompositionResult(null, diagnostics);
        }

        // Determine the root document
        var rootDoc = DetermineRootDocument(sources, rootComponentName, diagnostics);
        if (rootDoc == null)
        {
            return new CompositionResult(null, diagnostics);
        }

        // Create composed document
        var composed = new HudDocument
        {
            Name = rootDoc.Document.Name,
            Metadata = rootDoc.Document.Metadata,
            Root = rootDoc.Document.Root,
            Components = mergedComponents,
            Styles = mergedStyles
        };

        return new CompositionResult(composed, diagnostics);
    }

    private static SourcedDocument? DetermineRootDocument(
        IReadOnlyList<SourcedDocument> sources,
        string? rootComponentName,
        List<BoomHudDiagnostic> diagnostics)
    {
        if (string.IsNullOrEmpty(rootComponentName))
        {
            // Default: use first document's root
            return sources[0];
        }

        // Find document with matching root name
        var matching = sources.FirstOrDefault(s =>
            s.Document.Name.Equals(rootComponentName, StringComparison.OrdinalIgnoreCase));

        if (matching == null)
        {
            diagnostics.Add(new BoomHudDiagnostic(
                DiagnosticCodes.RootNotFound,
                DiagnosticSeverity.Error,
                $"Root component '{rootComponentName}' not found in any input document. Available: {string.Join(", ", sources.Select(s => s.Document.Name))}"));
            return null;
        }

        return matching;
    }
}
