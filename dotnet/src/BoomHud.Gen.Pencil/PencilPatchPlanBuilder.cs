using System.Text.Encodings.Web;
using System.Text.Json;
using BoomHud.Abstractions.IR;
using BoomHud.Generators.VisualIR;

namespace BoomHud.Gen.Pencil;

public sealed record PencilPatchPlan
{
    public required string DocumentName { get; init; }

    public required string TargetFormat { get; init; }

    public required int ActionCount { get; init; }

    public IReadOnlyList<PencilPatchPlanStep> Steps { get; init; } = [];
}

public sealed record PencilPatchPlanStep
{
    public required int Order { get; init; }

    public required string TargetStableId { get; init; }

    public string? TargetPenId { get; init; }

    public string? TargetSemanticClass { get; init; }

    public string? TargetSourceSemanticRole { get; init; }

    public string? ReasonPhase { get; init; }

    public string? ActionType { get; init; }

    public required string Description { get; init; }

    public required bool RequiresStructuralRewrite { get; init; }

    public IReadOnlyDictionary<string, object?> SuggestedProperties { get; init; }
        = new Dictionary<string, object?>(StringComparer.Ordinal);
}

public static class PencilPatchPlanBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static PencilPatchPlan? Build(VisualDocument visualDocument, VisualRefinementSummary refinementSummary)
    {
        ArgumentNullException.ThrowIfNull(visualDocument);
        ArgumentNullException.ThrowIfNull(refinementSummary);

        if (refinementSummary.Actions.Count == 0)
        {
            return null;
        }

        var nodesByStableId = Flatten(visualDocument.Root).ToDictionary(static node => node.StableId, StringComparer.Ordinal);
        var steps = refinementSummary.Actions
            .Select(action => BuildStep(action, nodesByStableId))
            .ToList();

        return new PencilPatchPlan
        {
            DocumentName = visualDocument.DocumentName,
            TargetFormat = "pen",
            ActionCount = steps.Count,
            Steps = steps
        };
    }

    public static string ToJson(PencilPatchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return JsonSerializer.Serialize(plan, JsonOptions);
    }

    private static PencilPatchPlanStep BuildStep(
        VisualRefinementAction action,
        Dictionary<string, VisualNode> nodesByStableId)
    {
        nodesByStableId.TryGetValue(action.TargetStableId, out var node);
        var suggestedProperties = BuildSuggestedProperties(action.ActionType, node);

        return new PencilPatchPlanStep
        {
            Order = action.Iteration,
            TargetStableId = action.TargetStableId,
            TargetPenId = ResolvePenTargetId(node, action.TargetStableId),
            TargetSemanticClass = action.TargetSemanticClass ?? node?.SemanticClass,
            TargetSourceSemanticRole = action.TargetSourceSemanticRole ?? node?.SourceSemanticRole,
            ReasonPhase = action.ReasonPhase,
            ActionType = action.ActionType,
            Description = action.Description,
            RequiresStructuralRewrite = string.Equals(action.ActionType, "panel-motif-split", StringComparison.Ordinal),
            SuggestedProperties = suggestedProperties
        };
    }

    private static Dictionary<string, object?> BuildSuggestedProperties(string actionType, VisualNode? node)
    {
        if (node == null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        return actionType switch
        {
            "bounded-offset-adjustment" or "preserve-start-edge-shell-contract"
                => BuildOffsetProperties(node),
            "preserve-preferred-width"
                => BuildDimensionProperties(node.Box.Width, axis: "width"),
            "preserve-preferred-height"
                => BuildDimensionProperties(node.Box.Height, axis: "height"),
            "tighten-shell-padding"
                => BuildSpacingProperties(node),
            "disable-unwanted-cross-axis-stretch"
                => BuildAlignmentProperties(node),
            "preserve-cross-axis-hug"
                => BuildHugProperties(node),
            "edge-contract-adjustment"
                => BuildShellProperties(node),
            "metric-profile-adjustment"
                => BuildMetricProperties(node),
            _ => new Dictionary<string, object?>(StringComparer.Ordinal)
        };
    }

    private static Dictionary<string, object?> BuildOffsetProperties(VisualNode node)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (node.Box.IsAbsolutePositioned)
        {
            result["layout"] = "none";
        }

        if (node.Box.Left is { } left)
        {
            result["x"] = ConvertNumericDimension(left);
        }

        if (node.Box.Top is { } top)
        {
            result["y"] = ConvertNumericDimension(top);
        }

        return result;
    }

    private static Dictionary<string, object?> BuildDimensionProperties(Dimension? dimension, string axis)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (dimension != null)
        {
            result[axis] = ConvertDimension(dimension.Value);
        }

        return result;
    }

    private static Dictionary<string, object?> BuildSpacingProperties(VisualNode node)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (node.Box.Padding is { } padding)
        {
            result["padding"] = ConvertSpacing(padding);
        }

        if (node.Box.Gap is { } gap)
        {
            result["gap"] = ConvertSpacingValue(gap.Left);
        }

        return result;
    }

    private static Dictionary<string, object?> BuildAlignmentProperties(VisualNode node)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (node.Box.Align is { } align && align != Alignment.Start)
        {
            result["alignItems"] = align switch
            {
                Alignment.Center => "center",
                Alignment.End => "end",
                Alignment.Stretch => "stretch",
                _ => "start"
            };
        }

        if (node.Box.Justify is { } justify && justify != Justification.Start)
        {
            result["justifyContent"] = justify switch
            {
                Justification.Center => "center",
                Justification.End => "end",
                Justification.SpaceBetween => "space-between",
                Justification.SpaceAround => "space-around",
                Justification.SpaceEvenly => "space-evenly",
                _ => "start"
            };
        }

        return result;
    }

    private static Dictionary<string, object?> BuildHugProperties(VisualNode node)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (node.Box.Width is { Unit: DimensionUnit.Auto })
        {
            result["width"] = "auto";
        }

        if (node.Box.Height is { Unit: DimensionUnit.Auto })
        {
            result["height"] = "auto";
        }

        return result;
    }

    private static Dictionary<string, object?> BuildShellProperties(VisualNode node)
    {
        var result = BuildOffsetProperties(node);
        foreach (var pair in BuildDimensionProperties(node.Box.Width, axis: "width"))
        {
            result[pair.Key] = pair.Value;
        }

        foreach (var pair in BuildDimensionProperties(node.Box.Height, axis: "height"))
        {
            result[pair.Key] = pair.Value;
        }

        foreach (var pair in BuildSpacingProperties(node))
        {
            result[pair.Key] = pair.Value;
        }

        foreach (var pair in BuildAlignmentProperties(node))
        {
            result[pair.Key] = pair.Value;
        }

        if (node.Box.LayoutType != null)
        {
            result["layout"] = node.Box.LayoutType switch
            {
                LayoutType.Horizontal => "horizontal",
                LayoutType.Grid => "grid",
                LayoutType.Absolute => "none",
                _ => "vertical"
            };
        }

        if (node.Box.ClipContent)
        {
            result["clip"] = true;
        }

        return result;
    }

    private static Dictionary<string, object?> BuildMetricProperties(VisualNode node)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (node.Typography != null)
        {
            if (!string.IsNullOrWhiteSpace(node.Typography.ResolvedFontFamily))
            {
                result["fontFamily"] = node.Typography.ResolvedFontFamily;
            }

            if (node.Typography.ResolvedFontSize is { } fontSize)
            {
                result["fontSize"] = ConvertSpacingValue(fontSize);
            }

            if (node.Typography.ResolvedLetterSpacing is { } letterSpacing)
            {
                result["letterSpacing"] = letterSpacing;
            }

            if (!string.IsNullOrWhiteSpace(node.Typography.TextGrowth))
            {
                result["textGrowth"] = node.Typography.TextGrowth;
            }
        }

        if (node.Icon != null)
        {
            if (!string.IsNullOrWhiteSpace(node.Icon.ResolvedFontFamily))
            {
                result["iconFontFamily"] = node.Icon.ResolvedFontFamily;
            }

            if (node.Icon.ResolvedFontSize is { } iconSize)
            {
                result["fontSize"] = ConvertSpacingValue(iconSize);
            }
        }

        return result;
    }

    private static string ResolvePenTargetId(VisualNode? node, string stableId)
        => !string.IsNullOrWhiteSpace(node?.SourceNodeId)
                ? node.SourceNodeId!
                : !string.IsNullOrWhiteSpace(node?.SourceId)
                    ? node.SourceId!
                : stableId;

    private static IEnumerable<VisualNode> Flatten(VisualNode root)
    {
        yield return root;
        foreach (var child in root.Children)
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    private static object ConvertDimension(Dimension dimension)
    {
        return dimension.Unit switch
        {
            DimensionUnit.Pixels => ConvertSpacingValue(dimension.Value),
            DimensionUnit.Percent => $"{dimension.Value:0.####}%",
            DimensionUnit.Auto => "auto",
            DimensionUnit.Fill or DimensionUnit.Star => "fill_container",
            DimensionUnit.Cells => $"{dimension.Value:0.####}cell",
            _ => ConvertSpacingValue(dimension.Value)
        };
    }

    private static double ConvertNumericDimension(Dimension dimension)
        => dimension.Value;

    private static object ConvertSpacing(Spacing spacing)
    {
        if (spacing.Top == spacing.Right && spacing.Right == spacing.Bottom && spacing.Bottom == spacing.Left)
        {
            return ConvertSpacingValue(spacing.Top);
        }

        return new object[]
        {
            ConvertSpacingValue(spacing.Top),
            ConvertSpacingValue(spacing.Right),
            ConvertSpacingValue(spacing.Bottom),
            ConvertSpacingValue(spacing.Left)
        };
    }

    private static double ConvertSpacingValue(double value)
        => Math.Round(value, 4);
}
