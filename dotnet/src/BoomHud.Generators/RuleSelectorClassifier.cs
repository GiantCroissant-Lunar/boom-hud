using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators;

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
            "value-row" => IsValueRow(node, context),
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

    private static bool IsValueRow(ComponentNode node, RuleSelectionContext context)
    {
        if (!IsHorizontalContainer(node) || node.Children.Count < 2)
        {
            return false;
        }

        var hasValueLikeChild = node.Children
            .Select((child, index) => (child, ctx: context.ForChild(node, index)))
            .Any(pair => IsRightAlignedQuantity(pair.child, pair.ctx)
                         || IsCompactNumericReadout(pair.child));
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
