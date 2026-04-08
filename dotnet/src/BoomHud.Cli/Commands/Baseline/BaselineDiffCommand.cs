using System.CommandLine;
using System.CommandLine.Invocation;
using BoomHud.Cli.Handlers.Baseline;

namespace BoomHud.Cli.Commands.Baseline;

/// <summary>
/// CLI command definition for baseline diff.
/// Defines the CLI surface and wires to the handler.
/// </summary>
public static class BaselineDiffCommand
{
    /// <summary>
    /// Builds the baseline diff command with all options.
    /// </summary>
    public static Command Build()
    {
        var command = new Command("diff", "Generate diff images for changed frames");

        var currentOption = new Option<DirectoryInfo?>("--current", "Current snapshots directory (with snapshots.manifest.json)");
        currentOption.AddAlias("-c");

        var baselineOption = new Option<DirectoryInfo?>("--baseline", "Baseline snapshots directory (with snapshots.manifest.json)");
        baselineOption.AddAlias("-b");

        var outOption = new Option<DirectoryInfo?>("--out", "Output directory for diff images (default: ui/diffs)");
        outOption.AddAlias("-o");

        var reportOption = new Option<FileInfo?>("--report", "Output diff report file (default: diff-report.json in output directory)");

        var toleranceOption = new Option<int>("--tolerance", () => 0, "Per-channel delta tolerance (0-255). Pixels within tolerance are considered unchanged.");

        var verboseOption = new Option<bool>("--verbose", () => false, "Enable verbose output");

        command.AddOption(currentOption);
        command.AddOption(baselineOption);
        command.AddOption(outOption);
        command.AddOption(reportOption);
        command.AddOption(toleranceOption);
        command.AddOption(verboseOption);

        command.SetHandler((InvocationContext context) =>
        {
            var options = new BaselineDiffOptions
            {
                CurrentDir = context.ParseResult.GetValueForOption(currentOption),
                BaselineDir = context.ParseResult.GetValueForOption(baselineOption),
                OutputDir = context.ParseResult.GetValueForOption(outOption),
                ReportFile = context.ParseResult.GetValueForOption(reportOption),
                Tolerance = context.ParseResult.GetValueForOption(toleranceOption),
                Verbose = context.ParseResult.GetValueForOption(verboseOption)
            };

            context.ExitCode = BaselineDiffHandler.Execute(options);
        });

        return command;
    }
}