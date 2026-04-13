using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;
using BoomHud.Generators.VisualIR;

namespace BoomHud.Generators;

public sealed record PreparedGenerationDocument
{
    public required HudDocument Document { get; init; }

    public SyntheticComponentizationSummary? SyntheticComponentization { get; init; }

    public required VisualDocument VisualDocument { get; init; }

    public required VisualSynthesisSummary VisualSynthesis { get; init; }

    public required VisualRefinementSummary VisualRefinement { get; init; }

    public UGuiBuildProgram? UGuiBuildProgram { get; init; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];
}

public sealed record SyntheticComponentizationSummary
{
    public required int CandidateGroupCount { get; init; }

    public required int ChosenGroupCount { get; init; }

    public required int RewrittenOccurrenceCount { get; init; }

    public IReadOnlyList<SyntheticComponentizationComponentSummary> Components { get; init; } = [];
}

public sealed record SyntheticComponentizationComponentSummary
{
    public required string ComponentId { get; init; }

    public required string ComponentName { get; init; }

    public required string RootType { get; init; }

    public required int OccurrenceCount { get; init; }

    public required int NodeCount { get; init; }

    public required int Depth { get; init; }

    public required string SignatureHash { get; init; }
}

public static class GenerationDocumentPreprocessor
{
    private const int MinimumOccurrenceCount = 2;
    private const int MinimumNodeCount = 3;
    private const int MinimumDepth = 2;
    private static readonly HashSet<string> SyntheticComponentizationDisabledBackends = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static PreparedGenerationDocument Prepare(HudDocument document, GenerationOptions options, string? backendId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        var result = ShouldComponentize(backendId)
            ? SyntheticComponentizer.Componentize(document, options)
            : (Document: document, Summary: (SyntheticComponentizationSummary?)null);
        var diagnostics = new List<Diagnostic>();

        if (result.Summary != null)
        {
            diagnostics.Add(Diagnostic.Info(
                $"Synthetic componentization selected {result.Summary.ChosenGroupCount} reusable groups across {result.Summary.RewrittenOccurrenceCount} occurrences.",
                code: "BHG1100"));
        }

        var visualDocument = VisualDocumentBuilder.Build(result.Document, options, backendId);
        var visualSynthesis = VisualSynthesisPlanner.Synthesize(visualDocument);
        var visualRefinement = VisualRefinementPlanner.Plan(
            visualSynthesis.Document,
            iterationBudget: options.VisualRefinementIterationBudget);
        var uguiBuildProgram = string.Equals(backendId, "ugui", StringComparison.OrdinalIgnoreCase)
            ? UGuiBuildProgramPlanner.Plan(visualSynthesis.Document)
            : null;

        return new PreparedGenerationDocument
        {
            Document = result.Document,
            SyntheticComponentization = result.Summary,
            VisualDocument = visualSynthesis.Document,
            VisualSynthesis = visualSynthesis.Summary,
            VisualRefinement = visualRefinement,
            UGuiBuildProgram = uguiBuildProgram,
            Diagnostics = diagnostics
        };
    }

    private static bool ShouldComponentize(string? backendId)
        => string.IsNullOrWhiteSpace(backendId) || !SyntheticComponentizationDisabledBackends.Contains(backendId);

