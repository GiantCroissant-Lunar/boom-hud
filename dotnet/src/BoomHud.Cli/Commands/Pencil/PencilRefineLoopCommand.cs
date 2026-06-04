using System.CommandLine;
using System.CommandLine.Invocation;
using BoomHud.Cli.Handlers.Pencil;

namespace BoomHud.Cli.Commands.Pencil;

public static class PencilRefineLoopCommand
{
    public static Command Build()
    {
        var command = new Command("refine-loop", "Run a bounded Pencil refine/render/score/apply loop against a reference image");

        var penOption = new Option<FileInfo?>("--pen", "Source .pen file to refine")
        {
            IsRequired = true
        };
        var referenceOption = new Option<FileInfo?>("--reference", "Reference image file")
        {
            IsRequired = true
        };
        var workDirOption = new Option<DirectoryInfo?>("--work-dir", "Working directory for loop artifacts (default: build/pencil-refine-loop/<pen-name>)");
        var maxIterationsOption = new Option<int>("--max-iterations", () => 5, "Maximum number of refine iterations to run");
        var normalizeOption = new Option<string>("--normalize", () => "stretch", "Normalize candidate to reference dimensions before scoring: off, stretch, or cover");
        var toleranceOption = new Option<int>("--tolerance", () => 8, "Per-channel delta tolerance (0-255)");
        var fontPathOption = new Option<FileInfo?>("--font-path", "Optional font file passed through to the Pen render script");
        var summaryOption = new Option<bool>("--summary", () => true, "Print summary to stdout");

        command.AddOption(penOption);
        command.AddOption(referenceOption);
        command.AddOption(workDirOption);
        command.AddOption(maxIterationsOption);
        command.AddOption(normalizeOption);
        command.AddOption(toleranceOption);
        command.AddOption(fontPathOption);
        command.AddOption(summaryOption);

        command.SetHandler((InvocationContext context) =>
        {
            var options = new PencilRefinementLoopOptions
            {
                PenFile = context.ParseResult.GetValueForOption(penOption),
                ReferenceFile = context.ParseResult.GetValueForOption(referenceOption),
                WorkingDirectory = context.ParseResult.GetValueForOption(workDirOption),
                MaxIterations = context.ParseResult.GetValueForOption(maxIterationsOption),
                NormalizeMode = context.ParseResult.GetValueForOption(normalizeOption) ?? "stretch",
                Tolerance = context.ParseResult.GetValueForOption(toleranceOption),
                FontPath = context.ParseResult.GetValueForOption(fontPathOption),
                PrintSummary = context.ParseResult.GetValueForOption(summaryOption)
            };

            context.ExitCode = PencilRefinementLoopHandler.Execute(options);
        });

        return command;
    }
}
