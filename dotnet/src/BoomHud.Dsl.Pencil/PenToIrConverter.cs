using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BoomHud.Abstractions.IR;

namespace BoomHud.Dsl.Pencil;

/// <summary>
/// Converts PenDto to BoomHud IR types.
/// Supports both the schema-first .pen shape and the raw editor export shape used by Pencil.
/// </summary>
public sealed class PenToIrConverter
{
    private static readonly JsonSerializerOptions CloneJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
    private static readonly HashSet<string> RootMergeSkipKeys = new(StringComparer.Ordinal)
    {
        "type",
        "children",
        "reusable",
        "ref",
        "descendants"
    };
    private static readonly HashSet<string> DescendantMergeSkipKeys = new(StringComparer.Ordinal)
    {
        "children",
        "reusable",
        "ref",
        "descendants"
    };
    private static readonly HashSet<string> EmptyMergeSkipKeys = [];

    private readonly PenDto _pen;
    private readonly List<string> _warnings = [];
    private readonly Dictionary<string, PenNodeDto> _reusableNodesById = new(StringComparer.Ordinal);

    public PenToIrConverter(PenDto pen)
    {
        _pen = pen ?? throw new ArgumentNullException(nameof(pen));
    }

    public IReadOnlyList<string> Warnings => _warnings;

    public HudDocument Convert()
    {
        var topLevelNodes = _pen.Nodes ?? _pen.Children ?? [];
        if (topLevelNodes.Count == 0)
        {
            throw new InvalidOperationException("No nodes found in .pen file");
        }

        foreach (var node in topLevelNodes.Where(IsReusableNode))
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                _warnings.Add($"Skipping reusable component without id: '{node.Name ?? "<unnamed>"}'");
                continue;
            }

