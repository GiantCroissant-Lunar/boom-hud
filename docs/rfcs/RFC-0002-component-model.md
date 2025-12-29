# RFC-0002: Component Model

- **Status**: Draft
- **Created**: 2025-12-10
- **Authors**: BoomHud Contributors

## Summary

This RFC defines the component model for BoomHud - the set of primitive and composite components that can be expressed in the DSL and generated for each backend.

## Component Categories

### 1. Primitive Components

These are leaf components that render directly:

| Component | Description | Terminal.Gui | Avalonia | MAUI |
|-----------|-------------|--------------|----------|------|
| `label` | Text display | Label | TextBlock | Label |
| `button` | Clickable button | Button | Button | Button |
| `textInput` | Single-line input | TextField | TextBox | Entry |
| `textArea` | Multi-line input | TextView | TextBox | Editor |
| `checkbox` | Boolean toggle | CheckBox | CheckBox | CheckBox |
| `radioButton` | Single selection | RadioGroup | RadioButton | RadioButton |
| `progressBar` | Progress indicator | ProgressBar | ProgressBar | ProgressBar |
| `slider` | Range input | Slider | Slider | Slider |
| `icon` | Icon/emoji display | Label | Image/PathIcon | Image |
| `image` | Image display | (limited) | Image | Image |

### 2. Container Components

These contain and arrange children:

| Component | Description | Terminal.Gui | Avalonia | MAUI |
|-----------|-------------|--------------|----------|------|
| `container` | Generic container | View | Border/Panel | Frame |
| `scrollView` | Scrollable area | ScrollView | ScrollViewer | ScrollView |
| `panel` | Titled container | FrameView | HeaderedContentControl | Frame |
| `tabView` | Tabbed container | TabView | TabControl | TabbedPage |
| `splitView` | Resizable split | (emulated) | SplitView | (emulated) |

### 3. List Components

For displaying collections:

| Component | Description | Terminal.Gui | Avalonia | MAUI |
|-----------|-------------|--------------|----------|------|
| `listBox` | Simple list | ListView | ListBox | ListView |
| `listView` | Complex list items | ListView | ItemsControl | CollectionView |
| `treeView` | Hierarchical list | TreeView | TreeView | (emulated) |
| `dataGrid` | Tabular data | TableView | DataGrid | (emulated) |

### 4. Layout Components

Invisible components that control arrangement:

| Component | Description | Terminal.Gui | Avalonia | MAUI |
|-----------|-------------|--------------|----------|------|
| `stack` | Linear stack | (manual) | StackPanel | StackLayout |
| `grid` | Grid layout | (manual) | Grid | Grid |
| `dock` | Dock layout | (manual) | DockPanel | (emulated) |
| `spacer` | Flexible space | (manual) | (empty) | (empty) |

## Component Schema

### Base Component Properties

All components share these properties:

```json
// Common properties for all components
{
  "id": "string",              // Unique identifier (optional)
  "type": "ComponentType",     // Required - component type
  "visible": "boolean | Binding", // Default: true
  "enabled": "boolean | Binding", // Default: true
  "style": "StyleReference | InlineStyle",
  "tooltip": "string | Binding"
}
```

### Component-Specific Properties

#### Label

```json
{
  "type": "label",
  "properties": {
    "text": "string | Binding",     // Required
    "wrap": "boolean",              // Default: false
    "maxLines": "number",           // Optional
    "ellipsis": "boolean"           // Default: true when maxLines set
  }
}
```

#### Button

```json
{
  "type": "button",
  "properties": {
    "text": "string | Binding",
    "icon": "string",               // Icon name or emoji
    "command": "string",            // Command binding path
    "commandParameter": "any"       // Parameter for command
  }
}
```

#### ProgressBar

```json
{
  "type": "progressBar",
  "properties": {
    "value": "number | Binding",    // Required
    "min": "number",                // Default: 0
    "max": "number",                // Default: 100
    "orientation": "horizontal | vertical",  // Default: horizontal
    "showText": "boolean"           // Show percentage text
  }
}
```

#### Container

```json
{
  "type": "container",
  "properties": {
    "background": "Color",
    "border": "BorderSpec",
    "padding": "Spacing",
    "children": "ComponentNode[]"
  },
  "layout": {
    "type": "stack | grid | dock"
    // ... layout-specific properties
  }
}
```

#### ListBox

```json
{
  "type": "listBox",
  "properties": {
    "items": "Binding",             // Collection binding
    "selectedItem": "Binding",      // Two-way binding
    "selectedIndex": "Binding",     // Two-way binding
    "itemTemplate": "ComponentNode"  // Template for each item
  }
}
```

## Capability Annotations

Components can declare required capabilities:

```json
{
  "type": "richTextLabel",
  "capabilities": [
    "richText",
    "inlineImages"
  ],
  "properties": {
    "content": "RichTextBinding"
  }
}
```

The generator checks these against the backend's capability manifest and either:
1. Uses native implementation
2. Emulates if possible
3. Applies fallback policy (warn, error, or degrade)

## Component Composition

### Composite Components

Users can define reusable composite components:

```json
// components/health-bar.hud.json
{
  "defineComponent": "HealthBar",
  "parameters": [
    {
      "name": "value",
      "type": "number",
      "bind": true
    },
    {
      "name": "maxValue",
      "type": "number",
      "default": 100
    },
    {
      "name": "color",
      "type": "color",
      "default": "red"
    }
  ],
  "template": {
    "type": "container",
    "layout": { "type": "horizontal", "gap": "4px" },
    "children": [
      { "type": "icon", "value": "❤️" },
      {
        "type": "progressBar",
        "bind": {
          "value": "$value",
          "max": "$maxValue"
        },
        "style": {
          "foreground": "$color",
          "width": "150px"
        }
      }
    ]
  }
}
```

Usage:

```json
{
  "type": "HealthBar",
  "bind": {
    "value": "Player.Health",
    "maxValue": "Player.MaxHealth"
  },
  "properties": {
    "color": "green"
  }
}
```

## IR Representation

```csharp
public record ComponentNode
{
    public string? Id { get; init; }
    public required ComponentType Type { get; init; }
    
    // Visibility and state
    public BindableValue<bool> Visible { get; init; } = true;
    public BindableValue<bool> Enabled { get; init; } = true;
    
    // Layout
    public LayoutSpec? Layout { get; init; }
    
    // Children (for containers)
    public IReadOnlyList<ComponentNode> Children { get; init; } = [];
    
    // Type-specific properties
    public IReadOnlyDictionary<string, BindableValue> Properties { get; init; } = new();
    
    // Bindings (shorthand populated from bind: in DSL)
    public IReadOnlyList<BindingSpec> Bindings { get; init; } = [];
    
    // Style
    public StyleSpec? Style { get; init; }
    
    // Capabilities required by this component
    public IReadOnlySet<string> RequiredCapabilities { get; init; } = new HashSet<string>();
}

public readonly record struct BindableValue<T>
{
    public T? Value { get; init; }
    public string? BindingPath { get; init; }
    public bool IsBound => BindingPath != null;
    
    public static implicit operator BindableValue<T>(T value) => new() { Value = value };
}
```

## Related RFCs

- RFC-0001: Core Architecture
- RFC-0003: Layout System
- RFC-0004: Data Binding
