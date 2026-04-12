using System.Globalization;
using System.Text;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators.VisualIR;

internal static class VisualDocumentBuilder
{
    private const string SourceGenerationMode = "post-v1-prepared-hud-document";
    private static readonly string[] TextSemanticClasses =
    [
        "pixel-text",
        "heading-label",
        "stacked-text-line",
        "stacked-text-group"
    ];

    private static readonly string[] IconSemanticClasses =
    [
        "icon-glyph",
        "icon-shell"
    ];

    public static VisualDocument Build(HudDocument document, GenerationOptions options, string? backendId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        var backendFamily = string.IsNullOrWhiteSpace(backendId) ? "neutral" : backendId.Trim();
        var resolver = new RuleResolver(options.RuleSet, backendFamily);
        var state = new BuilderState(document.Name, backendFamily, resolver);

        var root = BuildNode(document.Root, RuleSelectionContext.Root, "root", state, parentLayoutType: null);
        var components = document.Components
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new VisualComponentDefinition
            {
                Id = pair.Key,
                Name = pair.Value.Name,
                Root = BuildNode(pair.Value.Root, RuleSelectionContext.Root, "component:" + pair.Key, state, parentLayoutType: null)
            })
            .ToList();

        return new VisualDocument
        {
            DocumentName = document.Name,
            BackendFamily = backendFamily,
            SourceGenerationMode = SourceGenerationMode,
            Root = root,
            Components = components,
            MetricProfiles = state.MetricProfiles.ToList()
        };
    }

    private static VisualNode BuildNode(
        ComponentNode node,
        RuleSelectionContext context,
        string stableId,
        BuilderState state,
        LayoutType? parentLayoutType)
    {
        var policy = state.Resolver.Resolve(state.DocumentName, node, context);
        var kind = Classify(node.Type);
        var widthDimension = node.Layout?.Width ?? node.Style?.Width;
        var heightDimension = node.Layout?.Height ?? node.Style?.Height;
        var semanticClass = ResolveSemanticClass(node, context, kind);
        var sizeBand = RuleSelectorClassifier.ResolveSizeBand(node);
        var typography = BuildTypographyContract(node, policy, kind, semanticClass, sizeBand, widthDimension, heightDimension);
        var icon = BuildIconContract(node, policy, kind, semanticClass, sizeBand, widthDimension, heightDimension);
        var metricProfileId = state.ResolveMetricProfileId(semanticClass, typography, icon);

        var children = node.Children
            .Select((child, index) => BuildNode(
                child,
                context.ForChild(node, index),
                stableId + "/" + index.ToString(CultureInfo.InvariantCulture),
                state,
                node.Layout?.Type))
            .ToList();

        return new VisualNode
        {
            StableId = stableId,
            SourceId = node.Id,
            SourceNodeId = ResolveSourceNodeId(node),
            Kind = kind,
            SourceType = node.Type,
            ComponentRefId = node.ComponentRefId,
            SemanticClass = semanticClass,
            MetricProfileId = metricProfileId,
            StaticProperties = ExtractStaticProperties(node),
            PropertyOverrides = ExtractPropertyOverrides(node),
            Box = BuildVisualBox(node, policy, parentLayoutType),
            EdgeContract = BuildEdgeContract(node, policy, kind, widthDimension, heightDimension, parentLayoutType),
            Typography = typography,
            Icon = icon,
            Children = children
        };
    }

    private static VisualNodeKind Classify(ComponentType type)
        => type switch
        {
            ComponentType.Container or ComponentType.Panel or ComponentType.Stack or ComponentType.Grid or ComponentType.Dock or ComponentType.ScrollView or ComponentType.TabView or ComponentType.SplitView
                => VisualNodeKind.Container,
            ComponentType.Label or ComponentType.Badge => VisualNodeKind.Text,
            ComponentType.Icon => VisualNodeKind.Icon,
            ComponentType.Image => VisualNodeKind.Image,
            ComponentType.Button or ComponentType.MenuItem or ComponentType.TextInput or ComponentType.TextArea or ComponentType.Checkbox or ComponentType.RadioButton or ComponentType.Slider
                => VisualNodeKind.Interactive,
            ComponentType.ProgressBar => VisualNodeKind.Value,
            ComponentType.ListBox or ComponentType.ListView or ComponentType.TreeView or ComponentType.DataGrid or ComponentType.Timeline or ComponentType.MenuBar or ComponentType.Menu
                => VisualNodeKind.Collection,
            ComponentType.Spacer => VisualNodeKind.Spacer,
            _ => VisualNodeKind.Other
        };

    private static VisualBox BuildVisualBox(ComponentNode node, ResolvedGeneratorPolicy policy, LayoutType? parentLayoutType)
    {
        var layout = node.Layout;
        var style = node.Style;
        var hasAbsolutePlacement = parentLayoutType == LayoutType.Absolute || LayoutPolicyService.HasAbsolutePlacement(node, policy);
        return new VisualBox
        {
            SourceType = node.Type,
            LayoutType = layout?.Type,
            Width = layout?.Width ?? style?.Width,
            Height = layout?.Height ?? style?.Height,
            MinWidth = layout?.MinWidth,
            MinHeight = layout?.MinHeight,
            MaxWidth = layout?.MaxWidth,
            MaxHeight = layout?.MaxHeight,
            Gap = layout?.Gap,
            Padding = layout?.Padding,
            Margin = layout?.Margin,
            Left = hasAbsolutePlacement
                ? ResolveAbsoluteOffset(node, static currentLayout => currentLayout.Left, BoomHudMetadataKeys.PencilLeft, policy, "x")
                : layout?.Left ?? ResolveMetadataDimension(node, BoomHudMetadataKeys.PencilLeft),
            Top = hasAbsolutePlacement
                ? ResolveAbsoluteOffset(node, static currentLayout => currentLayout.Top, BoomHudMetadataKeys.PencilTop, policy, "y")
                : layout?.Top ?? ResolveMetadataDimension(node, BoomHudMetadataKeys.PencilTop),
            IsAbsolutePositioned = hasAbsolutePlacement,
            ClipContent = layout?.ClipContent == true,
            Align = layout?.Align,
            Justify = layout?.Justify,
            Weight = layout?.Weight,
            Background = style?.Background,
            Border = style?.Border,
            BorderRadius = style?.BorderRadius,
            Opacity = style?.Opacity
        };
    }

    private static EdgeContract BuildEdgeContract(
        ComponentNode node,
        ResolvedGeneratorPolicy policy,
        VisualNodeKind kind,
        Dimension? widthDimension,
        Dimension? heightDimension,
        LayoutType? parentLayoutType)
    {
        return new EdgeContract
        {
            Participation = parentLayoutType == LayoutType.Absolute || LayoutPolicyService.HasAbsolutePlacement(node, policy)
                ? LayoutParticipation.Overlay
                : LayoutParticipation.NormalFlow,
            WidthSizing = MapAxisSizing(widthDimension),
            HeightSizing = MapAxisSizing(heightDimension),
            HorizontalPin = EdgePin.Start,
            VerticalPin = EdgePin.Start,
            OverflowX = node.Layout?.ClipContent == true ? OverflowBehavior.Clip : OverflowBehavior.Visible,
            OverflowY = node.Layout?.ClipContent == true ? OverflowBehavior.Clip : OverflowBehavior.Visible,
            WrapPressure = kind == VisualNodeKind.Text && TextPolicyService.ShouldWrapText(node, policy)
                ? WrapPressurePolicy.Tight
                : WrapPressurePolicy.Allow
        };
    }

    private static TypographyContract? BuildTypographyContract(
        ComponentNode node,
        ResolvedGeneratorPolicy policy,
        VisualNodeKind kind,
        string semanticClass,
        string? sizeBand,
        Dimension? widthDimension,
        Dimension? heightDimension)
    {
        if (kind != VisualNodeKind.Text)
        {
            return null;
        }

        var fontSize = TextPolicyService.ResolveFontSize(node, widthDimension, heightDimension, policy);
        return new TypographyContract
        {
            SemanticClass = semanticClass,
            ResolvedFontFamily = TextPolicyService.ResolveFontFamily(node, policy),
            ResolvedFontSize = fontSize,
            ResolvedLineHeight = TextPolicyService.ResolveLineHeight(node.Style, fontSize, policy),
            ResolvedLetterSpacing = TextPolicyService.ResolveLetterSpacing(node, policy),
            WrapText = TextPolicyService.ShouldWrapText(node, policy),
            TextGrowth = policy.Text.TextGrowth ?? RuleSelectorClassifier.ResolveTextGrowth(node),
            SizeBand = sizeBand
        };
    }

    private static IconContract? BuildIconContract(
        ComponentNode node,
        ResolvedGeneratorPolicy policy,
        VisualNodeKind kind,
        string semanticClass,
        string? sizeBand,
        Dimension? widthDimension,
        Dimension? heightDimension)
    {
        if (kind != VisualNodeKind.Icon)
        {
            return null;
        }

        return new IconContract
        {
            SemanticClass = semanticClass,
            ResolvedFontFamily = TextPolicyService.ResolveFontFamily(node, policy),
            ResolvedFontSize = IconPolicyService.ResolveFontSize(node, widthDimension, heightDimension, policy)
                               ?? TextPolicyService.ResolveFontSize(node, widthDimension, heightDimension, policy),
            BaselineOffset = IconPolicyService.ResolveBaselineOffset(policy),
            OpticalCentering = IconPolicyService.UseOpticalCentering(policy),
            SizeMode = IconPolicyService.ResolveSizeMode(policy),
            SizeBand = sizeBand
        };
    }

    private static string ResolveSemanticClass(ComponentNode node, RuleSelectionContext context, VisualNodeKind kind)
    {
        var semanticClasses = kind switch
        {
            VisualNodeKind.Text => TextSemanticClasses,
            VisualNodeKind.Icon => IconSemanticClasses,
            _ => []
        };

        foreach (var semanticClass in semanticClasses)
        {
            if (RuleSelectorClassifier.HasSemanticClass(node, context, semanticClass))
            {
                return semanticClass;
            }
        }

        return kind switch
        {
            VisualNodeKind.Text => "text",
            VisualNodeKind.Icon => "icon",
            VisualNodeKind.Image => "image",
            VisualNodeKind.Interactive => "interactive",
            VisualNodeKind.Value => "value",
            VisualNodeKind.Collection => "collection",
            VisualNodeKind.Container => "container",
            VisualNodeKind.Spacer => "spacer",
            _ => node.Type.ToString().ToLowerInvariant()
        };
    }

    private static AxisSizing MapAxisSizing(Dimension? dimension)
        => dimension?.Unit switch
        {
            DimensionUnit.Fill or DimensionUnit.Star => AxisSizing.Fill,
            DimensionUnit.Pixels or DimensionUnit.Cells => AxisSizing.Fixed,
            DimensionUnit.Auto or null => AxisSizing.Hug,
            _ => AxisSizing.Hug
        };

    private static Dimension? ResolveMetadataDimension(ComponentNode node, string key)
    {
        if (!node.InstanceOverrides.TryGetValue(key, out var raw) || raw == null)
        {
            return null;
        }

        return raw switch
        {
            double doubleValue => Dimension.Pixels(doubleValue),
            float floatValue => Dimension.Pixels(floatValue),
            int intValue => Dimension.Pixels(intValue),
            long longValue => Dimension.Pixels(longValue),
            decimal decimalValue => Dimension.Pixels((double)decimalValue),
            string stringValue when double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                => Dimension.Pixels(parsed),
            _ => null
        };
    }

    private static Dimension? ResolveAbsoluteOffset(
        ComponentNode node,
        Func<LayoutSpec, Dimension?> selector,
        string metadataKey,
        ResolvedGeneratorPolicy policy,
        string axis)
    {
        double? value = null;
        if (node.Layout != null && selector(node.Layout) is { Unit: DimensionUnit.Pixels } dimension)
        {
            value = dimension.Value;
        }
        else if (ResolveMetadataDimension(node, metadataKey) is { Unit: DimensionUnit.Pixels } metadataDimension)
        {
            value = metadataDimension.Value;
        }

        var adjustment = LayoutPolicyService.ResolveOffsetAdjustment(axis, policy);
        if (value == null)
        {
            return Math.Abs(adjustment) > double.Epsilon ? Dimension.Pixels(adjustment) : null;
        }

        return Dimension.Pixels(value.Value + adjustment);
    }

    private static string? ResolveSourceNodeId(ComponentNode node)
    {
        if (!node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.OriginalPencilId, out var raw) || raw == null)
        {
            return null;
        }

        return GeneratorRuleMetadata.NormalizeValue(raw);
    }

    private static Dictionary<string, object?> ExtractStaticProperties(ComponentNode node)
    {
        if (node.Properties.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in node.Properties.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (pair.Value.IsBound || !ComponentInstanceOverrideSupport.IsSupportedProperty(node, pair.Key))
            {
                continue;
            }

            properties[ComponentInstanceOverrideSupport.NormalizePropertyName(pair.Key)] = pair.Value.Value;
        }

        return properties;
    }

    private static Dictionary<string, IReadOnlyDictionary<string, object?>> ExtractPropertyOverrides(ComponentNode node)
    {
        var overrides = ComponentInstanceOverrideSupport.GetPropertyOverrides(node);
        if (overrides.Count == 0)
        {
            return new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
        }

        return overrides.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyDictionary<string, object?>)new SortedDictionary<string, object?>(pair.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    private sealed class BuilderState
    {
        private readonly Dictionary<string, string> _metricProfileIds = new(StringComparer.Ordinal);
        private readonly List<MetricProfileDefinition> _metricProfiles = [];

        public BuilderState(string documentName, string backendFamily, RuleResolver resolver)
        {
            DocumentName = documentName;
            BackendFamily = backendFamily;
            Resolver = resolver;
        }

        public string DocumentName { get; }

        public string BackendFamily { get; }

        public RuleResolver Resolver { get; }

        public IReadOnlyList<MetricProfileDefinition> MetricProfiles => _metricProfiles;

        public string? ResolveMetricProfileId(string semanticClass, TypographyContract? typography, IconContract? icon)
        {
            if (typography == null && icon == null)
            {
                return null;
            }

            var key = CreateMetricProfileKey(BackendFamily, semanticClass, typography, icon);
            if (_metricProfileIds.TryGetValue(key, out var existingId))
            {
                return existingId;
            }

            var id = "metric-" + (_metricProfiles.Count + 1).ToString(CultureInfo.InvariantCulture);
            _metricProfileIds[key] = id;
            _metricProfiles.Add(new MetricProfileDefinition
            {
                Id = id,
                BackendFamily = BackendFamily,
                SemanticClass = semanticClass,
                Text = typography == null
                    ? null
                    : new TextMetricProfile
                    {
                        ResolvedFontFamily = typography.ResolvedFontFamily,
                        ResolvedFontSize = typography.ResolvedFontSize,
                        ResolvedLineHeight = typography.ResolvedLineHeight,
                        ResolvedLetterSpacing = typography.ResolvedLetterSpacing,
                        WrapText = typography.WrapText,
                        TextGrowth = typography.TextGrowth,
                        SizeBand = typography.SizeBand
                    },
                Icon = icon == null
                    ? null
                    : new IconMetricProfile
                    {
                        ResolvedFontFamily = icon.ResolvedFontFamily,
                        ResolvedFontSize = icon.ResolvedFontSize,
                        BaselineOffset = icon.BaselineOffset,
                        OpticalCentering = icon.OpticalCentering,
                        SizeMode = icon.SizeMode,
                        SizeBand = icon.SizeBand
                    }
            });

            return id;
        }

        private static string CreateMetricProfileKey(
            string backendFamily,
            string semanticClass,
            TypographyContract? typography,
            IconContract? icon)
        {
            var builder = new StringBuilder();
            builder.Append("backend=").Append(backendFamily);
            builder.Append("|semantic=").Append(semanticClass);

            if (typography != null)
            {
                builder.Append("|text.family=").Append(typography.ResolvedFontFamily ?? string.Empty);
                builder.Append("|text.size=").Append(FormatDouble(typography.ResolvedFontSize));
                builder.Append("|text.line=").Append(FormatDouble(typography.ResolvedLineHeight));
                builder.Append("|text.letter=").Append(FormatDouble(typography.ResolvedLetterSpacing));
                builder.Append("|text.wrap=").Append(typography.WrapText ? "true" : "false");
                builder.Append("|text.growth=").Append(typography.TextGrowth ?? string.Empty);
                builder.Append("|text.band=").Append(typography.SizeBand ?? string.Empty);
            }

            if (icon != null)
            {
                builder.Append("|icon.family=").Append(icon.ResolvedFontFamily ?? string.Empty);
                builder.Append("|icon.size=").Append(FormatDouble(icon.ResolvedFontSize));
                builder.Append("|icon.baseline=").Append(FormatDouble(icon.BaselineOffset));
                builder.Append("|icon.center=").Append(icon.OpticalCentering ? "true" : "false");
                builder.Append("|icon.mode=").Append(icon.SizeMode);
                builder.Append("|icon.band=").Append(icon.SizeBand ?? string.Empty);
            }

            return builder.ToString();
        }

        private static string FormatDouble(double? value)
            => value?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
