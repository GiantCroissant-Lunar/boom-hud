using System.CommandLine;
using System.CommandLine.Invocation;
using BoomHud.Cli.Handlers.Rules;

namespace BoomHud.Cli.Commands.Rules;

public static class RulesSelectSubtreeCandidateCommand
{
    public static Command Build()
    {
        var command = new Command("select-subtree-candidate", "Select the best replayable uGUI subtree candidate from a grouped proof report");

        var proofReportOption = new Option<FileInfo?>("--proof-report", "Grouped proof report emitted by rules proof-subtree")
        {
            IsRequired = true
        };
        var buildProgramOption = new Option<FileInfo?>("--build-program", "Replayable uGUI build-program artifact to update")
        {
            IsRequired = true
        };
        var subtreeStableIdOption = new Option<string?>("--subtree-stable-id", "Optional subtree stable id override (defaults to the proof report value)");
        var baselineCandidateIdOption = new Option<string?>("--baseline-candidate-id", "Optional baseline candidate id override (defaults to the build-program accepted candidate)");
        var candidateIdMapOption = new Option<string[]>("--candidate-id-map", "Optional mapping in the form candidate-key=candidate-id")
        {
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };
        var primaryDropToleranceOption = new Option<double>("--primary-drop-tolerance", () => 0.25d, "Maximum allowed local-objective regression from baseline");
        var improvementEpsilonOption = new Option<double>("--improvement-epsilon", () => 0.05d, "Minimum root or outside-subtree improvement required to replace the baseline candidate");
        var outOption = new Option<FileInfo?>("--out", "Output build-program path (defaults to overwriting --build-program)");
        var decisionOutOption = new Option<FileInfo?>("--decision-out", "Optional output path for the selection decision JSON");
        var summaryOption = new Option<bool>("--summary", () => true, "Print a concise selection summary");

        command.AddOption(proofReportOption);
        command.AddOption(buildProgramOption);
        command.AddOption(subtreeStableIdOption);
        command.AddOption(baselineCandidateIdOption);
        command.AddOption(candidateIdMapOption);
        command.AddOption(primaryDropToleranceOption);
        command.AddOption(improvementEpsilonOption);
        command.AddOption(outOption);
        command.AddOption(decisionOutOption);
        command.AddOption(summaryOption);

        command.SetHandler((InvocationContext context) =>
        {
            var options = new UGuiSubtreeCandidateSelectionOptions
            {
                ProofReportFile = context.ParseResult.GetValueForOption(proofReportOption),
                BuildProgramFile = context.ParseResult.GetValueForOption(buildProgramOption),
                SubtreeStableId = context.ParseResult.GetValueForOption(subtreeStableIdOption),
                BaselineCandidateId = context.ParseResult.GetValueForOption(baselineCandidateIdOption),
                CandidateIdMappings = context.ParseResult.GetValueForOption(candidateIdMapOption) ?? [],
                PrimaryDropTolerance = context.ParseResult.GetValueForOption(primaryDropToleranceOption),
                ImprovementEpsilon = context.ParseResult.GetValueForOption(improvementEpsilonOption),
                OutFile = context.ParseResult.GetValueForOption(outOption),
                DecisionOutFile = context.ParseResult.GetValueForOption(decisionOutOption),
                PrintSummary = context.ParseResult.GetValueForOption(summaryOption)
            };

            context.ExitCode = UGuiSubtreeCandidateSelectionHandler.Execute(options);
        });

        return command;
    }
}
