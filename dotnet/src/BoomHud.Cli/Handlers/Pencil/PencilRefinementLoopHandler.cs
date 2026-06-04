using System.Diagnostics;
using System.Text.Json;
using BoomHud.Cli.Handlers.Baseline;
using BoomHud.Dsl.Pencil;
using BoomHud.Generators;

namespace BoomHud.Cli.Handlers.Pencil;

public static class PencilRefinementLoopHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static int Execute(PencilRefinementLoopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.PenFile == null)
        {
            Console.Error.WriteLine("Error: --pen is required.");
            return 1;
        }

        if (options.ReferenceFile == null)
        {
            Console.Error.WriteLine("Error: --reference is required.");
            return 1;
        }

        if (!options.PenFile.Exists)
        {
            Console.Error.WriteLine($"Error: Pen file not found: {options.PenFile.FullName}");
            return 1;
        }

        if (!options.ReferenceFile.Exists)
        {
            Console.Error.WriteLine($"Error: Reference image not found: {options.ReferenceFile.FullName}");
            return 1;
        }

        if (options.FontPath != null && !options.FontPath.Exists)
        {
            Console.Error.WriteLine($"Error: Font file not found: {options.FontPath.FullName}");
            return 1;
        }

        if (options.MaxIterations <= 0)
        {
            Console.Error.WriteLine("Error: --max-iterations must be greater than 0.");
            return 1;
        }

        try
        {
            var result = RunLoop(
                options,
                RenderPenFixture,
                ScoreIteration);

            if (options.PrintSummary)
            {
                Console.WriteLine($"Loop iterations:    {result.Iterations.Count}");
                Console.WriteLine($"Stop reason:        {result.StopReason}");
                Console.WriteLine($"Final pen:          {result.FinalPenPath}");
                Console.WriteLine($"Summary:            {result.SummaryPath}");
                if (result.Iterations.Count > 0)
                {
                    var latest = result.Iterations[^1];
                    Console.WriteLine($"Latest render:      {latest.RenderedImagePath}");
                    if (latest.OverallSimilarityPercent.HasValue)
                    {
                        Console.WriteLine($"Latest similarity:  {latest.OverallSimilarityPercent.Value:F2}%");
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    internal static PencilRefinementLoopResult RunLoop(
        PencilRefinementLoopOptions options,
        Func<PencilRenderRequest, int> renderPen,
        Func<PencilScoreRequest, int> scoreIteration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(renderPen);
        ArgumentNullException.ThrowIfNull(scoreIteration);

        var workDir = options.WorkingDirectory?.FullName
            ?? Path.Combine(
                Environment.CurrentDirectory,
                "build",
                "pencil-refine-loop",
                Path.GetFileNameWithoutExtension(options.PenFile!.Name));
        Directory.CreateDirectory(workDir);

        var parser = new PenParser();
        var iterations = new List<PencilRefinementLoopIterationResult>();
        var currentPenPath = options.PenFile!.FullName;
        var stopReason = PencilRefinementLoopStopReason.MaxIterationsReached;

        for (var iteration = 1; iteration <= options.MaxIterations; iteration++)
        {
            var iterationDirectory = Path.Combine(workDir, $"iteration-{iteration:00}");
            Directory.CreateDirectory(iterationDirectory);

            var document = parser.ParseFile(currentPenPath);
            var prepared = GenerationDocumentPreprocessor.Prepare(document, new Abstractions.Generation.GenerationOptions(), "pencil");
            var visualIrPath = Path.Combine(iterationDirectory, $"{document.Name}.visual-ir.json");
            File.WriteAllText(visualIrPath, GenerationDocumentPreprocessor.ToJson(prepared.VisualDocument));

            var renderedImagePath = Path.Combine(iterationDirectory, $"{document.Name}.pen-render.png");
            if (renderPen(new PencilRenderRequest(currentPenPath, renderedImagePath, options.FontPath?.FullName)) != 0)
            {
                throw new InvalidOperationException($"Failed to render iteration {iteration} for '{currentPenPath}'.");
            }

            var reportPath = Path.Combine(iterationDirectory, $"{document.Name}.reference-score.json");
            var refinementPath = Path.Combine(iterationDirectory, $"{document.Name}.visual-refinement.json");
            var patchedPenPath = Path.Combine(iterationDirectory, $"{document.Name}.patched.pen");

            if (scoreIteration(new PencilScoreRequest(
                    options.ReferenceFile!.FullName,
                    renderedImagePath,
                    reportPath,
                    visualIrPath,
                    refinementPath,
                    currentPenPath,
                    patchedPenPath,
                    options.NormalizeMode,
                    options.Tolerance)) != 0)
            {
                throw new InvalidOperationException($"Failed to score iteration {iteration} for '{currentPenPath}'.");
            }

            double? similarity = null;
            if (File.Exists(reportPath))
            {
                var report = JsonSerializer.Deserialize<ImageSimilarityReport>(File.ReadAllText(reportPath));
                similarity = report?.OverallSimilarityPercent;
            }

            var batchOpsPath = Path.Combine(iterationDirectory, $"{document.Name}.pen-batch-ops.txt");
            var iterationResult = new PencilRefinementLoopIterationResult
            {
                Iteration = iteration,
                SourcePenPath = currentPenPath,
                VisualIrPath = visualIrPath,
                RenderedImagePath = renderedImagePath,
                ReportPath = reportPath,
                RefinementPath = refinementPath,
                BatchOpsPath = File.Exists(batchOpsPath) ? batchOpsPath : null,
                PatchedPenPath = File.Exists(patchedPenPath) ? patchedPenPath : null,
                OverallSimilarityPercent = similarity
            };
            iterations.Add(iterationResult);

            if (!File.Exists(patchedPenPath))
            {
                stopReason = PencilRefinementLoopStopReason.NoDeterministicPatchOps;
                break;
            }

            if (string.Equals(
                File.ReadAllText(currentPenPath),
                File.ReadAllText(patchedPenPath),
                StringComparison.Ordinal))
            {
                stopReason = PencilRefinementLoopStopReason.NoEffectiveChange;
                break;
            }

            currentPenPath = patchedPenPath;
        }

        var finalPenPath = iterations.Count == 0
            ? currentPenPath
            : iterations[^1].PatchedPenPath ?? iterations[^1].SourcePenPath;
        var summaryPath = Path.Combine(workDir, "pencil-refine-loop-summary.json");
        var result = new PencilRefinementLoopResult
        {
            SourcePenPath = options.PenFile!.FullName,
            ReferenceImagePath = options.ReferenceFile!.FullName,
            WorkingDirectory = workDir,
            FinalPenPath = finalPenPath,
            StopReason = stopReason,
            Iterations = iterations,
            SummaryPath = summaryPath
        };

        File.WriteAllText(summaryPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    private static int RenderPenFixture(PencilRenderRequest request)
    {
        var repoRoot = ResolveRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "render-pen-fixture-ref.ps1");
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Pen render script not found: {scriptPath}", scriptPath);
        }

        var shell = ResolveShellPath();
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = shell,
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(scriptPath);
        process.StartInfo.ArgumentList.Add("-InputPen");
        process.StartInfo.ArgumentList.Add(request.InputPenPath);
        process.StartInfo.ArgumentList.Add("-OutputPng");
        process.StartInfo.ArgumentList.Add(request.OutputPngPath);
        if (!string.IsNullOrWhiteSpace(request.FontPath))
        {
            process.StartInfo.ArgumentList.Add("-FontPath");
            process.StartInfo.ArgumentList.Add(request.FontPath);
        }

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Pen render script failed with exit code {process.ExitCode}: {error}{Environment.NewLine}{output}".Trim());
        }

        return 0;
    }

    private static int ScoreIteration(PencilScoreRequest request)
    {
        return ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(request.ReferenceImagePath),
            CandidateFile = new FileInfo(request.CandidateImagePath),
            OutFile = new FileInfo(request.ReportPath),
            VisualIrFile = new FileInfo(request.VisualIrPath),
            VisualRefinementOutFile = new FileInfo(request.RefinementPath),
            PencilSourceFile = new FileInfo(request.SourcePenPath),
            PatchedPenOutFile = new FileInfo(request.PatchedPenPath),
            AutoApplyPencilPatch = true,
            NormalizeMode = request.NormalizeMode,
            Tolerance = request.Tolerance,
            PrintSummary = false
        });
    }

    private static string ResolveShellPath()
    {
        try
        {
            using var pwsh = Process.Start(new ProcessStartInfo
            {
                FileName = "pwsh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            pwsh?.Kill(entireProcessTree: true);
            return "pwsh";
        }
        catch
        {
            return "powershell";
        }
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var scriptPath = Path.Combine(current.FullName, "scripts", "render-pen-fixture-ref.ps1");
            if (File.Exists(scriptPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root containing scripts/render-pen-fixture-ref.ps1.");
    }
}

internal sealed record PencilRenderRequest(string InputPenPath, string OutputPngPath, string? FontPath);

internal sealed record PencilScoreRequest(
    string ReferenceImagePath,
    string CandidateImagePath,
    string ReportPath,
    string VisualIrPath,
    string RefinementPath,
    string SourcePenPath,
    string PatchedPenPath,
    string NormalizeMode,
    int Tolerance);

public sealed record PencilRefinementLoopResult
{
    public required string SourcePenPath { get; init; }

    public required string ReferenceImagePath { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string FinalPenPath { get; init; }

    public required PencilRefinementLoopStopReason StopReason { get; init; }

    public required string SummaryPath { get; init; }

    public IReadOnlyList<PencilRefinementLoopIterationResult> Iterations { get; init; } = [];
}

public sealed record PencilRefinementLoopIterationResult
{
    public required int Iteration { get; init; }

    public required string SourcePenPath { get; init; }

    public required string VisualIrPath { get; init; }

    public required string RenderedImagePath { get; init; }

    public required string ReportPath { get; init; }

    public required string RefinementPath { get; init; }

    public string? BatchOpsPath { get; init; }

    public string? PatchedPenPath { get; init; }

    public double? OverallSimilarityPercent { get; init; }
}

public enum PencilRefinementLoopStopReason
{
    NoDeterministicPatchOps,
    NoEffectiveChange,
    MaxIterationsReached
}
