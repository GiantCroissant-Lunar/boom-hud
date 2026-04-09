using BoomHud.Abstractions.IR;

namespace BoomHud.Cli;

internal sealed record ReactGeneratedAssetCopyResult
{
    public IReadOnlyList<string> CopiedFiles { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

internal static class ReactGeneratedAssetCopier
{
    internal static ReactGeneratedAssetCopyResult CopyBackgroundImageAssets(
        HudDocument document,
        IEnumerable<FileInfo> inputs,
        string outputRoot)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);

        var copiedFiles = new List<string>();
        var warnings = new List<string>();
        var inputList = inputs.Where(static input => input.Directory != null).ToList();

        foreach (var url in EnumerateBackgroundImageUrls(document).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = NormalizeRelativeAssetPath(url);
            if (relativePath == null)
            {
                continue;
            }

            var sourcePath = ResolveSourceAssetPath(relativePath, inputList);
            if (sourcePath == null)
            {
                warnings.Add($"Warning: React background image asset '{url}' was not found relative to any input source.");
                continue;
            }

            var targetPath = Path.Combine(outputRoot, relativePath);
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.Copy(sourcePath, targetPath, overwrite: true);
            copiedFiles.Add(targetPath);
        }

        return new ReactGeneratedAssetCopyResult
        {
            CopiedFiles = copiedFiles,
            Warnings = warnings
        };
    }

    private static IEnumerable<string> EnumerateBackgroundImageUrls(HudDocument document)
    {
        foreach (var url in EnumerateBackgroundImageUrls(document.Root))
        {
            yield return url;
        }

        foreach (var component in document.Components.Values)
        {
            foreach (var url in EnumerateBackgroundImageUrls(component.Root))
            {
                yield return url;
            }
        }
    }

    private static IEnumerable<string> EnumerateBackgroundImageUrls(ComponentNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Style?.BackgroundImage?.Url))
        {
            yield return node.Style.BackgroundImage.Url;
        }

        foreach (var child in node.Children)
        {
            foreach (var url in EnumerateBackgroundImageUrls(child))
            {
                yield return url;
            }
        }
    }

    private static string? NormalizeRelativeAssetPath(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return null;
        }

        var sanitized = url.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        while (sanitized.StartsWith($".{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            sanitized = sanitized[2..];
        }

        sanitized = sanitized.TrimStart(Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return null;
        }

        var segments = sanitized
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Any(static segment => segment is "." or ".."))
        {
            return null;
        }

        return Path.Combine(segments);
    }

    private static string? ResolveSourceAssetPath(string relativePath, IReadOnlyList<FileInfo> inputs)
    {
        foreach (var input in inputs)
        {
            var sourceDirectory = input.Directory?.FullName;
            if (string.IsNullOrWhiteSpace(sourceDirectory))
            {
                continue;
            }

            var candidate = Path.Combine(sourceDirectory, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}