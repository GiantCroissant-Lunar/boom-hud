using System.CommandLine;
using System.CommandLine.Invocation;
using BoomHud.Cli.Handlers.Pencil;

namespace BoomHud.Cli.Commands.Pencil;

public static class PencilApplyPatchCommand
{
    public static Command Build()
    {
        var command = new Command("apply-patch", "Apply deterministic BoomHud Pencil patch artifacts to a .pen file");

        var penOption = new Option<FileInfo?>("--pen", "Target .pen file to patch")
        {
            IsRequired = true
        };

        var batchOpsOption = new Option<FileInfo?>("--batch-ops", "Executable Pencil batch operations artifact (*.pen-batch-ops.txt)");
        var patchPlanOption = new Option<FileInfo?>("--patch-plan", "Pencil patch plan artifact (*.pen-patch-plan.json)");
        var outOption = new Option<FileInfo?>("--out", "Output .pen path (defaults to sibling *.patched.pen)");
        var summaryOption = new Option<bool>("--summary", () => true, "Print summary to stdout");

        command.AddOption(penOption);
        command.AddOption(batchOpsOption);
        command.AddOption(patchPlanOption);
        command.AddOption(outOption);
        command.AddOption(summaryOption);

        command.SetHandler((InvocationContext context) =>
        {
            var options = new PenPatchApplyOptions
            {
                PenFile = context.ParseResult.GetValueForOption(penOption),
                BatchOpsFile = context.ParseResult.GetValueForOption(batchOpsOption),
                PatchPlanFile = context.ParseResult.GetValueForOption(patchPlanOption),
                OutFile = context.ParseResult.GetValueForOption(outOption),
                PrintSummary = context.ParseResult.GetValueForOption(summaryOption)
            };

            context.ExitCode = PenPatchApplyHandler.Execute(options);
        });

        return command;
    }
}
