using System.Globalization;
using System.Text.Json;
using BoomHud.Abstractions.IR;

namespace BoomHud.Dsl.Pencil;

/// <summary>
/// Converts PenDto to BoomHud IR types.
/// Implementation follows docs/PENCIL-IR-MAPPING.md precisely.
/// </summary>
public sealed class PenToIrConverter
{
    private readonly PenDto _pen;
    private readonly List<string> _warnings = [];

    public PenToIrConverter(PenDto pen)
    {
        _pen = pen ?? throw new ArgumentNullException(nameof(pen));
    }

    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// Converts the entire .pen file to a HudDocument.
    /// </summary>
    public HudDocument Convert()
    {
        if (_pen.Nodes == null || _pen.Nodes.Count == 0)
        {
            throw new InvalidOperationException("No nodes found in .pen file");
        }

        // Find root node (first node at top level)
        var rootPenNode = _pen.Nodes[0];
        var bindings = _pen.Bindings ?? [];
        var rootComponent = ConvertNode(rootPenNode, bindings);

        // Derive name from root node
        var name = rootPenNode.Name ?? rootPenNode.Id ?? "Untitled";

        return new HudDocument
        {
            Name = name,
            Root = rootComponent
        };
    }

    /// <summary>
    /// Converts a single PenNodeDto to ComponentNode.
    /// </summary>
    private ComponentNode ConvertNode(PenNodeDto node, List<PenBindingDto> allBindings)
    {
        // Collect bindings: from both top-level array and inline node bindings
        var nodeBindings = new List<BindingSpec>();
        
        // From top-level bindings array
        nodeBindings.AddRange(allBindings
            .Where(b => b.NodeId == node.Id)
            .Select(ConvertBinding));
        
        // From inline bindings on node
        if (node.Bindings != null)
        {
            nodeBindings.AddRange(ConvertInlineBindings(node.Bindings));
        }

        // Convert children recursively
        var children = new List<ComponentNode>();
        if (node.Children != null)
        {
            foreach (var childNode in node.Children)
            {
                children.Add(ConvertNode(childNode, allBindings));
            }
        }

        // Build properties dictionary for text/image content
        var properties = new Dictionary<string, BindableValue<object?>>();
        
        // Text content can come from node.Content or node.Text.Content
        var textContent = node.Content ?? node.Text?.Content ?? node.Text?.Template;
        if (textContent != null)
        {
            properties["Text"] = new BindableValue<object?> { Value = textContent };
        }
        if (node.Image?.Src != null)
        {
            properties["Source"] = new BindableValue<object?> { Value = node.Image.Src };
        }
        if (node.Image?.Fit != null)
        {
            properties["Stretch"] = new BindableValue<object?> { Value = node.Image.Fit };
        }

        return new ComponentNode
        {
            Id = node.Id,
            Type = MapNodeType(node.Type),
            Layout = ConvertLayout(node.Layout),
            Style = ConvertStyle(node.Style),
            Children = children,
            Bindings = nodeBindings,
            Properties = properties
        };
    }

    /// <summary>
    /// Converts inline bindings object to BindingSpec list.
    /// Handles both simple string path and object with $bind/format/mode.
    /// </summary>
    private static List<BindingSpec> ConvertInlineBindings(Dictionary<string, object> bindings)
    {
        var result = new List<BindingSpec>();
        
        foreach (var (property, value) in bindings)
        {
            if (value is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.String)
                {
                    // Simple path: "content": "Fps"
                    result.Add(new BindingSpec
                    {
                        Property = property,
                        Path = je.GetString() ?? "",
                        Mode = BindingMode.OneWay
                    });
                }
                else if (je.ValueKind == JsonValueKind.Object)
                {
                    // Complex binding: "content": { "$bind": "Fps", "format": "{0:F0}" }
                    // Also support: "content": { "path": "Fps", "mode": "twoWay" }
                    string? path = null;
                    if (je.TryGetProperty("$bind", out var bindProp))
                        path = bindProp.GetString();
                    else if (je.TryGetProperty("path", out var pathProp))
                        path = pathProp.GetString();
                    
                    var format = je.TryGetProperty("format", out var formatProp) ? formatProp.GetString() : null;
                    var mode = je.TryGetProperty("mode", out var modeProp) ? modeProp.GetString() : "oneWay";
                    var converter = je.TryGetProperty("converter", out var convProp) ? convProp.GetString() : null;
                    var fallback = je.TryGetProperty("fallback", out var fbProp) ? fbProp.GetString() : null;
                    
                    result.Add(new BindingSpec
                    {
                        Property = property,
                        Path = path ?? "",
                        Mode = MapBindingMode(mode),
                        Format = format,
                        Converter = converter,
                        Fallback = fallback
                    });
                }
            }
            else if (value is string s)
            {
                // Simple path as string
                result.Add(new BindingSpec
                {
                    Property = property,
                    Path = s,
                    Mode = BindingMode.OneWay
                });
            }
        }
        
