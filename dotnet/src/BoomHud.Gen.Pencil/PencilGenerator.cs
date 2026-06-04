using System.Text.Encodings.Web;
using System.Text.Json;
using BoomHud.Abstractions.Capabilities;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Generators;
using BoomHud.Generators.VisualIR;

namespace BoomHud.Gen.Pencil;

public sealed class PencilGenerator : IBackendGenerator
{
    public string TargetFramework => "Pencil";

    public ICapabilityManifest Capabilities => PencilCapabilities.Instance;

    public GenerationResult Generate(HudDocument document, GenerationOptions options)
    {
        var diagnostics = new List<Diagnostic>();
        var files = new List<GeneratedFile>();
        var prepared = GenerationDocumentPreprocessor.Prepare(document, options, "pencil");
        document = prepared.Document;
        diagnostics.AddRange(prepared.Diagnostics);

        try
        {
            var serializer = new PencilDocumentSerializer();
            files.Add(new GeneratedFile
            {
                Path = $"{document.Name}.pen",
                Content = serializer.Serialize(document, prepared.VisualDocument),
                Type = GeneratedFileType.Other
            });
        }
        catch (Exception ex)
        {
            diagnostics.Add(Diagnostic.Error($"Pencil generation failed: {ex.Message}"));
        }

        if (GenerationDocumentPreprocessor.CreateSummaryArtifact(document.Name, prepared.SyntheticComponentization) is { } artifact)
        {
            files.Add(artifact);
        }

        if (options.EmitSourceSemanticArtifact
            && GenerationDocumentPreprocessor.CreateSourceSemanticArtifact(document.Name, prepared.SourceSemanticDocument) is { } sourceSemanticArtifact)
        {
            files.Add(sourceSemanticArtifact);
        }

        if (options.EmitVisualIrArtifact
            && GenerationDocumentPreprocessor.CreateVisualIrArtifact(document.Name, prepared.VisualDocument) is { } visualIrArtifact)
        {
            files.Add(visualIrArtifact);
        }

        if (options.EmitVisualSynthesisArtifact
            && GenerationDocumentPreprocessor.CreateVisualSynthesisArtifact(document.Name, prepared.VisualSynthesis) is { } visualSynthesisArtifact)
        {
            files.Add(visualSynthesisArtifact);
        }

        if (options.EmitVisualRefinementArtifact
            && GenerationDocumentPreprocessor.CreateVisualRefinementArtifact(document.Name, prepared.VisualRefinement) is { } visualRefinementArtifact)
        {
            files.Add(visualRefinementArtifact);
        }

        if (PencilPatchPlanBuilder.Build(prepared.VisualDocument, prepared.VisualRefinement) is { } patchPlan)
        {
            files.Add(new GeneratedFile
            {
                Path = $"{document.Name}.pen-patch-plan.json",
                Content = PencilPatchPlanBuilder.ToJson(patchPlan),
                Type = GeneratedFileType.Other
            });

            if (PencilPatchScriptBuilder.Build(patchPlan) is { } patchScript)
            {
                files.Add(new GeneratedFile
                {
                    Path = $"{document.Name}.pen-patch-script.txt",
                    Content = patchScript,
                    Type = GeneratedFileType.Other
                });
            }

            if (PencilBatchOpsBuilder.Build(patchPlan) is { } batchOps)
            {
                files.Add(new GeneratedFile
                {
                    Path = $"{document.Name}.pen-batch-ops.txt",
                    Content = batchOps,
                    Type = GeneratedFileType.Other
                });
            }
        }

        return new GenerationResult
        {
            Files = files,
            Diagnostics = diagnostics
        };
    }
}

