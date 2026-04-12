namespace BoomHud.Generators.VisualIR;

public sealed record VisualResolvedNode(
    VisualNode Node,
    MetricProfileDefinition? MetricProfile);

public sealed class VisualToUnityToolkitPlan
{
    private readonly Dictionary<string, VisualResolvedNode> _nodesBySourceId;
    private readonly Dictionary<string, VisualResolvedNode> _nodesByDocumentPath;

    private VisualToUnityToolkitPlan(Dictionary<string, VisualResolvedNode> nodesBySourceId, Dictionary<string, VisualResolvedNode> nodesByDocumentPath)
    {
        _nodesBySourceId = nodesBySourceId;
        _nodesByDocumentPath = nodesByDocumentPath;
    }

    public static VisualToUnityToolkitPlan? Build(VisualDocument? document)
        => document == null ? null : new VisualToUnityToolkitPlan(
            VisualBackendAdapterIndex.CreateBySourceId(document),
            VisualBackendAdapterIndex.CreateByDocumentPath(document));

    public VisualResolvedNode? Resolve(string? sourceId, string? documentName = null, string? relativePath = null)
    {
        if (!string.IsNullOrWhiteSpace(documentName)
            && !string.IsNullOrWhiteSpace(relativePath)
            && _nodesByDocumentPath.TryGetValue(VisualBackendAdapterIndex.BuildDocumentPathKey(documentName, relativePath), out var byPath))
        {
            return byPath;
        }

        return string.IsNullOrWhiteSpace(sourceId) || !_nodesBySourceId.TryGetValue(sourceId, out var resolved) ? null : resolved;
    }

}

public sealed class VisualToUGuiPlan
{
    private readonly Dictionary<string, VisualResolvedNode> _nodesBySourceId;
    private readonly Dictionary<string, VisualResolvedNode> _nodesByDocumentPath;

    private VisualToUGuiPlan(Dictionary<string, VisualResolvedNode> nodesBySourceId, Dictionary<string, VisualResolvedNode> nodesByDocumentPath)
    {
        _nodesBySourceId = nodesBySourceId;
        _nodesByDocumentPath = nodesByDocumentPath;
    }

    public static VisualToUGuiPlan? Build(VisualDocument? document)
        => document == null ? null : new VisualToUGuiPlan(
            VisualBackendAdapterIndex.CreateBySourceId(document),
            VisualBackendAdapterIndex.CreateByDocumentPath(document));

    public VisualResolvedNode? Resolve(string? sourceId, string? documentName = null, string? relativePath = null)
    {
        if (!string.IsNullOrWhiteSpace(documentName)
            && !string.IsNullOrWhiteSpace(relativePath)
            && _nodesByDocumentPath.TryGetValue(VisualBackendAdapterIndex.BuildDocumentPathKey(documentName, relativePath), out var byPath))
        {
            return byPath;
        }

        return string.IsNullOrWhiteSpace(sourceId) || !_nodesBySourceId.TryGetValue(sourceId, out var resolved) ? null : resolved;
    }
}

public sealed class VisualToReactPlan
{
    private readonly Dictionary<string, VisualResolvedNode> _nodesBySourceId;
    private readonly Dictionary<string, VisualResolvedNode> _nodesByDocumentPath;

    private VisualToReactPlan(Dictionary<string, VisualResolvedNode> nodesBySourceId, Dictionary<string, VisualResolvedNode> nodesByDocumentPath)
    {
        _nodesBySourceId = nodesBySourceId;
        _nodesByDocumentPath = nodesByDocumentPath;
    }

    public static VisualToReactPlan? Build(VisualDocument? document)
        => document == null ? null : new VisualToReactPlan(
            VisualBackendAdapterIndex.CreateBySourceId(document),
            VisualBackendAdapterIndex.CreateByDocumentPath(document));

    public VisualResolvedNode? Resolve(string? sourceId, string? documentName = null, string? relativePath = null)
    {
        if (!string.IsNullOrWhiteSpace(documentName)
            && !string.IsNullOrWhiteSpace(relativePath)
            && _nodesByDocumentPath.TryGetValue(VisualBackendAdapterIndex.BuildDocumentPathKey(documentName, relativePath), out var byPath))
        {
            return byPath;
        }

        return string.IsNullOrWhiteSpace(sourceId) || !_nodesBySourceId.TryGetValue(sourceId, out var resolved) ? null : resolved;
    }

}

internal static class VisualBackendAdapterIndex
{
    public static Dictionary<string, VisualResolvedNode> CreateBySourceId(VisualDocument document)
    {
        var profiles = document.MetricProfiles.ToDictionary(static profile => profile.Id, StringComparer.Ordinal);
        var result = new Dictionary<string, VisualResolvedNode>(StringComparer.Ordinal);
        IndexNodeBySourceId(document.Root, profiles, result);
        foreach (var component in document.Components)
        {
            IndexNodeBySourceId(component.Root, profiles, result);
        }

        return result;
    }

    public static Dictionary<string, VisualResolvedNode> CreateByDocumentPath(VisualDocument document)
    {
        var profiles = document.MetricProfiles.ToDictionary(static profile => profile.Id, StringComparer.Ordinal);
        var result = new Dictionary<string, VisualResolvedNode>(StringComparer.Ordinal);
        IndexNodeByDocumentPath(document.DocumentName, "$", document.Root, profiles, result);
        foreach (var component in document.Components)
        {
            IndexNodeByDocumentPath(component.Name, "$", component.Root, profiles, result);
        }

        return result;
    }

    public static string BuildDocumentPathKey(string documentName, string relativePath)
        => $"{documentName}|{relativePath}";

    private static void IndexNodeBySourceId(
        VisualNode node,
        IReadOnlyDictionary<string, MetricProfileDefinition> profiles,
        IDictionary<string, VisualResolvedNode> result)
    {
        if (!string.IsNullOrWhiteSpace(node.SourceId))
        {
            profiles.TryGetValue(node.MetricProfileId ?? string.Empty, out var profile);
            result[node.SourceId] = new VisualResolvedNode(node, profile);
        }

        foreach (var child in node.Children)
        {
            IndexNodeBySourceId(child, profiles, result);
        }
    }

    private static void IndexNodeByDocumentPath(
        string documentName,
        string relativePath,
        VisualNode node,
        IReadOnlyDictionary<string, MetricProfileDefinition> profiles,
        IDictionary<string, VisualResolvedNode> result)
    {
        profiles.TryGetValue(node.MetricProfileId ?? string.Empty, out var profile);
        result[BuildDocumentPathKey(documentName, relativePath)] = new VisualResolvedNode(node, profile);

        for (var index = 0; index < node.Children.Count; index++)
        {
            IndexNodeByDocumentPath(documentName, $"{relativePath}/{index}", node.Children[index], profiles, result);
        }
    }
}