        return result;
    }

    /// <summary>
    /// Converts a PenBindingDto to BindingSpec.
    /// </summary>
    private static BindingSpec ConvertBinding(PenBindingDto binding)
    {
        return new BindingSpec
        {
            Property = binding.Property ?? "Text",
            Path = binding.Path ?? "",
            Mode = MapBindingMode(binding.Mode),
            Converter = binding.Converter,
            Format = binding.Format,
            Fallback = binding.Fallback
        };
    }

    /// <summary>
    /// Maps .pen node type to IR ComponentType.
    /// </summary>
    private static ComponentType MapNodeType(string? penType)
    {
        return penType?.ToLowerInvariant() switch
        {
            "frame" => ComponentType.Container,
            "text" => ComponentType.Label,
            "image" => ComponentType.Image,
            "component" => ComponentType.Container, // Custom components map to container
            "slot" => ComponentType.Container,      // Slots map to container
            "button" => ComponentType.Button,
            "input" => ComponentType.TextInput,
            "checkbox" => ComponentType.Checkbox,
            "slider" => ComponentType.Slider,
            "progress" => ComponentType.ProgressBar,
            "list" => ComponentType.ListBox,
            "scroll" => ComponentType.ScrollView,
            _ => ComponentType.Container // Default to container
        };
    }

    /// <summary>
    /// Converts PenLayoutDto to LayoutSpec.
    /// </summary>
    private LayoutSpec ConvertLayout(PenLayoutDto? layout)
    {
        if (layout == null)
        {
            return new LayoutSpec { Type = LayoutType.Vertical };
        }

        // Determine layout type from mode, type, direction, or position
        var layoutType = MapLayoutType(layout.Mode ?? layout.Type, layout.Direction, layout.Position);

        // Get alignment - can be from align, alignment, or justify properties
        var alignStr = layout.Align ?? layout.Alignment;
        
        return new LayoutSpec
        {
            Type = layoutType,
            Width = ParseDimension(layout.Width),
            Height = ParseDimension(layout.Height),
            Gap = ParseSpacingUniform(layout.Gap),
            Padding = ParseSpacing(layout.Padding),
            Margin = ParseSpacing(layout.Margin),
            Align = MapAlignment(alignStr),
            Justify = MapJustification(layout.Justify ?? GetJustifyFromAlignment(layout.Alignment))
            // Note: X/Y absolute positioning not in IR LayoutSpec - would need extension
        };
    }

    /// <summary>
    /// Some .pen files use alignment for both align and justify (e.g., "space-between").
    /// </summary>
    private static string? GetJustifyFromAlignment(string? alignment)
    {
        return alignment?.ToLowerInvariant() switch
        {
            "space-between" or "space-around" or "space-evenly" => alignment,
            _ => null
        };
    }

    /// <summary>
    /// Maps .pen layout type/mode + direction + position to IR LayoutType.
    /// </summary>
    private static LayoutType MapLayoutType(string? modeOrType, string? direction, string? position)
    {
        // Absolute position overrides everything
        if (position?.Equals("absolute", StringComparison.OrdinalIgnoreCase) == true)
            return LayoutType.Absolute;

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

    /// <summary>
    /// Maps alignment string to Alignment enum.
    /// </summary>
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

    /// <summary>
    /// Maps justify string to Justification enum.
    /// </summary>
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

    /// <summary>
    /// Converts PenStyleDto to StyleSpec.
    /// </summary>
    private StyleSpec ConvertStyle(PenStyleDto? style)
    {
        if (style == null)
        {
            return new StyleSpec();
        }

        // Use cornerRadius if borderRadius is not set
        var borderRadius = style.BorderRadius ?? style.CornerRadius;

        return new StyleSpec
        {
            Background = ParseColorFromObject(style.Background),
            BackgroundToken = IsTokenRef(style.Background) ? ExtractTokenName(style.Background) : null,
            Foreground = ParseColorFromObject(style.Foreground),
            ForegroundToken = IsTokenRef(style.Foreground) ? ExtractTokenName(style.Foreground) : null,
            FontSize = ParseDouble(style.FontSize),
            FontWeight = ParseFontWeight(style.FontWeight),
            Border = ParseBorder(style.BorderWidth, style.BorderColor),
            BorderColorToken = IsTokenRef(style.BorderColor) ? ExtractTokenName(style.BorderColor) : null,
            BorderRadius = ParseDouble(borderRadius),
            Opacity = style.Opacity
        };
    }

    /// <summary>
    /// Parses a color value from object (string or { "$ref": ... }) or returns null for token refs.
    /// </summary>
    private Color? ParseColorFromObject(object? value)
    {
        if (value == null) return null;

        // Handle JsonElement
        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
            {
                var s = je.GetString();
                return ParseColor(s);
            }
            // Token ref object { "$ref": "..." } - return null, use token instead
            if (je.ValueKind == JsonValueKind.Object)
                return null;
        }

        if (value is string str)
            return ParseColor(str);

        return null;
    }

    /// <summary>
    /// Parses a color value or returns null for token refs.
    /// </summary>
    private Color? ParseColor(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.StartsWith('$')) return null; // Token ref - use token instead
        
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

    /// <summary>
    /// Parses a double from object (number or string).
    /// </summary>
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
        if (value is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    /// <summary>
    /// Parses border specification.
    /// </summary>
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

    /// <summary>
    /// Parses a dimension value (number, string like "100px", "50%", "auto", or token ref).
    /// </summary>
    private Dimension? ParseDimension(object? value)
    {
        if (value == null) return null;

        // Handle JsonElement from System.Text.Json
        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number => Dimension.Pixels((float)jsonElement.GetDouble()),
                JsonValueKind.String => ParseDimensionString(jsonElement.GetString()),
                _ => null
            };
        }

        // Handle raw types
        if (value is double d) return Dimension.Pixels((float)d);
        if (value is int i) return Dimension.Pixels(i);
        if (value is float f) return Dimension.Pixels(f);
        if (value is string s) return ParseDimensionString(s);

        return null;
    }

    /// <summary>
    /// Parses dimension from string like "100px", "50%", "auto", "1*".
    /// </summary>
    private Dimension? ParseDimensionString(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        
        s = s.Trim();

        if (s.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return Dimension.Auto;

        if (s.Equals("fill", StringComparison.OrdinalIgnoreCase))
            return Dimension.Fill;

        if (s.StartsWith('$'))
        {
            // Token reference - resolve later
            _warnings.Add($"Token reference '{s}' in dimension - token resolution not yet implemented");
            return null;
        }

        // Try parsing with Dimension.Parse (handles px, %, *, etc.)
        try
        {
            return Dimension.Parse(s);
        }
        catch
        {
            // Parse failed, fall through
        }

        // Fallback: try as plain number
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
            return Dimension.Pixels((float)num);

        _warnings.Add($"Could not parse dimension: '{s}'");
        return null;
    }

    /// <summary>
    /// Parses spacing value (single number/token for uniform, or object for per-edge).
    /// </summary>
    private Spacing ParseSpacing(object? value)
    {
        if (value == null) return new Spacing();

        // Handle JsonElement
        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number => new Spacing((float)jsonElement.GetDouble()),
                JsonValueKind.String => ParseSpacingString(jsonElement.GetString()),
                JsonValueKind.Object => ParseSpacingObject(jsonElement),
                _ => new Spacing()
            };
        }

        // Handle raw types
        if (value is double d) return new Spacing((float)d);
        if (value is int i) return new Spacing(i);
        if (value is float f) return new Spacing(f);
        if (value is string s) return ParseSpacingString(s);

        return new Spacing();
    }

    /// <summary>
    /// Parses spacing to uniform Spacing? for Gap.
    /// </summary>
    private Spacing? ParseSpacingUniform(object? value)
    {
        if (value == null) return null;

        var spacingValue = ParseSpacingValue(value);
        if (spacingValue == 0f) return null;

        return new Spacing(spacingValue);
    }

    /// <summary>
    /// Parses single spacing value to float.
    /// </summary>
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

        // Remove units suffix
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
        var top = element.TryGetProperty("top", out var t) ? ParseSpacingValue(t) : 0f;
        var bottom = element.TryGetProperty("bottom", out var b) ? ParseSpacingValue(b) : 0f;
        var left = element.TryGetProperty("left", out var l) ? ParseSpacingValue(l) : 0f;
        var right = element.TryGetProperty("right", out var r) ? ParseSpacingValue(r) : 0f;
        
        return new Spacing(top, right, bottom, left);
    }

    /// <summary>
    /// Parses font weight from number or string.
    /// IR FontWeight only has: Light, Normal, Bold
    /// </summary>
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
            // Map numeric weights to Light/Normal/Bold
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

    /// <summary>
    /// Checks if a string is a token reference (starts with $).
    /// </summary>
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
            // Handle { "$ref": "..." } format
            if (je.ValueKind == JsonValueKind.Object && je.TryGetProperty("$ref", out _))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Extracts token name from "$token-name" or { "$ref": "tokens.x" } format.
    /// </summary>
    private static string? ExtractTokenName(string? tokenRef)
    {
        if (string.IsNullOrEmpty(tokenRef) || !tokenRef.StartsWith('$'))
            return null;
        return tokenRef[1..]; // Remove leading $
    }

    /// <summary>
    /// Extracts token name from object (string or { "$ref": "..." }).
    /// </summary>
    private static string? ExtractTokenName(object? value)
    {
        if (value is string s)
            return ExtractTokenName(s);
        
        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
                return ExtractTokenName(je.GetString());
            
            // Handle { "$ref": "tokens.colors.x" } format
            if (je.ValueKind == JsonValueKind.Object && je.TryGetProperty("$ref", out var refProp))
            {
                var refValue = refProp.GetString();
                if (!string.IsNullOrEmpty(refValue))
                {
                    // Convert "tokens.colors.debug-bg" -> "colors.debug-bg"
                    if (refValue.StartsWith("tokens.", StringComparison.OrdinalIgnoreCase))
                        return refValue["tokens.".Length..];
                    return refValue;
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// Maps binding mode string to BindingMode enum.
    /// </summary>
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
