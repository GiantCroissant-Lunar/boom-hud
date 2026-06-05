using System.Text.Json;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Snapshots;
using BoomHud.Cli.Handlers.Pencil;
using BoomHud.Gen.Pencil;
using BoomHud.Generators.VisualIR;

namespace BoomHud.Cli.Handlers.Baseline;

public static partial class ImageSimilarityHandler
{
    private static VisualRefinementEmissionResult EmitVisualRefinementArtifact(
        FileInfo visualIrFile,
        FileInfo? requestedOutput,
        ImageSimilarityReport report,
        int iterationBudget,
        MeasuredLayoutReport? measuredLayout)
    {
        var visualDocument = JsonSerializer.Deserialize<VisualDocument>(File.ReadAllText(visualIrFile.FullName));
        if (visualDocument == null)
        {
            throw new InvalidOperationException($"Failed to deserialize Visual IR artifact '{visualIrFile.FullName}'.");
        }

        var summary = VisualRefinementPlanner.Plan(
            visualDocument,
            ConvertRecursiveAnalysis(report.RecursiveAnalysis),
            iterationBudget,
            measuredLayout?.Issues.Select(static issue => new VisualMeasuredIssue
            {
                Category = issue.Category,
                Severity = issue.Severity,
                LocalPath = issue.LocalPath,
                ExpectedSemanticClass = issue.ExpectedSemanticClass,
                ExpectedSourceSemanticRole = issue.ExpectedSourceSemanticRole,
                ExpectedSourceAssetRealization = issue.ExpectedSourceAssetRealization,
                Summary = issue.Summary,
                SuggestedAction = issue.SuggestedAction
            }).ToList());

        var outputPath = requestedOutput?.FullName ?? ResolveDefaultVisualRefinementPath(visualIrFile, visualDocument);
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputPath, VisualRefinementPlanner.ToJson(summary));

        string? patchPlanPath = null;
        string? patchScriptPath = null;
        string? batchOpsPath = null;

        if (string.Equals(visualDocument.BackendFamily, "pencil", StringComparison.OrdinalIgnoreCase)
            && PencilPatchPlanBuilder.Build(visualDocument, summary) is { } patchPlan)
        {
            patchPlanPath = ResolveDefaultPencilPatchPlanPath(outputPath, visualDocument);
            File.WriteAllText(
                patchPlanPath,
                PencilPatchPlanBuilder.ToJson(patchPlan));

            if (PencilPatchScriptBuilder.Build(patchPlan) is { } patchScript)
            {
                patchScriptPath = ResolveDefaultPencilPatchScriptPath(outputPath, visualDocument);
                File.WriteAllText(
                    patchScriptPath,
                    patchScript);
            }

            if (PencilBatchOpsBuilder.Build(patchPlan) is { } batchOps)
            {
                batchOpsPath = ResolveDefaultPencilBatchOpsPath(outputPath, visualDocument);
                File.WriteAllText(
                    batchOpsPath,
                    batchOps);
            }
        }

