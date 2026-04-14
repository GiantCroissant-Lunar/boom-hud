using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators.SourceSemantics;

internal static class SourceSemanticDocumentBuilder
{
    private const string SourceGenerationMode = "post-v1-prepared-hud-document";

    private static readonly string[] TextSemanticClasses =
    [
        "compact-numeric-readout",
        "right-aligned-quantity",
        "tab-label",
        "button-label",
        "compact-label",
        "heading-label",
        "stacked-text-line",
        "stacked-text-group",
        "pixel-text"
    ];

    private static readonly string[] IconSemanticClasses =
    [
        "badge-icon",
        "leading-icon",
        "inline-icon",
        "icon-glyph",
        "icon-shell"
    ];

    private static readonly string[] ContainerSemanticClasses =
    [
        "value-row"
    ];

    public static SourceSemanticDocument Build(HudDocument document, GenerationOptions options, string? backendId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        var backendFamily = string.IsNullOrWhiteSpace(backendId) ? "neutral" : backendId.Trim();
        var root = BuildNode(document.Root, RuleSelectionContext.Root, "root", parentLayoutType: null);
        var components = document.Components
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new SourceSemanticComponentDefinition
            {
                Id = pair.Key,
                Name = pair.Value.Name,
                Root = BuildNode(pair.Value.Root, RuleSelectionContext.Root, "component:" + pair.Key, parentLayoutType: null)
            })
            .ToList();

