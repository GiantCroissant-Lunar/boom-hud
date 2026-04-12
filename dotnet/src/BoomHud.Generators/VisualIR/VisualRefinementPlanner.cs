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
        if (scoreTree == null || boundedBudget == 0)
        {
            var normalizedIssues = NormalizeMeasuredIssues(measuredIssues);
            return new VisualRefinementSummary
            {
                IterationBudget = boundedBudget,
                IterationCount = boundedBudget == 0 ? 0 : Math.Min(boundedBudget, normalizedIssues.Count),
                Converged = normalizedIssues.Count == 0,
                ScoreTree = scoreTree,
                MeasuredIssues = normalizedIssues,
                Actions = boundedBudget == 0
                    ? []
                    : BuildMeasuredIssueActions(document, normalizedIssues, boundedBudget)
            };
        }

        var normalizedMeasuredIssues = NormalizeMeasuredIssues(measuredIssues);
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
            var actionType = ResolvePhaseActionType(target.Phase.Phase);
            if (!reservedTargets.Add(targetStableId + "|" + actionType))
            {
                continue;
            }

            actions.Add(new VisualRefinementAction
            {
                Iteration = actions.Count + 1,
                TargetStableId = targetStableId,
                ReasonPhase = target.Phase.Phase,
                ActionType = actionType,
                Description = BuildDescription(target)
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
            if (!seenTargets.Add(targetStableId + "|" + actionType))
            {
                continue;
            }

            actions.Add(new VisualRefinementAction
            {
                Iteration = actions.Count + 1,
                TargetStableId = targetStableId,
                ReasonPhase = ResolveIssuePhase(issue.Category),
                ActionType = actionType,
                Description = BuildIssueDescription(issue, targetStableId),
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

    private static string BuildDescription(ScoreTarget target)
        => target.Phase.Phase switch
        {
            "structural-match" => $"Split or regroup the {target.Node.Level} region '{target.Node.RegionId}' before applying smaller layout corrections.",
            "outer-frame-match" => $"Adjust outer edge participation, inset, or clipping around '{target.Node.RegionId}' to recover shell fidelity.",
            "inner-layout-match" => $"Adjust fill/hug sizing or edge pressure inside '{target.Node.RegionId}' before tweaking typography.",
            "text-icon-metrics" => $"Tune font size, line height, letter spacing, icon baseline, or optical centering for '{target.Node.RegionId}'.",
            "polish-offsets" => $"Apply bounded inset or per-edge offset corrections around '{target.Node.RegionId}' after structure and metrics stabilize.",
            _ => $"Review region '{target.Node.RegionId}' for local fidelity drift."
        };

    private static string BuildIssueDescription(VisualMeasuredIssue issue, string targetStableId)
        => issue.Category switch
        {
            "height-collapsed-vs-preferred" => $"Preserve preferred shell height for '{targetStableId}' because the realized subtree is collapsing below its measured preferred height.",
            "width-stretched-vs-preferred" => $"Preserve preferred shell width for '{targetStableId}' because the realized subtree is stretching beyond its measured preferred width.",
            "cross-axis-stretch-mismatch" => $"Disable unwanted cross-axis stretch for '{targetStableId}' before changing metrics or text policies.",
            "shell-padding-or-child-stack-mismatch" => $"Tighten shell padding or gap on '{targetStableId}' so the measured child stack fits the intended shell bounds.",
            "portrait-or-status-row-shell-drift" => $"Preserve the portrait or status-row shell bounds for '{targetStableId}' before tuning icons or typography.",
            "start-edge-underflow" or "start-edge-overshift" => $"Preserve the start-edge shell contract for '{targetStableId}' before changing gaps or text metrics.",
            _ => issue.SuggestedAction ?? issue.Summary
        };

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
}
