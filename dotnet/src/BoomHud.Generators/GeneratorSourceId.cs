using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators;

/// <summary>
/// Shared drift-detection helpers used by the backend generators. Computes a stable,
/// backend-independent source id from a <see cref="HudDocument"/> structure and collects
/// normalized pseudo-node metadata for embedding into generated views.
/// </summary>
/// <remarks>
/// This logic was previously duplicated verbatim in the Godot, Terminal.Gui, and Avalonia
/// generators. The source id is persisted by consumers for drift detection, so the hashing
/// and tree-walk here MUST remain byte-stable; <c>GeneratorSourceIdTests</c> pins the exact
/// hash for a known document.
/// </remarks>
public static class GeneratorSourceId
{
    /// <summary>Computes the stable <c>sha256:</c>-prefixed source id for a document.</summary>
    public static string ComputeSourceId(HudDocument document)
    {
        var sb = new StringBuilder();
        sb.Append("doc:").Append(document.Name).Append('\n');
        AppendNode(sb, document.Root);
        return "sha256:" + ComputeSha256Hex(sb.ToString());
    }

    /// <summary>Collects <c>path|originalType|mappedType</c> entries for pseudo-normalized nodes, ordered.</summary>
    public static List<string> CollectNormalizedPseudoNodes(HudDocument document)
    {
        var results = new List<string>();
        CollectNormalizedPseudoNodes(document.Root, currentPath: [], results);
        results.Sort(StringComparer.Ordinal);
        return results;
    }

    /// <summary>Formats a list of strings as a C# array literal with fully-escaped string elements.</summary>
    public static string FormatStringArrayLiteral(List<string> items)
    {
        if (items.Count == 0)
        {
            return "new string[0]";
        }

        return "new[] { " + string.Join(", ", items.Select(s => "\"" + EscapeCSharpString(s) + "\"")) + " }";
    }

    private static void CollectNormalizedPseudoNodes(ComponentNode node, List<string> currentPath, List<string> results)
    {
        var nextPath = new List<string>(currentPath);
        if (!string.IsNullOrWhiteSpace(node.Id))
        {
            nextPath.Add(node.Id);
        }

        if (node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.NormalizedFromPseudoType, out var normalized)
            && normalized is bool normalizedBool
            && normalizedBool
            && node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.OriginalFigmaType, out var original)
            && original is string originalStr)
        {
            results.Add($"{string.Join("/", nextPath)}|{originalStr}|{node.Type}");
        }

        foreach (var child in node.Children)
        {
            CollectNormalizedPseudoNodes(child, nextPath, results);
        }
    }

    private static void AppendNode(StringBuilder sb, ComponentNode node)
    {
        sb.Append("node:")
            .Append(node.Type.ToString()).Append('|')
            .Append(node.Id ?? string.Empty).Append('|')
            .Append(node.SlotKey ?? string.Empty).Append('|')
            .Append(node.ComponentRefId ?? string.Empty).Append('\n');

        foreach (var b in node.Bindings.OrderBy(b => b.Property, StringComparer.Ordinal).ThenBy(b => b.Path, StringComparer.Ordinal))
        {
            sb.Append("bind:")
                .Append(b.Property).Append('|')
                .Append(b.Path).Append('|')
                .Append(b.Key ?? string.Empty).Append('|')
                .Append(b.Format ?? string.Empty).Append('\n');
        }

        if (node.Command != null)
        {
            sb.Append("cmd:").Append(node.Command).Append('\n');
        }

        foreach (var child in node.Children)
        {
            AppendNode(sb, child);
        }
    }

    private static string ComputeSha256Hex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        var hex = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }
        return hex.ToString();
    }

    private static string EscapeCSharpString(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\n", "\\n")
        .Replace("\r", "\\r")
        .Replace("\t", "\\t");
}