        return new SourceSemanticDocument
        {
            DocumentName = document.Name,
            BackendFamily = backendFamily,
            SourceGenerationMode = SourceGenerationMode,
            Root = root,
            Components = components
        };
    }

    private static SourceSemanticNode BuildNode(
        ComponentNode node,
        RuleSelectionContext context,
        string stableId,
        LayoutType? parentLayoutType)
    {
        var semanticRole = ResolveSemanticRole(node, context);
        var children = node.Children
            .Select((child, index) => BuildNode(
                child,
                context.ForChild(node, index),
                stableId + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                node.Layout?.Type))
            .ToList();

        return new SourceSemanticNode
        {
            StableId = stableId,
            SourceId = node.Id,
            SourceNodeId = ResolveSourceNodeId(node),
            SourceType = node.Type,
            SemanticRole = semanticRole,
            AssetRealization = ResolveAssetRealization(node, semanticRole),
            SizeBand = RuleSelectorClassifier.ResolveSizeBand(node),
            FontFamily = RuleSelectorClassifier.ResolveFontFamily(node),
            TextGrowth = RuleSelectorClassifier.ResolveTextGrowth(node),
            Facts = BuildFacts(node, context, semanticRole, parentLayoutType),
            Children = children
        };
    }

    private static string ResolveSemanticRole(ComponentNode node, RuleSelectionContext context)
    {
        foreach (var semanticClass in TextSemanticClasses)
        {
            if (RuleSelectorClassifier.HasSemanticClass(node, context, semanticClass))
            {
                return semanticClass;
            }
        }

        foreach (var semanticClass in IconSemanticClasses)
        {
            if (RuleSelectorClassifier.HasSemanticClass(node, context, semanticClass))
            {
                return semanticClass;
            }
        }

        foreach (var semanticClass in ContainerSemanticClasses)
        {
            if (RuleSelectorClassifier.HasSemanticClass(node, context, semanticClass))
            {
                return semanticClass;
            }
        }

        return node.Type switch
        {
            ComponentType.Label or ComponentType.Badge => "text",
            ComponentType.Icon => "icon",
            ComponentType.Image => "image",
            ComponentType.Container or ComponentType.Panel or ComponentType.Stack or ComponentType.Grid or ComponentType.Dock
                or ComponentType.ScrollView or ComponentType.TabView or ComponentType.SplitView => "container",
            _ => node.Type.ToString().ToLowerInvariant()
        };
    }

    private static AssetRealizationKind ResolveAssetRealization(ComponentNode node, string semanticRole)
        => node.ComponentRefId != null
            ? AssetRealizationKind.ComponentInstance
            : node.Type switch
            {
                ComponentType.Image => AssetRealizationKind.ImageAsset,
                ComponentType.Icon when semanticRole is "icon-glyph" or "leading-icon" or "inline-icon" or "badge-icon"
                    => AssetRealizationKind.IconGlyph,
                ComponentType.Label or ComponentType.Badge => AssetRealizationKind.TextPrimitive,
                ComponentType.Container or ComponentType.Panel or ComponentType.Stack or ComponentType.Grid or ComponentType.Dock
                    or ComponentType.ScrollView or ComponentType.TabView or ComponentType.SplitView => AssetRealizationKind.LayoutContainer,
                _ => AssetRealizationKind.Native
            };

    private static Dictionary<string, object?> BuildFacts(
        ComponentNode node,
        RuleSelectionContext context,
        string semanticRole,
        LayoutType? parentLayoutType)
    {
        var assetRealization = ResolveAssetRealization(node, semanticRole);
        var isRowEndPinned = (parentLayoutType == LayoutType.Horizontal && context.Parent != null && context.SiblingIndex == context.Parent.Children.Count - 1)
                             || node.Layout?.Align == Alignment.End
                             || context.Parent?.Layout?.Justify is Justification.End or Justification.SpaceBetween;
        var facts = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["childCount"] = node.Children.Count,
            ["hasComponentRef"] = node.ComponentRefId != null,
            ["nodeKind"] = ResolveNodeKind(node),
            ["semanticRole"] = semanticRole,
            ["assetRealization"] = assetRealization.ToString(),
            ["parentLayoutType"] = parentLayoutType?.ToString(),
            ["isAbsolutePositioned"] = node.Layout?.IsAbsolutePositioned == true || parentLayoutType == LayoutType.Absolute,
            ["isPixelText"] = RuleSelectorClassifier.HasSemanticClass(node, RuleSelectionContext.Root, "pixel-text"),
            ["siblingIndex"] = context.SiblingIndex,
            ["siblingCount"] = context.Parent?.Children.Count,
            ["isLastInParent"] = context.Parent != null && context.SiblingIndex == context.Parent.Children.Count - 1,
            ["isRowEndPinned"] = isRowEndPinned,
            ["hasBackground"] = node.Style?.Background != null,
            ["hasBorder"] = node.Style?.Border != null,
            ["isInteractiveShell"] = node.Type is ComponentType.Button or ComponentType.MenuItem or ComponentType.Checkbox or ComponentType.RadioButton or ComponentType.Slider or ComponentType.TextInput or ComponentType.TextArea
        };

        if (node.Style?.FontSize is { } fontSize)
        {
            facts["fontSize"] = fontSize;
        }

        if (node.Layout?.Type is { } layoutType)
        {
            facts["layoutType"] = layoutType.ToString();
        }

        if (node.Layout?.Align is { } align)
        {
            facts["align"] = align.ToString();
        }

        if (node.Layout?.Justify is { } justify)
        {
            facts["justify"] = justify.ToString();
        }

        return facts;
    }

    private static string ResolveNodeKind(ComponentNode node)
        => node.Type switch
        {
            ComponentType.Label or ComponentType.Badge => "text",
            ComponentType.Icon => "icon",
            ComponentType.Image => "image",
            ComponentType.Button or ComponentType.MenuItem or ComponentType.Checkbox or ComponentType.RadioButton or ComponentType.Slider
                or ComponentType.TextInput or ComponentType.TextArea => "interactive",
            ComponentType.Container or ComponentType.Panel or ComponentType.Stack or ComponentType.Grid or ComponentType.Dock
                or ComponentType.ScrollView or ComponentType.TabView or ComponentType.SplitView => "container",
            _ => "other"
        };

    private static string? ResolveSourceNodeId(ComponentNode node)
    {
        if (!node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.OriginalPencilId, out var raw) || raw == null)
        {
            return null;
        }

        return GeneratorRuleMetadata.NormalizeValue(raw);
    }
}
