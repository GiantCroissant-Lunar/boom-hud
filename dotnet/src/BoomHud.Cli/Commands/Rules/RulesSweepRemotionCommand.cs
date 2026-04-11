using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;

namespace BoomHud.Cli.Commands.Rules;

public static class RulesSweepRemotionCommand
{
    public static Command Build()
    {
        var command = new Command("sweep-remotion", "Run the Remotion fixture rule sweep harness and rank candidate catalogs or planned rule sets against PEN references");
        var rulesGlobOption = new Option<string>("--rules-glob", () => "samples/rules/quest-sidebar*.catalog.json", "Glob for candidate action catalog or rule set JSON files");
        var manifestOption = new Option<FileInfo?>("--manifest", "Remotion fixture manifest path");
        var outOption = new Option<DirectoryInfo?>("--out", "Output directory for sweep artifacts");
        var toleranceOption = new Option<int>("--tolerance", () => 8, "Per-channel score tolerance");
        var normalizeOption = new Option<string>("--normalize", () => "stretch", "Score normalization mode");
        var factOption = new Option<string[]>("--fact", () => new[]
        {
            "finding.text-or-icon-metrics-mismatch=present",
            "finding.edge-alignment-mismatch=present"
        }, "Initial planning fact in the form key=value (repeatable)")
        {
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };
        var skipBaselineOption = new Option<bool>("--skip-baseline", "Do not evaluate the no-rules baseline");
        var noRestoreOption = new Option<bool>("--no-restore-default", "Do not restore default no-rules React fixture generation after the sweep");

        command.AddOption(rulesGlobOption);
        command.AddOption(manifestOption);
        command.AddOption(outOption);
        command.AddOption(toleranceOption);
        command.AddOption(normalizeOption);
        command.AddOption(factOption);
        command.AddOption(skipBaselineOption);
        command.AddOption(noRestoreOption);

        command.SetHandler((InvocationContext context) =>
        {
            var rulesGlob = context.ParseResult.GetValueForOption(rulesGlobOption) ?? "samples/rules/quest-sidebar*.catalog.json";
            var manifest = context.ParseResult.GetValueForOption(manifestOption);
            var outDir = context.ParseResult.GetValueForOption(outOption);
            var tolerance = context.ParseResult.GetValueForOption(toleranceOption);
            var normalize = context.ParseResult.GetValueForOption(normalizeOption) ?? "stretch";
            var facts = context.ParseResult.GetValueForOption(factOption) ?? [];
            var skipBaseline = context.ParseResult.GetValueForOption(skipBaselineOption);
            var noRestore = context.ParseResult.GetValueForOption(noRestoreOption);

            context.ExitCode = Execute(rulesGlob, manifest, outDir, tolerance, normalize, facts, skipBaseline, noRestore);
        });

        return command;
    }

    private static int Execute(
        string rulesGlob,
        FileInfo? manifest,
        DirectoryInfo? outDir,
        int tolerance,
        string normalize,
        string[] facts,
        bool skipBaseline,
        bool noRestoreDefault)
    {
        var repoRoot = Directory.GetCurrentDirectory();
        var scriptPath = Path.Combine(repoRoot, "scripts", "run-fixture-remotion-rule-sweep.ps1");
        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine($"Error: Remotion sweep script not found: {scriptPath}");
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

        if (facts.Length > 0)
        {
            psi.ArgumentList.Add("-PlanningFacts");
            psi.ArgumentList.Add(string.Join(";", facts));
        }

        if (manifest != null)
        {
            psi.ArgumentList.Add("-RemotionManifestPath");
            psi.ArgumentList.Add(manifest.FullName);
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
