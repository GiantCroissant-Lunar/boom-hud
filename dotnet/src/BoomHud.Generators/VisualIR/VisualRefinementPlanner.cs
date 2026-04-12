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
        int iterationBudget = 4)
    {
        ArgumentNullException.ThrowIfNull(document);

        var boundedBudget = Math.Max(0, iterationBudget);
        if (scoreTree == null || boundedBudget == 0)
        {
            return new VisualRefinementSummary
            {
                IterationBudget = boundedBudget,
                IterationCount = 0,
                Converged = true,
                ScoreTree = scoreTree,
                Actions = []
            };
        }

        var scoreTargets = Flatten(scoreTree)
            .SelectMany(node => node.Phases.Select(phase => new ScoreTarget(node, phase, PhasePriorityIndex(phase.Phase))))
            .Where(static target => target.Priority >= 0)
            .OrderBy(static target => target.Phase.SimilarityPercent)
            .ThenBy(static target => target.Priority)
            .ThenBy(static target => target.Node.RegionId, StringComparer.Ordinal)
            .ToList();

        var actions = new List<VisualRefinementAction>();
        for (var index = 0; index < scoreTargets.Count && actions.Count < boundedBudget; index++)
        {
            var target = scoreTargets[index];
            actions.Add(new VisualRefinementAction
            {
                Iteration = actions.Count + 1,
                TargetStableId = ResolveTargetStableId(document, target.Node.RegionId),
                ReasonPhase = target.Phase.Phase,
                ActionType = ResolveActionType(target.Phase.Phase),
                Description = BuildDescription(target)
            });
        }

        return new VisualRefinementSummary
        {
            IterationBudget = boundedBudget,
            IterationCount = actions.Count,
            Converged = actions.Count == 0,
            ScoreTree = scoreTree,
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

        return FindBestTarget(document.Root, regionId) ?? document.Root.StableId;
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

    private static string ResolveActionType(string phase)
        => phase switch
        {
            "structural-match" => "panel-motif-split",
            "outer-frame-match" => "edge-contract-adjustment",
            "inner-layout-match" => "edge-contract-adjustment",
            "text-icon-metrics" => "metric-profile-adjustment",
            "polish-offsets" => "bounded-offset-adjustment",
            _ => "no-op"
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

    private sealed record ScoreTarget(
        RecursiveFidelityScoreNode Node,
        RecursiveFidelityPhaseScore Phase,
        int Priority);
}
