using BoomHud.Abstractions.IR;
using System.Text.Json;

namespace BoomHud.Generators.VisualIR;

public static class VisualRefinementPlanner
{
    private static readonly string[] PhasePriority =
    [
        "structural-match",
        "outer-frame-match",
        "inner-layout-match",
        "text-icon-metrics",
        "polish-offsets"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static VisualRefinementSummary Plan(
        VisualDocument document,
        RecursiveFidelityScoreNode? scoreTree = null,
        int iterationBudget = 4,
        IReadOnlyList<VisualMeasuredIssue>? measuredIssues = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var boundedBudget = Math.Max(0, iterationBudget);
        var normalizedMeasuredIssues = NormalizeMeasuredIssues(measuredIssues);
        if (scoreTree != null && normalizedMeasuredIssues.Count == 0 && IsPerfectScoreTree(scoreTree))
        {
            return new VisualRefinementSummary
            {
                IterationBudget = boundedBudget,
                IterationCount = 0,
                Converged = true,
                ScoreTree = scoreTree,
                MeasuredIssues = normalizedMeasuredIssues,
                Actions = []
            };
        }

        if (scoreTree == null || boundedBudget == 0)
        {
            return new VisualRefinementSummary
            {
                IterationBudget = boundedBudget,
                IterationCount = boundedBudget == 0 ? 0 : Math.Min(boundedBudget, normalizedMeasuredIssues.Count),
                Converged = normalizedMeasuredIssues.Count == 0,
                ScoreTree = scoreTree,
                MeasuredIssues = normalizedMeasuredIssues,
                Actions = boundedBudget == 0
                    ? []
                    : BuildMeasuredIssueActions(document, normalizedMeasuredIssues, boundedBudget)
            };
        }

        var actions = BuildMeasuredIssueActions(document, normalizedMeasuredIssues, boundedBudget);
        var reservedTargets = new HashSet<string>(
            actions.Select(static action => action.TargetStableId + "|" + action.ActionType),
            StringComparer.Ordinal);

        var scoreTargets = Flatten(scoreTree)
            .SelectMany(node => node.Phases.Select(phase => new ScoreTarget(node, phase, PhasePriorityIndex(phase.Phase))))
            .Where(static target => target.Priority >= 0)
            .OrderBy(static target => target.Phase.SimilarityPercent)
            .ThenBy(static target => target.Priority)
            .ThenBy(static target => target.Node.RegionId, StringComparer.Ordinal)
            .ToList();

        for (var index = 0; index < scoreTargets.Count && actions.Count < boundedBudget; index++)
        {
            var target = scoreTargets[index];
            var targetStableId = ResolveTargetStableId(document, target.Node.RegionId);
            var targetNode = FindNode(document.Root, targetStableId);
            var actionType = ResolvePhaseActionType(target.Phase.Phase);
            if (!reservedTargets.Add(targetStableId + "|" + actionType))
            {
                continue;
            }

            actions.Add(new VisualRefinementAction
            {
                Iteration = actions.Count + 1,
                TargetStableId = targetStableId,
                TargetSemanticClass = targetNode?.SemanticClass,
                TargetSourceSemanticRole = targetNode?.SourceSemanticRole,
                TargetSourceAssetRealization = targetNode?.SourceAssetRealization,
                ReasonPhase = target.Phase.Phase,
                ActionType = actionType,
                Description = BuildDescription(target, targetNode)
            });
        }

        return new VisualRefinementSummary
        {
            IterationBudget = boundedBudget,
            IterationCount = actions.Count,
            Converged = actions.Count == 0,
            ScoreTree = scoreTree,
            MeasuredIssues = normalizedMeasuredIssues,
            Actions = actions
        };
    }

    private static bool IsPerfectScoreTree(RecursiveFidelityScoreNode node)
        => node.OverallSimilarityPercent >= 100d
           && node.Phases.All(static phase => phase.SimilarityPercent >= 100d)
           && node.Children.All(IsPerfectScoreTree);

    public static string ToJson(VisualRefinementSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    private static string ResolveTargetStableId(VisualDocument document, string regionId)
    {
        if (string.IsNullOrWhiteSpace(regionId))
        {
            return document.Root.StableId;
        }

        var candidate = regionId;
        while (!string.IsNullOrWhiteSpace(candidate))
        {
            var match = FindBestTarget(document.Root, candidate);
            if (match != null)
            {
                return match;
            }

            var slash = candidate.LastIndexOf('/');
            if (slash <= 0)
            {
                break;
            }

            candidate = candidate[..slash];
        }

        if (TryParseRegionBounds(regionId, out var regionBounds)
            && FindBestTargetByBounds(document.Root, regionBounds) is { } boundsMatch)
        {
            return boundsMatch;
        }

        return document.Root.StableId;
    }

    private static string? FindBestTarget(VisualNode node, string regionId)
    {
        if (string.Equals(node.StableId, regionId, StringComparison.Ordinal))
        {
            return node.StableId;
        }

        foreach (var child in node.Children)
        {
            var match = FindBestTarget(child, regionId);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static VisualNode? FindNode(VisualNode node, string stableId)
    {
        if (string.Equals(node.StableId, stableId, StringComparison.Ordinal))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var match = FindNode(child, stableId);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static string? FindBestTargetByBounds(VisualNode root, RegionBounds regionBounds)
    {
        var regionArea = regionBounds.Width * regionBounds.Height;
        if (regionArea <= 0d)
        {
            return null;
        }

        NodeBoundsCandidate? best = null;
        foreach (var node in FlattenVisualNodes(root))
        {
            if (!TryResolveNodeBounds(node, out var nodeBounds))
            {
                continue;
            }

            var intersectionWidth = Math.Max(0d, Math.Min(regionBounds.Right, nodeBounds.Right) - Math.Max(regionBounds.X, nodeBounds.X));
            var intersectionHeight = Math.Max(0d, Math.Min(regionBounds.Bottom, nodeBounds.Bottom) - Math.Max(regionBounds.Y, nodeBounds.Y));
            var intersectionArea = intersectionWidth * intersectionHeight;
            if (intersectionArea <= 0d)
            {
                continue;
            }

            var nodeArea = nodeBounds.Width * nodeBounds.Height;
            if (nodeArea <= 0d)
            {
                continue;
            }

            if (nodeArea < regionArea * 0.05d)
            {
                continue;
            }

            var unionArea = regionArea + nodeArea - intersectionArea;
            var intersectionOverUnion = unionArea > 0d ? intersectionArea / unionArea : 0d;
            var nodeCoverage = intersectionArea / nodeArea;
            var centerInsideRegion = ContainsPoint(regionBounds, nodeBounds.X + (nodeBounds.Width / 2d), nodeBounds.Y + (nodeBounds.Height / 2d));
            var areaPenalty = nodeArea > regionArea
                ? Math.Log(nodeArea / regionArea)
                : 0d;
            var rootPenalty = string.Equals(node.StableId, root.StableId, StringComparison.Ordinal) ? 1d : 0d;
            var score = (intersectionOverUnion * 5d)
                        + (nodeCoverage * 2d)
                        + (centerInsideRegion ? 1d : 0d)
                        + (Depth(node.StableId) * 0.02d)
                        - (areaPenalty * 0.25d)
                        - rootPenalty;

            var candidate = new NodeBoundsCandidate(node.StableId, score, intersectionOverUnion, nodeCoverage, nodeArea, Depth(node.StableId));
            if (best == null || candidate.CompareTo(best.Value) > 0)
            {
                best = candidate;
            }
        }

        return best?.StableId;
    }

    private static bool TryParseRegionBounds(string regionId, out RegionBounds bounds)
    {
        bounds = default;
        var atIndex = regionId.LastIndexOf('@');
        if (atIndex < 0 || atIndex == regionId.Length - 1)
        {
            return false;
        }

        var coordinatePart = regionId[(atIndex + 1)..];
        var pieces = coordinatePart.Split([',', 'x'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pieces.Length != 4)
        {
            return false;
        }

        if (!double.TryParse(pieces[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
            || !double.TryParse(pieces[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)
            || !double.TryParse(pieces[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var width)
            || !double.TryParse(pieces[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var height))
        {
            return false;
        }

        if (width <= 0d || height <= 0d)
        {
            return false;
        }

        bounds = new RegionBounds(x, y, width, height);
        return true;
    }

    private static bool TryResolveNodeBounds(VisualNode node, out RegionBounds bounds)
    {
        bounds = default;

        var width = ResolvePixelDimension(node.Box.Width);
        var height = ResolvePixelDimension(node.Box.Height);
        if (width is null || height is null || width <= 0d || height <= 0d)
        {
            return false;
        }

        var left = ResolvePixelDimension(node.Box.Left) ?? (string.Equals(node.StableId, "root", StringComparison.Ordinal) ? 0d : null);
        var top = ResolvePixelDimension(node.Box.Top) ?? (string.Equals(node.StableId, "root", StringComparison.Ordinal) ? 0d : null);
        if (left is null || top is null)
        {
            return false;
        }

        bounds = new RegionBounds(left.Value, top.Value, width.Value, height.Value);
        return true;
    }

    private static double? ResolvePixelDimension(Dimension? dimension)
        => dimension?.Unit switch
        {
            DimensionUnit.Pixels => dimension.Value.Value,
            DimensionUnit.Cells => dimension.Value.Value,
            _ => null
        };

    private static bool ContainsPoint(RegionBounds bounds, double x, double y)
        => x >= bounds.X
           && x <= bounds.Right
           && y >= bounds.Y
           && y <= bounds.Bottom;

    private static int Depth(string stableId)
        => stableId.Count(static c => c == '/');

    private static IEnumerable<VisualNode> FlattenVisualNodes(VisualNode root)
    {
        yield return root;
        foreach (var child in root.Children)
        {
            foreach (var descendant in FlattenVisualNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static List<VisualMeasuredIssue> NormalizeMeasuredIssues(IReadOnlyList<VisualMeasuredIssue>? measuredIssues)
        => measuredIssues?
            .OrderByDescending(static issue => SeverityRank(issue.Severity))
            .ThenBy(static issue => IssuePriorityRank(issue.Category))
            .ThenBy(static issue => issue.LocalPath, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Category, StringComparer.Ordinal)
            .ToList()
            ?? [];

    private static List<VisualRefinementAction> BuildMeasuredIssueActions(
        VisualDocument document,
        List<VisualMeasuredIssue> measuredIssues,
        int boundedBudget)
    {
        var actions = new List<VisualRefinementAction>();
        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < measuredIssues.Count && actions.Count < boundedBudget; index++)
        {
            var issue = measuredIssues[index];
            var actionType = ResolveIssueActionType(issue.Category);
            if (string.Equals(actionType, "no-op", StringComparison.Ordinal))
            {
                continue;
            }

            var targetStableId = ResolveTargetStableId(document, issue.LocalPath);
            var targetNode = FindNode(document.Root, targetStableId);
            if (!seenTargets.Add(targetStableId + "|" + actionType))
            {
                continue;
            }

            actions.Add(new VisualRefinementAction
            {
                Iteration = actions.Count + 1,
                TargetStableId = targetStableId,
                TargetSemanticClass = targetNode?.SemanticClass,
                TargetSourceSemanticRole = targetNode?.SourceSemanticRole,
                TargetSourceAssetRealization = targetNode?.SourceAssetRealization,
                ReasonPhase = ResolveIssuePhase(issue.Category),
                ActionType = actionType,
                Description = BuildIssueDescription(issue, targetStableId, targetNode),
                TriggerIssueCategory = issue.Category,
                TriggerIssueLocalPath = issue.LocalPath
            });
        }

        return actions;
    }

    private static string ResolvePhaseActionType(string phase)
        => phase switch
        {
            "structural-match" => "panel-motif-split",
            "outer-frame-match" => "edge-contract-adjustment",
            "inner-layout-match" => "edge-contract-adjustment",
            "text-icon-metrics" => "metric-profile-adjustment",
            "polish-offsets" => "bounded-offset-adjustment",
            _ => "no-op"
        };

    private static string ResolveIssueActionType(string category)
        => category switch
        {
            "height-collapsed-vs-preferred" => "preserve-preferred-height",
            "width-stretched-vs-preferred" => "preserve-preferred-width",
            "cross-axis-stretch-mismatch" => "disable-unwanted-cross-axis-stretch",
            "shell-padding-or-child-stack-mismatch" => "tighten-shell-padding",
            "portrait-or-status-row-shell-drift" => "preserve-preferred-height",
            "start-edge-underflow" or "start-edge-overshift" => "preserve-start-edge-shell-contract",
            "fill-underflow" => "preserve-preferred-width",
            "hug-stretched-to-fill" => "preserve-cross-axis-hug",
            "child-structure-mismatch" => "panel-motif-split",
            "wrap-pressure-risk" => "preserve-preferred-width",
            "font-size-drift" => "metric-profile-adjustment",
            "clip-mismatch" => "edge-contract-adjustment",
            _ => "no-op"
        };

    private static string ResolveIssuePhase(string category)
        => category switch
        {
            "child-structure-mismatch" => "structural-match",
            "font-size-drift" => "text-icon-metrics",
            "wrap-pressure-risk" or "fill-underflow" or "hug-stretched-to-fill" or "cross-axis-stretch-mismatch" => "inner-layout-match",
            _ => "outer-frame-match"
        };

    private static string BuildDescription(ScoreTarget target, VisualNode? targetNode)
        => target.Phase.Phase switch
        {
            "structural-match" => $"Split or regroup the {target.Node.Level} region '{target.Node.RegionId}' before applying smaller layout corrections.",
            "outer-frame-match" => $"Adjust outer edge participation, inset, or clipping around '{target.Node.RegionId}' to recover shell fidelity.",
            "inner-layout-match" => $"Adjust fill/hug sizing or edge pressure inside '{target.Node.RegionId}' before tweaking typography.",
            "text-icon-metrics" when IsRightAlignedQuantityTarget(targetNode)
                => $"Preserve nowrap and content-hug behavior for the row-end value '{target.Node.RegionId}', then tune font size or spacing if drift remains.",
            "text-icon-metrics" when IsIconMetricTarget(targetNode)
                => $"Tune icon font size, baseline, and optical centering for '{target.Node.RegionId}' before revisiting broader layout rules.",
            "text-icon-metrics" => $"Tune font size, line height, letter spacing, icon baseline, or optical centering for '{target.Node.RegionId}'.",
            "polish-offsets" => $"Apply bounded inset or per-edge offset corrections around '{target.Node.RegionId}' after structure and metrics stabilize.",
            _ => $"Review region '{target.Node.RegionId}' for local fidelity drift."
        };

    private static string BuildIssueDescription(VisualMeasuredIssue issue, string targetStableId, VisualNode? targetNode)
        => issue.Category switch
        {
            "height-collapsed-vs-preferred" => $"Preserve preferred shell height for '{targetStableId}' because the realized subtree is collapsing below its measured preferred height.",
            "width-stretched-vs-preferred" => $"Preserve preferred shell width for '{targetStableId}' because the realized subtree is stretching beyond its measured preferred width.",
            "cross-axis-stretch-mismatch" => $"Disable unwanted cross-axis stretch for '{targetStableId}' before changing metrics or text policies.",
            "shell-padding-or-child-stack-mismatch" => $"Tighten shell padding or gap on '{targetStableId}' so the measured child stack fits the intended shell bounds.",
            "portrait-or-status-row-shell-drift" => $"Preserve the portrait or status-row shell bounds for '{targetStableId}' before tuning icons or typography.",
            "start-edge-underflow" or "start-edge-overshift" => $"Preserve the start-edge shell contract for '{targetStableId}' before changing gaps or text metrics.",
            "wrap-pressure-risk" when IsRightAlignedQuantityTarget(targetNode)
                => $"Preserve the row-end content-hug contract for '{targetStableId}' before changing font size, because this quantity is compressing against the available width.",
            "font-size-drift" when IsIconMetricTarget(targetNode)
                => $"Inspect icon-specific metric calibration for '{targetStableId}' before changing local layout heuristics.",
            "clip-mismatch" when IsImageAssetTarget(targetNode)
                => $"Inspect image shell overflow or mask emission for '{targetStableId}' before changing local content metrics.",
            _ => issue.SuggestedAction ?? issue.Summary
        };

    private static bool IsRightAlignedQuantityTarget(VisualNode? node)
        => string.Equals(node?.SemanticClass, "right-aligned-quantity", StringComparison.Ordinal)
           || string.Equals(node?.SourceSemanticRole, "right-aligned-quantity", StringComparison.Ordinal);

    private static bool IsIconMetricTarget(VisualNode? node)
        => node?.Kind == VisualNodeKind.Icon
           || string.Equals(node?.SourceAssetRealization, "IconGlyph", StringComparison.Ordinal);

    private static bool IsImageAssetTarget(VisualNode? node)
        => node?.Kind == VisualNodeKind.Image
           || string.Equals(node?.SourceAssetRealization, "ImageAsset", StringComparison.Ordinal);

    private static List<RecursiveFidelityScoreNode> Flatten(RecursiveFidelityScoreNode root)
    {
        var result = new List<RecursiveFidelityScoreNode> { root };
        foreach (var child in root.Children)
        {
            result.AddRange(Flatten(child));
        }

        return result;
    }

    private static int PhasePriorityIndex(string phase)
        => Array.FindIndex(PhasePriority, candidate => string.Equals(candidate, phase, StringComparison.Ordinal));

    private static int IssuePriorityRank(string category)
        => category switch
        {
            "child-structure-mismatch" => 0,
            "height-collapsed-vs-preferred" => 1,
            "portrait-or-status-row-shell-drift" => 2,
            "start-edge-underflow" or "start-edge-overshift" => 3,
            "cross-axis-stretch-mismatch" => 4,
            "width-stretched-vs-preferred" => 5,
            "shell-padding-or-child-stack-mismatch" => 6,
            "fill-underflow" or "hug-stretched-to-fill" or "wrap-pressure-risk" => 7,
            "font-size-drift" => 8,
            "clip-mismatch" => 9,
            _ => 10
        };

    private static int SeverityRank(string severity)
        => severity switch
        {
            "error" => 0,
            "warning" => 1,
            "info" => 2,
            _ => 3
        };

    private sealed record ScoreTarget(
        RecursiveFidelityScoreNode Node,
        RecursiveFidelityPhaseScore Phase,
        int Priority);

    private readonly record struct RegionBounds(double X, double Y, double Width, double Height)
    {
        public double Right => X + Width;

        public double Bottom => Y + Height;
    }

    private readonly record struct NodeBoundsCandidate(
        string StableId,
        double Score,
        double IntersectionOverUnion,
        double NodeCoverage,
        double Area,
        int Depth)
        : IComparable<NodeBoundsCandidate>
    {
        public int CompareTo(NodeBoundsCandidate other)
        {
            var scoreComparison = Score.CompareTo(other.Score);
            if (scoreComparison != 0)
            {
                return scoreComparison;
            }

            var overlapComparison = IntersectionOverUnion.CompareTo(other.IntersectionOverUnion);
            if (overlapComparison != 0)
            {
                return overlapComparison;
            }

            var coverageComparison = NodeCoverage.CompareTo(other.NodeCoverage);
            if (coverageComparison != 0)
            {
                return coverageComparison;
            }

            var depthComparison = Depth.CompareTo(other.Depth);
            if (depthComparison != 0)
            {
                return depthComparison;
            }

            return other.Area.CompareTo(Area);
        }
    }
}
