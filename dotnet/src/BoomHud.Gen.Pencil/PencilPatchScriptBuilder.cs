using System.Globalization;
using System.Text;

namespace BoomHud.Gen.Pencil;

public static class PencilPatchScriptBuilder
{
    public static string? Build(PencilPatchPlan? plan)
    {
        if (plan == null || plan.Steps.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("// Auto-generated first-pass Pen patch script.");
        builder.AppendLine("// Review each step before applying to the source .pen file.");

        foreach (var step in plan.Steps.OrderBy(static step => step.Order))
        {
            builder.AppendLine();
            builder.AppendLine("// Step " + step.Order.ToString(CultureInfo.InvariantCulture) + ": " + (step.ActionType ?? "review"));
            builder.AppendLine("// Target: " + (step.TargetPenId ?? step.TargetStableId));
            builder.AppendLine("// Reason: " + (step.ReasonPhase ?? "unspecified"));
            builder.AppendLine("// " + step.Description);

            if (step.RequiresStructuralRewrite || string.IsNullOrWhiteSpace(step.TargetPenId))
            {
                builder.AppendLine("// MANUAL: inspect '" + EscapeComment(step.TargetPenId ?? step.TargetStableId) + "' and rewrite structure by hand.");
                continue;
            }

            if (step.SuggestedProperties.Count == 0)
            {
                builder.AppendLine("// MANUAL: no deterministic property patch available for '" + EscapeComment(step.TargetPenId) + "'.");
                continue;
            }

            builder.AppendLine(PencilPatchFormatting.SerializeUpdate(step.TargetPenId, step.SuggestedProperties));
        }

        return builder.ToString();
    }

    private static string EscapeComment(string value)
        => value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