internal sealed class PencilDocumentSerializer
{
    private readonly HashSet<string> _usedIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _componentOutputIds = new(StringComparer.Ordinal);
    private int _nextId = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Serialize(HudDocument document, VisualDocument? visualDocument = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        RegisterExistingIds(document.Root);
        foreach (var component in document.Components.Values)
        {
            RegisterExistingIds(component.Root);
            if (!string.IsNullOrWhiteSpace(component.Id))
            {
                _usedIds.Add(component.Id);
            }
        }

        foreach (var component in document.Components.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            _componentOutputIds[component.Key] = ReserveId(component.Key, component.Value.Name);
        }

        var skeleton = new PencilDocumentSkeleton(document.Name);
        var visualComponents = visualDocument?.Components.ToDictionary(static component => component.Id, StringComparer.Ordinal);
        foreach (var component in document.Components.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            skeleton.Children.Add(BuildSkeletonNode(
                component.Value.Root,
                component.Value.Name,
                reusable: true,
                forcedId: _componentOutputIds[component.Key],
                allowComponentRef: false));
        }

        skeleton.Children.Add(BuildSkeletonNode(
            document.Root,
            document.Name,
            reusable: false,
            forcedId: null,
            allowComponentRef: true));

        if (visualDocument != null)
        {
            for (var index = 0; index < document.Components.Count; index++)
            {
                var component = document.Components.OrderBy(static pair => pair.Key, StringComparer.Ordinal).ElementAt(index);
                var visualComponent = visualComponents != null && visualComponents.TryGetValue(component.Key, out var resolvedVisualComponent)
                    ? resolvedVisualComponent
                    : null;
                ApplyVisualRefinement(skeleton.Children[index], visualComponent?.Root);
            }

            ApplyVisualRefinement(skeleton.Children[^1], visualDocument.Root);
        }

        return JsonSerializer.Serialize(skeleton.ToSerializable(), JsonOptions);
    }

    private void RegisterExistingIds(ComponentNode node)
    {
        if (TryGetPreferredId(node, out var id))
        {
            _usedIds.Add(id);
        }

        foreach (var child in node.Children)
        {
            RegisterExistingIds(child);
        }
    }

    private PencilNodeSkeleton BuildSkeletonNode(
        ComponentNode node,
        string fallbackName,
        bool reusable,
        string? forcedId,
        bool allowComponentRef)
    {
        if (allowComponentRef
            && !string.IsNullOrWhiteSpace(node.ComponentRefId)
            && node.Children.Count == 0)
        {
            return BuildReferenceSkeletonNode(node, fallbackName, forcedId);
        }

        var penType = ResolvePenType(node);
        var result = new Dictionary<string, object?>();
        result["type"] = penType;
        result["id"] = ResolveNodeId(node, fallbackName, forcedId);
        result["name"] = ResolveName(node, fallbackName);
        if (reusable)
        {
            result["reusable"] = true;
        }

        ApplyLayout(result, node, penType, visualNode: null);
        ApplyStyle(result, node, penType, visualNode: null);
        ApplyProperties(result, node, penType, visualNode: null);
        ApplyBindings(result, node, penType);

        var skeleton = new PencilNodeSkeleton(node, fallbackName, penType, result);
        if (node.Children.Count > 0)
        {
            skeleton.Children.AddRange(node.Children
                .Select((child, index) => BuildSkeletonNode(
                    child,
                    $"{ResolveName(node, fallbackName)}{index + 1}",
                    reusable: false,
                    forcedId: null,
                    allowComponentRef: true))
                .ToList());
        }

        return skeleton;
    }

    private PencilNodeSkeleton BuildReferenceSkeletonNode(ComponentNode node, string fallbackName, string? forcedId)
    {
        var result = new Dictionary<string, object?>
        {
            ["type"] = "ref",
            ["id"] = ResolveNodeId(node, fallbackName, forcedId),
            ["name"] = ResolveName(node, fallbackName),
            ["ref"] = ResolveReferenceId(node.ComponentRefId)
        };

        ApplyLayout(result, node, "ref", visualNode: null);
        ApplyStyle(result, node, "ref", visualNode: null);
        ApplyProperties(result, node, "ref", visualNode: null);
        ApplyBindings(result, node, "ref");
        return new PencilNodeSkeleton(node, fallbackName, "ref", result);
    }

    private static void ApplyVisualRefinement(PencilNodeSkeleton node, VisualNode? visualNode)
    {
        if (visualNode != null)
        {
            ApplyLayout(node.Properties, node.SourceNode, node.PenType, visualNode);
            ApplyStyle(node.Properties, node.SourceNode, node.PenType, visualNode);
            ApplyProperties(node.Properties, node.SourceNode, node.PenType, visualNode);
        }

        for (var index = 0; index < node.Children.Count; index++)
        {
            var childVisualNode = index < (visualNode?.Children.Count ?? 0) ? visualNode!.Children[index] : null;
            ApplyVisualRefinement(node.Children[index], childVisualNode);
        }
    }

