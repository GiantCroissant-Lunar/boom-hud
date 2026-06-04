namespace BoomHud.Gen.Pencil;

public static class PencilBatchOpsBuilder
{
    public static string? Build(PencilPatchPlan? plan)
    {
        if (plan == null || plan.Steps.Count == 0)
        {
            return null;
        }

        var operations = plan.Steps
            .OrderBy(static step => step.Order)
            .Where(static step =>
                !step.RequiresStructuralRewrite
                && !string.IsNullOrWhiteSpace(step.TargetPenId)
                && step.SuggestedProperties.Count > 0)
            .Select(static step => PencilPatchFormatting.SerializeUpdate(step.TargetPenId!, step.SuggestedProperties))
            .ToList();

        if (operations.Count == 0)
        {
            return null;
        }

        return string.Join(Environment.NewLine, operations) + Environment.NewLine;
    }
}
