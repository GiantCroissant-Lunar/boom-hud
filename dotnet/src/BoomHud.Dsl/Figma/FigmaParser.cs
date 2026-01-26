using System;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using BoomHud.Abstractions.IR;

namespace BoomHud.Dsl.Figma;

/// <summary>
/// Parser for Figma REST API JSON format.
/// Converts Figma node tree to BoomHud IR.
/// </summary>
public sealed class FigmaParser : IFigmaParser
{
    public HudDocument ParseFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return Parse(json);
    }

    public HudDocument Parse(string json)
    {
        return Parse(json, out _);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    public HudDocument Parse(string json, out IReadOnlyList<string> warnings)
    {
        var warningList = new List<string>();
        warnings = warningList;

        var pseudoTypesByNodeId = new Dictionary<string, string>(StringComparer.Ordinal);
        json = NormalizePseudoNodeTypes(json, warningList, pseudoTypesByNodeId);
        var figmaFile = FigmaDto.FromJson(json)
            ?? throw new InvalidOperationException("Failed to parse Figma JSON");

        if (figmaFile.Document == null)
        {
            throw new InvalidOperationException("Figma file has no document node");
        }

        // Find the first CANVAS (page) with children
        var canvas = FindFirstCanvas(figmaFile.Document);
        if (canvas == null)
        {
            throw new InvalidOperationException("No canvas/page found in Figma file");
        }

        // Find the first suitable node on the canvas as the root component
        // Accept FRAME, COMPONENT, RECTANGLE, GROUP, SECTION, or any node with children
        var rootFrame = canvas.Children?.FirstOrDefault(n => IsSuitableRoot(n));
        
        if (rootFrame == null)
        {
            throw new InvalidOperationException("No suitable root node found on canvas");
        }

        return ConvertToHudDocument(rootFrame, figmaFile, pseudoTypesByNodeId);
    }

    public HudDocument ParseNode(string json, string nodeId)
    {
        return ParseNode(json, nodeId, out _);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    public HudDocument ParseNode(string json, string nodeId, out IReadOnlyList<string> warnings)
    {
        var warningList = new List<string>();
        warnings = warningList;

        var pseudoTypesByNodeId = new Dictionary<string, string>(StringComparer.Ordinal);
        json = NormalizePseudoNodeTypes(json, warningList, pseudoTypesByNodeId);
        var figmaFile = FigmaDto.FromJson(json)
            ?? throw new InvalidOperationException("Failed to parse Figma JSON");

        if (figmaFile.Document == null)
        {
            throw new InvalidOperationException("Figma file has no document node");
        }

        var node = FindNodeById(figmaFile.Document, nodeId)
            ?? throw new InvalidOperationException($"Node with ID '{nodeId}' not found");

        return ConvertToHudDocument(node, figmaFile, pseudoTypesByNodeId);
    }

    public ValidationResult Validate(string json)
    {
        return Validate(json, out _);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    public ValidationResult Validate(string json, out IReadOnlyList<string> warnings)
    {
        var warningList = new List<string>();
        warnings = warningList;

        try
        {
            json = NormalizePseudoNodeTypes(json, warningList, pseudoTypesByNodeId: null);
            var figmaFile = FigmaDto.FromJson(json);

            if (figmaFile == null)
            {
                return ValidationResult.Fail(new ValidationError { Message = "Failed to parse JSON" });
            }

            if (figmaFile.Document == null)
            {
                return ValidationResult.Fail(new ValidationError { Message = "Missing 'document' property" });
            }

            return ValidationResult.Ok();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Fail(new ValidationError
            {
                Message = $"Invalid JSON: {ex.Message}",
                Line = (int?)ex.LineNumber,
                Column = (int?)ex.BytePositionInLine
            });
        }
    }

    private static string NormalizePseudoNodeTypes(string json, List<string>? warnings, Dictionary<string, string>? pseudoTypesByNodeId)
    {
        if (json.IndexOf("\"BUTTON\"", StringComparison.OrdinalIgnoreCase) < 0
            && json.IndexOf("\"SLIDER\"", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return json;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch
        {
            return json;
        }

        if (root is null)
        {
            return json;
        }

        var changed = false;

        void Visit(JsonNode? node)
        {
            if (node is null)
            {
                return;
            }

            if (node is JsonObject obj)
            {
                // Only normalize objects that look like Figma nodes.
                // This avoids touching unrelated nested objects (e.g., paints) that also have a "type" field.
                if (obj.TryGetPropertyValue("id", out var idNode)
                    && idNode is JsonValue idVal
                    && idVal.TryGetValue<string>(out var idStr)
                    && obj.TryGetPropertyValue("name", out var nameNode)
                    && nameNode is JsonValue nameVal
                    && nameVal.TryGetValue<string>(out var nameStr)
                    && obj.TryGetPropertyValue("type", out var typeNode)
                    && typeNode is JsonValue typeVal
                    && typeVal.TryGetValue<string>(out var typeStr))
                {
                    if (string.Equals(typeStr, "BUTTON", StringComparison.OrdinalIgnoreCase))
                    {
                        pseudoTypesByNodeId?.TryAdd(idStr, typeStr);
                        obj["type"] = "TEXT";
                        changed = true;
                        warnings?.Add($"Pseudo node type 'BUTTON' was normalized at Id='{idStr}', Name='{nameStr}'. Consider adding a set.type override in annotations.");
                    }
                    else if (string.Equals(typeStr, "SLIDER", StringComparison.OrdinalIgnoreCase))
                    {
                        pseudoTypesByNodeId?.TryAdd(idStr, typeStr);
                        obj["type"] = "FRAME";
                        changed = true;
                        warnings?.Add($"Pseudo node type 'SLIDER' was normalized at Id='{idStr}', Name='{nameStr}'. Consider adding a set.type override in annotations.");
                    }
                }

                foreach (var kv in obj)
                {
                    Visit(kv.Value);
                }

                return;
            }

            if (node is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    Visit(item);
                }
            }
        }

        Visit(root);

        if (!changed)
        {
            return json;
        }

        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    private static CanvasNode? FindFirstCanvas(DocumentNode node)
    {
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (child.Type == CanvasNodeType.Canvas)
                {
                    return child;
                }
            }
        }
        return null;
    }

    private static bool IsSuitableRoot(SubcanvasNode node)
    {
        return node.Type switch
        {
            SubcanvasNodeType.Frame => true,
            SubcanvasNodeType.Component => true,
            SubcanvasNodeType.ComponentSet => true,
            SubcanvasNodeType.Group => true,
            SubcanvasNodeType.Section => true,
            SubcanvasNodeType.Rectangle => true,
            SubcanvasNodeType.Ellipse => true,
            SubcanvasNodeType.Instance => true,
            _ => false
        };
    }

    private static SubcanvasNode? FindNodeById(DocumentNode doc, string nodeId)
    {
        // DocumentNode has CanvasNodes as children
        if (doc.Children == null) return null;

        foreach (var canvas in doc.Children)
        {
            if (canvas.Id == nodeId) return null; // Can't return a CanvasNode as SubcanvasNode easily unless we change return type, but usually we look for components inside canvases.

            if (canvas.Children != null)
            {
                foreach (var child in canvas.Children)
                {
                    var found = FindNodeRecursive(child, nodeId);
                    if (found != null) return found;
                }
            }
        }

        return null;
    }

    private static SubcanvasNode? FindNodeRecursive(SubcanvasNode node, string nodeId)
    {
        if (node.Id == nodeId) return node;

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                var found = FindNodeRecursive(child, nodeId);
                if (found != null) return found;
            }
        }

        return null;
    }

    private static HudDocument ConvertToHudDocument(SubcanvasNode rootNode, FigmaDto file, IReadOnlyDictionary<string, string> pseudoTypesByNodeId)
    {
        var componentDefinitions = new Dictionary<string, HudComponentDefinition>(StringComparer.Ordinal);
        if (file.Document != null && file.Document.Children != null)
        {
            foreach (var canvas in file.Document.Children)
            {
                if (canvas.Children != null)
                {
                    foreach (var child in canvas.Children)
                    {
                        CollectComponentDefinitions(child, file, componentDefinitions, pseudoTypesByNodeId);
                    }
                }
            }
        }

        var componentNode = ConvertNode(rootNode, file, componentDefinitions, isComponentDefinition: false, pseudoTypesByNodeId);

        return new HudDocument
        {
            Name = SanitizeName(rootNode.Name),
            Metadata = new ComponentMetadata
            {
                Description = $"Imported from Figma: {file.Name}",
                Version = file.Version ?? "1.0.0",
                Tags = ["figma", "imported"]
            },
            Root = componentNode,
            Components = componentDefinitions
        };
    }

    private static void CollectComponentDefinitions(
        SubcanvasNode node,
        FigmaDto file,
        IDictionary<string, HudComponentDefinition> components,
        IReadOnlyDictionary<string, string> pseudoTypesByNodeId)
    {
        if (node.Type == SubcanvasNodeType.Component &&
            node.Children != null && node.Children.Count > 0)
        {
            var id = node.ComponentId ?? node.Id;

            ComponentMetadata? metadata = null;
            if (!string.IsNullOrWhiteSpace(node.ComponentId) && file.Components != null &&
                file.Components.TryGetValue(node.ComponentId, out var componentMeta) &&
                !string.IsNullOrWhiteSpace(componentMeta.Description))
            {
                metadata = new ComponentMetadata
                {
                    Description = componentMeta.Description,
                    Tags = ["figma", "component"]
                };
            }

            if (!components.ContainsKey(id))
            {
                var placeholder = new Dictionary<string, HudComponentDefinition>(StringComparer.Ordinal);
                var root = ConvertNode(node, file, placeholder, isComponentDefinition: true, pseudoTypesByNodeId);

                components[id] = new HudComponentDefinition
                {
                    Id = id,
                    Name = SanitizeName(node.Name),
                    Metadata = metadata,
                    Root = root
                };
            }
        }

        if (node.Children == null)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            CollectComponentDefinitions(child, file, components, pseudoTypesByNodeId);
        }
    }

    private static ComponentNode ConvertNode(
        SubcanvasNode figmaNode,
        FigmaDto file,
        IReadOnlyDictionary<string, HudComponentDefinition> components,
        bool isComponentDefinition,
        IReadOnlyDictionary<string, string> pseudoTypesByNodeId)
    {
        var componentType = MapFigmaTypeToComponentType(figmaNode.Type);
        var layout = ExtractLayout(figmaNode);
        var style = ExtractStyle(figmaNode, file);

        var isInstanceLike = figmaNode.Type == SubcanvasNodeType.Instance ||
                             figmaNode.Type == SubcanvasNodeType.Component;

        string? componentRefId = null;
        Dictionary<string, object?> instanceOverrides = new(StringComparer.Ordinal);

        if (pseudoTypesByNodeId.TryGetValue(figmaNode.Id, out var originalType))
        {
            instanceOverrides[BoomHudMetadataKeys.OriginalFigmaType] = originalType;
            instanceOverrides[BoomHudMetadataKeys.NormalizedFromPseudoType] = true;
        }

        if (!isComponentDefinition && isInstanceLike && !string.IsNullOrWhiteSpace(figmaNode.ComponentId))
        {
            componentRefId = figmaNode.ComponentId;

            if (figmaNode.Overrides != null)
            {
                foreach (var ov in figmaNode.Overrides)
                {
                    // Overrides in new DTO structure are List<Overrides>
                    // We need to map them if possible, but the generated DTO 'Overrides' class might handle it differently.
                    // Let's check Overrides definition.
                    // It seems Overrides is a list of changes. 
                    // However, in the old parser we expected a dictionary.
                    // The standard Figma REST API "overrides" property on INSTANCE nodes describes changes.
                    // But in the new DTO, "overrides" is List<Overrides>. 
                    
                    // Actually, looking at Figma API docs, INSTANCE nodes have 'overrides' which is an array of objects { id, overriddenFields }.
                    // But typically component properties (variants) are in 'componentProperties'.
                    // Let's stick to 'componentProperties' for now which is Dictionary<string, ComponentProperty>.
                }
            }
            
            // Map Component Properties (Variants/Boolean/Text props)
            if (figmaNode.ComponentProperties != null)
            {
                foreach (var kvp in figmaNode.ComponentProperties)
                {
                    // kvp.Value is ComponentProperty { Type, Value }
                    // Value is DefaultValueUnion
                    var val = kvp.Value.Value;

                    if (val.Bool != null)
                    {
                        instanceOverrides[kvp.Key] = val.Bool.Value;
                    }
                    else if (val.String != null)
                    {
                        instanceOverrides[kvp.Key] = val.String;
                    }
                    else
                    {
                        instanceOverrides[kvp.Key] = val.ToString();
                    }
                }
            }
        }

        var node = new ComponentNode
        {
            Id = SanitizeId(figmaNode.Name),
            SlotKey = null,
            Type = componentType,
            Visible = new BindableValue<bool> { Value = figmaNode.Visible ?? true },
            Layout = layout,
            Style = style,
            ComponentRefId = componentRefId,
            InstanceOverrides = instanceOverrides
        };

        var properties = node.Properties.ToDictionary(kv => kv.Key, kv => kv.Value);
        var bindings = node.Bindings.ToList();

        // Extract bindings from name: "My Label [bind: MyProperty]"
        var nameBinding = ExtractBindingFromName(figmaNode.Name);
        if (nameBinding != null)
        {
            // Determine target property based on component type
            var targetProperty = componentType switch
            {
                ComponentType.Label => "text",
                ComponentType.ProgressBar => "value",
                ComponentType.Slider => "value",
                ComponentType.Checkbox => "checked",
                _ => "value" // Default
            };

            bindings.Add(new BindingSpec
            {
                Property = targetProperty,
                Path = nameBinding
            });
        }

        // Handle text content as a static value by default (if not bound)
        if (figmaNode.Type == SubcanvasNodeType.Text && !string.IsNullOrEmpty(figmaNode.Characters))
        {
            // Only set static text if not bound to text
            if (!bindings.Any(b => b.Property == "text"))
            {
                properties["text"] = new BindableValue<object?> { Value = figmaNode.Characters };
            }
        }

        node = node with
        {
            Properties = properties,
            Bindings = bindings
        };

        // Convert explicit children (skip when this node is a component reference).
        List<ComponentNode>? children = null;
        if (componentRefId == null && figmaNode.Children != null && figmaNode.Children.Count > 0)
        {
            children = figmaNode.Children
                .Where(c => c.Visible ?? true) // Skip hidden nodes
                .Select(child => ConvertNode(child, file, components, isComponentDefinition: false, pseudoTypesByNodeId))
                .ToList();
        }

        if (children != null && children.Count > 0)
        {
            node = node with { Children = children };
        }

        return node;
    }

    private static ComponentType MapFigmaTypeToComponentType(SubcanvasNodeType figmaType)
    {
        return figmaType switch
        {
            SubcanvasNodeType.Frame => ComponentType.Container,
            SubcanvasNodeType.Group => ComponentType.Container,
            SubcanvasNodeType.Component => ComponentType.Container,
            SubcanvasNodeType.ComponentSet => ComponentType.Container,
            SubcanvasNodeType.Instance => ComponentType.Container,
            SubcanvasNodeType.Section => ComponentType.Container,
            SubcanvasNodeType.Rectangle => ComponentType.Panel,
            SubcanvasNodeType.Ellipse => ComponentType.Panel,
            SubcanvasNodeType.Vector => ComponentType.Icon,
            SubcanvasNodeType.Star => ComponentType.Icon,
            SubcanvasNodeType.Line => ComponentType.Spacer,
            SubcanvasNodeType.Text => ComponentType.Label,
            SubcanvasNodeType.BooleanOperation => ComponentType.Container,
            SubcanvasNodeType.Slice => ComponentType.Container,
            _ => ComponentType.Container
        };
    }

    private static LayoutSpec? ExtractLayout(SubcanvasNode node)
    {
        var hasLayout = node.AbsoluteBoundingBox != null ||
                        node.LayoutMode != null ||
                        node.PaddingLeft != null ||
                        node.ItemSpacing != null;

        if (!hasLayout) return null;

        // Enum check for LayoutMode
        var layoutType = node.LayoutMode switch
        {
            LayoutMode.Horizontal => LayoutType.Horizontal,
            LayoutMode.Vertical => LayoutType.Vertical,
            _ => LayoutType.Vertical // Default fallback, likely absolute if not set but caught by hasLayout checks?
        };
        
        // If layoutMode is null, it's typically absolute positioning (Frame/Group without AutoLayout)
        if (node.LayoutMode == null)
        {
             // For BoomHud, if it's absolute, we might map to Absolute or Vertical default.
             // Usually Frames without AutoLayout are Absolute.
             // But let's stick to Vertical as default container if unknown, or maybe we should support Absolute.
             // BoomHud LayoutType has 'Absolute'? The IR definition shows Horizontal/Vertical/Stack/Grid/Dock/Absolute (from schema earlier).
             // Let's check IR LayoutType enum. Assuming it exists.
             // Actually standard BoomHud might not support Absolute fully yet, but schema had "absolute".
             // Let's assume Vertical for compatibility if not specified, or "Absolute" if supported.
             layoutType = LayoutType.Vertical; // Fallback
        }

        Dimension? width = null;
        Dimension? height = null;

        if (node.AbsoluteBoundingBox != null)
        {
            width = Dimension.Pixels((float)node.AbsoluteBoundingBox.Width);
            height = Dimension.Pixels((float)node.AbsoluteBoundingBox.Height);
        }

        Spacing? padding = null;
        if (node.PaddingLeft != null || node.PaddingTop != null ||
            node.PaddingRight != null || node.PaddingBottom != null)
        {
            padding = new Spacing(
                (float)(node.PaddingTop ?? 0),
                (float)(node.PaddingRight ?? 0),
                (float)(node.PaddingBottom ?? 0),
                (float)(node.PaddingLeft ?? 0)
            );
        }

        Spacing? gap = null;
        if (node.ItemSpacing != null)
        {
            var gapValue = (float)node.ItemSpacing.Value;
            gap = new Spacing(gapValue);
        }

        var align = node.CounterAxisAlignItems switch
        {
            CounterAxisAlignItems.Min => Abstractions.IR.Alignment.Start,
            CounterAxisAlignItems.Center => Abstractions.IR.Alignment.Center,
            CounterAxisAlignItems.Max => Abstractions.IR.Alignment.End,
            CounterAxisAlignItems.Baseline => Abstractions.IR.Alignment.Start,
            _ => (Abstractions.IR.Alignment?)null
        };

        var justify = node.PrimaryAxisAlignItems switch
        {
            PrimaryAxisAlignItems.Min => Justification.Start,
            PrimaryAxisAlignItems.Center => Justification.Center,
            PrimaryAxisAlignItems.Max => Justification.End,
            PrimaryAxisAlignItems.SpaceBetween => Justification.SpaceBetween,
            _ => (Justification?)null
        };

        return new LayoutSpec
        {
            Type = layoutType,
            Width = width,
            Height = height,
            Padding = padding,
            Gap = gap,
            Align = align,
            Justify = justify
        };
    }

    private static StyleSpec? ExtractStyle(SubcanvasNode node, FigmaDto file)
    {
        // In new DTO, Fills/Strokes are lists of Paint
        var hasFill = node.Fills?.Any(f => f.Visible ?? true) ?? false;
        var hasStroke = node.Strokes?.Any(s => s.Visible ?? true) ?? false;
        var hasOpacity = node.Opacity != null && node.Opacity < 1.0;
        var hasCornerRadius = node.CornerRadius != null;
        var hasTextStyle = node.Style != null;

        if (!hasFill && !hasStroke && !hasOpacity && !hasCornerRadius && !hasTextStyle)
        {
            return null;
        }

        Color? foreground = null;
        Color? background = null;

        // Extract background from fills
        var solidFill = node.Fills?.FirstOrDefault(f => (f.Visible ?? true) && f.Type == PaintType.Solid);
        if (solidFill?.Color != null)
        {
            background = FigmaColorToIrColor(solidFill.Color, solidFill.Opacity);
        }

        // Extract text color
        if (node.Type == SubcanvasNodeType.Text)
        {
             // Text color is also in Fills for text nodes in Figma API
            var textFill = node.Fills?.FirstOrDefault(f => (f.Visible ?? true) && f.Type == PaintType.Solid);
            
            // Fallback to Style.Fills
            if (textFill == null && node.Style?.Fills != null)
            {
                textFill = node.Style.Fills.FirstOrDefault(f => (f.Visible ?? true) && f.Type == PaintType.Solid);
            }

            if (textFill?.Color != null)
            {
                foreground = FigmaColorToIrColor(textFill.Color, textFill.Opacity);
            }
        }

        // Extract border from strokes
        BorderSpec? border = null;
        var solidStroke = node.Strokes?.FirstOrDefault(s => (s.Visible ?? true) && s.Type == PaintType.Solid);
        if (solidStroke?.Color != null && node.StrokeWeight != null)
        {
            border = new BorderSpec
            {
                Width = (int)node.StrokeWeight.Value,
                Color = FigmaColorToIrColor(solidStroke.Color, solidStroke.Opacity),
                Style = BorderStyle.Solid
            };
        }

        // Font properties
        int? fontSize = null;
        FontWeight? fontWeight = null;
        Abstractions.IR.FontStyle? fontStyle = null;

        if (node.Style != null)
        {
            if (node.Style.FontSize != null)
            {
                fontSize = (int)node.Style.FontSize.Value;
            }

            if (node.Style.FontWeight != null)
            {
                fontWeight = node.Style.FontWeight.Value switch
                {
                    < 400 => FontWeight.Light,
                    >= 400 and < 600 => FontWeight.Normal,
                    >= 600 => FontWeight.Bold,
                    _ => FontWeight.Normal
                };
            }

            if (node.Style.Italic == true)
            {
                fontStyle = Abstractions.IR.FontStyle.Italic;
            }
        }

        // Try to capture style-based token keys using node.styles -> file.styles metadata.
        string? foregroundToken = null;
        string? backgroundToken = null;
        string? borderToken = null;
        string? fontSizeToken = null;

        var nodeStyles = node.Styles;
        var fileStyles = file.Styles;
        if (nodeStyles != null && fileStyles != null)
        {
            if (nodeStyles.TryGetValue("fill", out var fillStyleId) &&
                fileStyles.TryGetValue(fillStyleId, out var fillStyle) &&
                !string.IsNullOrWhiteSpace(fillStyle.Name))
            {
                backgroundToken = NormalizeStyleNameToTokenKey(fillStyle.Name);
            }

            if (nodeStyles.TryGetValue("stroke", out var strokeStyleId) &&
                fileStyles.TryGetValue(strokeStyleId, out var strokeStyle) &&
                !string.IsNullOrWhiteSpace(strokeStyle.Name))
            {
                borderToken = NormalizeStyleNameToTokenKey(strokeStyle.Name);
            }

            if (nodeStyles.TryGetValue("text", out var textStyleId) &&
                fileStyles.TryGetValue(textStyleId, out var textStyle) &&
                !string.IsNullOrWhiteSpace(textStyle.Name))
            {
                var textToken = NormalizeStyleNameToTokenKey(textStyle.Name);
                foregroundToken = textToken;
                fontSizeToken = textToken;
            }
        }

        return new StyleSpec
        {
            Foreground = foreground,
            ForegroundToken = foregroundToken,
            Background = background,
            BackgroundToken = backgroundToken,
            FontSize = fontSize,
            FontSizeToken = fontSizeToken,
            FontWeight = fontWeight,
            FontStyle = fontStyle,
            Border = border,
            BorderColorToken = borderToken,
            BorderRadius = node.CornerRadius != null ? (int)node.CornerRadius.Value : null,
            Opacity = hasOpacity ? (float?)node.Opacity : null
        };
    }

    private static string? NormalizeStyleNameToTokenKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var result = name.Trim()
            .Replace(" ", ".", StringComparison.Ordinal)
            .Replace("/", ".", StringComparison.Ordinal)
            .Replace("\\", ".", StringComparison.Ordinal);

        return result;
    }

    private static Color FigmaColorToIrColor(Rgba figmaColor, double? opacity = null)
    {
        var r = (byte)(figmaColor.R * 255);
        var g = (byte)(figmaColor.G * 255);
        var b = (byte)(figmaColor.B * 255);
        var a = (byte)((opacity ?? figmaColor.A) * 255);

        return new Color(r, g, b, a);
    }

    private static string? ExtractBindingFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var start = name.IndexOf("[bind:", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;

        var end = name.IndexOf(']', start);
        if (end < 0) return null;

        var bindingContent = name.Substring(start + 6, end - start - 6).Trim();
        return bindingContent;
    }

    private static string SanitizeName(string name)
    {
        // Remove special characters, keep alphanumeric and spaces
        var sanitized = new string(name
            .Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-')
            .ToArray());

        // Convert to PascalCase
        var words = sanitized.Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join("", words.Select(w =>
            char.ToUpper(w[0], CultureInfo.InvariantCulture) +
            (w.Length > 1 ? w[1..].ToLower(CultureInfo.InvariantCulture) : "")));
    }

    private static string SanitizeId(string name)
    {
        // Convert to camelCase identifier
        var pascalCase = SanitizeName(name);
        if (string.IsNullOrEmpty(pascalCase)) return "element";

        return char.ToLower(pascalCase[0], CultureInfo.InvariantCulture) +
               (pascalCase.Length > 1 ? pascalCase[1..] : "");
    }

}
