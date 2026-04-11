using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators;

public sealed class RuleResolver
{
    private readonly string _backend;
    private readonly IReadOnlyList<OrderedRule> _rules;

    public RuleResolver(GeneratorRuleSet? ruleSet, string backend)
    {
        _backend = backend ?? string.Empty;
        _rules = (ruleSet?.Rules ?? [])
            .Select((rule, index) => new OrderedRule(GeneratorRuleExecutionCompiler.Compile(rule), index))
            .ToList();
    }

    public ResolvedGeneratorPolicy Resolve(string documentName, ComponentNode node)
    {
        var resolved = new ResolvedGeneratorPolicy();
        foreach (var match in _rules
                     .Where(candidate => Matches(candidate.Rule.Selector, documentName, node))
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

    private bool Matches(GeneratorRuleSelector selector, string documentName, ComponentNode node)
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
        if (policy.Text.FontSize is { } policyFontSize and > 0d)
        {
            return policyFontSize;
        }

        if (node.Style?.FontSize is { } explicitFontSize)
        {
            return explicitFontSize;
        }

        if (node.Type != ComponentType.Icon)
        {
            return null;
        }

        var width = Pixels(widthDimension);
        var height = Pixels(heightDimension);
        var inferred = (width, height) switch
        {
            ({ } w, { } h) => Math.Min(w, h),
            ({ } w, null) => w,
            (null, { } h) => h,
            _ => 16d
        };

        return inferred <= 0d ? null : inferred;
    }

    public static double? ResolveLetterSpacing(ComponentNode node, ResolvedGeneratorPolicy policy)
        => policy.Text.LetterSpacing ?? node.Style?.LetterSpacing;

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

    public static double? ResolveFontSize(ResolvedGeneratorPolicy policy)
        => policy.Icon.FontSize is > 0d ? policy.Icon.FontSize : null;
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
