using System.CommandLine;
using System.CommandLine.Invocation;
using BoomHud.Cli.Handlers.Rules;

namespace BoomHud.Cli.Commands.Rules;

public static class RulesFrontierOptimizeCommand
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Command Build()
    {
        var command = new Command("frontier-optimize", "Rank fidelity candidates with bounded Pareto pruning and final regression guards");
        var inputOption = new Option<FileInfo?>("--input", "Input optimizer state JSON file")
        {
            IsRequired = true
        };
        var outOption = new Option<FileInfo?>("--out", "Output optimizer summary JSON file");
        var summaryOption = new Option<bool>("--summary", () => true, "Print a short summary to stdout");

        command.AddOption(inputOption);
        command.AddOption(outOption);
        command.AddOption(summaryOption);

        command.SetHandler((InvocationContext context) =>
        {
            var input = context.ParseResult.GetValueForOption(inputOption);
            var outFile = context.ParseResult.GetValueForOption(outOption);
            var printSummary = context.ParseResult.GetValueForOption(summaryOption);
            context.ExitCode = Execute(input, outFile, printSummary);
        });

        return command;
    }

    private static int Execute(FileInfo? input, FileInfo? outFile, bool printSummary)
    {
        if (input == null || !input.Exists)
        {
            Console.Error.WriteLine($"Error: Optimizer input file not found: {input?.FullName}");
            return 1;
        }

        try
        {
            var state = System.Text.Json.JsonSerializer.Deserialize<FidelityFrontierOptimizerState>(
                File.ReadAllText(input.FullName),
                JsonOptions) ?? throw new InvalidOperationException($"Failed to deserialize optimizer input '{input.FullName}'.");
            var summary = FidelityFrontierOptimizer.Optimize(state);
            var outputPath = outFile?.FullName ?? Path.Combine(input.DirectoryName ?? Environment.CurrentDirectory, "optimizer-summary.json");
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, summary.ToJson());

            if (printSummary)
            {
                Console.WriteLine("=== Fidelity Frontier Optimizer ===");
                Console.WriteLine($"Selected candidate:  {summary.SelectedCandidateId}");
                Console.WriteLine($"Baseline candidate:  {summary.BaselineCandidateId}");
                Console.WriteLine($"Selected is baseline:{(summary.SelectedCandidateIsBaseline ? " YES" : " NO")}");
                Console.WriteLine($"Summary:             {outputPath}");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Error: Failed to optimize frontier candidates: {exception.Message}");
            return 1;
        }
    }
}
