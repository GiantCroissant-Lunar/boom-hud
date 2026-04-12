using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators.VisualIR;

internal static class VisualSynthesisPlanner
{
    private const int MinimumOccurrenceCount = 2;
    private const int MinimumNodeCount = 3;
    private const int MinimumDepth = 2;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static VisualSynthesisResult Synthesize(VisualDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var analyzed = AnalyzeSubtree(document.Root, []);
        var candidates = analyzed.AllSubtrees
            .Where(static subtree => subtree.IsEligible)
            .GroupBy(static subtree => subtree.Signature, StringComparer.Ordinal)
            .Select(group => new CandidateGroup(group.Key, group.ToList()))
            .Where(static group => group.Occurrences.Count >= MinimumOccurrenceCount)
            .Select(group => group with
            {
                NodeCount = group.Occurrences[0].NodeCount,
                Depth = group.Occurrences[0].Depth,
                SignatureHash = ComputeHash(group.Signature)
            })
            .OrderByDescending(static group => group.Occurrences.Count)
            .ThenByDescending(static group => group.NodeCount)
            .ThenByDescending(static group => group.Depth)
            .ThenByDescending(static group => group.Occurrences[0].LayoutTextIconWeight)
            .ThenBy(static group => group.Occurrences[0].TraversalOrder)
            .ToList();

        if (candidates.Count == 0)
        {
            return new VisualSynthesisResult(document, new VisualSynthesisSummary
            {
                CandidateFamilyCount = 0,
                ChosenFamilyCount = 0,
                RewrittenOccurrenceCount = 0
            });
        }

        var existingIds = new HashSet<string>(document.Components.Select(static component => component.Id), StringComparer.Ordinal);
        var existingNames = new HashSet<string>(document.Components.Select(static component => component.Name), StringComparer.Ordinal);
        var consumedRoots = new List<int[]>();
        var selected = new List<SelectedGroup>();
        var replacementByPath = new Dictionary<string, ReplacementInfo>(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var availableOccurrences = candidate.Occurrences
                .Where(occurrence => !OverlapsAny(occurrence.Path, consumedRoots))
                .ToList();

            if (availableOccurrences.Count < MinimumOccurrenceCount)
            {
                continue;
            }

            var componentId = ReserveComponentId($"visual:{candidate.SignatureHash[..12]}", existingIds);
            var componentName = ReserveComponentName(
                $"Visual{availableOccurrences[0].Node.SourceType}{candidate.SignatureHash[..8].ToUpperInvariant()}",
                existingNames);

            var componentRoot = CloneNode(availableOccurrences[0].Node);
            foreach (var occurrence in availableOccurrences)
            {
                consumedRoots.Add(occurrence.Path);
                replacementByPath[occurrence.PathKey] = new ReplacementInfo(
                    componentId,
                    componentName,
                    occurrence.Node.SourceType,
                    BuildPropertyOverrides(componentRoot, occurrence.Node, ComponentInstanceOverrideSupport.RootPath));
            }

            selected.Add(new SelectedGroup(candidate, componentId, componentName, componentRoot, availableOccurrences));
        }

        if (selected.Count == 0)
        {
            return new VisualSynthesisResult(document, new VisualSynthesisSummary
            {
                CandidateFamilyCount = candidates.Count,
                ChosenFamilyCount = 0,
                RewrittenOccurrenceCount = 0
            });
        }

        var rewrittenRoot = RewriteNode(document.Root, [], replacementByPath);
        var rewrittenComponents = document.Components
            .Concat(selected.Select(group => new VisualComponentDefinition
            {
                Id = group.ComponentId,
                Name = group.ComponentName,
                Root = group.ComponentRoot
            }))
            .OrderBy(static component => component.Id, StringComparer.Ordinal)
            .ToList();

        return new VisualSynthesisResult(
            document with
            {
                Root = rewrittenRoot,
                Components = rewrittenComponents
            },
            new VisualSynthesisSummary
            {
                CandidateFamilyCount = candidates.Count,
                ChosenFamilyCount = selected.Count,
                RewrittenOccurrenceCount = selected.Sum(static group => group.Occurrences.Count),
                ComponentFamilies = selected
                    .Select(group => new VisualSynthesisFamilySummary
                    {
                        ComponentId = group.ComponentId,
                        ComponentName = group.ComponentName,
                        RootType = group.Occurrences[0].Node.SourceType.ToString(),
                        OccurrenceCount = group.Occurrences.Count,
                        NodeCount = group.Candidate.NodeCount,
                        Depth = group.Candidate.Depth,
                        SignatureHash = group.Candidate.SignatureHash,
                        OverridePathCount = group.Occurrences.Sum(occurrence => BuildPropertyOverrides(group.ComponentRoot, occurrence.Node, ComponentInstanceOverrideSupport.RootPath).Count)
                    })
                    .ToList()
            });
    }

