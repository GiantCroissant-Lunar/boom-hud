using System.CommandLine;
using System.CommandLine.Invocation;
using BoomHud.Cli.Handlers.Rules;

namespace BoomHud.Cli.Commands.Rules;

public static class RulesScaffoldSubtreeCandidatesCommand
{
    public static Command Build()
    {
        var command = new Command("scaffold-subtree-candidates", "Append direct-child replayable uGUI candidate catalogs beneath a subtree");

        var visualIrOption = new Option<FileInfo?>("--visual-ir", "Visual IR artifact emitted from generation")
        {
            IsRequired = true
        };
        var buildProgramOption = new Option<FileInfo?>("--build-program", "Replayable uGUI build-program artifact to update")
        {
            IsRequired = true
        };
        var subtreeStableIdOption = new Option<string>("--subtree-stable-id", "Stable id of the subtree whose direct children should receive candidate catalogs")
        {
            IsRequired = true
        };
        var overwriteOption = new Option<bool>("--overwrite-existing", "Replace existing direct-child catalogs instead of skipping them");
        var outOption = new Option<FileInfo?>("--out", "Output build-program path (defaults to overwriting --build-program)");
        var reportOutOption = new Option<FileInfo?>("--report-out", "Optional scaffold report JSON path");
        var summaryOption = new Option<bool>("--summary", () => true, "Print a concise scaffold summary");

        command.AddOption(visualIrOption);
        command.AddOption(buildProgramOption);
        command.AddOption(subtreeStableIdOption);
        command.AddOption(overwriteOption);
        command.AddOption(outOption);
        command.AddOption(reportOutOption);
        command.AddOption(summaryOption);

        command.SetHandler((InvocationContext context) =>
        {
            var options = new UGuiSubtreeCandidateScaffoldOptions
            {
                VisualIrFile = context.ParseResult.GetValueForOption(visualIrOption),
                BuildProgramFile = context.ParseResult.GetValueForOption(buildProgramOption),
                SubtreeStableId = context.ParseResult.GetValueForOption(subtreeStableIdOption) ?? string.Empty,
                OverwriteExistingCatalogs = context.ParseResult.GetValueForOption(overwriteOption),
                OutFile = context.ParseResult.GetValueForOption(outOption),
                ReportOutFile = context.ParseResult.GetValueForOption(reportOutOption),
                PrintSummary = context.ParseResult.GetValueForOption(summaryOption)
            };

            context.ExitCode = UGuiSubtreeCandidateScaffoldHandler.Execute(options);
        });

        return command;
    }
}
