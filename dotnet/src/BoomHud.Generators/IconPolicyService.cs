using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators;

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
