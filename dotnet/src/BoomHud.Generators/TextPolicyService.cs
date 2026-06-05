using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators;

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
        double? letterSpacing = policy.Text.LetterSpacing;
        if (letterSpacing is not { } && node.Style?.LetterSpacing is { } explicitSpacing)
        {
            letterSpacing = explicitSpacing;
        }

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
