using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators;

public readonly record struct RuleSelectionContext(
    ComponentNode? Parent,
    ComponentNode? Grandparent,
    int SiblingIndex)
{
    public static RuleSelectionContext Root => new(null, null, 0);

    public RuleSelectionContext ForChild(ComponentNode parent, int siblingIndex)
        => new(parent, Parent, siblingIndex);
}

public sealed class RuleResolver
{
    private readonly string _backend;
    private readonly IReadOnlyList<OrderedMetricProfile> _metricProfiles;
    private readonly IReadOnlyList<OrderedRule> _rules;

    public RuleResolver(GeneratorRuleSet? ruleSet, string backend)
    {
        _backend = backend ?? string.Empty;
        _metricProfiles = (ruleSet?.MetricProfiles ?? [])
            .Select((profile, index) => new OrderedMetricProfile(GeneratorRuleExecutionCompiler.Compile(profile), index))
            .ToList();
        _rules = (ruleSet?.Rules ?? [])
            .Select((rule, index) => new OrderedRule(GeneratorRuleExecutionCompiler.Compile(rule), index))
            .ToList();
    }

    public ResolvedGeneratorPolicy Resolve(string documentName, ComponentNode node)
        => Resolve(documentName, node, RuleSelectionContext.Root);

    public ResolvedGeneratorPolicy Resolve(string documentName, ComponentNode node, RuleSelectionContext context)
        => Resolve(documentName, node, context, includeMetricProfiles: true);

    public ResolvedGeneratorPolicy Resolve(string documentName, ComponentNode node, RuleSelectionContext context, bool includeMetricProfiles)
    {
        var resolved = new ResolvedGeneratorPolicy();
        IEnumerable<OrderedMetricProfile> metricProfiles = includeMetricProfiles
            ? _metricProfiles
                .Where(candidate => Matches(candidate.Profile.Selector, documentName, node, context))
                .OrderBy(candidate => GeneratorRulePlanner.GetSpecificity(candidate.Profile.Selector))
                .ThenBy(candidate => candidate.Index)
            : Array.Empty<OrderedMetricProfile>();
        foreach (var match in metricProfiles)
        {
            resolved = resolved.Apply(match.Profile.Action);
        }

        foreach (var match in _rules
                     .Where(candidate => Matches(candidate.Rule.Selector, documentName, node, context))
                     .OrderBy(candidate => GeneratorRulePlanner.GetPhaseOrder(candidate.Rule.Phase))
                     .ThenBy(candidate => GeneratorRulePlanner.GetSpecificity(candidate.Rule.Selector))
                     .ThenBy(candidate => candidate.Index))
        {
            resolved = resolved.Apply(match.Rule.Action);
        }

        return resolved;
    }

    public ResolvedGeneratorMotionPolicy ResolveMotion(string documentName, MotionRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var resolved = new ResolvedGeneratorMotionPolicy();
        foreach (var match in _rules
                     .Where(candidate => MatchesMotion(candidate.Rule.Selector, documentName, context))
                     .OrderBy(candidate => GeneratorRulePlanner.GetPhaseOrder(candidate.Rule.Phase))
                     .ThenBy(candidate => GeneratorRulePlanner.GetSpecificity(candidate.Rule.Selector))
                     .ThenBy(candidate => candidate.Index))
        {
            resolved = resolved.Apply(match.Rule.Action.Motion);
        }

        return resolved;
    }

