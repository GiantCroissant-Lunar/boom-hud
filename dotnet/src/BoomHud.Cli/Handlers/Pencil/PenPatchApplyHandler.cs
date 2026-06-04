using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BoomHud.Gen.Pencil;

namespace BoomHud.Cli.Handlers.Pencil;

public static class PenPatchApplyHandler
{
    private static readonly JsonDocumentOptions JsonDocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    public static int Execute(PenPatchApplyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.PenFile == null)
        {
            Console.Error.WriteLine("Error: --pen is required.");
            return 1;
        }

        if (!options.PenFile.Exists)
        {
            Console.Error.WriteLine($"Error: Pen file not found: {options.PenFile.FullName}");
            return 1;
        }

        var inputCount = (options.BatchOpsFile != null ? 1 : 0) + (options.PatchPlanFile != null ? 1 : 0);
        if (inputCount != 1)
        {
            Console.Error.WriteLine("Error: specify exactly one of --batch-ops or --patch-plan.");
            return 1;
        }

        try
        {
            var batchOpsText = ResolveBatchOpsText(options);
            if (string.IsNullOrWhiteSpace(batchOpsText))
            {
                if (options.PrintSummary)
                {
                    Console.WriteLine("No deterministic Pencil update operations were found. No file written.");
                }

                return 0;
            }

            var operations = ParseBatchOps(batchOpsText);
            if (operations.Count == 0)
            {
                if (options.PrintSummary)
                {
                    Console.WriteLine("No deterministic Pencil update operations were parsed. No file written.");
                }

                return 0;
            }

            var penRoot = JsonNode.Parse(File.ReadAllText(options.PenFile.FullName), documentOptions: JsonDocumentOptions);
            if (penRoot is not JsonObject rootObject)
            {
                Console.Error.WriteLine($"Error: failed to parse pen file '{options.PenFile.FullName}' as a JSON object.");
                return 1;
            }

            var appliedCount = 0;
            foreach (var operation in operations)
            {
                if (!TryApplyUpdate(rootObject, operation, out var changed))
                {
                    Console.Error.WriteLine($"Error: target '{operation.TargetPath}' was not found in '{options.PenFile.FullName}'.");
                    return 1;
                }

                if (changed)
                {
                    appliedCount++;
                }
            }

            if (appliedCount == 0)
            {
                if (options.PrintSummary)
                {
                    Console.WriteLine("No material deterministic Pencil update operations were found. No file written.");
                }

                return 0;
            }

            var outputPath = options.OutFile?.FullName ?? ResolveDefaultOutputPath(options.PenFile.FullName);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllText(outputPath, rootObject.ToJsonString(JsonSerializerOptions), Encoding.UTF8);

            if (options.PrintSummary)
            {
                Console.WriteLine($"Applied {appliedCount} deterministic Pencil update operation(s).");
                Console.WriteLine($"Wrote: {outputPath}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static string? ResolveBatchOpsText(PenPatchApplyOptions options)
    {
        if (options.BatchOpsFile is { } batchOpsFile)
        {
            if (!batchOpsFile.Exists)
            {
                throw new FileNotFoundException($"Batch ops file not found: {batchOpsFile.FullName}", batchOpsFile.FullName);
            }

            return File.ReadAllText(batchOpsFile.FullName);
        }

        if (options.PatchPlanFile is { } patchPlanFile)
        {
            if (!patchPlanFile.Exists)
            {
                throw new FileNotFoundException($"Patch plan file not found: {patchPlanFile.FullName}", patchPlanFile.FullName);
            }

            var patchPlan = JsonSerializer.Deserialize<PencilPatchPlan>(File.ReadAllText(patchPlanFile.FullName))
                ?? throw new InvalidOperationException($"Failed to deserialize patch plan '{patchPlanFile.FullName}'.");
            return PencilBatchOpsBuilder.Build(patchPlan);
        }

        return null;
    }

    internal static IReadOnlyList<PencilUpdateOperation> ParseBatchOps(string batchOpsText)
    {
        ArgumentNullException.ThrowIfNull(batchOpsText);

        var operations = new List<PencilUpdateOperation>();
        using var reader = new StringReader(batchOpsText);
        string? line;
        var lineNumber = 0;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            operations.Add(ParseUpdateLine(trimmed, lineNumber));
        }

        return operations;
    }

    private static PencilUpdateOperation ParseUpdateLine(string line, int lineNumber)
    {
        if (!line.StartsWith("U(", StringComparison.Ordinal) || !line.EndsWith(')'))
        {
            throw new InvalidOperationException($"Unsupported batch op on line {lineNumber}: '{line}'.");
        }

        var index = 2;
        SkipWhitespace(line, ref index);

        if (index >= line.Length || line[index] != '"')
        {
            throw new InvalidOperationException($"Invalid update target on line {lineNumber}: '{line}'.");
        }

        var targetEnd = FindJsonStringEnd(line, index);
        var targetLiteral = line[index..(targetEnd + 1)];
        var targetPath = JsonSerializer.Deserialize<string>(targetLiteral)
            ?? throw new InvalidOperationException($"Invalid update target string on line {lineNumber}: '{line}'.");

        index = targetEnd + 1;
        SkipWhitespace(line, ref index);
        if (index >= line.Length || line[index] != ',')
        {
            throw new InvalidOperationException($"Expected ',' after update target on line {lineNumber}: '{line}'.");
        }

        index++;
        SkipWhitespace(line, ref index);

        var objectText = line[index..^1].Trim();
        var properties = JsonNode.Parse(objectText, documentOptions: JsonDocumentOptions) as JsonObject
            ?? throw new InvalidOperationException($"Invalid update object on line {lineNumber}: '{line}'.");

        return new PencilUpdateOperation(targetPath, properties);
    }

    private static int FindJsonStringEnd(string text, int startIndex)
    {
        var escaped = false;
        for (var index = startIndex + 1; index < text.Length; index++)
        {
            var current = text[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                continue;
            }

            if (current == '"')
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Unterminated JSON string in batch op: '{text}'.");
    }

    private static void SkipWhitespace(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
    }

    private static bool TryApplyUpdate(JsonObject rootObject, PencilUpdateOperation operation, out bool changed)
    {
        changed = false;
        var slashIndex = operation.TargetPath.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex >= 0)
        {
            var instanceId = operation.TargetPath[..slashIndex];
            var descendantPath = operation.TargetPath[(slashIndex + 1)..];
            if (FindNodeById(rootObject, instanceId) is not JsonObject instanceNode)
            {
                return false;
            }

            changed = ApplyDescendantOverride(instanceNode, descendantPath, operation.Properties);
            return true;
        }

        if (FindNodeById(rootObject, operation.TargetPath) is not JsonObject targetNode)
        {
            return false;
        }

        changed = MergeProperties(targetNode, operation.Properties);
        return true;
    }

    private static bool ApplyDescendantOverride(JsonObject instanceNode, string descendantPath, JsonObject properties)
    {
        var descendants = instanceNode["descendants"] as JsonObject ?? new JsonObject();
        var existing = descendants[descendantPath] as JsonObject ?? new JsonObject();
        var changed = MergeProperties(existing, properties);
        descendants[descendantPath] = existing;
        instanceNode["descendants"] = descendants;
        return changed;
    }

    private static bool MergeProperties(JsonObject target, JsonObject properties)
    {
        var changed = false;
        foreach (var pair in properties)
        {
            var incoming = pair.Value?.DeepClone();
            if (JsonNode.DeepEquals(target[pair.Key], incoming))
            {
                continue;
            }

            target[pair.Key] = incoming;
            changed = true;
        }

        return changed;
    }

    private static JsonObject? FindNodeById(JsonNode? current, string targetId)
    {
        if (current is not JsonObject currentObject)
        {
            return null;
        }

        if (string.Equals(currentObject["id"]?.GetValue<string>(), targetId, StringComparison.Ordinal))
        {
            return currentObject;
        }

        if (currentObject["children"] is JsonArray children)
        {
            foreach (var child in children)
            {
                if (FindNodeById(child, targetId) is { } foundChild)
                {
                    return foundChild;
                }
            }
        }

        if (currentObject["nodes"] is JsonArray nodes)
        {
            foreach (var node in nodes)
            {
                if (FindNodeById(node, targetId) is { } foundNode)
                {
                    return foundNode;
                }
            }
        }

        return null;
    }

    private static string ResolveDefaultOutputPath(string penFilePath)
    {
        var directory = Path.GetDirectoryName(penFilePath) ?? Environment.CurrentDirectory;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(penFilePath);
        var extension = Path.GetExtension(penFilePath);
        return Path.Combine(directory, fileNameWithoutExtension + ".patched" + extension);
    }

    internal sealed record PencilUpdateOperation(string TargetPath, JsonObject Properties);
}
