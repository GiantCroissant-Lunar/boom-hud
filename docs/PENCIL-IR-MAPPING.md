# Pencil-to-IR Mapping Reference

This document provides a precise mapping from `.pen` schema fields to `BoomHud.Abstractions.IR` types.

The parser now supports two `.pen` shapes:

- Schema-first `.pen` documents that use top-level `nodes`, nested `layout`, and nested `style`.
- Raw Pencil editor exports that use top-level `children`, inline fields like `x`, `y`, `width`, `height`, `fill`, `stroke`, `padding`, `gap`, plus `reusable` component definitions and `ref` instances with `descendants` overrides.

For a concrete raw-export sample, see `samples/pencil/raw-hud-components.pen`.

## Node Type Mapping

| `.pen` node type | IR `ComponentType` | Notes |
|------------------|-------------------|-------|
| `frame` | `Container` | Use layout.mode to determine stack vs absolute |
| `text` | `Label` | |
| `image` | `Image` | |
| `rectangle` | `Panel` | Panel/background block |
| `ellipse` | `Container` | Requires style.borderRadius |
| `group` | `Container` | Layout type = Absolute |
| `icon_font` | `Icon` | Converted as text-like icon node |
| `ref` | Expanded from reusable component | `descendants` overrides are applied during Pencil parsing |
| `component` | Creates `HudComponentDefinition` | Registered in document.Components |

## Layout Mapping

### `.pen` layout → IR `LayoutSpec`

| `.pen` field | IR `LayoutSpec` field | Conversion |
|--------------|----------------------|------------|
| `mode: "vertical"` | `Type = LayoutType.Vertical` | |
| `mode: "horizontal"` | `Type = LayoutType.Horizontal` | |
| `mode: "grid"` | `Type = LayoutType.Grid` | |
| `mode: "none"` | `Type = LayoutType.Absolute` | |
| `gap: 8` | `Gap = Spacing.Uniform(8)` | |
| `padding: 8` | `Padding = Spacing.Uniform(8)` | |
| `padding: { top, right, bottom, left }` | `Padding = new Spacing(t, r, b, l)` | |
| `padding: [vertical, horizontal]` | `Padding = new Spacing(v, h)` | Raw export shorthand |
| `padding: [top, right, bottom, left]` | `Padding = new Spacing(t, r, b, l)` | Raw export shorthand |
| `width: "fill"` | `Width = Dimension.Fill` | |
| `width: "fill_container"` | `Width = Dimension.Fill` | Raw export alias |
| `width: "hug"` | `Width = Dimension.Auto` | |
| `width: 100` | `Width = Dimension.Pixels(100)` | |
| `width: { type: "fixed", value: 100 }` | `Width = Dimension.Pixels(100)` | |
| `height: "fill"` | `Height = Dimension.Fill` | |
| `height: "hug"` | `Height = Dimension.Auto` | |
| `height: 50` | `Height = Dimension.Pixels(50)` | |
| `minWidth: 100` | `MinWidth = Dimension.Pixels(100)` | |
| `maxWidth: 400` | `MaxWidth = Dimension.Pixels(400)` | |
| `alignment: "start"` | `Align = Alignment.Start` | Cross-axis |
| `alignment: "center"` | `Align = Alignment.Center` | |
| `alignment: "end"` | `Align = Alignment.End` | |
| `alignment: "stretch"` | `Align = Alignment.Stretch` | |
| `alignment: "space-between"` | `Justify = Justification.SpaceBetween` | Main-axis |
| `crossAlignment: "start"` | `Align = Alignment.Start` | |
| `position: "absolute"` | `Type = LayoutType.Absolute` | Override mode |
| `x: 10, y: 20` | *(use absolute positioning in generator)* | |

### Dimension Conversion Function