    public static string ToJson(SyntheticComponentizationSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    public static string ToJson(VisualDocument visualDocument)
    {
        ArgumentNullException.ThrowIfNull(visualDocument);
        return JsonSerializer.Serialize(visualDocument, JsonOptions);
    }

    public static string ToJson(VisualSynthesisSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    public static string ToJson(VisualRefinementSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    public static string ToJson(UGuiBuildProgram buildProgram)
    {
        ArgumentNullException.ThrowIfNull(buildProgram);
        return JsonSerializer.Serialize(buildProgram, JsonOptions);
    }

    public static GeneratedFile? CreateSummaryArtifact(string documentName, SyntheticComponentizationSummary? summary)
    {
        if (summary == null)
        {
            return null;
        }

        return new GeneratedFile
        {
            Path = $"{documentName}.synthetic-components.json",
            Content = ToJson(summary),
            Type = GeneratedFileType.Other
        };
    }

    public static GeneratedFile? CreateVisualIrArtifact(string documentName, VisualDocument? visualDocument)
    {
        if (visualDocument == null)
        {
            return null;
        }

        return new GeneratedFile
        {
            Path = $"{documentName}.visual-ir.json",
            Content = ToJson(visualDocument),
            Type = GeneratedFileType.Other
        };
    }

    public static GeneratedFile? CreateVisualSynthesisArtifact(string documentName, VisualSynthesisSummary? summary)
    {
        if (summary == null)
        {
            return null;
        }

        return new GeneratedFile
        {
            Path = $"{documentName}.visual-synthesis.json",
            Content = ToJson(summary),
            Type = GeneratedFileType.Other
        };
    }

    public static GeneratedFile? CreateVisualRefinementArtifact(string documentName, VisualRefinementSummary? summary)
    {
        if (summary == null)
        {
            return null;
        }

        return new GeneratedFile
        {
            Path = $"{documentName}.visual-refinement.json",
            Content = ToJson(summary),
            Type = GeneratedFileType.Other
        };
    }

    public static GeneratedFile? CreateUGuiBuildProgramArtifact(string documentName, UGuiBuildProgram? buildProgram)
    {
        if (buildProgram == null)
        {
            return null;
        }

        return new GeneratedFile
        {
            Path = $"{documentName}.ugui-build-program.json",
            Content = ToJson(buildProgram),
            Type = GeneratedFileType.Other
        };
    }

    private static class SyntheticComponentizer
    {
        public static (HudDocument Document, SyntheticComponentizationSummary? Summary) Componentize(
            HudDocument document,
            GenerationOptions options)
        {
            var protectedIds = CollectProtectedIds(options);
            var existingComponentIds = new HashSet<string>(document.Components.Keys, StringComparer.Ordinal);
            var existingComponentNames = new HashSet<string>(document.Components.Values.Select(component => component.Name), StringComparer.Ordinal);
            var analyzed = AnalyzeSubtree(document.Root, [], protectedIds);
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
                return (document, null);
            }

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

                var componentId = ReserveComponentId($"synthetic:{candidate.SignatureHash[..12]}", existingComponentIds);
                var componentName = ReserveComponentName(
                    $"Synthetic{availableOccurrences[0].Node.Type}{candidate.SignatureHash[..8].ToUpperInvariant()}",
                    existingComponentNames);

                var componentRoot = CloneNode(availableOccurrences[0].Node);
                var componentDefinition = new HudComponentDefinition
                {
                    Id = componentId,
                    Name = componentName,
                    Metadata = new ComponentMetadata
                    {
                        Description = $"Synthetic exact subtree extracted from {document.Name}",
                        Tags = ["synthetic", "exact-reuse"]
                    },
                    Root = componentRoot
                };

                foreach (var occurrence in availableOccurrences)
                {
                    consumedRoots.Add(occurrence.Path);
                    replacementByPath[occurrence.PathKey] = new ReplacementInfo(
                        componentId,
                        occurrence.Node.Type,
                        occurrence.Node.Id,
                        occurrence.Node.SlotKey,
                        BuildPropertyOverrides(componentRoot, occurrence.Node, ComponentInstanceOverrideSupport.RootPath));
                }

                selected.Add(new SelectedGroup(candidate, componentDefinition, availableOccurrences));
            }

            if (selected.Count == 0)
            {
                return (document, null);
            }

            var rewrittenRoot = RewriteNode(document.Root, [], replacementByPath);
            var rewrittenComponents = new Dictionary<string, HudComponentDefinition>(document.Components, StringComparer.Ordinal);
            foreach (var group in selected)
            {
                rewrittenComponents[group.Component.Id] = group.Component;
            }

            var rewrittenDocument = document with
            {
                Root = rewrittenRoot,
                Components = rewrittenComponents
            };

            return (rewrittenDocument, new SyntheticComponentizationSummary
            {
                CandidateGroupCount = candidates.Count,
                ChosenGroupCount = selected.Count,
                RewrittenOccurrenceCount = selected.Sum(static group => group.Occurrences.Count),
                Components = selected
                    .Select(group => new SyntheticComponentizationComponentSummary
                    {
                        ComponentId = group.Component.Id,
                        ComponentName = group.Component.Name,
                        RootType = group.Occurrences[0].Node.Type.ToString(),
                        OccurrenceCount = group.Occurrences.Count,
                        NodeCount = group.Candidate.NodeCount,
                        Depth = group.Candidate.Depth,
                        SignatureHash = group.Candidate.SignatureHash
                    })
                    .ToList()
            });
        }

        private static AnalyzedSubtree AnalyzeSubtree(ComponentNode node, int[] path, HashSet<string> protectedIds)
        {
            var childSubtrees = new List<AnalyzedSubtree>();
            var descendantCount = 1;
            var depth = 1;
            var layoutTextIconWeight = ScoreLayoutTextIconMix(node);
            var traversalOrder = ComputeTraversalOrder(path);

            for (var index = 0; index < node.Children.Count; index++)
            {
                var childPath = path.Concat([index]).ToArray();
                var child = AnalyzeSubtree(node.Children[index], childPath, protectedIds);
                childSubtrees.Add(child);
                descendantCount += child.NodeCount;
                depth = Math.Max(depth, child.Depth + 1);
                layoutTextIconWeight += child.LayoutTextIconWeight;
            }

            var hasDynamicBinding = HasDynamicBinding(node) || childSubtrees.Any(static child => child.HasDynamicBinding);
            var hasComponentReference = node.ComponentRefId != null || childSubtrees.Any(static child => child.HasComponentReference);
            var containsProtectedId = IsProtectedNode(node, protectedIds) || childSubtrees.Any(static child => child.ContainsProtectedId);

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
                hasDynamicBinding,
                hasComponentReference,
                containsProtectedId,
                IsEligible(node, descendantCount, depth, hasDynamicBinding, hasComponentReference, containsProtectedId),
                allSubtrees);

            allSubtrees.Insert(0, subtree);
            return subtree;
        }

        private static bool IsEligible(
            ComponentNode node,
            int nodeCount,
            int depth,
            bool hasDynamicBinding,
            bool hasComponentReference,
            bool containsProtectedId)
        {
            if (node.Children.Count == 0)
            {
                return false;
            }

            if (hasDynamicBinding || hasComponentReference || containsProtectedId)
            {
                return false;
            }

            return nodeCount >= MinimumNodeCount || depth >= MinimumDepth;
        }

        private static string BuildSignature(ComponentNode node, IReadOnlyList<AnalyzedSubtree> childSubtrees)
        {
            var builder = new StringBuilder();
            builder.Append("type=").Append(node.Type);
            builder.Append("|layout=").Append(SerializeLayout(node.Layout));
            builder.Append("|style=").Append(SerializeStyle(node.Style));
            builder.Append("|visible=").Append(SerializeBindable(node.Visible));
            builder.Append("|enabled=").Append(SerializeBindable(node.Enabled));
            builder.Append("|tooltip=").Append(node.Tooltip is { } tooltip ? SerializeBindable(tooltip) : "null");
            builder.Append("|properties=").Append(SerializeProperties(node, node.Properties));
            builder.Append("|bindings=").Append(SerializeBindings(node.Bindings));
            builder.Append("|componentRef=").Append(node.ComponentRefId ?? string.Empty);
            builder.Append("|required=").Append(string.Join(",", node.RequiredCapabilities.OrderBy(static capability => capability, StringComparer.Ordinal)));
            builder.Append("|metadata=").Append(SerializeInstanceOverrides(node.InstanceOverrides));
            builder.Append("|children=[");
            for (var index = 0; index < childSubtrees.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(childSubtrees[index].Signature);
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static ComponentNode RewriteNode(
            ComponentNode node,
            int[] path,
            IReadOnlyDictionary<string, ReplacementInfo> replacements)
        {
            var pathKey = PathKey(path);
            if (replacements.TryGetValue(pathKey, out var replacement))
            {
                return new ComponentNode
                {
                    Id = replacement.NodeId,
                    SlotKey = replacement.SlotKey,
                    Type = replacement.Type,
                    ComponentRefId = replacement.ComponentId,
                    InstanceOverrides = BuildReplacementOverrides(node, replacement.PropertyOverrides)
                };
            }

            if (node.Children.Count == 0)
            {
                return node;
            }

            var rewrittenChildren = node.Children
                .Select((child, index) => RewriteNode(child, path.Concat([index]).ToArray(), replacements))
                .ToList();

            return node with
            {
                Children = rewrittenChildren
            };
        }

        private static Dictionary<string, object?> BuildReplacementOverrides(
            ComponentNode occurrence,
            SortedDictionary<string, SortedDictionary<string, object?>> propertyOverrides)
        {
            var overrides = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [BoomHudMetadataKeys.SyntheticComponentInstance] = true
            };

            foreach (var metadataKey in SyntheticInstanceMetadataKeys)
            {
                if (occurrence.InstanceOverrides.TryGetValue(metadataKey, out var metadataValue))
                {
                    overrides[metadataKey] = metadataValue;
                }
            }

            if (propertyOverrides.Count > 0)
            {
                overrides[BoomHudMetadataKeys.ComponentPropertyOverrides] = propertyOverrides;
            }

            return overrides;
        }

        private static SortedDictionary<string, SortedDictionary<string, object?>> BuildPropertyOverrides(
            ComponentNode canonical,
            ComponentNode occurrence,
            string path)
        {
            var overrides = new SortedDictionary<string, SortedDictionary<string, object?>>(StringComparer.Ordinal);
            AppendPropertyOverrides(overrides, canonical, occurrence, path);
            return overrides;
        }

        private static void AppendPropertyOverrides(
            SortedDictionary<string, SortedDictionary<string, object?>> overrides,
            ComponentNode canonical,
            ComponentNode occurrence,
            string path)
        {
            foreach (var canonicalProperty in canonical.Properties.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                if (!occurrence.Properties.TryGetValue(canonicalProperty.Key, out var occurrenceValue))
                {
                    continue;
                }

                if (!ComponentInstanceOverrideSupport.CanParameterizeProperty(canonical, canonicalProperty.Key, canonicalProperty.Value))
                {
                    continue;
                }

                var canonicalValue = ComponentInstanceOverrideSupport.ToSerializableOverrideValue(canonicalProperty.Value.Value);
                var candidateValue = ComponentInstanceOverrideSupport.ToSerializableOverrideValue(occurrenceValue.Value);
                if (SerializeObject(canonicalValue) == SerializeObject(candidateValue))
                {
                    continue;
                }

                if (!overrides.TryGetValue(path, out var propertyOverrides))
                {
                    propertyOverrides = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                    overrides[path] = propertyOverrides;
                }

                propertyOverrides[canonicalProperty.Key] = candidateValue;
            }

            for (var index = 0; index < canonical.Children.Count && index < occurrence.Children.Count; index++)
            {
                AppendPropertyOverrides(
                    overrides,
                    canonical.Children[index],
                    occurrence.Children[index],
                    ComponentInstanceOverrideSupport.ChildPath(path, index));
            }
        }

        private static ComponentNode CloneNode(ComponentNode node)
        {
            var clonedChildren = node.Children.Select(CloneNode).ToList();
            var clonedBindings = node.Bindings.Select(binding => binding with { }).ToList();
            var clonedProperties = node.Properties.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal);
            var clonedRequiredCapabilities = new HashSet<string>(node.RequiredCapabilities, StringComparer.Ordinal);
            var clonedOverrides = new Dictionary<string, object?>(node.InstanceOverrides, StringComparer.Ordinal);

            return node with
            {
                Children = clonedChildren,
                Bindings = clonedBindings,
                Properties = clonedProperties,
                RequiredCapabilities = clonedRequiredCapabilities,
                InstanceOverrides = clonedOverrides
            };
        }

        private static bool HasDynamicBinding(ComponentNode node)
        {
            return node.Visible.IsBound
                   || node.Enabled.IsBound
                   || node.Tooltip?.IsBound == true
                   || node.Bindings.Count > 0
                   || node.Properties.Values.Any(static property => property.IsBound);
        }

        private static bool IsProtectedNode(ComponentNode node, HashSet<string> protectedIds)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                return false;
            }

