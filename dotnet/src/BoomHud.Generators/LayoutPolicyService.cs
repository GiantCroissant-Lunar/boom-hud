using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators;

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
