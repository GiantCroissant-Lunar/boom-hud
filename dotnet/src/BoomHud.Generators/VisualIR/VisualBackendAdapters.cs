namespace BoomHud.Generators.VisualIR;

public sealed record VisualResolvedNode(
    VisualNode Node,
    MetricProfileDefinition? MetricProfile);

public sealed class VisualToUnityToolkitPlan
{
    private readonly Dictionary<string, VisualResolvedNode> _nodesBySourceId;

    private VisualToUnityToolkitPlan(Dictionary<string, VisualResolvedNode> nodesBySourceId)
    {
        _nodesBySourceId = nodesBySourceId;
    }

    public static VisualToUnityToolkitPlan? Build(VisualDocument? document)
        => document == null ? null : new VisualToUnityToolkitPlan(VisualBackendAdapterIndex.Create(document));

    public VisualResolvedNode? Resolve(string? sourceId)
        => string.IsNullOrWhiteSpace(sourceId) || !_nodesBySourceId.TryGetValue(sourceId, out var resolved) ? null : resolved;

}

public sealed class VisualToUGuiPlan
{
    private readonly Dictionary<string, VisualResolvedNode> _nodesBySourceId;

    private VisualToUGuiPlan(Dictionary<string, VisualResolvedNode> nodesBySourceId)
    {
        _nodesBySourceId = nodesBySourceId;
    }

    public static VisualToUGuiPlan? Build(VisualDocument? document)
        => document == null ? null : new VisualToUGuiPlan(VisualBackendAdapterIndex.Create(document));

    public VisualResolvedNode? Resolve(string? sourceId)
        => string.IsNullOrWhiteSpace(sourceId) || !_nodesBySourceId.TryGetValue(sourceId, out var resolved) ? null : resolved;
}

public sealed class VisualToReactPlan
{
    private readonly Dictionary<string, VisualResolvedNode> _nodesBySourceId;

    private VisualToReactPlan(Dictionary<string, VisualResolvedNode> nodesBySourceId)
    {
        _nodesBySourceId = nodesBySourceId;
    }

    public static VisualToReactPlan? Build(VisualDocument? document)
        => document == null ? null : new VisualToReactPlan(VisualBackendAdapterIndex.Create(document));

    public VisualResolvedNode? Resolve(string? sourceId)
        => string.IsNullOrWhiteSpace(sourceId) || !_nodesBySourceId.TryGetValue(sourceId, out var resolved) ? null : resolved;

}

internal static class VisualBackendAdapterIndex
{
    public static Dictionary<string, VisualResolvedNode> Create(VisualDocument document)
    {
        var profiles = document.MetricProfiles.ToDictionary(static profile => profile.Id, StringComparer.Ordinal);
        var result = new Dictionary<string, VisualResolvedNode>(StringComparer.Ordinal);
        IndexNode(document.Root, profiles, result);
        foreach (var component in document.Components)
        {
            IndexNode(component.Root, profiles, result);
        }

        return result;
    }

    private static void IndexNode(
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
            IndexNode(child, profiles, result);
        }
    }
}