    public static string ToJson(VisualSynthesisSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    private static AnalyzedSubtree AnalyzeSubtree(VisualNode node, int[] path)
    {
        var childSubtrees = new List<AnalyzedSubtree>();
        var descendantCount = 1;
        var depth = 1;
        var layoutTextIconWeight = ScoreLayoutTextIconMix(node);
        var traversalOrder = ComputeTraversalOrder(path);

        for (var index = 0; index < node.Children.Count; index++)
        {
            var childPath = path.Concat([index]).ToArray();
            var child = AnalyzeSubtree(node.Children[index], childPath);
            childSubtrees.Add(child);
            descendantCount += child.NodeCount;
            depth = Math.Max(depth, child.Depth + 1);
            layoutTextIconWeight += child.LayoutTextIconWeight;
        }

        var hasComponentReference = node.ComponentRefId != null || childSubtrees.Any(static child => child.HasComponentReference);
        var signature = BuildSignature(node, childSubtrees);
        var allSubtrees = childSubtrees.SelectMany(static child => child.AllSubtrees).ToList();
        var subtree = new AnalyzedSubtree(
            node,
            path,
            PathKey(path),
            signature,
            descendantCount,
            depth,
            traversalOrder,
            layoutTextIconWeight,
            hasComponentReference,
            IsEligible(node, descendantCount, depth, hasComponentReference),
            allSubtrees);

        allSubtrees.Insert(0, subtree);
        return subtree;
    }

    private static bool IsEligible(VisualNode node, int nodeCount, int depth, bool hasComponentReference)
    {
        if (node.Children.Count == 0)
        {
            return false;
        }

        if (hasComponentReference)
        {
            return false;
        }

        return nodeCount >= MinimumNodeCount || depth >= MinimumDepth;
    }

    private static string BuildSignature(VisualNode node, IReadOnlyList<AnalyzedSubtree> childSubtrees)
    {
        var builder = new StringBuilder();
        builder.Append(node.Kind).Append('|');
        builder.Append(node.SourceType).Append('|');
        builder.Append(node.SemanticClass ?? string.Empty).Append('|');
        builder.Append(SerializeBox(node.Box)).Append('|');
        builder.Append(SerializeEdge(node.EdgeContract)).Append('|');
        builder.Append(SerializeTypography(node.Typography)).Append('|');
        builder.Append(SerializeIcon(node.Icon)).Append('|');
        builder.Append(SerializeStaticProperties(node.StaticProperties)).Append('|');
        builder.Append(childSubtrees.Count).Append('|');
        foreach (var child in childSubtrees)
        {
            builder.Append('[').Append(child.Signature).Append(']');
        }

        return builder.ToString();
    }

    private static SortedDictionary<string, SortedDictionary<string, object?>> BuildPropertyOverrides(VisualNode canonical, VisualNode occurrence, string path)
    {
        var result = new SortedDictionary<string, SortedDictionary<string, object?>>(StringComparer.Ordinal);

        foreach (var pair in occurrence.StaticProperties)
        {
            if (!canonical.StaticProperties.TryGetValue(pair.Key, out var canonicalValue))
            {
                continue;
            }

            if (!Equals(canonicalValue, pair.Value))
            {
                if (!result.TryGetValue(path, out var propertyBag))
                {
                    propertyBag = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                    result[path] = propertyBag;
                }

                propertyBag[pair.Key] = pair.Value;
            }
        }

        for (var index = 0; index < canonical.Children.Count && index < occurrence.Children.Count; index++)
        {
            foreach (var childPair in BuildPropertyOverrides(canonical.Children[index], occurrence.Children[index], ComponentInstanceOverrideSupport.ChildPath(path, index)))
            {
                result[childPair.Key] = childPair.Value;
            }
        }

        return result;
    }

    private static VisualNode RewriteNode(VisualNode node, int[] path, IReadOnlyDictionary<string, ReplacementInfo> replacementByPath)
    {
        var pathKey = PathKey(path);
        if (replacementByPath.TryGetValue(pathKey, out var replacement))
        {
            return new VisualNode
            {
                StableId = node.StableId,
                SourceId = node.SourceId,
                SourceNodeId = node.SourceNodeId,
                Kind = node.Kind,
                SourceType = replacement.Type,
                ComponentRefId = replacement.ComponentId,
                SemanticClass = node.SemanticClass,
                MetricProfileId = node.MetricProfileId,
                StaticProperties = new Dictionary<string, object?>(StringComparer.Ordinal),
                PropertyOverrides = replacement.PropertyOverrides.ToDictionary(
                    static pair => pair.Key,
                    static pair => (IReadOnlyDictionary<string, object?>)pair.Value,
                    StringComparer.Ordinal),
                Box = node.Box,
                EdgeContract = node.EdgeContract,
                Typography = node.Typography,
                Icon = node.Icon,
                Children = []
            };
        }

        return node with
        {
            Children = node.Children
                .Select((child, index) => RewriteNode(child, path.Concat([index]).ToArray(), replacementByPath))
                .ToList()
        };
    }

    private static VisualNode CloneNode(VisualNode node)
        => node with
        {
            StaticProperties = node.StaticProperties.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal),
            PropertyOverrides = node.PropertyOverrides.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyDictionary<string, object?>)pair.Value.ToDictionary(
                    static nested => nested.Key,
                    static nested => nested.Value,
                    StringComparer.Ordinal),
                StringComparer.Ordinal),
            Children = node.Children.Select(CloneNode).ToList()
        };

    private static string SerializeBox(VisualBox box)
        => string.Join(";",
            box.SourceType,
            box.LayoutType?.ToString() ?? string.Empty,
            box.Width?.ToString() ?? string.Empty,
            box.Height?.ToString() ?? string.Empty,
            box.MinWidth?.ToString() ?? string.Empty,
            box.MinHeight?.ToString() ?? string.Empty,
            box.MaxWidth?.ToString() ?? string.Empty,
            box.MaxHeight?.ToString() ?? string.Empty,
            box.Gap?.ToString() ?? string.Empty,
            box.Padding?.ToString() ?? string.Empty,
            box.Margin?.ToString() ?? string.Empty,
            box.Left?.ToString() ?? string.Empty,
            box.Top?.ToString() ?? string.Empty,
            box.IsAbsolutePositioned,
            box.ClipContent,
            box.Align?.ToString() ?? string.Empty,
            box.Justify?.ToString() ?? string.Empty,
            box.Weight?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty,
            box.Background?.ToString() ?? string.Empty,
            box.Border?.ToString() ?? string.Empty,
            box.BorderRadius?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty,
            box.Opacity?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty);

    private static string SerializeEdge(EdgeContract edge)
        => string.Join(";",
            edge.Participation,
            edge.WidthSizing,
            edge.HeightSizing,
            edge.HorizontalPin,
            edge.VerticalPin,
            edge.OverflowX,
            edge.OverflowY,
            edge.WrapPressure);

    private static string SerializeTypography(TypographyContract? typography)
        => typography == null
            ? "null"
            : string.Join(";",
                typography.SemanticClass,
                typography.ResolvedFontFamily ?? string.Empty,
                typography.ResolvedFontSize?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty,
                typography.ResolvedLineHeight?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty,
                typography.ResolvedLetterSpacing?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty,
                typography.WrapText,
                typography.TextGrowth ?? string.Empty,
                typography.SizeBand ?? string.Empty);

    private static string SerializeIcon(IconContract? icon)
        => icon == null
            ? "null"
            : string.Join(";",
                icon.SemanticClass,
                icon.ResolvedFontFamily ?? string.Empty,
                icon.ResolvedFontSize?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty,
                icon.BaselineOffset.ToString("0.####", CultureInfo.InvariantCulture),
                icon.OpticalCentering,
                icon.SizeMode,
                icon.SizeBand ?? string.Empty);

    private static string SerializeStaticProperties(IReadOnlyDictionary<string, object?> properties)
    {
        if (properties.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(",",
            properties
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=override:" + SerializeObject(pair.Value)));
    }

    private static string SerializeObject(object? value)
        => value switch
        {
            null => "null",
            string stringValue => stringValue,
            bool boolValue => boolValue ? "true" : "false",
            byte byteValue => byteValue.ToString(CultureInfo.InvariantCulture),
            sbyte sbyteValue => sbyteValue.ToString(CultureInfo.InvariantCulture),
            short shortValue => shortValue.ToString(CultureInfo.InvariantCulture),
            ushort ushortValue => ushortValue.ToString(CultureInfo.InvariantCulture),
            int intValue => intValue.ToString(CultureInfo.InvariantCulture),
            uint uintValue => uintValue.ToString(CultureInfo.InvariantCulture),
            long longValue => longValue.ToString(CultureInfo.InvariantCulture),
            ulong ulongValue => ulongValue.ToString(CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString("0.####", CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("0.####", CultureInfo.InvariantCulture),
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            _ => JsonSerializer.Serialize(value, JsonOptions)
        };

    private static string ReserveComponentId(string candidate, HashSet<string> existingIds)
    {
        var result = candidate;
        var suffix = 2;
        while (!existingIds.Add(result))
        {
            result = candidate + "-" + suffix.ToString(CultureInfo.InvariantCulture);
            suffix++;
        }

        return result;
    }

    private static string ReserveComponentName(string candidate, HashSet<string> existingNames)
    {
        var result = candidate;
        var suffix = 2;
        while (!existingNames.Add(result))
        {
            result = candidate + suffix.ToString(CultureInfo.InvariantCulture);
            suffix++;
        }

        return result;
    }

    private static bool OverlapsAny(int[] path, IReadOnlyList<int[]> consumedRoots)
        => consumedRoots.Any(consumed => PathsOverlap(path, consumed));

    private static bool PathsOverlap(IReadOnlyList<int> left, IReadOnlyList<int> right)
        => IsPrefix(left, right) || IsPrefix(right, left);

    private static bool IsPrefix(IReadOnlyList<int> prefix, IReadOnlyList<int> path)
    {
        if (prefix.Count > path.Count)
        {
            return false;
        }

        for (var index = 0; index < prefix.Count; index++)
        {
            if (prefix[index] != path[index])
            {
                return false;
            }
        }

        return true;
    }

    private static int ComputeTraversalOrder(IReadOnlyList<int> path)
    {
        var result = 0;
        foreach (var segment in path)
        {
            result = (result * 131) + segment + 1;
        }

        return result;
    }

    private static int ScoreLayoutTextIconMix(VisualNode node)
    {
        var score = node.Kind switch
        {
            VisualNodeKind.Container or VisualNodeKind.Collection => 4,
            VisualNodeKind.Text or VisualNodeKind.Icon => 2,
            _ => 1
        };

        if (node.Box.LayoutType != null)
        {
            score += 2;
        }

        if (node.Typography != null || node.Icon != null)
        {
            score += 1;
        }

        return score;
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string PathKey(IEnumerable<int> path)
        => string.Join("/", path);

    internal sealed record VisualSynthesisResult(VisualDocument Document, VisualSynthesisSummary Summary);

    private sealed record CandidateGroup(string Signature, List<AnalyzedSubtree> Occurrences)
    {
        public int NodeCount { get; init; }

        public int Depth { get; init; }

        public string SignatureHash { get; init; } = string.Empty;
    }

    private sealed record SelectedGroup(
        CandidateGroup Candidate,
        string ComponentId,
        string ComponentName,
        VisualNode ComponentRoot,
        List<AnalyzedSubtree> Occurrences);

    private sealed record ReplacementInfo(
        string ComponentId,
        string ComponentName,
        ComponentType Type,
        SortedDictionary<string, SortedDictionary<string, object?>> PropertyOverrides);

    private sealed class AnalyzedSubtree
    {
        public AnalyzedSubtree(
            VisualNode node,
            int[] path,
            string pathKey,
            string signature,
            int nodeCount,
            int depth,
            int traversalOrder,
            int layoutTextIconWeight,
            bool hasComponentReference,
            bool isEligible,
            List<AnalyzedSubtree> allSubtrees)
        {
            Node = node;
            Path = path;
            PathKey = pathKey;
            Signature = signature;
            NodeCount = nodeCount;
            Depth = depth;
            TraversalOrder = traversalOrder;
            LayoutTextIconWeight = layoutTextIconWeight;
            HasComponentReference = hasComponentReference;
            IsEligible = isEligible;
            AllSubtrees = allSubtrees;
        }

        public VisualNode Node { get; }

        public int[] Path { get; }

        public string PathKey { get; }

        public string Signature { get; }

        public int NodeCount { get; }

        public int Depth { get; }

        public int TraversalOrder { get; }

        public int LayoutTextIconWeight { get; }

        public bool HasComponentReference { get; }

        public bool IsEligible { get; }

        public List<AnalyzedSubtree> AllSubtrees { get; }
    }
}
