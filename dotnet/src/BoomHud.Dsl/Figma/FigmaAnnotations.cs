using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        return Apply(document, annotations, out _);
    }

    public static HudDocument Apply(HudDocument document, HudAnnotationsDocument annotations, out IReadOnlyList<string> warnings)
    {
        var warningList = new List<string>();
        warnings = warningList;

        var typeOverridePaths = new HashSet<string>(StringComparer.Ordinal);

        var updatedRoot = annotations.Nodes.Count == 0
            ? document.Root
            : ApplyToNode(document.Root, annotations, currentPath: [], typeOverridePaths);

        CollectPseudoTypeWarnings(updatedRoot, currentPath: [], typeOverridePaths, warningList);

        return document with { Root = updatedRoot };
    }

    private static ComponentNode ApplyToNode(
        ComponentNode node,
        HudAnnotationsDocument annotations,
        List<string> currentPath,
        HashSet<string> typeOverridePaths)
    {
        var id = node.Id ?? string.Empty;
        var nextPath = new List<string>(currentPath);
        if (!string.IsNullOrWhiteSpace(id))
        {
            nextPath.Add(id);
        }

        var updated = ApplyRules(node, annotations, nextPath, typeOverridePaths);

        if (updated.Children.Count > 0)
        {
            var updatedChildren = updated.Children
                .Select(child => ApplyToNode(child, annotations, nextPath, typeOverridePaths))
                .ToList();

            updated = updated with { Children = updatedChildren };
        }

        return updated;
    }

    private static ComponentNode ApplyRules(
        ComponentNode node,
        HudAnnotationsDocument annotations,
        IReadOnlyList<string> path,
        HashSet<string> typeOverridePaths)
    {
        var updated = node;

        var pathKey = string.Join("/", path);

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
                typeOverridePaths.Add(pathKey);
            }

            if (!string.IsNullOrWhiteSpace(annotation.Set?.SlotKey))
            {
                updated = updated with { SlotKey = annotation.Set.SlotKey };
            }

            if (annotation.Bindings != null && annotation.Bindings.Count > 0)
            {
                var properties = updated.Properties.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
                var bindings = updated.Bindings.ToList();

                foreach (var (property, bindingSpec) in annotation.Bindings)
                {
                    var trimmedProp = property.Trim();
                    var trimmedPath = bindingSpec.Path.Trim();
                    var trimmedKey = bindingSpec.Key?.Trim();
                    if (trimmedProp.Length == 0 || trimmedPath.Length == 0)
                    {
                        continue;
                    }

                    var memberPath = !string.IsNullOrWhiteSpace(trimmedKey) ? trimmedKey : trimmedPath;

                    // For text nodes we bind the text content directly.
                    if (string.Equals(trimmedProp, "text", StringComparison.OrdinalIgnoreCase))
                    {
                        properties["text"] = BindableValue<object?>.Bind(memberPath);
                    }

                    bindings.Add(new BindingSpec
                    {
                        Property = trimmedProp,
                        Path = trimmedPath,
                        Key = string.IsNullOrWhiteSpace(trimmedKey) ? null : trimmedKey,
                        Mode = BindingMode.OneWay
                    });
                }

                updated = updated with { Properties = properties, Bindings = bindings };
            }
        }

        return updated;
    }

    private static void CollectPseudoTypeWarnings(
        ComponentNode node,
        List<string> currentPath,
        HashSet<string> typeOverridePaths,
        List<string> warnings)
    {
        var id = node.Id ?? string.Empty;
        var nextPath = new List<string>(currentPath);
        if (!string.IsNullOrWhiteSpace(id))
        {
            nextPath.Add(id);
        }

        var pathKey = string.Join("/", nextPath);

        if (!typeOverridePaths.Contains(pathKey)
            && node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.NormalizedFromPseudoType, out var normalized)
            && normalized is bool normalizedBool
            && normalizedBool
            && node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.OriginalFigmaType, out var original)
            && original is string originalStr)
        {
            var suggestedType = string.Equals(originalStr, "BUTTON", StringComparison.OrdinalIgnoreCase)
                ? ComponentType.Button.ToString()
                : string.Equals(originalStr, "SLIDER", StringComparison.OrdinalIgnoreCase)
                    ? ComponentType.Slider.ToString()
                    : "(unknown)";

            warnings.Add($"Node '{id}' at path '{pathKey}' was normalized from pseudo type '{originalStr}'. Consider adding annotations set.type='{suggestedType}'.");
        }

        foreach (var child in node.Children)
        {
            CollectPseudoTypeWarnings(child, nextPath, typeOverridePaths, warnings);
        }
    }

    private static bool Matches(ComponentNode node, NodeMatch match, IReadOnlyList<string> path)
    {
        if (match.Path != null && match.Path.Count > 0)
        {
            if (!path.SequenceEqual(match.Path, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(match.Id))
        {
            if (!string.Equals(node.Id, match.Id, StringComparison.OrdinalIgnoreCase))
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
    public IReadOnlyDictionary<string, BindingAnnotation>? Bindings { get; init; }
}

[JsonConverter(typeof(BindingAnnotationJsonConverter))]
public sealed record BindingAnnotation
{
    public required string Path { get; init; }
    public string? Key { get; init; }
}

internal sealed class BindingAnnotationJsonConverter : JsonConverter<BindingAnnotation>
{
    public override BindingAnnotation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var path = reader.GetString() ?? string.Empty;
            return new BindingAnnotation { Path = path };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected binding value to be a string or object");
        }

        string? pathValue = null;
        string? keyValue = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name while reading binding object");
            }

            var propName = reader.GetString();
            reader.Read();

            if (string.Equals(propName, "path", StringComparison.OrdinalIgnoreCase))
            {
                pathValue = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                continue;
            }

            if (string.Equals(propName, "key", StringComparison.OrdinalIgnoreCase))
            {
                keyValue = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                continue;
            }

            reader.Skip();
        }

        if (string.IsNullOrWhiteSpace(pathValue))
        {
            throw new JsonException("Binding object must contain a non-empty 'path'");
        }

        return new BindingAnnotation { Path = pathValue!, Key = keyValue };
    }

    public override void Write(Utf8JsonWriter writer, BindingAnnotation value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("path", value.Path);
        if (!string.IsNullOrWhiteSpace(value.Key))
        {
            writer.WriteString("key", value.Key);
        }
        writer.WriteEndObject();
    }
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
    public string? SlotKey { get; init; }
}