    private bool Matches(GeneratorRuleSelector selector, string documentName, ComponentNode node, RuleSelectionContext context)
    {
        if (!string.IsNullOrWhiteSpace(selector.Backend)
            && !string.Equals(selector.Backend, _backend, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.DocumentName)
            && !string.Equals(selector.DocumentName, documentName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.NodeId)
            && !string.Equals(selector.NodeId, node.Id, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.SourceNodeId))
        {
            if (!node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.OriginalPencilId, out var rawSourceNodeId))
            {
                return false;
            }

            var normalizedSourceNodeId = GeneratorRuleMetadata.NormalizeValue(rawSourceNodeId);
            if (!string.Equals(selector.SourceNodeId, normalizedSourceNodeId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (selector.ComponentType is { } componentType && componentType != node.Type)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.FontFamily))
        {
            var nodeFontFamily = RuleSelectorClassifier.ResolveFontFamily(node);
            if (!string.Equals(selector.FontFamily, nodeFontFamily, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(selector.TextGrowth))
        {
            var nodeTextGrowth = RuleSelectorClassifier.ResolveTextGrowth(node);
            if (!string.Equals(selector.TextGrowth, nodeTextGrowth, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(selector.SemanticClass)
            && !RuleSelectorClassifier.HasSemanticClass(node, context, selector.SemanticClass))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.SizeBand))
        {
            var nodeSizeBand = RuleSelectorClassifier.ResolveSizeBand(node);
            if (!string.Equals(selector.SizeBand, nodeSizeBand, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(selector.MetadataKey))
        {
            return true;
        }

        if (!node.InstanceOverrides.TryGetValue(selector.MetadataKey, out var rawMetadata))
        {
            return false;
        }

        if (selector.MetadataValue == null)
        {
            return true;
        }

        var normalized = GeneratorRuleMetadata.NormalizeValue(rawMetadata);
        return string.Equals(normalized, selector.MetadataValue, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesMotion(GeneratorRuleSelector selector, string documentName, MotionRuleContext context)
    {
        if (!string.IsNullOrWhiteSpace(selector.Backend)
            && !string.Equals(selector.Backend, _backend, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.DocumentName)
            && !string.Equals(selector.DocumentName, documentName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.ClipId)
            && !string.Equals(selector.ClipId, context.ClipId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.TrackId)
            && !string.Equals(selector.TrackId, context.TrackId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.TargetId)
            && !string.Equals(selector.TargetId, context.TargetId, StringComparison.Ordinal))
        {
            return false;
        }

        if (selector.MotionProperty is { } property && property != context.MotionProperty)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.SequenceId)
            && !string.Equals(selector.SequenceId, context.SequenceId, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private sealed record OrderedRule(GeneratorRule Rule, int Index);

    private sealed record OrderedMetricProfile(GeneratorMetricProfile Profile, int Index);
}

internal static class RuleSelectorClassifier
{
    public static string? ResolveFontFamily(ComponentNode node)
        => node.Style?.FontFamily;

    public static string? ResolveTextGrowth(ComponentNode node)
        => node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.PencilTextGrowth, out var rawTextGrowth)
            ? GeneratorRuleMetadata.NormalizeValue(rawTextGrowth)
            : null;

    public static bool HasSemanticClass(ComponentNode node, RuleSelectionContext context, string semanticClass)
    {
        if (string.IsNullOrWhiteSpace(semanticClass))
        {
            return false;
        }

        return semanticClass.Trim().ToLowerInvariant() switch
        {
            "compact-numeric-readout" => IsCompactNumericReadout(node),
            "compact-label" => IsCompactLabel(node, context),
            "button-label" => IsButtonLabel(node, context),
            "tab-label" => IsTabLabel(node, context),
            "right-aligned-quantity" => IsRightAlignedQuantity(node, context),
            "pixel-text" => IsPixelText(node),
            "value-row" => IsValueRow(node),
            "badge-icon" => IsBadgeIcon(node, context),
            "leading-icon" => IsLeadingIcon(node, context),
            "inline-icon" => IsInlineIcon(node, context),
            "icon-glyph" => IsIconGlyph(node),
            "icon-shell" => IsIconShell(node),
            "heading-label" => IsHeadingLabel(node, context),
            "stacked-text-line" => IsStackedTextLine(node, context),
            "stacked-text-group" => IsStackedTextGroup(node),
            _ => false
        };
    }

    public static string? ResolveSizeBand(ComponentNode node)
    {
        var nominalSize = ResolveNominalSize(node);
        if (nominalSize is not > 0d)
        {
            return null;
        }

        return nominalSize.Value switch
        {
            <= 8.5d => "xsmall",
            <= 10.5d => "small",
            <= 14.5d => "medium",
            <= 24d => "large",
            _ => "xlarge"
        };
    }

    private static bool IsPixelText(ComponentNode node)
    {
        if (node.Type == ComponentType.Icon)
        {
            return false;
        }

        var textGrowth = ResolveTextGrowth(node);
        if (string.Equals(textGrowth, "fixed-width", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fontFamily = ResolveFontFamily(node);
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            return false;
        }

        var normalized = fontFamily.Trim().ToLowerInvariant();
        return normalized.Contains("press start", StringComparison.Ordinal)
               || normalized.Contains("pixel", StringComparison.Ordinal)
               || normalized.Contains("bitmap", StringComparison.Ordinal)
               || normalized.Contains("arcade", StringComparison.Ordinal);
    }

    private static bool IsIconGlyph(ComponentNode node)
    {
        if (node.Type == ComponentType.Icon)
        {
            return true;
        }

        var fontFamily = ResolveFontFamily(node);
        return string.Equals(fontFamily, "lucide", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIconShell(ComponentNode node)
        => node.Type == ComponentType.Container
           && node.Children.Count == 1
           && IsIconGlyph(node.Children[0]);

    private static bool IsCompactNumericReadout(ComponentNode node)
        => IsTextLabel(node)
           && IsCompactPixelText(node)
           && LooksLikeNoWrapText(node)
           && IsNumericDominantText(node);

    private static bool IsCompactLabel(ComponentNode node, RuleSelectionContext context)
        => IsTextLabel(node)
           && IsCompactPixelText(node)
           && LooksLikeNoWrapText(node)
           && !IsNumericDominantText(node)
           && !IsButtonLabel(node, context)
           && !IsTabLabel(node, context)
           && !IsRightAlignedQuantity(node, context)
           && !IsHeadingLabel(node, context)
           && !IsStackedTextLine(node, context);

    private static bool IsButtonLabel(ComponentNode node, RuleSelectionContext context)
        => IsTextLabel(node)
           && context.Parent?.Type is ComponentType.Button or ComponentType.MenuItem;

    private static bool IsTabLabel(ComponentNode node, RuleSelectionContext context)
    {
        if (!IsTextLabel(node) || !IsCompactPixelText(node) || !LooksLikeNoWrapText(node))
        {
            return false;
        }

        var rowContainer = ResolveTabRowContainer(context);
        if (!IsHorizontalContainer(rowContainer))
        {
            return false;
        }

        var siblings = rowContainer!.Children;
        if (siblings.Count < 2)
        {
            return false;
        }

        var tabLikeCount = siblings.Count(IsTabLikeSibling);
        if (tabLikeCount < Math.Max(2, siblings.Count - 1))
        {
            return false;
        }

        var heights = siblings
            .Select(ResolveNominalSize)
            .Where(static value => value is > 0d)
            .Select(static value => value!.Value)
            .ToList();
        if (heights.Count >= 2 && heights.Max() - heights.Min() > 8d)
        {
            return false;
        }

        return siblings.All(LooksLikeSingleLineContent);
    }

    private static bool IsRightAlignedQuantity(ComponentNode node, RuleSelectionContext context)
    {
        if (!IsTextLabel(node) || !IsCompactPixelText(node) || !LooksLikeNoWrapText(node))
        {
            return false;
        }

        var rowContainer = ResolveRowContainer(context);
        var isLastInHorizontalRow = IsHorizontalContainer(rowContainer)
                                    && context.SiblingIndex == rowContainer!.Children.Count - 1;
        var endPinned = node.Layout?.Align == Alignment.End
                        || rowContainer?.Layout?.Justify is Justification.End or Justification.SpaceBetween;

        return (isLastInHorizontalRow || endPinned)
               && (IsNumericDominantText(node) || LooksLikeQuantityId(node.Id));
    }

    private static bool IsInlineIcon(ComponentNode node, RuleSelectionContext context)
        => IsIconGlyph(node)
           && IsHorizontalContainer(context.Parent)
           && context.Parent!.Children.Any(static child => IsTextLabel(child))
           && !IsIconShell(context.Parent)
           && !IsLeadingIcon(node, context)
           && !IsBadgeIcon(node, context);

    private static bool IsLeadingIcon(ComponentNode node, RuleSelectionContext context)
        => IsIconGlyph(node)
           && IsHorizontalContainer(context.Parent)
           && context.SiblingIndex == 0
           && context.Parent!.Children.Skip(1).Any(static child => IsTextLabel(child));

    private static bool IsBadgeIcon(ComponentNode node, RuleSelectionContext context)
        => IsIconGlyph(node)
           && context.Parent != null
           && IsIconShell(context.Parent)
           && ResolveNominalSize(context.Parent) is { } shellSize
           && shellSize <= 44d;

    private static bool IsHeadingLabel(ComponentNode node, RuleSelectionContext context)
    {
        if (!IsTextLabel(node) || context.SiblingIndex != 0)
        {
            return false;
        }

        if (!IsVerticalTextContainer(context.Parent) || context.Parent!.Children.Count < 2)
        {
            return false;
        }

        return context.Parent.Children
            .Skip(1)
            .Any(static child => !IsTextLabel(child));
    }

    private static bool IsStackedTextLine(ComponentNode node, RuleSelectionContext context)
        => IsTextLabel(node)
           && IsVerticalTextContainer(context.Parent)
           && context.Parent!.Children.Count >= 2
           && context.Parent.Children.All(static child => IsTextLabel(child));

    private static bool IsStackedTextGroup(ComponentNode node)
        => node.Type is ComponentType.Container or ComponentType.Panel or ComponentType.Stack
           && IsVerticalTextContainer(node)
           && node.Children.Count >= 2
           && node.Children.All(static child => IsTextLabel(child));

    private static bool IsValueRow(ComponentNode node)
    {
        if (!IsHorizontalContainer(node) || node.Children.Count < 2)
        {
            return false;
        }

        var hasValueLikeChild = node.Children
            .Select((child, index) => new RuleSelectionContext(node, null, index))
            .Any(pairContext => IsRightAlignedQuantity(node.Children[pairContext.SiblingIndex], pairContext)
                                || IsCompactNumericReadout(node.Children[pairContext.SiblingIndex]));
        if (!hasValueLikeChild)
        {
            return false;
        }

        return node.Children.Any(static child => IsTextLabel(child) || IsIconGlyph(child));
    }

    private static bool IsTextLabel(ComponentNode node)
        => node.Type is ComponentType.Label or ComponentType.Badge
           && !IsIconGlyph(node);

    private static bool IsVerticalTextContainer(ComponentNode? node)
        => node?.Layout?.Type is LayoutType.Vertical or LayoutType.Stack;

    private static bool IsHorizontalContainer(ComponentNode? node)
        => node?.Layout?.Type == LayoutType.Horizontal;

    private static bool IsCompactPixelText(ComponentNode node)
        => IsPixelText(node)
           && ResolveSizeBand(node) is "xsmall" or "small";

    private static bool LooksLikeNoWrapText(ComponentNode node)
    {
        var text = ResolveLiteralText(node);
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return text.IndexOfAny(['\r', '\n']) < 0;
    }

    private static bool IsNumericDominantText(ComponentNode node)
    {
        var text = ResolveLiteralText(node);
        if (string.IsNullOrWhiteSpace(text))
        {
            return LooksLikeQuantityId(node.Id);
        }

        var digitsOrQuantity = 0;
        var letters = 0;
        foreach (var ch in text)
        {
            if (char.IsDigit(ch) || ch is '%' or '/' or '+' or '-' or ':' or 'x' or 'X')
            {
                digitsOrQuantity++;
            }
            else if (char.IsLetter(ch))
            {
                letters++;
            }
        }

        return digitsOrQuantity > 0 && digitsOrQuantity >= letters;
    }

    private static bool LooksLikeQuantityId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var normalized = id.Trim().ToLowerInvariant();
        return normalized.Contains("qty", StringComparison.Ordinal)
               || normalized.Contains("count", StringComparison.Ordinal)
               || normalized.Contains("amount", StringComparison.Ordinal)
               || normalized.Contains("value", StringComparison.Ordinal)
               || normalized.Contains("number", StringComparison.Ordinal)
               || normalized.Contains("price", StringComparison.Ordinal)
               || normalized.Contains("cost", StringComparison.Ordinal);
    }

    private static bool IsTabLikeSibling(ComponentNode node)
        => node.Type is ComponentType.Label or ComponentType.Badge or ComponentType.Button or ComponentType.MenuItem
           || node.Children.Any(static child => child.Type is ComponentType.Label or ComponentType.Badge);

    private static bool LooksLikeSingleLineContent(ComponentNode node)
    {
        var literal = ResolveLiteralText(node);
        if (!string.IsNullOrWhiteSpace(literal))
        {
            return literal.IndexOfAny(['\r', '\n']) < 0;
        }

        return node.Children.All(LooksLikeSingleLineContent);
    }

    private static ComponentNode? ResolveRowContainer(RuleSelectionContext context)
        => IsHorizontalContainer(context.Parent)
            ? context.Parent
            : context.Parent?.Type is ComponentType.Button or ComponentType.MenuItem && IsHorizontalContainer(context.Grandparent)
                ? context.Grandparent
                : null;

    private static ComponentNode? ResolveTabRowContainer(RuleSelectionContext context)
        => ResolveRowContainer(context);

    private static string? ResolveLiteralText(ComponentNode node)
    {
        if (TryResolveLiteralProperty(node, "text", out var text)
            || TryResolveLiteralProperty(node, "content", out text)
            || TryResolveLiteralProperty(node, "value", out text))
        {
            return text;
        }

        return null;
    }

    private static bool TryResolveLiteralProperty(ComponentNode node, string propertyName, out string? text)
    {
        text = null;
        foreach (var pair in node.Properties)
        {
            if (!string.Equals(pair.Key, propertyName, StringComparison.OrdinalIgnoreCase) || pair.Value.IsBound)
            {
                continue;
            }

            text = pair.Value.Value?.ToString();
            return !string.IsNullOrWhiteSpace(text);
        }

        return false;
    }

    private static double? ResolveNominalSize(ComponentNode node)
    {
        if (node.Style?.FontSize is > 0d and var fontSize)
        {
            return fontSize;
        }

        var width = Pixels(node.Layout?.Width ?? node.Style?.Width);
        var height = Pixels(node.Layout?.Height ?? node.Style?.Height);

        return (width, height) switch
        {
            ({ } w, { } h) when w > 0d && h > 0d => Math.Min(w, h),
            ({ } w, null) when w > 0d => w,
            (null, { } h) when h > 0d => h,
            _ => null
        };
    }

    private static double? Pixels(Dimension? dimension)
        => dimension switch
        {
            { Unit: DimensionUnit.Pixels } pixels => pixels.Value,
            { Unit: DimensionUnit.Cells } cells => cells.Value,
            _ => null
        };
}

public static class TextPolicyService
{
    public static string? ResolveFontFamily(ComponentNode node, ResolvedGeneratorPolicy policy)
        => policy.Text.FontFamily ?? node.Style?.FontFamily;

    public static bool ShouldWrapText(ComponentNode node, ResolvedGeneratorPolicy policy)
    {
        if (policy.Text.WrapText is { } wrapText)
        {
            return wrapText;
        }

        var textGrowth = policy.Text.TextGrowth
            ?? (node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.PencilTextGrowth, out var raw)
                ? raw as string
                : null);

        return string.Equals(textGrowth, "fixed-width", StringComparison.OrdinalIgnoreCase);
    }

    public static double? ResolveLineHeight(StyleSpec? style, double? fontSize, ResolvedGeneratorPolicy policy)
    {
        var lineHeight = policy.Text.LineHeight ?? style?.LineHeight;
        if (lineHeight is not > 0d)
        {
            return null;
        }

        if (lineHeight <= 5d)
        {
            return fontSize is > 0d ? lineHeight.Value * fontSize.Value : null;
        }

        return lineHeight;
    }

    public static double? ResolveLineSpacing(ComponentNode node, Dimension? widthDimension, Dimension? heightDimension, ResolvedGeneratorPolicy policy)
    {
        var lineHeight = policy.Text.LineHeight ?? node.Style?.LineHeight;
        if (lineHeight is not > 0d)
        {
            return null;
        }

        if (lineHeight <= 5d)
        {
            return lineHeight;
        }

        var fontSize = ResolveFontSize(node, widthDimension, heightDimension, policy);
        return fontSize is > 0d ? lineHeight.Value / fontSize.Value : null;
    }

    public static double? ResolveFontSize(ComponentNode node, Dimension? widthDimension, Dimension? heightDimension, ResolvedGeneratorPolicy policy)
    {
        double? fontSize = policy.Text.FontSize is { } policyFontSize and > 0d
            ? policyFontSize
            : null;

        if (fontSize is not > 0d && node.Style?.FontSize is { } explicitFontSize)
        {
            fontSize = explicitFontSize;
        }

        if (fontSize is not > 0d && node.Type == ComponentType.Icon)
        {
            var width = Pixels(widthDimension);
            var height = Pixels(heightDimension);
            var inferred = (width, height) switch
            {
                ({ } w, { } h) => Math.Min(w, h),
                ({ } w, null) => w,
                (null, { } h) => h,
                _ => 16d
            };

            fontSize = inferred <= 0d ? null : inferred;
        }

        if (policy.Text.FontSizeDelta is { } fontSizeDelta)
        {
            fontSize = (fontSize ?? 0d) + fontSizeDelta;
        }

        return fontSize is > 0d ? fontSize : null;
    }

    public static double? ResolveLetterSpacing(ComponentNode node, ResolvedGeneratorPolicy policy)
    {
        double? letterSpacing = policy.Text.LetterSpacing ?? node.Style?.LetterSpacing;
        if (policy.Text.LetterSpacingDelta is { } letterSpacingDelta)
        {
            letterSpacing = (letterSpacing ?? 0d) + letterSpacingDelta;
        }

        return letterSpacing;
    }

    private static double? Pixels(Dimension? dimension)
        => dimension switch
        {
            { Unit: DimensionUnit.Pixels } pixels => pixels.Value,
            { Unit: DimensionUnit.Cells } cells => cells.Value,
            _ => null
        };
}

public static class IconPolicyService
{
    public static double ResolveBaselineOffset(ResolvedGeneratorPolicy policy)
        => policy.Icon.BaselineOffset ?? 0d;

    public static bool UseOpticalCentering(ResolvedGeneratorPolicy policy)
        => policy.Icon.OpticalCentering ?? true;

    public static string ResolveSizeMode(ResolvedGeneratorPolicy policy)
        => string.IsNullOrWhiteSpace(policy.Icon.SizeMode) ? "fit-box" : policy.Icon.SizeMode!;

    public static double? ResolveFontSize(ComponentNode node, Dimension? widthDimension, Dimension? heightDimension, ResolvedGeneratorPolicy policy)
    {
        if (policy.Icon.FontSize is not > 0d && policy.Icon.FontSizeDelta is not { })
        {
            return null;
        }

        double? fontSize = policy.Icon.FontSize is > 0d
            ? policy.Icon.FontSize
            : null;

        if (fontSize is not > 0d)
        {
            var width = Pixels(widthDimension);
            var height = Pixels(heightDimension);
            var inferred = (width, height) switch
            {
                ({ } w, { } h) => Math.Min(w, h),
                ({ } w, null) => w,
                (null, { } h) => h,
                _ => 16d
            };

            fontSize = inferred <= 0d ? null : inferred;
        }

        if (policy.Icon.FontSizeDelta is { } fontSizeDelta)
        {
            fontSize = (fontSize ?? 0d) + fontSizeDelta;
        }

        return fontSize is > 0d ? fontSize : null;
    }

    private static double? Pixels(Dimension? dimension)
        => dimension switch
        {
            { Unit: DimensionUnit.Pixels } pixels => pixels.Value,
            { Unit: DimensionUnit.Cells } cells => cells.Value,
            _ => null
        };
}

public static class LayoutPolicyService
{
    public static bool HasAbsolutePlacement(ComponentNode node, ResolvedGeneratorPolicy policy)
    {
        var positionMode = policy.Layout.PositionMode;
        if (!string.IsNullOrWhiteSpace(positionMode))
        {
            if (string.Equals(positionMode, "absolute", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(positionMode, "relative", StringComparison.OrdinalIgnoreCase)
                || string.Equals(positionMode, "flow", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (policy.Layout.ForceAbsolutePositioning is { } forced)
        {
            return forced;
        }

        return node.Layout?.IsAbsolutePositioned == true
               || node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.PencilPosition, out var raw)
               && raw is string value
               && string.Equals(value, "absolute", StringComparison.OrdinalIgnoreCase);
    }

    public static double? ResolveFlexibleSize(
        Dimension? dimension,
        string axis,
        LayoutType? parentLayout,
        bool isFlexibleContainer,
        ResolvedGeneratorPolicy policy)
    {
        var explicitPreference = axis == "width"
            ? policy.Layout.PreferContentWidth
            : policy.Layout.PreferContentHeight;
        if (explicitPreference == true)
        {
            return null;
        }

        if (dimension is { Unit: DimensionUnit.Fill or DimensionUnit.Star })
        {
            return dimension.Value.Value == 0 ? 1d : dimension.Value.Value;
        }

        var explicitStretch = axis == "width"
            ? policy.Layout.StretchWidth
            : policy.Layout.StretchHeight;

        if (explicitStretch is { } stretch)
        {
            return stretch ? 1d : null;
        }

        if (dimension != null || parentLayout == null || !isFlexibleContainer)
        {
            return null;
        }

        return (axis, parentLayout) switch
        {
            ("width", LayoutType.Horizontal) => 1d,
            ("height", LayoutType.Vertical or LayoutType.Stack) => 1d,
            ("width", LayoutType.Vertical or LayoutType.Stack or LayoutType.Grid or LayoutType.Dock) => 1d,
            ("height", LayoutType.Horizontal) => 1d,
            _ => null
        };
    }

    public static bool ShouldPreferContentSize(
        string axis,
        bool hasParentLayout,
        LayoutSpec? layout,
        bool isFlexibleContainer,
        ResolvedGeneratorPolicy policy)
    {
        var explicitPreference = axis == "width"
            ? policy.Layout.PreferContentWidth
            : policy.Layout.PreferContentHeight;

        if (explicitPreference is { } preferContent)
        {
            return preferContent;
        }

        if (hasParentLayout || layout == null || !isFlexibleContainer)
        {
            return false;
        }

        var dimension = axis == "width" ? layout.Width : layout.Height;
        return dimension is null or { Unit: DimensionUnit.Auto };
    }

    public static double? ResolvePreferredSize(
        Dimension? dimension,
        string axis,
        ResolvedGeneratorPolicy policy)
    {
        var resolved = Pixels(dimension);
        var delta = axis == "width"
            ? policy.Layout.PreferredWidthDelta
            : policy.Layout.PreferredHeightDelta;

        if (delta is not { } deltaValue)
        {
            return resolved;
        }

        var baseline = resolved ?? 0d;
        var adjusted = baseline + deltaValue;
        return adjusted > 0d ? adjusted : null;
    }

    private static double? Pixels(Dimension? dimension)
        => dimension switch
        {
            { Unit: DimensionUnit.Pixels } pixels => pixels.Value,
            { Unit: DimensionUnit.Cells } cells => cells.Value,
            _ => null
        };

    public static Spacing? ResolveGap(Spacing? spacing, ResolvedGeneratorPolicy policy)
    {
        var resolved = policy.Layout.Gap is { } gap ? Spacing.Uniform(gap) : spacing;
        return ApplySpacingDelta(resolved, policy.Layout.GapDelta);
    }

    public static Spacing? ResolvePadding(Spacing? spacing, ResolvedGeneratorPolicy policy)
    {
        var resolved = policy.Layout.Padding is { } padding ? Spacing.Uniform(padding) : spacing;
        resolved = ApplySpacingDelta(resolved, policy.Layout.PaddingDelta);

        if (resolved == null
            && (policy.Layout.PaddingTop != null
                || policy.Layout.PaddingRight != null
                || policy.Layout.PaddingBottom != null
                || policy.Layout.PaddingLeft != null
                || policy.Layout.PaddingTopDelta != null
                || policy.Layout.PaddingRightDelta != null
                || policy.Layout.PaddingBottomDelta != null
                || policy.Layout.PaddingLeftDelta != null))
        {
            resolved = Spacing.Zero;
        }

        if (resolved == null)
        {
            return null;
        }

        var current = resolved ?? Spacing.Zero;
        return new Spacing(
            ResolveSpacingEdge(current.Top, policy.Layout.PaddingTop, policy.Layout.PaddingTopDelta),
            ResolveSpacingEdge(current.Right, policy.Layout.PaddingRight, policy.Layout.PaddingRightDelta),
            ResolveSpacingEdge(current.Bottom, policy.Layout.PaddingBottom, policy.Layout.PaddingBottomDelta),
            ResolveSpacingEdge(current.Left, policy.Layout.PaddingLeft, policy.Layout.PaddingLeftDelta));
    }

    public static double ResolveOffsetAdjustment(string axis, ResolvedGeneratorPolicy policy)
        => axis == "x"
            ? (policy.Layout.OffsetX ?? 0d) + (policy.Layout.OffsetXDelta ?? 0d)
            : (policy.Layout.OffsetY ?? 0d) + (policy.Layout.OffsetYDelta ?? 0d);

    public static Dimension? ResolveInset(string edge, Dimension? dimension, ResolvedGeneratorPolicy policy)
    {
        var (absolute, delta) = edge.Trim().ToLowerInvariant() switch
        {
            "top" => (policy.Layout.InsetTop, policy.Layout.InsetTopDelta),
            "right" => (policy.Layout.InsetRight, policy.Layout.InsetRightDelta),
            "bottom" => (policy.Layout.InsetBottom, policy.Layout.InsetBottomDelta),
            "left" => (policy.Layout.InsetLeft, policy.Layout.InsetLeftDelta),
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Inset edge must be top, right, bottom, or left.")
        };

        var resolved = absolute switch
        {
            { } value => Dimension.Pixels(value),
            _ => dimension
        };

        if (delta is not { } deltaValue || Math.Abs(deltaValue) <= double.Epsilon)
        {
            return resolved;
        }

        return resolved switch
        {
            { Unit: DimensionUnit.Pixels } pixels => Dimension.Pixels(pixels.Value + deltaValue),
            { Unit: DimensionUnit.Cells } cells => new Dimension(cells.Value + deltaValue, DimensionUnit.Cells),
            null => Dimension.Pixels(deltaValue),
            _ => resolved
        };
    }

    public static string? ResolveAnchorPreset(ResolvedGeneratorPolicy policy)
        => string.IsNullOrWhiteSpace(policy.Layout.AnchorPreset) ? null : policy.Layout.AnchorPreset;

    public static string? ResolvePivotPreset(ResolvedGeneratorPolicy policy)
        => string.IsNullOrWhiteSpace(policy.Layout.PivotPreset) ? null : policy.Layout.PivotPreset;

    public static string? ResolveRectTransformMode(ResolvedGeneratorPolicy policy)
        => string.IsNullOrWhiteSpace(policy.Layout.RectTransformMode) ? null : policy.Layout.RectTransformMode;

    public static string? ResolveEdgeInsetPolicy(ResolvedGeneratorPolicy policy)
        => string.IsNullOrWhiteSpace(policy.Layout.EdgeInsetPolicy) ? null : policy.Layout.EdgeInsetPolicy;

    public static string? ResolvePositionMode(ResolvedGeneratorPolicy policy)
        => string.IsNullOrWhiteSpace(policy.Layout.PositionMode) ? null : policy.Layout.PositionMode;

    public static string? ResolveFlexAlignmentPreset(ResolvedGeneratorPolicy policy)
        => string.IsNullOrWhiteSpace(policy.Layout.FlexAlignmentPreset) ? null : policy.Layout.FlexAlignmentPreset;

    private static Spacing? ApplySpacingDelta(Spacing? spacing, double? delta)
    {
        if (delta is not { } amount || Math.Abs(amount) <= double.Epsilon)
        {
            return spacing;
        }

        var current = spacing ?? Spacing.Zero;
        return new Spacing(
            ClampNonNegative(current.Top + amount),
            ClampNonNegative(current.Right + amount),
            ClampNonNegative(current.Bottom + amount),
            ClampNonNegative(current.Left + amount));
    }

    private static double ClampNonNegative(double value)
        => value < 0d ? 0d : value;

    private static double ResolveSpacingEdge(double baseline, double? absolute, double? delta)
    {
        var resolved = absolute ?? baseline;
        if (delta is { } amount)
        {
            resolved += amount;
        }

        return ClampNonNegative(resolved);
    }
}
