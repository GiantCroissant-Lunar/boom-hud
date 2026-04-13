using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;

namespace BoomHud.Cli.Commands.Rules;

public static class RulesSweepCommand
{
    public static Command Build()
    {
        var command = new Command("sweep", "Run the fixture rule sweep harness and rank candidate action catalogs or executable rule sets");
        var rulesGlobOption = new Option<string>("--rules-glob", () => "build/fixture-manifests/rules/*.json", "Glob for candidate action catalog or rule set JSON files");
        var compareManifestOption = new Option<FileInfo?>("--compare-manifest", "Fixture compare manifest path");
        var motionManifestOption = new Option<FileInfo?>("--motion-manifest", "Optional motion fidelity manifest path");
        var unityProjectOption = new Option<DirectoryInfo?>("--unity-project", "Unity project path (defaults to samples/UnityFullPenCompare)");
        var unityExeOption = new Option<FileInfo?>("--unity-exe", "Unity executable path");
        var outOption = new Option<DirectoryInfo?>("--out", "Output directory for sweep artifacts");
        var toleranceOption = new Option<int>("--tolerance", () => 8, "Per-channel score tolerance");
        var normalizeOption = new Option<string>("--normalize", () => "stretch", "Score normalization mode");
        var optimizerModeOption = new Option<string>("--optimizer-mode", () => "strict", "Optimizer mode: strict, frontier, or cem");
        var beamWidthOption = new Option<int>("--beam-width", () => 5, "Maximum retained frontier candidates per depth");
        var searchDepthOption = new Option<int>("--search-depth", () => 3, "Number of frontier search depths to evaluate");
        var expansionBudgetOption = new Option<int>("--expansion-budget", () => 6, "Maximum action expansions per retained candidate at each depth");
        var cemIterationsOption = new Option<int>("--cem-iterations", () => 3, "Number of CEM sampling iterations to evaluate");
        var cemSampleCountOption = new Option<int>("--cem-sample-count", () => 8, "Number of candidates to sample per CEM iteration");
        var cemEliteCountOption = new Option<int>("--cem-elite-count", () => 3, "Number of top guard-passing candidates used to update the CEM distribution");
        var maxActionsPerSampleOption = new Option<int>("--max-actions-per-sample", () => 6, "Maximum sampled actions retained in any CEM candidate");
        var cemFocusOption = new Option<string>("--cem-focus", () => "all", "CEM metric group focus: all, ugui, or unity");
        var randomSeedOption = new Option<int?>("--random-seed", "Optional random seed for deterministic sampled optimizer runs");
        var factOption = new Option<string[]>("--fact", () => new[]
        {
            "finding.text-or-icon-metrics-mismatch=present",
            "finding.edge-alignment-mismatch=present",
            "motion.enabled=true"
        }, "Initial planning fact in the form key=value (repeatable)")
        {
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };
        var skipBaselineOption = new Option<bool>("--skip-baseline", "Do not evaluate the no-rules baseline");
        var noRestoreOption = new Option<bool>("--no-restore-default", "Do not restore default generation after the sweep");

        command.AddOption(rulesGlobOption);
        command.AddOption(compareManifestOption);
        command.AddOption(motionManifestOption);
        command.AddOption(unityProjectOption);
        command.AddOption(unityExeOption);
        command.AddOption(outOption);
        command.AddOption(toleranceOption);
        command.AddOption(normalizeOption);
        command.AddOption(optimizerModeOption);
        command.AddOption(beamWidthOption);
        command.AddOption(searchDepthOption);
        command.AddOption(expansionBudgetOption);
        command.AddOption(cemIterationsOption);
        command.AddOption(cemSampleCountOption);
        command.AddOption(cemEliteCountOption);
        command.AddOption(maxActionsPerSampleOption);
        command.AddOption(cemFocusOption);
        command.AddOption(randomSeedOption);
        command.AddOption(factOption);
        command.AddOption(skipBaselineOption);
        command.AddOption(noRestoreOption);

        command.SetHandler((InvocationContext context) =>
        {
            var rulesGlob = context.ParseResult.GetValueForOption(rulesGlobOption) ?? "build/fixture-manifests/rules/*.json";
            var compareManifest = context.ParseResult.GetValueForOption(compareManifestOption);
            var motionManifest = context.ParseResult.GetValueForOption(motionManifestOption);
            var unityProject = context.ParseResult.GetValueForOption(unityProjectOption);
            var unityExe = context.ParseResult.GetValueForOption(unityExeOption);
            var outDir = context.ParseResult.GetValueForOption(outOption);
            var tolerance = context.ParseResult.GetValueForOption(toleranceOption);
            var normalize = context.ParseResult.GetValueForOption(normalizeOption) ?? "stretch";
            var optimizerMode = context.ParseResult.GetValueForOption(optimizerModeOption) ?? "strict";
            var beamWidth = context.ParseResult.GetValueForOption(beamWidthOption);
            var searchDepth = context.ParseResult.GetValueForOption(searchDepthOption);
            var expansionBudget = context.ParseResult.GetValueForOption(expansionBudgetOption);
            var cemIterations = context.ParseResult.GetValueForOption(cemIterationsOption);
            var cemSampleCount = context.ParseResult.GetValueForOption(cemSampleCountOption);
            var cemEliteCount = context.ParseResult.GetValueForOption(cemEliteCountOption);
            var maxActionsPerSample = context.ParseResult.GetValueForOption(maxActionsPerSampleOption);
            var cemFocus = context.ParseResult.GetValueForOption(cemFocusOption) ?? "all";
            var randomSeed = context.ParseResult.GetValueForOption(randomSeedOption);
            var facts = context.ParseResult.GetValueForOption(factOption) ?? [];
            var skipBaseline = context.ParseResult.GetValueForOption(skipBaselineOption);
            var noRestore = context.ParseResult.GetValueForOption(noRestoreOption);

            context.ExitCode = Execute(rulesGlob, compareManifest, motionManifest, unityProject, unityExe, outDir, tolerance, normalize, optimizerMode, beamWidth, searchDepth, expansionBudget, cemIterations, cemSampleCount, cemEliteCount, maxActionsPerSample, cemFocus, randomSeed, facts, skipBaseline, noRestore);
        });

        return command;
    }