            _reusableNodesById[node.Id] = node;
        }

        var bindings = _pen.Bindings ?? [];
        var rootPenNode = topLevelNodes.FirstOrDefault(node => !IsReusableNode(node)) ?? topLevelNodes[0];
        var rootComponent = ConvertNode(rootPenNode, bindings);
        var documentName = BuildTypeName(_pen.Name ?? rootPenNode.Name ?? rootPenNode.Id ?? "Untitled");
        var metadata = string.IsNullOrWhiteSpace(_pen.Version)
            ? null
            : new ComponentMetadata { Version = _pen.Version };

        return new HudDocument
        {
            Name = documentName,
            Metadata = metadata,
            Root = rootComponent,
            Components = BuildComponentDefinitions(bindings)
        };
    }

    private Dictionary<string, HudComponentDefinition> BuildComponentDefinitions(List<PenBindingDto> allBindings)
    {
        var components = new Dictionary<string, HudComponentDefinition>(StringComparer.Ordinal);

        foreach (var reusableNode in _reusableNodesById.Values)
        {
            var componentId = reusableNode.Id!;
            components[componentId] = new HudComponentDefinition
            {
                Id = componentId,
                Name = BuildTypeName(GetLeafName(reusableNode.Name) ?? reusableNode.Id ?? "Component"),
                Metadata = new ComponentMetadata
                {
                    Description = reusableNode.Name,
                    Tags = ["pencil", "component"]
                },
                Root = ConvertNode(reusableNode, allBindings)
            };
        }

        return components;
    }

    private ComponentNode ConvertNode(PenNodeDto sourceNode, List<PenBindingDto> allBindings)
    {
        var node = ExpandReferenceNode(sourceNode);
        var componentType = MapNodeType(node.Type);
        var nodeBindings = new List<BindingSpec>();

        nodeBindings.AddRange(allBindings
            .Where(b => string.Equals(b.NodeId, node.Id, StringComparison.Ordinal))
            .Select(b => ConvertBinding(b, componentType)));

        if (node.Bindings != null)
        {
            nodeBindings.AddRange(ConvertInlineBindings(node.Bindings, componentType));
        }

        var children = new List<ComponentNode>();
        if (node.Children != null)
        {
            foreach (var childNode in node.Children)
            {
                children.Add(ConvertNode(childNode, allBindings));
            }
        }

        var properties = new Dictionary<string, BindableValue<object?>>(StringComparer.Ordinal);
        var textContent = node.Content ?? node.Text?.Content ?? node.Text?.Template;
        if (!string.IsNullOrWhiteSpace(textContent))
        {
            properties["Text"] = new BindableValue<object?> { Value = textContent };
        }
        else if (componentType == ComponentType.Icon && !string.IsNullOrWhiteSpace(node.IconFontName))
        {
            properties["Text"] = new BindableValue<object?> { Value = node.IconFontName };
        }

        if (node.Image?.Src != null)
        {
            properties["Source"] = new BindableValue<object?> { Value = node.Image.Src };
        }
        if (node.Image?.Fit != null)
        {
            properties["Stretch"] = new BindableValue<object?> { Value = node.Image.Fit };
        }

        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(node.Type))
        {
            metadata[BoomHudMetadataKeys.OriginalPencilType] = node.Type;
        }
        if (node.X is { } x)
        {
            metadata[BoomHudMetadataKeys.PencilLeft] = x;
        }
        if (node.Y is { } y)
        {
            metadata[BoomHudMetadataKeys.PencilTop] = y;
        }
        if (HasAbsolutePosition(node))
        {
            metadata[BoomHudMetadataKeys.PencilPosition] = "absolute";
        }
        if (node.Clip is true)
        {
            metadata[BoomHudMetadataKeys.PencilClip] = true;
        }
        if (!string.IsNullOrWhiteSpace(sourceNode.Ref))
        {
            metadata[BoomHudMetadataKeys.PencilComponentRef] = sourceNode.Ref;
        }

        return new ComponentNode
        {
            Id = GetNodeIdentifier(node),
            Type = componentType,
            Layout = ConvertLayout(node),
            Style = ConvertStyle(node),
            Children = children,
            Bindings = nodeBindings,
            Properties = properties,
            InstanceOverrides = metadata
        };
    }

    private PenNodeDto ExpandReferenceNode(PenNodeDto node)
    {
        if (!string.Equals(node.Type, "ref", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(node.Ref))
        {
            return node;
        }

        if (!_reusableNodesById.TryGetValue(node.Ref, out var reusableNode))
        {
            _warnings.Add($"Could not resolve reusable component ref '{node.Ref}' for node '{node.Name ?? node.Id ?? "<unnamed>"}'");
            return node;
        }

        var mergedObject = JsonSerializer.SerializeToNode(reusableNode, CloneJsonOptions)?.AsObject()
            ?? throw new InvalidOperationException("Failed to clone reusable Pencil component.");
        var instanceObject = JsonSerializer.SerializeToNode(node, CloneJsonOptions)?.AsObject()
            ?? throw new InvalidOperationException("Failed to serialize Pencil component instance.");

        MergeNodeObjects(mergedObject, instanceObject, RootMergeSkipKeys);

        if (node.Descendants != null)
        {
            foreach (var (descendantId, overrideElement) in node.Descendants)
            {
                if (!TryFindDescendantObject(mergedObject, descendantId, out var target))
                {
                    _warnings.Add($"Could not find descendant override target '{descendantId}' on ref '{node.Ref}'");
                    continue;
                }

                if (JsonNode.Parse(overrideElement.GetRawText()) is JsonObject overrideNode)
                {
                    MergeNodeObjects(target, overrideNode, DescendantMergeSkipKeys);
                }
            }
        }

        var expanded = mergedObject.Deserialize<PenNodeDto>(CloneJsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize merged Pencil component instance.");
        expanded.Reusable = false;
        expanded.Ref = null;
        expanded.Descendants = null;
        return expanded;
    }

    private static void MergeNodeObjects(JsonObject target, JsonObject overlay, HashSet<string> skipKeys)
    {
        foreach (var pair in overlay)
        {
            if (skipKeys.Contains(pair.Key) || pair.Value == null)
            {
                continue;
            }

            if (pair.Value is JsonObject overlayObject && target[pair.Key] is JsonObject targetObject)
            {
                MergeNodeObjects(targetObject, overlayObject, EmptyMergeSkipKeys);
                continue;
            }

            target[pair.Key] = JsonNode.Parse(pair.Value.ToJsonString());
        }
    }

    private static bool TryFindDescendantObject(JsonObject node, string descendantId, out JsonObject result)
    {
        if (string.Equals(node["id"]?.GetValue<string>(), descendantId, StringComparison.Ordinal))
        {
            result = node;
            return true;
        }

        if (node["children"] is JsonArray children)
        {
            foreach (var child in children.OfType<JsonObject>())
            {
                if (TryFindDescendantObject(child, descendantId, out result))
                {
                    return true;
                }
            }
        }

        result = null!;
        return false;
    }

    private static string? GetLeafName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var lastSlash = name.LastIndexOf('/');
        return lastSlash >= 0 ? name[(lastSlash + 1)..] : name;
    }

    private static string GetNodeIdentifier(PenNodeDto node)
    {
        if (!string.IsNullOrWhiteSpace(node.Id) && !LooksGeneratedIdentifier(node.Id))
        {
            return node.Id;
        }

        return node.Name ?? node.Id ?? "Node";
    }

    private static bool LooksGeneratedIdentifier(string id)
    {
        var hasDigit = false;
        var hasUpper = false;
        var hasLower = false;

        foreach (var ch in id)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                return false;
            }

            hasDigit |= char.IsDigit(ch);
            hasUpper |= char.IsUpper(ch);
            hasLower |= char.IsLower(ch);
        }

        return id.Length >= 5 && (hasDigit || (hasUpper && hasLower));
    }

    private static string BuildTypeName(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        var token = new System.Text.StringBuilder();

        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                token.Append(ch);
                continue;
            }

            AppendTypeNameToken(builder, token);
        }

        AppendTypeNameToken(builder, token);

        if (builder.Length == 0)
        {
            return "GeneratedHud";
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, 'N');
        }

        return builder.ToString();
    }

    private static void AppendTypeNameToken(System.Text.StringBuilder builder, System.Text.StringBuilder token)
    {
        if (token.Length == 0)
        {
            return;
        }

        var tokenValue = token.ToString();
        if (tokenValue.Length > 1 && tokenValue.All(char.IsUpper))
        {
            tokenValue = char.ToUpperInvariant(tokenValue[0]) + tokenValue[1..].ToLowerInvariant();
        }
        else
        {
            tokenValue = char.ToUpperInvariant(tokenValue[0]) + tokenValue[1..];
        }

        builder.Append(tokenValue);
        token.Clear();
    }

    private static bool IsReusableNode(PenNodeDto node)
    {
        return node.Reusable == true;
    }

    private static List<BindingSpec> ConvertInlineBindings(Dictionary<string, object> bindings, ComponentType componentType)
    {
        var result = new List<BindingSpec>();

        foreach (var (property, value) in bindings)
        {
            if (value is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.String)
                {
                    result.Add(new BindingSpec
                    {
                        Property = NormalizeBindingProperty(property, componentType),
                        Path = je.GetString() ?? string.Empty,
                        Mode = BindingMode.OneWay
                    });
                }
                else if (je.ValueKind == JsonValueKind.Object)
                {
                    string? path = null;
                    if (je.TryGetProperty("$bind", out var bindProp))
                        path = bindProp.GetString();
                    else if (je.TryGetProperty("path", out var pathProp))
                        path = pathProp.GetString();

                    var format = je.TryGetProperty("format", out var formatProp) ? formatProp.GetString() : null;
                    var mode = je.TryGetProperty("mode", out var modeProp) ? modeProp.GetString() : "oneWay";
                    var converter = je.TryGetProperty("converter", out var convProp) ? convProp.GetString() : null;
                    var fallback = je.TryGetProperty("fallback", out var fbProp) ? ExtractBindingValue(fbProp) : null;
                    var map = je.TryGetProperty("map", out var mapProp) ? ExtractBindingValue(mapProp) : null;

                    result.Add(new BindingSpec
                    {
                        Property = NormalizeBindingProperty(property, componentType),
                        Path = path ?? string.Empty,
                        Mode = MapBindingMode(mode),
                        Format = format,
                        Converter = converter,
                        ConverterParameter = map,
                        Fallback = fallback
                    });
                }
            }
            else if (value is string s)
            {
                result.Add(new BindingSpec
                {
                    Property = NormalizeBindingProperty(property, componentType),
                    Path = s,
                    Mode = BindingMode.OneWay
                });
            }
        }

        return result;
    }

    private static BindingSpec ConvertBinding(PenBindingDto binding, ComponentType componentType)
    {
        return new BindingSpec
        {
            Property = NormalizeBindingProperty(binding.Property, componentType),
            Path = binding.Path ?? string.Empty,
            Mode = MapBindingMode(binding.Mode),
            Converter = binding.Converter,
            ConverterParameter = binding.Map,
            Format = binding.Format,
            Fallback = binding.Fallback
        };
    }

    private static string NormalizeBindingProperty(string? property, ComponentType componentType)
    {
        return property?.Trim().ToLowerInvariant() switch
        {
            null or "" => "Text",
            "content" => "Text",
            "text.content" => "Text",
            "style.fill" => BindsFillToForeground(componentType) ? "style.foreground" : "style.background",
            "style.stroke" => "style.borderColor",
            "style.strokewidth" => "style.borderWidth",
            _ => property!
        };
    }

    private static bool BindsFillToForeground(ComponentType componentType)
    {
        return componentType is ComponentType.Label
            or ComponentType.Badge
            or ComponentType.Icon
            or ComponentType.Button
            or ComponentType.TextInput
            or ComponentType.TextArea
            or ComponentType.Checkbox
            or ComponentType.RadioButton;
    }

    private static object? ExtractBindingValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var intValue) => intValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.Object or JsonValueKind.Array => element.Clone(),
            _ => element.Clone()
        };
    }

    private static ComponentType MapNodeType(string? penType)
    {
        return penType?.ToLowerInvariant() switch
        {
            "frame" => ComponentType.Container,
            "text" => ComponentType.Label,
            "image" => ComponentType.Image,
            "rectangle" => ComponentType.Panel,
            "component" => ComponentType.Container,
            "slot" => ComponentType.Container,
            "button" => ComponentType.Button,
            "input" => ComponentType.TextInput,
            "checkbox" => ComponentType.Checkbox,
            "slider" => ComponentType.Slider,
            "progress" => ComponentType.ProgressBar,
            "list" => ComponentType.ListBox,
            "scroll" => ComponentType.ScrollView,
            "icon_font" => ComponentType.Icon,
            _ => ComponentType.Container
        };
    }

    private LayoutSpec ConvertLayout(PenNodeDto node)
    {
        string? mode = null;
        string? type = null;
        string? direction = null;
        string? position = null;
        string? align = null;
        string? alignment = null;
        string? justify = null;
        object? width = node.Width;
        object? height = node.Height;
        object? gap = node.Gap;
        object? padding = node.Padding;
        object? margin = null;

        if (node.Layout is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                type = GetOptionalString(jsonElement, "type");
                mode = GetOptionalString(jsonElement, "mode");
                direction = GetOptionalString(jsonElement, "direction");
                position = GetOptionalString(jsonElement, "position");
                align = GetOptionalString(jsonElement, "align");
                alignment = GetOptionalString(jsonElement, "alignment");
                justify = GetOptionalString(jsonElement, "justify");
                width ??= GetOptionalProperty(jsonElement, "width");
                height ??= GetOptionalProperty(jsonElement, "height");
                gap ??= GetOptionalProperty(jsonElement, "gap");
                padding ??= GetOptionalProperty(jsonElement, "padding");
                margin = GetOptionalProperty(jsonElement, "margin");
            }
            else if (jsonElement.ValueKind == JsonValueKind.String)
            {
                mode = jsonElement.GetString();
            }
        }
        else if (node.Layout is string layoutString)
        {
            mode = layoutString;
        }

        justify ??= node.JustifyContent;
        align ??= node.AlignItems;

        var layoutMode = mode ?? type ?? GetDefaultLayoutMode(node.Type);
        var layoutType = MapLayoutType(layoutMode, direction, position);
        return new LayoutSpec
        {
            Type = layoutType,
            Width = ParseDimension(width),
            Height = ParseDimension(height),
            Gap = ParseSpacingUniform(gap),
            Padding = ParseSpacing(padding),
            Margin = ParseSpacing(margin),
            Align = MapAlignment(align ?? alignment),
            Justify = MapJustification(justify ?? GetJustifyFromAlignment(alignment ?? align))
        };
    }

    private static string? GetDefaultLayoutMode(string? nodeType)
    {
        return nodeType?.ToLowerInvariant() switch
        {
            "frame" => "horizontal",
            "group" => "none",
            _ => null
        };
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static JsonElement? GetOptionalProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) ? property.Clone() : null;
    }

    private static string? GetJustifyFromAlignment(string? alignment)
    {
        return alignment?.ToLowerInvariant() switch
        {
            "space-between" or "space-around" or "space-evenly" => alignment,
            _ => null
        };
    }

    private static LayoutType MapLayoutType(string? modeOrType, string? direction, string? position)
    {
        if (position?.Equals("absolute", StringComparison.OrdinalIgnoreCase) == true
            && string.IsNullOrWhiteSpace(modeOrType))
        {
            return LayoutType.Absolute;
        }

        return modeOrType?.ToLowerInvariant() switch
        {
            "absolute" or "none" => LayoutType.Absolute,
            "grid" => LayoutType.Grid,
            "vertical" => LayoutType.Vertical,
            "horizontal" => LayoutType.Horizontal,
            "flex" => direction?.ToLowerInvariant() == "row" ? LayoutType.Horizontal : LayoutType.Vertical,
            _ => LayoutType.Vertical
        };
    }

    private static bool HasAbsolutePosition(PenNodeDto node)
    {
        if (node.Layout is PenLayoutDto layoutDto)
        {
            return string.Equals(layoutDto.Position, "absolute", StringComparison.OrdinalIgnoreCase)
                || string.Equals(layoutDto.Type, "absolute", StringComparison.OrdinalIgnoreCase);
        }

        if (node.Layout is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            if (jsonElement.TryGetProperty("position", out var position)
                && position.ValueKind == JsonValueKind.String
                && string.Equals(position.GetString(), "absolute", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (jsonElement.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && string.Equals(type.GetString(), "absolute", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Alignment MapAlignment(string? align)
    {
        return align?.ToLowerInvariant() switch
        {
            "start" => Alignment.Start,
            "center" => Alignment.Center,
            "end" => Alignment.End,
            "stretch" => Alignment.Stretch,
            _ => Alignment.Start
        };
    }

    private static Justification MapJustification(string? justify)
    {
        return justify?.ToLowerInvariant() switch
        {
            "start" => Justification.Start,
            "center" => Justification.Center,
            "end" => Justification.End,
            "space-between" => Justification.SpaceBetween,
            "space-around" => Justification.SpaceAround,
            "space-evenly" => Justification.SpaceEvenly,
            _ => Justification.Start
        };
    }

    private StyleSpec ConvertStyle(PenNodeDto node)
    {
        var style = node.Style;
        var borderRadius = style?.BorderRadius ?? style?.CornerRadius;
        var primaryFill = node.Fill ?? style?.Fill ?? style?.Background;
        var isTextLikeNode = string.Equals(node.Type, "text", StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.Type, "icon_font", StringComparison.OrdinalIgnoreCase);
        var foregroundSource = style?.Foreground ?? node.Fill ?? (isTextLikeNode ? primaryFill : null);
        var backgroundSource = isTextLikeNode ? style?.Background : primaryFill;
        var strokeSource = node.Stroke ?? style?.Stroke ?? style?.BorderColor;
        var borderColorSource = ExtractStrokeFill(strokeSource) ?? style?.BorderColor ?? style?.Stroke ?? node.Stroke;
        var borderWidthSource = node.StrokeWidth ?? style?.StrokeWidth ?? style?.BorderWidth ?? ExtractStrokeThickness(strokeSource);

        return new StyleSpec
        {
            Background = ParseColorFromObject(backgroundSource),
            BackgroundImage = ParseBackgroundImageFill(backgroundSource),
            BackgroundToken = IsTokenRef(backgroundSource) ? ExtractTokenName(backgroundSource) : null,
            Foreground = ParseColorFromObject(foregroundSource),
            ForegroundToken = IsTokenRef(foregroundSource) ? ExtractTokenName(foregroundSource) : null,
            FontSize = ParseDouble(node.FontSize ?? style?.FontSize),
            FontFamily = node.IconFontFamily ?? node.FontFamily ?? style?.FontFamily,
            FontWeight = ParseFontWeight(node.FontWeight ?? style?.FontWeight),
            LetterSpacing = node.LetterSpacing ?? style?.LetterSpacing,
            Border = ParseBorder(borderWidthSource, borderColorSource),
            BorderColorToken = IsTokenRef(borderColorSource) ? ExtractTokenName(borderColorSource) : null,
            BorderRadius = ParseDouble(borderRadius),
            Opacity = style?.Opacity
        };
    }

    private BackgroundImageSpec? ParseBackgroundImageFill(object? value)
    {
        if (value is not JsonElement { ValueKind: JsonValueKind.Object } element)
        {
            return null;
        }

        if (element.TryGetProperty("fill", out var fillProp))
        {
            return ParseBackgroundImageFill(fillProp.Clone());
        }

        if (!element.TryGetProperty("type", out var typeProp)
            || typeProp.ValueKind != JsonValueKind.String
            || !string.Equals(typeProp.GetString(), "image", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (element.TryGetProperty("enabled", out var enabledProp)
            && enabledProp.ValueKind is JsonValueKind.False)
        {
            return null;
        }

        if (!element.TryGetProperty("url", out var urlProp)
            || urlProp.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(urlProp.GetString()))
        {
            _warnings.Add("Encountered image paint fill in .pen file without a url; skipping background image.");
            return null;
        }

        return new BackgroundImageSpec
        {
            Url = urlProp.GetString()!,
            Mode = ParseBackgroundImageMode(element.TryGetProperty("mode", out var modeProp) ? modeProp.GetString() : null)
        };
    }

    private static BackgroundImageMode ParseBackgroundImageMode(string? mode)
        => mode?.Trim().ToLowerInvariant() switch
        {
            "contain" => BackgroundImageMode.Contain,
            "fit" => BackgroundImageMode.Contain,
            "stretch" => BackgroundImageMode.Stretch,
            "tile" => BackgroundImageMode.Tile,
            "original" => BackgroundImageMode.Original,
            "fill" => BackgroundImageMode.Fill,
            "cover" => BackgroundImageMode.Fill,
            _ => BackgroundImageMode.Fill
        };

    private static JsonElement? ExtractStrokeFill(object? stroke)
    {
        return stroke is JsonElement { ValueKind: JsonValueKind.Object } element && element.TryGetProperty("fill", out var fill)
            ? fill.Clone()
            : null;
    }

    private static JsonElement? ExtractStrokeThickness(object? stroke)
    {
        return stroke is JsonElement { ValueKind: JsonValueKind.Object } element && element.TryGetProperty("thickness", out var thickness)
            ? thickness.Clone()
            : null;
    }

    private Color? ParseColorFromObject(object? value)
    {
        if (value == null) return null;

        var tokenName = ExtractTokenName(value);
        if (!string.IsNullOrWhiteSpace(tokenName))
        {
            return ResolveInlineColorToken(tokenName);
        }

        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
            {
                return ParseColor(je.GetString());
            }

            if (je.ValueKind == JsonValueKind.Object)
            {
                if (je.TryGetProperty("fill", out var fillProp))
                {
                    return ParseColorFromObject(fillProp.Clone());
                }

                return null;
            }
        }

        if (value is string str)
            return ParseColor(str);

        return null;
    }

    private Color? ParseColor(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.StartsWith('$')) return null;

        try
        {
            return Color.Parse(value);
        }
        catch
        {
            _warnings.Add($"Could not parse color: '{value}'");
            return null;
        }
    }

    private Color? ResolveInlineColorToken(string tokenName)
    {
        var colors = _pen.Tokens?.Colors;
        if (colors == null || colors.Count == 0)
        {
            return null;
        }

        var normalizedTokenName = tokenName.Trim();
        if (normalizedTokenName.StartsWith("colors.", StringComparison.OrdinalIgnoreCase))
        {
            normalizedTokenName = normalizedTokenName["colors.".Length..];
        }

        if (!colors.TryGetValue(normalizedTokenName, out var colorValue) || string.IsNullOrWhiteSpace(colorValue))
        {
            return null;
        }

        try
        {
            return Color.Parse(colorValue);
        }
        catch
        {
            _warnings.Add($"Could not parse inline token color '{tokenName}' with value '{colorValue}'.");
            return null;
        }
    }

    private static double? ParseDouble(object? value)
    {
        if (value == null) return null;

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number => jsonElement.GetDouble(),
                JsonValueKind.String => double.TryParse(jsonElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFromJson) ? parsedFromJson : null,
                _ => null
            };
        }

        if (value is double dbl) return dbl;
        if (value is int i) return i;
        if (value is float f) return f;
        if (value is long l) return l;
        if (value is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private BorderSpec? ParseBorder(object? width, object? color)
    {
        var borderWidth = ParseDouble(width);
        var borderColor = ParseColorFromObject(color);
        if (borderWidth == null && borderColor == null && !IsTokenRef(color)) return null;

        return new BorderSpec
        {
            Width = borderWidth ?? 1,
            Color = borderColor,
            Style = BorderStyle.Solid
        };
    }

    private Dimension? ParseDimension(object? value)
    {
        if (value == null) return null;

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number => Dimension.Pixels((float)jsonElement.GetDouble()),
                JsonValueKind.String => ParseDimensionString(jsonElement.GetString()),
                JsonValueKind.Object => ParseDimensionObject(jsonElement),
                _ => null
            };
        }

        if (value is double d) return Dimension.Pixels((float)d);
        if (value is int i) return Dimension.Pixels(i);
        if (value is float f) return Dimension.Pixels(f);
        if (value is long l) return Dimension.Pixels(l);
        if (value is string s) return ParseDimensionString(s);

        return null;
    }

    private Dimension? ParseDimensionString(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        s = s.Trim();

        if (s.Equals("auto", StringComparison.OrdinalIgnoreCase) || s.Equals("hug", StringComparison.OrdinalIgnoreCase))
            return Dimension.Auto;

        if (s.Equals("fill", StringComparison.OrdinalIgnoreCase)
            || s.Equals("fill_container", StringComparison.OrdinalIgnoreCase)
            || s.Equals("fill-container", StringComparison.OrdinalIgnoreCase))
            return Dimension.Fill;

        if (s.StartsWith('$'))
        {
            _warnings.Add($"Token reference '{s}' in dimension - token resolution not yet implemented");
            return null;
        }

        try
        {
            return Dimension.Parse(s);
        }
        catch
        {
            // fall through
        }

        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
            return Dimension.Pixels((float)num);

        _warnings.Add($"Could not parse dimension: '{s}'");
        return null;
    }

    private static Dimension? ParseDimensionObject(JsonElement element)
    {
        if (!element.TryGetProperty("type", out var typeProp))
        {
            return null;
        }

        var type = typeProp.GetString();
        var value = element.TryGetProperty("value", out var valueProp) && valueProp.ValueKind == JsonValueKind.Number
            ? valueProp.GetDouble()
            : 0d;

        return type?.ToLowerInvariant() switch
        {
            "fixed" => Dimension.Pixels(value),
            "fill" => Dimension.Fill,
            "hug" => Dimension.Auto,
            "auto" => Dimension.Auto,
            _ => null
        };
    }

    private Spacing ParseSpacing(object? value)
    {
        if (value == null) return new Spacing();

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number => new Spacing((float)jsonElement.GetDouble()),
                JsonValueKind.String => ParseSpacingString(jsonElement.GetString()),
                JsonValueKind.Object => ParseSpacingObject(jsonElement),
                JsonValueKind.Array => ParseSpacingArray(jsonElement),
                _ => new Spacing()
            };
        }

        if (value is double d) return new Spacing((float)d);
        if (value is int i) return new Spacing(i);
        if (value is float f) return new Spacing(f);
        if (value is long l) return new Spacing(l);
        if (value is string s) return ParseSpacingString(s);

        return new Spacing();
    }

    private Spacing? ParseSpacingUniform(object? value)
    {
        if (value == null) return null;

        var spacingValue = ParseSpacingValue(value);
        if (spacingValue == 0f) return null;

        return new Spacing(spacingValue);
    }

    private float ParseSpacingValue(object? value)
    {
        if (value == null) return 0f;

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number => (float)jsonElement.GetDouble(),
                JsonValueKind.String => ParseSpacingFloat(jsonElement.GetString()),
                _ => 0f
            };
        }

        if (value is double d) return (float)d;
        if (value is int i) return i;
        if (value is float f) return f;
        if (value is long l) return l;
        if (value is string s) return ParseSpacingFloat(s);

        return 0f;
    }

    private float ParseSpacingFloat(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0f;

        if (s.StartsWith('$'))
        {
            _warnings.Add($"Token reference '{s}' in spacing - token resolution not yet implemented");
            return 0f;
        }

        s = s.TrimEnd('p', 'x', 'd', 'P', 'X', 'D');

        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0f;
    }

    private Spacing ParseSpacingString(string? s)
    {
        var value = ParseSpacingFloat(s);
        return new Spacing(value);
    }

    private Spacing ParseSpacingObject(JsonElement element)
    {
        var vertical = element.TryGetProperty("vertical", out var verticalProp) ? ParseSpacingValue(verticalProp) : 0f;
        var horizontal = element.TryGetProperty("horizontal", out var horizontalProp) ? ParseSpacingValue(horizontalProp) : 0f;
        var top = element.TryGetProperty("top", out var t) ? ParseSpacingValue(t) : vertical;
        var bottom = element.TryGetProperty("bottom", out var b) ? ParseSpacingValue(b) : vertical;
        var left = element.TryGetProperty("left", out var l) ? ParseSpacingValue(l) : horizontal;
        var right = element.TryGetProperty("right", out var r) ? ParseSpacingValue(r) : horizontal;

        return new Spacing(top, right, bottom, left);
    }

    private Spacing ParseSpacingArray(JsonElement element)
    {
        if (element.GetArrayLength() == 2)
        {
            return new Spacing(ParseSpacingValue(element[0]), ParseSpacingValue(element[1]));
        }

        if (element.GetArrayLength() >= 4)
        {
            return new Spacing(
                ParseSpacingValue(element[0]),
                ParseSpacingValue(element[1]),
                ParseSpacingValue(element[2]),
                ParseSpacingValue(element[3]));
        }

        return new Spacing();
    }

    private static FontWeight ParseFontWeight(object? value)
    {
        if (value == null) return FontWeight.Normal;

        string? str = null;
        int? num = null;

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Number)
                num = jsonElement.GetInt32();
            else if (jsonElement.ValueKind == JsonValueKind.String)
                str = jsonElement.GetString();
        }
        else if (value is int i) num = i;
        else if (value is double d) num = (int)d;
        else if (value is string s) str = s;

        if (num.HasValue)
        {
            return num.Value switch
            {
                < 400 => FontWeight.Light,
                < 600 => FontWeight.Normal,
                _ => FontWeight.Bold
            };
        }

        if (str != null)
        {
            return str.ToLowerInvariant() switch
            {
                "thin" or "extralight" or "extra-light" or "light" => FontWeight.Light,
                "normal" or "regular" or "medium" => FontWeight.Normal,
                "semibold" or "semi-bold" or "bold" or "extrabold" or "extra-bold" or "black" or "heavy" => FontWeight.Bold,
                _ => FontWeight.Normal
            };
        }

        return FontWeight.Normal;
    }

    private static bool IsTokenRef(string? value)
    {
        return !string.IsNullOrEmpty(value) && value.StartsWith('$');
    }

    private static bool IsTokenRef(object? value)
    {
        if (value is string s) return IsTokenRef(s);
        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
                return IsTokenRef(je.GetString());
            if (je.ValueKind == JsonValueKind.Object && je.TryGetProperty("$ref", out _))
                return true;
        }
        return false;
    }

    private static string? ExtractTokenName(string? tokenRef)
    {
        if (string.IsNullOrEmpty(tokenRef) || !tokenRef.StartsWith('$'))
            return null;
        return tokenRef[1..];
    }

    private static string? ExtractTokenName(object? value)
    {
        if (value is string s)
            return ExtractTokenName(s);

        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
                return ExtractTokenName(je.GetString());

            if (je.ValueKind == JsonValueKind.Object && je.TryGetProperty("$ref", out var refProp))
            {
                var refValue = refProp.GetString();
                if (!string.IsNullOrEmpty(refValue))
                {
                    if (refValue.StartsWith("tokens.", StringComparison.OrdinalIgnoreCase))
                        return refValue["tokens.".Length..];
                    return refValue;
                }
            }
        }

        return null;
    }

    private static BindingMode MapBindingMode(string? mode)
    {
        return mode?.ToLowerInvariant() switch
        {
            "oneway" or "one-way" => BindingMode.OneWay,
            "twoway" or "two-way" => BindingMode.TwoWay,
            "onetime" or "one-time" => BindingMode.OneTime,
            _ => BindingMode.OneWay
        };
    }
}