```csharp
public static Dimension ConvertDimension(object? value)
{
    return value switch
    {
        null => Dimension.Auto,
        "fill" => Dimension.Fill,
        "hug" => Dimension.Auto,
        double d => Dimension.Pixels(d),
        int i => Dimension.Pixels(i),
        JsonElement { ValueKind: JsonValueKind.Number } je => Dimension.Pixels(je.GetDouble()),
        JsonElement { ValueKind: JsonValueKind.String } je => je.GetString() switch
        {
            "fill" => Dimension.Fill,
            "hug" => Dimension.Auto,
            var s when s!.EndsWith("px") => Dimension.Pixels(double.Parse(s[..^2])),
            var s when s!.EndsWith("%") => Dimension.Percent(double.Parse(s[..^1])),
            _ => Dimension.Auto
        },
        JsonElement { ValueKind: JsonValueKind.Object } je => ConvertDimensionObject(je),
        _ => Dimension.Auto
    };
}

private static Dimension ConvertDimensionObject(JsonElement je)
{
    var type = je.GetProperty("type").GetString();
    var val = je.TryGetProperty("value", out var v) ? v.GetDouble() : 0;
    return type switch
    {
        "fixed" => Dimension.Pixels(val),
        "fill" => Dimension.Fill,
        "hug" => Dimension.Auto,
        _ => Dimension.Auto
    };
}
```

## Style Mapping

### `.pen` style → IR `StyleSpec`

| `.pen` field | IR `StyleSpec` field | Conversion |
|--------------|---------------------|------------|
| `background: "#ff0000"` | `Background = Color.Parse("#ff0000")` | |
| `background: { $ref: "tokens.colors.x" }` | `BackgroundToken = "colors.x"` | Token ref |
| `fill: "#00ff00"` | `Foreground = Color.Parse("#00ff00")` | Text color |
| `fill: { $ref: "tokens.colors.x" }` | `ForegroundToken = "colors.x"` | Token ref |
| `fontSize: 14` | `FontSize = 14` | |
| `fontFamily: "monospace"` | *(captured in Properties)* | |
| `fontWeight: "bold"` | `FontWeight = FontWeight.Bold` | |
| `fontWeight: 600` | `FontWeight = FontWeight.Bold` | ≥600 = Bold |
| `cornerRadius: 4` | `BorderRadius = 4` | |
| `opacity: 0.5` | `Opacity = 0.5` | |
| `stroke: "#000"` | `Border = new BorderSpec { Color = ... }` | |
| `strokeWidth: 2` | `Border = new BorderSpec { Width = 2 }` | |

### Font Weight Conversion

```csharp
public static FontWeight? ConvertFontWeight(object? value)
{
    return value switch
    {
        "light" or "300" => FontWeight.Light,
        "normal" or "regular" or "400" => FontWeight.Normal,
        "medium" or "500" => FontWeight.Normal,
        "semibold" or "600" => FontWeight.Bold,
        "bold" or "700" => FontWeight.Bold,
        int i when i < 400 => FontWeight.Light,
        int i when i >= 600 => FontWeight.Bold,
        double d when d < 400 => FontWeight.Light,
        double d when d >= 600 => FontWeight.Bold,
        _ => FontWeight.Normal
    };
}
```

## Binding Mapping

### `.pen` bindings → IR `BindingSpec`

| `.pen` format | IR `BindingSpec` |
|---------------|------------------|
| `"content": "DebugInfo.Fps"` | `Property="Text", Path="DebugInfo.Fps", Mode=OneWay` |
| `"content": { "$bind": "X.Y" }` | `Property="Text", Path="X.Y", Mode=OneWay` |
| `"content": { "$bind": "X.Y", "mode": "twoWay" }` | `Property="Text", Path="X.Y", Mode=TwoWay` |
| `"content": { "$bind": "X.Y", "format": "{0:F0}" }` | `Property="Text", Path="X.Y", Format="{0:F0}"` |
| `"content": { "path": "X.Y", "fallback": "n/a" }` | `Property="Text", Path="X.Y", Fallback="n/a"` |
| `"style.fill": { "$bind": "Status", "map": { ... } }` on text nodes | `Property="style.foreground", Path="Status", ConverterParameter=<map object>` |

Binding aliases are normalized during Pencil parsing so downstream generators see canonical IR property names instead of `.pen`-specific field names. Current normalization rules include `content -> Text`, `style.fill -> style.foreground` for text-like components (or `style.background` for non-text components), `style.stroke -> style.borderColor`, and `style.strokeWidth -> style.borderWidth`.

### Binding Mode Conversion