    private static string ResolveName(ComponentNode node, string fallbackName)
    {
        var preferredName = string.Empty;
        if (node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.OriginalPencilId, out var originalName)
            && originalName is string originalNameString
            && !string.IsNullOrWhiteSpace(originalNameString))
        {
            preferredName = originalNameString;
        }

        if (string.IsNullOrWhiteSpace(preferredName) && !string.IsNullOrWhiteSpace(node.Id))
        {
            preferredName = node.Id!;
        }

        if (!string.IsNullOrWhiteSpace(fallbackName)
            && !LooksGeneratedIdentifier(fallbackName)
            && LooksGeneratedIdentifier(preferredName))
        {
            return fallbackName;
        }

        return !string.IsNullOrWhiteSpace(preferredName) ? preferredName : fallbackName;
    }

    private string ResolveNodeId(ComponentNode node, string fallbackName, string? forcedId)
    {
        if (!string.IsNullOrWhiteSpace(forcedId))
        {
            return forcedId;
        }

        if (TryGetPreferredId(node, out var preferredId))
        {
            return preferredId;
        }

        var baseId = SanitizeId(!string.IsNullOrWhiteSpace(node.Id) ? node.Id! : fallbackName);
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = "node";
        }

        var candidate = baseId;
        while (!_usedIds.Add(candidate))
        {
            candidate = $"{baseId}_{_nextId++:D4}";
        }

        return candidate;
    }

    private string ResolveReferenceId(string? componentRefId)
    {
        if (string.IsNullOrWhiteSpace(componentRefId))
        {
            return string.Empty;
        }

        return _componentOutputIds.TryGetValue(componentRefId, out var mappedId)
            ? mappedId
            : SanitizeId(componentRefId);
    }

    private static bool TryGetPreferredId(ComponentNode node, out string id)
    {
        if (node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.OriginalPencilId, out var originalId)
            && originalId is string originalIdString
            && !string.IsNullOrWhiteSpace(originalIdString))
        {
            id = originalIdString;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(node.Id) && !node.Id!.Contains('/'))
        {
            id = SanitizeId(node.Id!);
            return true;
        }

        id = string.Empty;
        return false;
    }

    private static string SanitizeId(string value)
    {
        var chars = value
            .Select(static ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_')
            .ToArray();
        var sanitized = new string(chars).Trim('_');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "node";
        }

        return char.IsDigit(sanitized[0]) ? $"node_{sanitized}" : sanitized;
    }

    private string ReserveId(string preferredId, string fallbackName)
    {
        var baseId = SanitizeId(!string.IsNullOrWhiteSpace(preferredId) ? preferredId : fallbackName);
        var candidate = baseId;
        while (!_usedIds.Add(candidate))
        {
            candidate = $"{baseId}_{_nextId++:D4}";
        }

        return candidate;
    }

    private static string ResolvePenType(ComponentNode node)
    {
        if (node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.OriginalPencilType, out var originalType)
            && originalType is string originalTypeString
            && !string.IsNullOrWhiteSpace(originalTypeString))
        {
            return originalTypeString;
        }

        return node.Type switch
        {
            ComponentType.Label or ComponentType.Badge => "text",
            ComponentType.Icon => "icon_font",
            ComponentType.Image => "image",
            ComponentType.Panel when node.Children.Count == 0 => "rectangle",
            ComponentType.Spacer when node.Children.Count == 0 => "rectangle",
            _ => "frame"
        };
    }

    private static void ApplyLayout(Dictionary<string, object?> target, ComponentNode node, string penType, VisualNode? visualNode)
    {
        var layout = node.Layout;
        var visualBox = visualNode?.Box;
        var width = visualBox?.Width ?? layout?.Width ?? node.Style?.Width;
        var height = visualBox?.Height ?? layout?.Height ?? node.Style?.Height;
        var supportsContainerLayout = penType is "frame" or "ref";

        if (width != null)
        {
            target["width"] = ConvertDimension(width.Value);
        }

        if (height != null)
        {
            target["height"] = ConvertDimension(height.Value);
        }

        var isAbsolute = visualBox?.IsAbsolutePositioned == true
            || layout?.IsAbsolutePositioned == true
            || IsAbsoluteFromMetadata(node);
        var hasExplicitLayout = layout != null || visualBox?.LayoutType != null || isAbsolute;

        if (supportsContainerLayout && isAbsolute)
        {
            target["layout"] = "none";
        }
        else if (supportsContainerLayout && hasExplicitLayout)
        {
            var layoutType = visualBox?.LayoutType ?? layout?.Type ?? LayoutType.Vertical;
            target["layout"] = layoutType switch
            {
                LayoutType.Horizontal => "horizontal",
                LayoutType.Grid => "grid",
                LayoutType.Absolute => "none",
                _ => "vertical"
            };
        }

        if (visualBox?.Left != null)
        {
            target["x"] = ConvertNumericDimension(visualBox.Left.Value);
        }
        else if (layout?.Left != null)
        {
            target["x"] = ConvertNumericDimension(layout.Left.Value);
        }
        else if (node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.PencilLeft, out var pencilLeft) && TryConvertDouble(pencilLeft, out var x))
        {
            target["x"] = x;
        }

        if (visualBox?.Top != null)
        {
            target["y"] = ConvertNumericDimension(visualBox.Top.Value);
        }
        else if (layout?.Top != null)
        {
            target["y"] = ConvertNumericDimension(layout.Top.Value);
        }
        else if (node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.PencilTop, out var pencilTop) && TryConvertDouble(pencilTop, out var y))
        {
            target["y"] = y;
        }

        var padding = visualBox?.Padding ?? layout?.Padding;
        if (supportsContainerLayout && padding is { } resolvedPadding && !IsZeroSpacing(resolvedPadding))
        {
            target["padding"] = ConvertSpacing(resolvedPadding);
        }

        var gap = visualBox?.Gap ?? layout?.Gap;
        if (supportsContainerLayout && gap is { } resolvedGap && !IsZeroSpacing(resolvedGap))
        {
            target["gap"] = ConvertSpacingValue(resolvedGap.Left);
        }

        var align = visualBox?.Align ?? layout?.Align;
        if (supportsContainerLayout && align is { } resolvedAlign && resolvedAlign != Alignment.Start)
        {
            target["alignItems"] = resolvedAlign switch
            {
                Alignment.Center => "center",
                Alignment.End => "end",
                Alignment.Stretch => "stretch",
                _ => "start"
            };
        }

        var justify = visualBox?.Justify ?? layout?.Justify;
        if (supportsContainerLayout && justify is { } resolvedJustify && resolvedJustify != Justification.Start)
        {
            target["justifyContent"] = resolvedJustify switch
            {
                Justification.Center => "center",
                Justification.End => "end",
                Justification.SpaceBetween => "space-between",
                Justification.SpaceAround => "space-around",
                Justification.SpaceEvenly => "space-evenly",
                _ => "start"
            };
        }

        if (visualBox?.ClipContent == true || layout?.ClipContent == true || node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.PencilClip, out var clipValue) && clipValue is true)
        {
            target["clip"] = true;
        }
    }

    private static bool IsAbsoluteFromMetadata(ComponentNode node)
    {
        return node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.PencilPosition, out var position)
               && position is string positionString
               && string.Equals(positionString, "absolute", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyStyle(Dictionary<string, object?> target, ComponentNode node, string penType, VisualNode? visualNode)
    {
        var style = node.Style;
        var visualBox = visualNode?.Box;
        var typography = visualNode?.Typography;
        var icon = visualNode?.Icon;
        if (style == null)
        {
            style = new StyleSpec();
        }

        var fill = penType is "text" or "icon_font"
            ? style.Foreground
            : visualBox?.Background ?? style.Background;

        if (fill != null)
        {
            target["fill"] = fill.Value.ToHex();
        }

        var border = visualBox?.Border ?? style.Border;
        if (border is { } resolvedBorder && resolvedBorder.Style != BorderStyle.None && resolvedBorder.Color != null)
        {
            target["stroke"] = new Dictionary<string, object?>
            {
                ["thickness"] = ConvertSpacingValue(resolvedBorder.Width),
                ["fill"] = resolvedBorder.Color.Value.ToHex()
            };
        }

        var radius = visualBox?.BorderRadius ?? style.BorderRadius;
        if (radius is { } resolvedRadius)
        {
            target["cornerRadius"] = ConvertSpacingValue(resolvedRadius);
        }

        var opacity = visualBox?.Opacity ?? style.Opacity;
        if (opacity is { } resolvedOpacity)
        {
            target["opacity"] = resolvedOpacity;
        }

        var resolvedFontFamily = penType switch
        {
            "text" => typography?.ResolvedFontFamily ?? style.FontFamily,
            "icon_font" => icon?.ResolvedFontFamily ?? style.FontFamily,
            _ => null
        };
        if (penType is "text" or "icon_font" && !string.IsNullOrWhiteSpace(resolvedFontFamily))
        {
            target["fontFamily"] = resolvedFontFamily;
        }

        var resolvedFontSize = penType switch
        {
            "text" => typography?.ResolvedFontSize ?? style.FontSize,
            "icon_font" => icon?.ResolvedFontSize ?? style.FontSize,
            _ => null
        };
        if (penType is "text" or "icon_font" && resolvedFontSize is { } fontSize)
        {
            target["fontSize"] = ConvertSpacingValue(fontSize);
        }

        if (penType is "text" or "icon_font" && style.FontWeight is { } fontWeight && fontWeight != FontWeight.Normal)
        {
            target["fontWeight"] = fontWeight switch
            {
                FontWeight.Light => "light",
                FontWeight.Bold => "bold",
                _ => "normal"
            };
        }

        var resolvedLetterSpacing = penType == "text"
            ? typography?.ResolvedLetterSpacing ?? style.LetterSpacing
            : style.LetterSpacing;
        if (penType is "text" or "icon_font" && resolvedLetterSpacing is { } letterSpacing)
        {
            target["letterSpacing"] = letterSpacing;
        }
    }

    private static void ApplyProperties(Dictionary<string, object?> target, ComponentNode node, string penType, VisualNode? visualNode)
    {
        if (penType == "text" && TryGetProperty(node, "Text", out var textValue) && textValue is string text)
        {
            target["content"] = text;
        }

        if (penType == "icon_font")
        {
            if (TryGetProperty(node, "Text", out var iconName) && iconName is string iconString)
            {
                target["iconFontName"] = iconString;
            }

            target["iconFontFamily"] = node.Style?.FontFamily ?? "lucide";
        }

        if (penType == "image")
        {
            var image = new Dictionary<string, object?>();
            if (TryGetProperty(node, "Source", out var source) && source is string sourceString)
            {
                image["src"] = sourceString;
            }

            if (TryGetProperty(node, "Stretch", out var stretch) && stretch is string stretchString)
            {
                image["fit"] = stretchString;
            }

            if (image.Count > 0)
            {
                target["image"] = image;
            }
        }

        var textGrowth = visualNode?.Typography?.TextGrowth;
        if (string.IsNullOrWhiteSpace(textGrowth)
            && node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.PencilTextGrowth, out var rawTextGrowth)
            && rawTextGrowth is string textGrowthString
            && !string.IsNullOrWhiteSpace(textGrowthString))
        {
            textGrowth = textGrowthString;
        }

        if (!string.IsNullOrWhiteSpace(textGrowth))
        {
            target["textGrowth"] = textGrowth;
        }
    }

    private static void ApplyBindings(Dictionary<string, object?> target, ComponentNode node, string penType)
    {
        var bindings = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var binding in node.Bindings)
        {
            bindings[MapBindingProperty(binding.Property, penType)] = SerializeBinding(binding);
        }

        foreach (var (propertyName, bindable) in node.Properties)
        {
            if (!bindable.IsBound)
            {
                continue;
            }

            bindings[MapBindingProperty(propertyName, penType)] = SerializeBinding(new BindingSpec
            {
                Property = propertyName,
                Path = bindable.BindingPath ?? string.Empty,
                Mode = bindable.Mode,
                Format = bindable.Format
            });
        }

        if (node.Visible.IsBound)
        {
            bindings["visible"] = SerializeBinding(new BindingSpec
            {
                Property = "visible",
                Path = node.Visible.BindingPath ?? string.Empty,
                Mode = node.Visible.Mode,
                Format = node.Visible.Format
            });
        }

        if (bindings.Count > 0)
        {
            target["bindings"] = bindings;
        }
    }

    private static object SerializeBinding(BindingSpec binding)
    {
        var needsObject = binding.Mode != BindingMode.OneWay
            || !string.IsNullOrWhiteSpace(binding.Format)
            || !string.IsNullOrWhiteSpace(binding.Converter)
            || binding.ConverterParameter != null
            || binding.Fallback != null;

        if (!needsObject)
        {
            return binding.Path;
        }

        var result = new Dictionary<string, object?>
        {
            ["path"] = binding.Path
        };

        if (binding.Mode != BindingMode.OneWay)
        {
            result["mode"] = binding.Mode switch
            {
                BindingMode.TwoWay => "twoWay",
                BindingMode.OneTime => "oneTime",
                _ => "oneWay"
            };
        }

        if (!string.IsNullOrWhiteSpace(binding.Format))
        {
            result["format"] = binding.Format;
        }

        if (!string.IsNullOrWhiteSpace(binding.Converter))
        {
            result["converter"] = binding.Converter;
        }

        if (binding.ConverterParameter != null)
        {
            result["map"] = binding.ConverterParameter;
        }

        if (binding.Fallback != null)
        {
            result["fallback"] = binding.Fallback;
        }

        return result;
    }

    private static string MapBindingProperty(string property, string penType)
    {
        return property.Trim().ToLowerInvariant() switch
        {
            "text" => "content",
            "source" => "image.src",
            "stretch" => "image.fit",
            "style.foreground" or "style.background" => "style.fill",
            "style.bordercolor" => "style.stroke",
            "style.borderwidth" => "style.strokeWidth",
            "visible" => "visible",
            _ when penType == "text" && property.Equals("Text", StringComparison.OrdinalIgnoreCase) => "content",
            _ => property
        };
    }

    private static bool TryGetProperty(ComponentNode node, string propertyName, out object? value)
    {
        foreach (var (key, property) in node.Properties)
        {
            if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static object ConvertDimension(Dimension dimension)
    {
        return dimension.Unit switch
        {
            DimensionUnit.Pixels => ConvertSpacingValue(dimension.Value),
            DimensionUnit.Percent => $"{dimension.Value:0.####}%",
            DimensionUnit.Auto => "auto",
            DimensionUnit.Fill or DimensionUnit.Star => "fill_container",
            DimensionUnit.Cells => $"{dimension.Value:0.####}cell",
            _ => ConvertSpacingValue(dimension.Value)
        };
    }

    private static double ConvertNumericDimension(Dimension dimension)
    {
        return dimension.Value;
    }

    private static object ConvertSpacing(Spacing spacing)
    {
        if (spacing.Top == spacing.Right && spacing.Right == spacing.Bottom && spacing.Bottom == spacing.Left)
        {
            return ConvertSpacingValue(spacing.Top);
        }

        return new object[]
        {
            ConvertSpacingValue(spacing.Top),
            ConvertSpacingValue(spacing.Right),
            ConvertSpacingValue(spacing.Bottom),
            ConvertSpacingValue(spacing.Left)
        };
    }

    private static double ConvertSpacingValue(double value)
    {
        return Math.Round(value, 4);
    }

    private static bool TryConvertDouble(object? value, out double number)
    {
        switch (value)
        {
            case double doubleValue:
                number = doubleValue;
                return true;
            case float floatValue:
                number = floatValue;
                return true;
            case int intValue:
                number = intValue;
                return true;
            case long longValue:
                number = longValue;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static bool IsZeroSpacing(Spacing spacing)
    {
        return spacing.Top == 0
            && spacing.Right == 0
            && spacing.Bottom == 0
            && spacing.Left == 0;
    }

    private static bool LooksGeneratedIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith("synthetic_", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("node_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var hasDigit = false;

        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                return false;
            }

            hasDigit |= char.IsDigit(ch);
        }

        return value.Length >= 4 && hasDigit;
    }
}

internal sealed class PencilDocumentSkeleton(string name)
{
    public string Name { get; } = name;

    public List<PencilNodeSkeleton> Children { get; } = [];

    public object ToSerializable()
    {
        return new Dictionary<string, object?>
        {
            ["version"] = "2.10",
            ["name"] = Name,
            ["children"] = Children.Select(static child => child.ToSerializable()).ToList()
        };
    }
}

internal sealed class PencilNodeSkeleton(
    ComponentNode sourceNode,
    string fallbackName,
    string penType,
    Dictionary<string, object?> properties)
{
    public ComponentNode SourceNode { get; } = sourceNode;

    public string FallbackName { get; } = fallbackName;

    public string PenType { get; } = penType;

    public Dictionary<string, object?> Properties { get; } = properties;

    public List<PencilNodeSkeleton> Children { get; } = [];

    public object ToSerializable()
    {
        var result = new Dictionary<string, object?>(Properties, StringComparer.Ordinal);
        if (Children.Count > 0)
        {
            result["children"] = Children.Select(static child => child.ToSerializable()).ToList();
        }

        return result;
    }
}