    private static int Execute(
        string rulesGlob,
        FileInfo? compareManifest,
        FileInfo? motionManifest,
        DirectoryInfo? unityProject,
        FileInfo? unityExe,
        DirectoryInfo? outDir,
        int tolerance,
        string normalize,
        string optimizerMode,
        int beamWidth,
        int searchDepth,
        int expansionBudget,
        int cemIterations,
        int cemSampleCount,
        int cemEliteCount,
        int maxActionsPerSample,
        string cemFocus,
        int? randomSeed,
        string[] facts,
        bool skipBaseline,
        bool noRestoreDefault)
    {
        var repoRoot = Directory.GetCurrentDirectory();
        var scriptPath = Path.Combine(repoRoot, "scripts", "run-fixture-rule-sweep.ps1");
        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine($"Error: Sweep script not found: {scriptPath}");
            return 1;
        }

        var shell = ResolvePowerShellExecutable();
        var psi = new ProcessStartInfo
        {
            FileName = shell,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("-RepoRoot");
        psi.ArgumentList.Add(repoRoot);
        psi.ArgumentList.Add("-RuleManifestGlob");
        psi.ArgumentList.Add(rulesGlob);
        psi.ArgumentList.Add("-Tolerance");
        psi.ArgumentList.Add(tolerance.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-Normalization");
        psi.ArgumentList.Add(normalize);
        psi.ArgumentList.Add("-OptimizerMode");
        psi.ArgumentList.Add(optimizerMode);
        psi.ArgumentList.Add("-BeamWidth");
        psi.ArgumentList.Add(beamWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-SearchDepth");
        psi.ArgumentList.Add(searchDepth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-ExpansionBudget");
        psi.ArgumentList.Add(expansionBudget.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-CemIterations");
        psi.ArgumentList.Add(cemIterations.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-CemSampleCount");
        psi.ArgumentList.Add(cemSampleCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-CemEliteCount");
        psi.ArgumentList.Add(cemEliteCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-MaxActionsPerSample");
        psi.ArgumentList.Add(maxActionsPerSample.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-CemFocus");
        psi.ArgumentList.Add(cemFocus);

        if (randomSeed.HasValue)
        {
            psi.ArgumentList.Add("-RandomSeed");
            psi.ArgumentList.Add(randomSeed.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (facts.Length > 0)
        {
            psi.ArgumentList.Add("-PlanningFacts");
            psi.ArgumentList.Add(string.Join(";", facts));
        }

        if (compareManifest != null)
        {
            psi.ArgumentList.Add("-CompareManifestPath");
            psi.ArgumentList.Add(compareManifest.FullName);
        }

        if (motionManifest != null)
        {
            psi.ArgumentList.Add("-MotionManifestPath");
            psi.ArgumentList.Add(motionManifest.FullName);
        }

        if (unityProject != null)
        {
            psi.ArgumentList.Add("-UnityProjectPath");
            psi.ArgumentList.Add(unityProject.FullName);
        }

        if (unityExe != null)
        {
            psi.ArgumentList.Add("-UnityExe");
            psi.ArgumentList.Add(unityExe.FullName);
        }

        if (outDir != null)
        {
            psi.ArgumentList.Add("-OutputRoot");
            psi.ArgumentList.Add(outDir.FullName);
        }

        if (skipBaseline)
        {
            psi.ArgumentList.Add("-SkipBaseline");
        }

        if (noRestoreDefault)
        {
            psi.ArgumentList.Add("-NoRestoreDefault");
        }

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data != null)
            {
                Console.WriteLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null)
            {
                Console.Error.WriteLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static string ResolvePowerShellExecutable()
    {
        if (TryResolveExecutablePath("pwsh", out var pwshPath))
        {
            return pwshPath;
        }

        if (OperatingSystem.IsWindows())
        {
            return "powershell";
        }

        return "pwsh";
    }

    private static bool TryResolveExecutablePath(string executableName, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return false;
        }

        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", string.Empty }
            : new[] { string.Empty };

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, executableName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                    ? executableName
                    : executableName + extension);
                if (File.Exists(candidate))
                {
                    resolvedPath = candidate;
                    return true;
                }
            }
        }

        return false;
    }
}