```csharp
public static BindingMode ConvertBindingMode(string? mode)
{
    return mode?.ToLowerInvariant() switch
    {
        "oneway" => BindingMode.OneWay,
        "twoway" => BindingMode.TwoWay,
        "onetime" => BindingMode.OneTime,
        _ => BindingMode.OneWay
    };
}
```

## Token Reference Handling

### In Parser (Unresolved)

```csharp
// When encountering { "$ref": "tokens.colors.debug-bg" }
// Store as TokenRef, do NOT resolve yet
var tokenRef = new TokenRef("colors.debug-bg");

// In StyleSpec, use token key:
new StyleSpec
{
    BackgroundToken = "colors.debug-bg"  // unresolved reference
}
```

### In Composer (Resolution)

```csharp
// TokenResolver resolves against registry
var resolver = new TokenResolver(registry);
foreach (var component in document.Components.Values)
{
    ResolveTokensInNode(component.Root, resolver);
}

void ResolveTokensInNode(ComponentNode node, TokenResolver resolver)
{
    if (node.Style?.BackgroundToken is { } bgToken)
    {
        var source = new SourceLocation(node.SourceIdentity.FilePath, node.Id);
        var resolved = resolver.Resolve(new TokenRef(bgToken), source);
        // Apply resolved value
    }
    foreach (var child in node.Children)
    {
        ResolveTokensInNode(child, resolver);
    }
}
```

## Complete Node Conversion Example

### Input `.pen`

```json
{
  "id": "fps-row",
  "type": "frame",
  "name": "FpsRow",
  "layout": {
    "mode": "horizontal",
    "gap": 8,
    "alignment": "space-between",
    "width": "fill"
  },
  "children": [
    {
      "id": "fps-label",
      "type": "text",
      "name": "FpsLabel",
      "content": "FPS",
      "style": {
        "fill": { "$ref": "tokens.colors.debug-muted" },
        "fontSize": 12,
        "fontFamily": "monospace"
      }
    },
    {
      "id": "fps-value",
      "type": "text",
      "name": "FpsValue",
      "content": "60",
      "style": {
        "fill": { "$ref": "tokens.colors.debug-text" },
        "fontSize": 12,
        "fontWeight": "bold"
      },
      "bindings": {
        "content": {
          "$bind": "Fps",
          "format": "{0:F0}"
        }
      }
    }
  ]
}
```

### Output IR

```csharp
new ComponentNode
{
    Id = "fps-row",
    Type = ComponentType.Container,
    SourceIdentity = new SourceIdentity 
    { 
        FilePath = "ui/pencil/debug-overlay.pen",
        NodeId = "fps-row",
        Line = 45
    },
    Layout = new LayoutSpec
    {
        Type = LayoutType.Horizontal,
        Gap = Spacing.Uniform(8),
        Justify = Justification.SpaceBetween,
        Width = Dimension.Fill
    },
    Children = 
    [
        new ComponentNode
        {
            Id = "fps-label",
            Type = ComponentType.Label,
            Properties = new Dictionary<string, BindableValue<object?>>
            {
                ["text"] = "FPS"
            },
            Style = new StyleSpec
            {
                ForegroundToken = "colors.debug-muted",
                FontSize = 12
            }
        },
        new ComponentNode
        {
            Id = "fps-value",
            Type = ComponentType.Label,
            Properties = new Dictionary<string, BindableValue<object?>>
            {
                ["text"] = "60"
            },
            Style = new StyleSpec
            {
                ForegroundToken = "colors.debug-text",
                FontSize = 12,
                FontWeight = FontWeight.Bold
            },
            Bindings = 
            [
                new BindingSpec
                {
                    Property = "text",
                    Path = "Fps",
                    Mode = BindingMode.OneWay,
                    Format = "{0:F0}"
                }
            ]
        }
    ]
}
```

## Canvas Metadata Handling

Canvas properties (`units`, `scaleMode`, `safeArea`) are **not** part of IR node data.

They should be:
1. Stored in `HudDocument.Metadata` (extend `ComponentMetadata`)
2. Passed to generators via `GenerationOptions`
3. Used by generators to emit appropriate scaling/anchoring code

```csharp
public sealed record CanvasMetadata
{
    public int Width { get; init; }
    public int Height { get; init; }
    public string Units { get; init; } = "px";
    public string ScaleMode { get; init; } = "none";
    public Spacing? SafeArea { get; init; }
}
```
