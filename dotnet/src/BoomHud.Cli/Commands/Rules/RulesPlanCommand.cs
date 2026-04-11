using System.CommandLine;
using System.CommandLine.Invocation;
using BoomHud.Abstractions.Generation;
using BoomHud.Generators;

namespace BoomHud.Cli.Commands.Rules;

public static class RulesPlanCommand
{
    public static Command Build()
    {
        var command = new Command("plan", "Build a deterministic execution plan from a generator rule set");
        var rulesOption = new Option<FileInfo>("--rules", "Input generator rule set JSON file")
        {
            IsRequired = true
        };
        var factOption = new Option<string[]>("--fact", "Initial planning fact in the form key=value (repeatable)")
        {
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };
        var nameOption = new Option<string?>("--name", "Optional plan name");
        var outOption = new Option<FileInfo?>("--out", "Optional output path for the plan summary JSON");
        var emitRulesOption = new Option<FileInfo?>("--emit-rules", "Optional output path for an executable planned rule set JSON");

        command.AddOption(rulesOption);
        command.AddOption(factOption);
        command.AddOption(nameOption);
        command.AddOption(outOption);
        command.AddOption(emitRulesOption);

        command.SetHandler((InvocationContext context) =>
        {
            var rules = context.ParseResult.GetValueForOption(rulesOption);
            var facts = context.ParseResult.GetValueForOption(factOption) ?? [];
            var name = context.ParseResult.GetValueForOption(nameOption);
            var outFile = context.ParseResult.GetValueForOption(outOption);
            var emitRules = context.ParseResult.GetValueForOption(emitRulesOption);

            if (rules == null)
            {
                context.ExitCode = 1;
                Console.Error.WriteLine("Error: --rules is required.");
                return;
            }

            context.ExitCode = Execute(rules, facts, name, outFile, emitRules);
        });

        return command;
    }

    private static int Execute(
        FileInfo rules,
        string[] facts,
        string? name,
        FileInfo? outFile,
        FileInfo? emitRulesFile)
    {
        if (!rules.Exists)
        {
            Console.Error.WriteLine($"Error: Rule set file not found: {rules.FullName}");
            return 1;
        }

        var ruleSet = GeneratorRuleSet.LoadFromFile(rules.FullName);
        var initialFacts = facts.Select(ParseFact).ToList();
        var plan = GeneratorRulePlanner.CreatePlan(ruleSet, initialFacts, name);
        var planJson = plan.ToJson();

        if (outFile != null)
        {
            EnsureParentDirectory(outFile.FullName);
            File.WriteAllText(outFile.FullName, planJson);
            Console.WriteLine($"Wrote plan summary: {outFile.FullName}");
        }
        else
        {
            Console.WriteLine(planJson);
        }

        if (emitRulesFile != null)
        {
            var executableRuleSet = GeneratorRulePlanner.BuildExecutableRuleSet(plan);
            EnsureParentDirectory(emitRulesFile.FullName);
            File.WriteAllText(emitRulesFile.FullName, executableRuleSet.ToJson());
            Console.WriteLine($"Wrote executable planned rules: {emitRulesFile.FullName}");
        }

        return 0;
    }

    private static GeneratorRuleFact ParseFact(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("Planning fact cannot be empty.");
        }

        var separatorIndex = raw.IndexOf('=');
        if (separatorIndex < 0)
        {
            return new GeneratorRuleFact
            {
                Key = raw.Trim()
            };
        }

        return new GeneratorRuleFact
        {
            Key = raw[..separatorIndex].Trim(),
            Value = raw[(separatorIndex + 1)..].Trim()
        };
    }

    private static void EnsureParentDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
