namespace BoomHud.Generators.VisualIR;

public static class UGuiBuildProgramPlanner
{
    public static UGuiBuildProgram Plan(VisualDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metricProfiles = document.MetricProfiles
            .ToDictionary(static profile => profile.Id, StringComparer.Ordinal);
        var steps = new List<UGuiBuildStep>();
        var checkpoints = new List<UGuiBuildCheckpoint>();
        var nextOrder = 1;

        PlanNode(document.Root, parentStableId: null, metricProfiles, steps, checkpoints, ref nextOrder);

        return new UGuiBuildProgram
        {
            DocumentName = document.DocumentName,
            BackendFamily = document.BackendFamily,
            SourceGenerationMode = document.SourceGenerationMode,
            RootStableId = document.Root.StableId,
            Steps = steps,
            Checkpoints = checkpoints
        };
    }

    private static void PlanNode(
        VisualNode node,
        string? parentStableId,
        IReadOnlyDictionary<string, MetricProfileDefinition> metricProfiles,
        List<UGuiBuildStep> steps,
        List<UGuiBuildCheckpoint> checkpoints,
        ref int nextOrder)
    {
        var solveStage = ClassifySolveStage(node);

        steps.Add(new UGuiBuildStep
        {
            Order = nextOrder++,
            StableId = node.StableId,
            ParentStableId = parentStableId,
            SolveStage = solveStage,
            ActionType = "create-node",
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = node.Kind.ToString(),
                ["sourceType"] = node.SourceType.ToString(),
                ["semanticClass"] = node.SemanticClass,
                ["componentRefId"] = node.ComponentRefId,
                ["childCount"] = node.Children.Count
            }
        });

        steps.Add(new UGuiBuildStep
        {
            Order = nextOrder++,
            StableId = node.StableId,
            ParentStableId = parentStableId,
            SolveStage = solveStage,
            ActionType = "bind-edge-contract",
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["participation"] = node.EdgeContract.Participation.ToString(),
                ["widthSizing"] = node.EdgeContract.WidthSizing.ToString(),
                ["heightSizing"] = node.EdgeContract.HeightSizing.ToString(),
                ["horizontalPin"] = node.EdgeContract.HorizontalPin.ToString(),
                ["verticalPin"] = node.EdgeContract.VerticalPin.ToString(),
                ["overflowX"] = node.EdgeContract.OverflowX.ToString(),
                ["overflowY"] = node.EdgeContract.OverflowY.ToString(),
                ["wrapPressure"] = node.EdgeContract.WrapPressure.ToString(),
                ["layoutType"] = node.Box.LayoutType?.ToString(),
                ["absolute"] = node.Box.IsAbsolutePositioned,
                ["clipContent"] = node.Box.ClipContent
            }
        });

        if (node.MetricProfileId != null && metricProfiles.TryGetValue(node.MetricProfileId, out var metricProfile))
        {
            steps.Add(new UGuiBuildStep
            {
                Order = nextOrder++,
                StableId = node.StableId,
                ParentStableId = parentStableId,
                SolveStage = solveStage,
                ActionType = "bind-metric-profile",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["metricProfileId"] = metricProfile.Id,
                    ["semanticClass"] = metricProfile.SemanticClass,
                    ["hasTextProfile"] = metricProfile.Text != null,
                    ["hasIconProfile"] = metricProfile.Icon != null
                }
            });
        }

        foreach (var child in node.Children)
        {
            steps.Add(new UGuiBuildStep
            {
                Order = nextOrder++,
                StableId = child.StableId,
                ParentStableId = node.StableId,
                SolveStage = ClassifySolveStage(child),
                ActionType = "attach-child",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["parentStableId"] = node.StableId
                }
            });

            PlanNode(child, node.StableId, metricProfiles, steps, checkpoints, ref nextOrder);
        }

        steps.Add(new UGuiBuildStep
        {
            Order = nextOrder++,
            StableId = node.StableId,
            ParentStableId = parentStableId,
            SolveStage = solveStage,
            ActionType = "seal-subtree",
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["childCount"] = node.Children.Count
            }
        });

        checkpoints.Add(new UGuiBuildCheckpoint
        {
            Order = checkpoints.Count + 1,
            StableId = node.StableId,
            SolveStage = solveStage,
            LastStepOrder = nextOrder - 1,
            Purpose = "verify-subtree"
        });
    }

    private static string ClassifySolveStage(VisualNode node)
    {
        if (node.Children.Count == 0)
        {
            return "atom";
        }

        var nodeCount = CountNodes(node);
        if (nodeCount <= 4)
        {
            return "motif";
        }

        if (nodeCount <= 12)
        {
            return "component";
        }

        return "surface";
    }

    private static int CountNodes(VisualNode node)
    {
        var count = 1;
        foreach (var child in node.Children)
        {
            count += CountNodes(child);
        }

        return count;
    }
}
