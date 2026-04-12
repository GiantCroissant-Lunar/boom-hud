using BoomHud.Abstractions.IR;

namespace BoomHud.Cli;

internal sealed record ReactGeneratedAssetCopyResult
{
    public IReadOnlyList<string> CopiedFiles { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

internal sealed record ReactGeneratedAssetPreparationResult
{
    public required HudDocument Document { get; init; }
    public IReadOnlyList<string> CopiedFiles { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

internal static class ReactGeneratedAssetCopier
{
    private sealed record AssetRewrite(string? Url, bool Remove);

    internal static ReactGeneratedAssetPreparationResult PrepareBackgroundImageAssets(
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
        var assetOutputRoot = ResolveAssetOutputRoot(outputRoot);
        var rewrites = new Dictionary<string, AssetRewrite>(StringComparer.Ordinal);

        foreach (var url in EnumerateBackgroundImageUrls(document).Distinct(StringComparer.Ordinal))
        {
            var relativePath = NormalizeRelativeAssetPath(url);
            if (relativePath == null)
            {
                continue;
            }

            var sourcePath = ResolveSourceAssetPath(relativePath, inputList);
            if (sourcePath == null)
            {
                warnings.Add($"Warning: React background image asset '{url}' was not found relative to any input source. Omitting the background image.");
                rewrites[url] = new AssetRewrite(null, Remove: true);
                continue;
            }

            var targetPath = Path.Combine(assetOutputRoot, relativePath);
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.Copy(sourcePath, targetPath, overwrite: true);
            copiedFiles.Add(targetPath);
            rewrites[url] = new AssetRewrite(
                assetOutputRoot.Equals(Path.GetFullPath(outputRoot), StringComparison.OrdinalIgnoreCase)
                    ? url
                    : "/" + relativePath.Replace(Path.DirectorySeparatorChar, '/'),
                Remove: false);
        }

        return new ReactGeneratedAssetPreparationResult
        {
            Document = RewriteDocument(document, rewrites),
            CopiedFiles = copiedFiles,
            Warnings = warnings
        };
    }

    internal static ReactGeneratedAssetCopyResult CopyBackgroundImageAssets(
        HudDocument document,
        IEnumerable<FileInfo> inputs,
        string outputRoot)
    {
        var prepared = PrepareBackgroundImageAssets(document, inputs, outputRoot);

        return new ReactGeneratedAssetCopyResult
        {
            CopiedFiles = prepared.CopiedFiles,
            Warnings = prepared.Warnings
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

    private static string ResolveAssetOutputRoot(string outputRoot)
    {
        var fullOutputRoot = Path.GetFullPath(outputRoot);
        var outputDirectory = new DirectoryInfo(fullOutputRoot);
        var sourceDirectory = outputDirectory.Parent;
        if (sourceDirectory?.Name.Equals("src", StringComparison.OrdinalIgnoreCase) != true)
        {
            return fullOutputRoot;
        }

        var projectDirectory = sourceDirectory.Parent;
        if (projectDirectory == null)
        {
            return fullOutputRoot;
        }

        return Path.Combine(projectDirectory.FullName, "public");
    }

    private static HudDocument RewriteDocument(
        HudDocument document,
        IReadOnlyDictionary<string, AssetRewrite> rewrites)
    {
        if (rewrites.Count == 0)
        {
            return document;
        }

        return document with
        {
            Root = RewriteNode(document.Root, rewrites),
            Components = document.Components.ToDictionary(
                static pair => pair.Key,
                pair => pair.Value with { Root = RewriteNode(pair.Value.Root, rewrites) },
                StringComparer.Ordinal)
        };
    }

    private static ComponentNode RewriteNode(
        ComponentNode node,
        IReadOnlyDictionary<string, AssetRewrite> rewrites)
    {
        var rewrittenStyle = RewriteStyle(node.Style, rewrites);
        var rewrittenChildren = node.Children.Select(child => RewriteNode(child, rewrites)).ToArray();
        return node with
        {
            Style = rewrittenStyle,
            Children = rewrittenChildren
        };
    }

    private static StyleSpec? RewriteStyle(
        StyleSpec? style,
        IReadOnlyDictionary<string, AssetRewrite> rewrites)
    {
        if (style?.BackgroundImage == null)
        {
            return style;
        }

        if (!rewrites.TryGetValue(style.BackgroundImage.Url, out var rewrite))
        {
            return style;
        }

        if (rewrite.Remove)
        {
            return style with { BackgroundImage = null };
        }

        if (string.Equals(rewrite.Url, style.BackgroundImage.Url, StringComparison.Ordinal))
        {
            return style;
        }

        return style with
        {
            BackgroundImage = style.BackgroundImage with
            {
                Url = rewrite.Url ?? style.BackgroundImage.Url
            }
        };
    }
}
