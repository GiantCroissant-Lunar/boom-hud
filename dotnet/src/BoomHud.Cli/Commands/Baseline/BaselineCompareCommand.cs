using System.CommandLine;
using System.CommandLine.Invocation;

namespace BoomHud.Cli.Commands.Baseline;

/// <summary>
/// CLI command definition for baseline compare.
/// Defines the CLI surface and wires to the handler.
/// </summary>
public static class BaselineCompareCommand
{
    /// <summary>
    /// Builds the baseline compare command with all options.
    /// </summary>
    public static Command Build()
    {
        var command = new Command("compare", "Compare current snapshots against baseline (hash-level)");

        // Define options
        var currentOption = new Option<DirectoryInfo?>("--current", "Current snapshots directory (with snapshots.manifest.json)");
        currentOption.AddAlias("-c");

        var baselineOption = new Option<DirectoryInfo?>("--baseline", "Baseline snapshots directory (with snapshots.manifest.json)");
        baselineOption.AddAlias("-b");

        var outOption = new Option<FileInfo?>("--out", "Output report file (default: baseline-report.json in current directory)");
        outOption.AddAlias("-o");

        var summaryOption = new Option<bool>("--summary", () => false, "Print summary to stdout (for CI)");

        var failOnChangedOption = new Option<bool>("--fail-on-changed", () => false, "Exit with code 1 if any actionable frames changed (legacy, use --fail-on)");

        var failOnOption = new Option<string?>("--fail-on", "Fail condition: 'any' or 'percent:X' (e.g., 'percent:0.5' fails if any frame exceeds 0.5% changed)");

        var toleranceOption = new Option<int>("--tolerance", () => 0, "Per-channel delta tolerance (0-255). Pixels within tolerance are considered unchanged.");

        var minChangedOption = new Option<double>("--min-changed-percent", () => 0.0, "Minimum changed percent to report as changed (noise filter)");

        var protectedOption = new Option<string[]>("--protected", "Protected frame names (stricter: always fail if changed, regardless of threshold)")
        {
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };

        var ghActionsOption = new Option<bool>("--gh-actions", () => false, "Output GitHub Actions step summary format");

        var verboseOption = new Option<bool>("--verbose", () => false, "Enable verbose output");

        // Add options to command
        command.AddOption(currentOption);
        command.AddOption(baselineOption);
        command.AddOption(outOption);
        command.AddOption(summaryOption);
        command.AddOption(failOnChangedOption);
        command.AddOption(failOnOption);
        command.AddOption(toleranceOption);
        command.AddOption(minChangedOption);
        command.AddOption(protectedOption);
        command.AddOption(ghActionsOption);
        command.AddOption(verboseOption);

        // Wire up handler
        command.SetHandler((InvocationContext context) =>
        {
            var currentDir = context.ParseResult.GetValueForOption(currentOption);
            var baselineDir = context.ParseResult.GetValueForOption(baselineOption);
            var outFile = context.ParseResult.GetValueForOption(outOption);
            var printSummary = context.ParseResult.GetValueForOption(summaryOption);
            var failOnChanged = context.ParseResult.GetValueForOption(failOnChangedOption);
            var failOn = context.ParseResult.GetValueForOption(failOnOption);
            var tolerance = context.ParseResult.GetValueForOption(toleranceOption);
            var minChangedPercent = context.ParseResult.GetValueForOption(minChangedOption);
            var protectedFrames = context.ParseResult.GetValueForOption(protectedOption) ?? [];
            var ghActions = context.ParseResult.GetValueForOption(ghActionsOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            // Parse fail-on condition
            var (failOnMode, failPercent) = Handlers.Baseline.BaselineCompareOptions.ParseFailOn(failOn, failOnChanged);

            var options = new Handlers.Baseline.BaselineCompareOptions
            {
                CurrentDir = currentDir,
                BaselineDir = baselineDir,
                OutFile = outFile,
                PrintSummary = printSummary,
                GhActions = ghActions,
                FailOn = failOnMode,
                FailPercent = failPercent,
                Tolerance = tolerance,
                MinChangedPercent = minChangedPercent,
                ProtectedFrames = protectedFrames,
                Verbose = verbose
            };

            context.ExitCode = Handlers.Baseline.BaselineCompareHandler.Execute(options);
        });

        return command;
    }
}