            return protectedIds.Contains(node.Id);
        }

        private static HashSet<string> CollectProtectedIds(GenerationOptions options)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (var rule in options.RuleSet?.Rules ?? [])
            {
                if (!string.IsNullOrWhiteSpace(rule.Selector.NodeId))
                {
                    ids.Add(rule.Selector.NodeId);
                }
            }

            foreach (var clip in options.Motion?.Clips ?? [])
            {
                foreach (var track in clip.Tracks)
                {
                    if (!string.IsNullOrWhiteSpace(track.TargetId))
                    {
                        ids.Add(track.TargetId);
                    }
                }
            }

            return ids;
        }

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

        private static int ScoreLayoutTextIconMix(ComponentNode node)
        {
            var score = node.Type switch
            {
                ComponentType.Container or ComponentType.Panel or ComponentType.Stack or ComponentType.Grid or ComponentType.Dock => 4,
                ComponentType.Label or ComponentType.Badge or ComponentType.Icon => 2,
                _ => 1
            };

            if (node.Layout != null)
            {
                score += 2;
            }

            if (node.Style?.FontFamily != null || node.Style?.FontSize != null)
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

        private static string SerializeLayout(LayoutSpec? layout)
        {
            if (layout == null)
            {
                return "null";
            }

            return string.Join(";",
                layout.Type.ToString(),
                SerializeDimension(layout.Width),
                SerializeDimension(layout.Height),
                SerializeDimension(layout.MinWidth),
                SerializeDimension(layout.MinHeight),
                SerializeDimension(layout.MaxWidth),
                SerializeDimension(layout.MaxHeight),
                SerializeSpacing(layout.Gap),
                SerializeSpacing(layout.Padding),
                SerializeSpacing(layout.Margin),
                SerializeDimension(layout.Left),
                SerializeDimension(layout.Top),
                layout.IsAbsolutePositioned,
                layout.ClipContent,
                layout.Align?.ToString() ?? string.Empty,
                layout.Justify?.ToString() ?? string.Empty,
                SerializeDouble(layout.Weight),
                layout.GridColumn?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                layout.GridRow?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                layout.GridColumnSpan.ToString(CultureInfo.InvariantCulture),
                layout.GridRowSpan.ToString(CultureInfo.InvariantCulture),
                SerializeDimensions(layout.ColumnDefinitions),
                SerializeDimensions(layout.RowDefinitions),
                layout.Dock?.ToString() ?? string.Empty);
        }

        private static string SerializeStyle(StyleSpec? style)
        {
            if (style == null)
            {
                return "null";
            }

            return string.Join(";",
                style.Foreground?.ToString() ?? string.Empty,
                style.ForegroundToken ?? string.Empty,
                style.Background?.ToString() ?? string.Empty,
                style.BackgroundToken ?? string.Empty,
                SerializeBackgroundImage(style.BackgroundImage),
                SerializeDouble(style.FontSize),
                SerializeDouble(style.LineHeight),
                style.FontFamily ?? string.Empty,
                style.FontSizeToken ?? string.Empty,
                style.FontWeight?.ToString() ?? string.Empty,
                style.FontStyle?.ToString() ?? string.Empty,
                SerializeDouble(style.LetterSpacing),
                SerializeBorder(style.Border),
                style.BorderColorToken ?? string.Empty,
                SerializeDouble(style.BorderRadius),
                SerializeDouble(style.Opacity),
                style.Classes == null ? string.Empty : string.Join(",", style.Classes.OrderBy(static value => value, StringComparer.Ordinal)),
                SerializeDimension(style.Width),
                SerializeDimension(style.Height));
        }

        private static string SerializeBackgroundImage(BackgroundImageSpec? image)
            => image == null ? "null" : $"{image.Url}|{image.Mode}";

        private static string SerializeBorder(BorderSpec? border)
            => border == null ? "null" : $"{border.Style}|{border.Color?.ToString() ?? string.Empty}|{SerializeDouble(border.Width)}";

        private static string SerializeProperties(ComponentNode node, IReadOnlyDictionary<string, BindableValue<object?>> properties)
        {
            if (properties.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(",",
                properties
                    .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => SerializeProperty(node, pair.Key, pair.Value)));
        }

        private static string SerializeProperty(ComponentNode node, string propertyName, BindableValue<object?> value)
        {
            if (ComponentInstanceOverrideSupport.CanParameterizeProperty(node, propertyName, value))
            {
                return propertyName + "=override:" + ComponentInstanceOverrideSupport.NormalizePropertyName(propertyName);
            }

            return propertyName + "=" + SerializeObjectBindable(value);
        }

        private static string SerializeBindings(IReadOnlyList<BindingSpec> bindings)
        {
            if (bindings.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(",",
                bindings
                    .OrderBy(static binding => binding.Property, StringComparer.Ordinal)
                    .ThenBy(static binding => binding.Path, StringComparer.Ordinal)
                    .Select(binding => string.Join("|",
                        binding.Property,
                        binding.Path,
                        binding.Key ?? string.Empty,
                        binding.Mode.ToString(),
                        binding.Converter ?? string.Empty,
                        SerializeObject(binding.ConverterParameter),
                        binding.Format ?? string.Empty,
                        SerializeObject(binding.Fallback))));
        }

        private static string SerializeBindable<T>(BindableValue<T> value)
            => value.IsBound
                ? $"bind:{value.BindingPath}|{value.Mode}|{value.Format ?? string.Empty}"
                : $"static:{SerializeObject(value.Value)}";

        private static string SerializeObjectBindable(BindableValue<object?> value)
            => SerializeBindable(value);

        private static string SerializeInstanceOverrides(IReadOnlyDictionary<string, object?> overrides)
        {
            if (overrides.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(",",
                overrides
                    .Where(static pair => !ShouldIgnoreMetadata(pair.Key))
                    .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Key + "=" + SerializeObject(pair.Value)));
        }

        private static bool ShouldIgnoreMetadata(string key)
            => key switch
            {
                BoomHudMetadataKeys.OriginalPencilId => true,
                BoomHudMetadataKeys.PencilLeft => true,
                BoomHudMetadataKeys.PencilTop => true,
                BoomHudMetadataKeys.PencilPosition => true,
                _ => false
            };

        private static readonly string[] SyntheticInstanceMetadataKeys =
        [
            BoomHudMetadataKeys.OriginalPencilId,
            BoomHudMetadataKeys.PencilLeft,
            BoomHudMetadataKeys.PencilTop,
            BoomHudMetadataKeys.PencilPosition
        ];

        private static string SerializeDimensions(IReadOnlyList<Dimension>? dimensions)
            => dimensions == null ? string.Empty : string.Join(",", dimensions.Select(static dimension => SerializeDimension(dimension)));

        private static string SerializeDimension(Dimension? dimension)
            => dimension?.ToString() ?? string.Empty;

        private static string SerializeSpacing(Spacing? spacing)
            => spacing?.ToString() ?? string.Empty;

        private static string SerializeDouble(double? value)
            => value?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;

        private static string SerializeObject(object? value)
        {
            return value switch
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
                JsonElement element => JsonSerializer.Serialize(element, JsonOptions),
                IEnumerable<object?> list => "[" + string.Join(",", list.Select(SerializeObject)) + "]",
                _ => JsonSerializer.Serialize(value, JsonOptions)
            };
        }

        private sealed record CandidateGroup(string Signature, List<AnalyzedSubtree> Occurrences)
        {
            public int NodeCount { get; init; }

            public int Depth { get; init; }

            public string SignatureHash { get; init; } = string.Empty;
        }

        private sealed record SelectedGroup(
            CandidateGroup Candidate,
            HudComponentDefinition Component,
            List<AnalyzedSubtree> Occurrences);

        private sealed record ReplacementInfo(
            string ComponentId,
            ComponentType Type,
            string? NodeId,
            string? SlotKey,
            SortedDictionary<string, SortedDictionary<string, object?>> PropertyOverrides);

        private sealed class AnalyzedSubtree
        {
            public AnalyzedSubtree(
                ComponentNode node,
                int[] path,
                string pathKey,
                string signature,
                int nodeCount,
                int depth,
                int traversalOrder,
                int layoutTextIconWeight,
                bool hasDynamicBinding,
                bool hasComponentReference,
                bool containsProtectedId,
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
                HasDynamicBinding = hasDynamicBinding;
                HasComponentReference = hasComponentReference;
                ContainsProtectedId = containsProtectedId;
                IsEligible = isEligible;
                AllSubtrees = allSubtrees;
            }

            public ComponentNode Node { get; }

            public int[] Path { get; }

            public string PathKey { get; }

            public string Signature { get; }

            public int NodeCount { get; }

            public int Depth { get; }

            public int TraversalOrder { get; }

            public int LayoutTextIconWeight { get; }

            public bool HasDynamicBinding { get; }

            public bool HasComponentReference { get; }

            public bool ContainsProtectedId { get; }

            public bool IsEligible { get; }

            public List<AnalyzedSubtree> AllSubtrees { get; }
        }
    }
}
