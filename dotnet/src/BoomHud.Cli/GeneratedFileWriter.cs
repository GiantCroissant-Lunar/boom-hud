using System.Text.Json;
using BoomHud.Abstractions.Generation;

namespace BoomHud.Cli;

internal static class GeneratedFileWriter
{
    internal const string ManifestFileName = ".boomhud-generated-files.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void Write(string outputRoot, string scopeId, IReadOnlyList<GeneratedFile> files, TextWriter? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        ArgumentNullException.ThrowIfNull(files);

        Directory.CreateDirectory(outputRoot);

        var rootPath = EnsureTrailingSeparator(Path.GetFullPath(outputRoot));
        var manifestPath = Path.Combine(outputRoot, ManifestFileName);
        var manifest = LoadManifest(manifestPath);
        var normalizedScopeId = NormalizeRelativePath(scopeId);
        var previousFiles = manifest.Scopes.TryGetValue(normalizedScopeId, out var scopedFiles)
            ? scopedFiles.Where(static path => !string.IsNullOrWhiteSpace(path)).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : manifest.NeedsLegacySyntheticMigration
                ? CollectLegacySyntheticArtifacts(outputRoot).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var currentFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var targetPath = ResolveOutputPath(rootPath, file.Path);
            var normalizedRelativePath = NormalizeRelativePath(Path.GetRelativePath(outputRoot, targetPath));
            currentFiles[normalizedRelativePath] = targetPath;
        }

        foreach (var staleRelativePath in previousFiles.Except(currentFiles.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var stalePath = ResolveOutputPath(rootPath, staleRelativePath);
            if (!File.Exists(stalePath))
            {
                continue;
            }

            File.Delete(stalePath);
            log?.WriteLine($"Deleted stale generated file: {stalePath}");
            DeleteEmptyParentDirectories(outputRoot, stalePath);
        }

        foreach (var file in files)
        {
            var targetPath = ResolveOutputPath(rootPath, file.Path);
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(targetPath, file.Content);
            log?.WriteLine($"Wrote: {targetPath}");
        }

        manifest.Scopes[normalizedScopeId] = currentFiles.Keys.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        manifest.NeedsLegacySyntheticMigration = false;

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new GeneratedFileManifest
        {
            Scopes = manifest.Scopes
        }, JsonOptions));
    }

    private static GeneratedFileManifestState LoadManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return new GeneratedFileManifestState
            {
                NeedsLegacySyntheticMigration = true
            };
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<GeneratedFileManifest>(File.ReadAllText(manifestPath), JsonOptions);
            if (manifest?.Scopes != null)
            {
                return new GeneratedFileManifestState
                {
                    Scopes = manifest.Scopes.ToDictionary(
                        static pair => pair.Key,
                        static pair => (IReadOnlyList<string>)pair.Value.ToArray(),
                        StringComparer.OrdinalIgnoreCase)
                };
            }

            return new GeneratedFileManifestState
            {
                NeedsLegacySyntheticMigration = true
            };
        }
        catch
        {
            return new GeneratedFileManifestState
            {
                NeedsLegacySyntheticMigration = true
            };
        }
    }

    private static string[] CollectLegacySyntheticArtifacts(string outputRoot)
    {
        if (!Directory.Exists(outputRoot))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Where(static path => IsLegacySyntheticArtifact(Path.GetFileName(path)))
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(outputRoot, path)))
            .ToArray();
    }

    private static string ResolveOutputPath(string rootPath, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Generated file path '{relativePath}' resolves outside output root '{rootPath}'.");
        }

        return fullPath;
    }

    private static string NormalizeRelativePath(string relativePath)
        => relativePath.Replace('\\', '/');

    private static bool IsLegacySyntheticArtifact(string fileName)
    {
        if (fileName.EndsWith(".synthetic-components.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (fileName.StartsWith("Synthetic", StringComparison.OrdinalIgnoreCase)
            && fileName.Contains("View.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return fileName.StartsWith("ISynthetic", StringComparison.OrdinalIgnoreCase)
               && fileName.Contains("ViewModel.g.", StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static void DeleteEmptyParentDirectories(string outputRoot, string filePath)
    {
        var rootPath = Path.GetFullPath(outputRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var directory = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(directory)
               && !string.Equals(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), rootPath, StringComparison.OrdinalIgnoreCase)
               && Directory.Exists(directory)
               && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
            directory = Path.GetDirectoryName(directory);
        }
    }

    private sealed record GeneratedFileManifest
    {
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Scopes { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
    }

    private sealed record GeneratedFileManifestState
    {
        public Dictionary<string, IReadOnlyList<string>> Scopes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public bool NeedsLegacySyntheticMigration { get; set; }
    }
}