        return new VisualRefinementEmissionResult
        {
            OutputPath = outputPath,
            BackendFamily = visualDocument.BackendFamily,
            DocumentName = visualDocument.DocumentName,
            PencilPatchPlanPath = patchPlanPath,
            PencilPatchScriptPath = patchScriptPath,
            PencilBatchOpsPath = batchOpsPath
        };
    }

    private static MeasuredLayoutReport EmitMeasuredLayoutArtifact(
        FileInfo visualIrFile,
        FileInfo actualLayoutFile,
        FileInfo? requestedOutput)
    {
        var visualDocument = JsonSerializer.Deserialize<VisualDocument>(File.ReadAllText(visualIrFile.FullName));
        if (visualDocument == null)
        {
            throw new InvalidOperationException($"Failed to deserialize Visual IR artifact '{visualIrFile.FullName}'.");
        }

        var actualLayout = JsonSerializer.Deserialize<ActualLayoutSnapshot>(File.ReadAllText(actualLayoutFile.FullName));
        if (actualLayout == null)
        {
            throw new InvalidOperationException($"Failed to deserialize actual layout snapshot '{actualLayoutFile.FullName}'.");
        }

        var report = BuildMeasuredLayoutReport(visualDocument, actualLayout);
        var outputPath = requestedOutput?.FullName ?? ResolveDefaultMeasuredLayoutPath(actualLayoutFile, visualDocument);
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputPath, report.ToJson());
        return report;
    }

    private static string ResolveDefaultVisualRefinementPath(FileInfo visualIrFile, VisualDocument visualDocument)
        => Path.Combine(
            visualIrFile.DirectoryName ?? Environment.CurrentDirectory,
            $"{visualDocument.DocumentName}.visual-refinement.json");

    private static string ResolveDefaultMeasuredLayoutPath(FileInfo actualLayoutFile, VisualDocument visualDocument)
        => Path.Combine(
            actualLayoutFile.DirectoryName ?? Environment.CurrentDirectory,
            $"{visualDocument.DocumentName}.measured-layout.json");

    private static string ResolveDefaultPencilPatchPlanPath(string refinementPath, VisualDocument visualDocument)
        => Path.Combine(
            Path.GetDirectoryName(refinementPath) ?? Environment.CurrentDirectory,
            $"{visualDocument.DocumentName}.pen-patch-plan.json");

    private static string ResolveDefaultPencilPatchScriptPath(string refinementPath, VisualDocument visualDocument)
        => Path.Combine(
            Path.GetDirectoryName(refinementPath) ?? Environment.CurrentDirectory,
            $"{visualDocument.DocumentName}.pen-patch-script.txt");

    private static string ResolveDefaultPencilBatchOpsPath(string refinementPath, VisualDocument visualDocument)
        => Path.Combine(
            Path.GetDirectoryName(refinementPath) ?? Environment.CurrentDirectory,
            $"{visualDocument.DocumentName}.pen-batch-ops.txt");

    private static void AutoApplyPencilPatch(ImageSimilarityOptions options, VisualRefinementEmissionResult? visualRefinement)
    {
        if (visualRefinement == null)
        {
            throw new InvalidOperationException("Visual refinement emission did not run, so there is no Pencil patch to apply.");
        }

        if (!string.Equals(visualRefinement.BackendFamily, "pencil", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Auto-apply requires a Pencil Visual IR backend, but '{visualRefinement.DocumentName}' uses '{visualRefinement.BackendFamily}'.");
        }

        if (string.IsNullOrWhiteSpace(visualRefinement.PencilBatchOpsPath) || !File.Exists(visualRefinement.PencilBatchOpsPath))
        {
            if (options.PrintSummary || options.Verbose)
            {
                Console.WriteLine("Pencil auto-apply: no deterministic batch ops were emitted. No patched .pen file written.");
            }

            return;
        }

        var outputPath = options.PatchedPenOutFile?.FullName ?? ResolveDefaultPatchedPenOutput(options.PencilSourceFile!.FullName);
        var exitCode = PenPatchApplyHandler.Execute(new PenPatchApplyOptions
        {
            PenFile = options.PencilSourceFile,
            BatchOpsFile = new FileInfo(visualRefinement.PencilBatchOpsPath),
            OutFile = new FileInfo(outputPath),
            PrintSummary = false
        });

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to auto-apply deterministic Pencil patch ops to '{options.PencilSourceFile!.FullName}'.");
        }

        if (options.PrintSummary || options.Verbose)
        {
            Console.WriteLine($"Pencil auto-apply: wrote patched pen to {outputPath}");
        }
    }

    private static string ResolveDefaultPatchedPenOutput(string penFilePath)
    {
        var directory = Path.GetDirectoryName(penFilePath) ?? Environment.CurrentDirectory;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(penFilePath);
        var extension = Path.GetExtension(penFilePath);
        return Path.Combine(directory, fileNameWithoutExtension + ".patched" + extension);
    }
}
