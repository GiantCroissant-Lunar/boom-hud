using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BoomHud.Abstractions.IR;

namespace BoomHud.Dsl.Figma;

public static class FigmaAnnotations
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static HudAnnotationsDocument LoadFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return Parse(json);
    }

    public static HudAnnotationsDocument Parse(string json)
    {
        var doc = JsonSerializer.Deserialize<HudAnnotationsDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse annotations JSON");

        return doc;
    }

    public static HudDocument Apply(HudDocument document, HudAnnotationsDocument annotations)
    {
        if (annotations.Nodes.Count == 0)
        {
            return document;
        }

        var updatedRoot = ApplyToNode(document.Root, annotations, currentPath: []);

        return document with { Root = updatedRoot };
    }

    private static ComponentNode ApplyToNode(ComponentNode node, HudAnnotationsDocument annotations, List<string> currentPath)
    {
        var id = node.Id ?? string.Empty;
        var nextPath = new List<string>(currentPath);
        if (!string.IsNullOrWhiteSpace(id))
        {
            nextPath.Add(id);
        }

        var updated = ApplyRules(node, annotations, nextPath);

        if (updated.Children.Count > 0)
        {
            var updatedChildren = updated.Children
                .Select(child => ApplyToNode(child, annotations, nextPath))
                .ToList();

            updated = updated with { Children = updatedChildren };
        }

        return updated;
    }

    private static ComponentNode ApplyRules(ComponentNode node, HudAnnotationsDocument annotations, IReadOnlyList<string> path)
    {
        var updated = node;

        foreach (var annotation in annotations.Nodes)
        {
            if (!Matches(updated, annotation.Match, path))
            {
                continue;
            }

            if (annotation.Set?.Type != null)
            {
                if (!Enum.TryParse<ComponentType>(annotation.Set.Type, ignoreCase: true, out var parsed))
                {
                    throw new InvalidOperationException($"Unknown component type '{annotation.Set.Type}' in annotations.");
                }

                updated = updated with { Type = parsed };
            }

            if (annotation.Bindings != null && annotation.Bindings.Count > 0)
            {
                var properties = updated.Properties.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
                var bindings = updated.Bindings.ToList();

                foreach (var (property, bindingPath) in annotation.Bindings)
                {
                    var trimmedProp = property.Trim();
                    var trimmedPath = bindingPath.Trim();
                    if (trimmedProp.Length == 0 || trimmedPath.Length == 0)
                    {
                        continue;
                    }

                    // For text nodes we bind the text content directly.
                    if (string.Equals(trimmedProp, "text", StringComparison.OrdinalIgnoreCase))
                    {
                        properties["text"] = BindableValue<object?>.Bind(trimmedPath);
                    }

                    bindings.Add(new BindingSpec
                    {
                        Property = trimmedProp,
                        Path = trimmedPath,
                        Mode = BindingMode.OneWay
                    });
                }

                updated = updated with { Properties = properties, Bindings = bindings };
            }
        }

        return updated;
    }

    private static bool Matches(ComponentNode node, NodeMatch match, IReadOnlyList<string> path)
    {
        if (match.Path != null && match.Path.Count > 0)
        {
            if (!path.SequenceEqual(match.Path, StringComparer.Ordinal))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(match.Id))
        {
            if (!string.Equals(node.Id, match.Id, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(match.Type))
        {
            if (!Enum.TryParse<ComponentType>(match.Type, ignoreCase: true, out var parsed))
            {
                throw new InvalidOperationException($"Unknown match type '{match.Type}' in annotations.");
            }

            if (node.Type != parsed)
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record HudAnnotationsDocument
{
    public IReadOnlyList<NodeAnnotation> Nodes { get; init; } = Array.Empty<NodeAnnotation>();
}

public sealed record NodeAnnotation
{
    public required NodeMatch Match { get; init; }
    public NodeSet? Set { get; init; }
    public IReadOnlyDictionary<string, string>? Bindings { get; init; }
}

public sealed record NodeMatch
{
    // Robust selector: stable across copy/paste because it is derived from names, not Figma IDs.
    public IReadOnlyList<string>? Path { get; init; }

    // Convenience selector; use with care because IDs can collide.
    public string? Id { get; init; }

    public string? Type { get; init; }
}

public sealed record NodeSet
{
    public string? Type { get; init; }
}
