using System.CommandLine;
using System.CommandLine.Invocation;

namespace BoomHud.Cli.Commands.Baseline;

/// <summary>
/// CLI command definition for single-image similarity scoring.
/// Reuses the baseline diff metric family for one reference/candidate image pair.
/// </summary>
public static class BaselineScoreCommand
{
    /// <summary>
    /// Builds the baseline score command with all options.
    /// </summary>
    public static Command Build()
    {
        var command = new Command("score", "Measure similarity between two images and report a 0-100 score");

        var referenceOption = new Option<FileInfo?>("--reference", "Reference image file")
        {
            IsRequired = true
        };
        referenceOption.AddAlias("--baseline");
        referenceOption.AddAlias("-r");

        var candidateOption = new Option<FileInfo?>("--candidate", "Candidate image file")
        {
            IsRequired = true
        };
        candidateOption.AddAlias("--current");
        candidateOption.AddAlias("-c");

        var outOption = new Option<FileInfo?>("--out", "Output report file (default: image-similarity-report.json beside candidate)");
        outOption.AddAlias("-o");

        var diffOption = new Option<FileInfo?>("--diff", "Optional output path for a generated diff image");
        diffOption.AddAlias("-d");
        var visualIrOption = new Option<FileInfo?>("--visual-ir", "Optional Visual IR artifact to convert the recursive score tree into a visual refinement plan");
        var visualRefinementOutOption = new Option<FileInfo?>("--visual-refinement-out", "Optional output path for the generated visual refinement artifact");
        var actualLayoutOption = new Option<FileInfo?>("--actual-layout", "Optional Unity actual-layout snapshot emitted beside the candidate image");
        var measuredLayoutOutOption = new Option<FileInfo?>("--measured-layout-out", "Optional output path for the measured expected-vs-actual layout report");

        var normalizeOption = new Option<string>("--normalize", () => "off", "Normalize candidate to reference dimensions before scoring: off, stretch, or cover");
        var failBelowOption = new Option<double?>("--fail-below", "Fail with exit code 2 if overall similarity is below this percentage");
        var toleranceOption = new Option<int>("--tolerance", () => 8, "Per-channel delta tolerance (0-255)");
        var visualRefinementBudgetOption = new Option<int>("--visual-refinement-budget", () => 4, "Maximum number of refinement actions to emit when --visual-ir is provided");
        var summaryOption = new Option<bool>("--summary", () => true, "Print summary to stdout");
        var verboseOption = new Option<bool>("--verbose", () => false, "Enable verbose output");

        command.AddOption(referenceOption);
        command.AddOption(candidateOption);
        command.AddOption(outOption);
        command.AddOption(diffOption);
        command.AddOption(visualIrOption);
        command.AddOption(visualRefinementOutOption);
        command.AddOption(actualLayoutOption);
        command.AddOption(measuredLayoutOutOption);
        command.AddOption(normalizeOption);
        command.AddOption(failBelowOption);
        command.AddOption(toleranceOption);
        command.AddOption(visualRefinementBudgetOption);
        command.AddOption(summaryOption);
        command.AddOption(verboseOption);

        command.SetHandler((InvocationContext context) =>
        {
            var options = new Handlers.Baseline.ImageSimilarityOptions
            {
                ReferenceFile = context.ParseResult.GetValueForOption(referenceOption),
                CandidateFile = context.ParseResult.GetValueForOption(candidateOption),
                OutFile = context.ParseResult.GetValueForOption(outOption),
                DiffFile = context.ParseResult.GetValueForOption(diffOption),
                VisualIrFile = context.ParseResult.GetValueForOption(visualIrOption),
                VisualRefinementOutFile = context.ParseResult.GetValueForOption(visualRefinementOutOption),
                ActualLayoutFile = context.ParseResult.GetValueForOption(actualLayoutOption),
                MeasuredLayoutOutFile = context.ParseResult.GetValueForOption(measuredLayoutOutOption),
                NormalizeMode = context.ParseResult.GetValueForOption(normalizeOption) ?? "off",
                FailBelowOverallPercent = context.ParseResult.GetValueForOption(failBelowOption),
                Tolerance = context.ParseResult.GetValueForOption(toleranceOption),
                VisualRefinementIterationBudget = context.ParseResult.GetValueForOption(visualRefinementBudgetOption),
                PrintSummary = context.ParseResult.GetValueForOption(summaryOption),
                Verbose = context.ParseResult.GetValueForOption(verboseOption)
            };

            context.ExitCode = Handlers.Baseline.ImageSimilarityHandler.Execute(options);
        });

        return command;
    }
}
