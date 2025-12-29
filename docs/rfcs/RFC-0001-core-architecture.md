# RFC-0001: Core Architecture

- **Status**: Draft
- **Created**: 2025-12-10
- **Authors**: BoomHud Contributors

## Summary

BoomHud is a build-time code generation system that transforms declarative UI definitions into framework-specific implementations (Terminal.Gui v2, Avalonia, MAUI) without runtime overhead.

## Motivation

### Problem Statement

Different UI frameworks have fundamentally different paradigms:

- **Terminal.Gui v2**: Cell-based TUI, imperative C# construction, coordinate positioning
- **Avalonia**: Pixel-based, XAML markup, MVVM data binding
- **MAUI**: Similar to Avalonia but mobile-focused, different control set

Building cross-platform applications (especially games with console and desktop modes) currently requires maintaining parallel UI implementations, leading to:

1. Code duplication
2. Inconsistent behavior
3. High maintenance burden
4. Design-to-code friction

### Goals

1. **Single Source of Truth**: Define UI once in a declarative DSL
2. **Zero Runtime Overhead**: Build-time generation, not runtime abstraction
3. **Framework-Native Output**: Generated code looks hand-written, uses native patterns
4. **Capability-Aware**: Graceful handling of framework limitations
5. **Designer-Friendly**: Path to Figma/design tool import

### Non-Goals

- Runtime UI framework abstraction
- Visual UI editor (use Figma or similar)
- 100% feature parity across frameworks
- Animation system (framework-specific for now)

## Design Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Design Source                            │
│         (BoomHud DSL JSON / Figma Export)                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      DSL Parser                             │
│            (JSON → Unvalidated AST)                         │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Schema Validator                         │
│         (JSON Schema validation + semantic checks)          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              Intermediate Representation (IR)               │
│   ┌─────────────┬─────────────┬─────────────┬───────────┐   │
│   │ Components  │   Layout    │  Bindings   │  Styles   │   │
│   └─────────────┴─────────────┴─────────────┴───────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ Terminal.Gui v2 │ │     Avalonia    │ │      MAUI       │
│   Generator     │ │    Generator    │ │   Generator     │
│                 │ │                 │ │                 │
│ • C# classes    │ │ • AXAML markup  │ │ • XAML markup   │
│ • Imperative    │ │ • ViewModels    │ │ • ViewModels    │
│ • Cell coords   │ │ • Pixel layout  │ │ • Pixel layout  │
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

## Core Concepts

### 1. BoomHud DSL

A declarative JSON format for defining UI components:

```json
// status-bar.hud.json
{
  "component": "StatusBar",
  "metadata": {
    "description": "Game status bar showing health, mana, and location"
  },
  "layout": {
    "type": "horizontal",
    "height": "fixed(40px)",
    "width": "fill"
  },
  "children": [
    {
      "id": "healthSection",
      "type": "container",
      "layout": { "type": "horizontal", "gap": "8px" },
      "children": [
        { "type": "icon", "value": "" },
        {
          "type": "progressBar",
          "id": "healthBar",
          "bind": { "value": "Player.HealthPercent", "max": 100 },
          "style": { "foreground": "red", "width": "150px" }
        },
        {
          "type": "label",
          "bind": { "text": "Player.HealthText" }
        }
      ]
    },
    {
      "id": "manaSection",
      "type": "container",
      "layout": { "type": "horizontal", "gap": "8px" },
      "children": [
        { "type": "icon", "value": "" },
        {
          "type": "progressBar",
          "id": "manaBar",
          "bind": { "value": "Player.ManaPercent", "max": 100 },
          "style": { "foreground": "blue", "width": "150px" }
        }
      ]
    },
    {
      "type": "spacer",
      "layout": { "weight": 1 }
    },
    {
      "id": "locationLabel",
      "type": "label",
      "bind": { "text": "Player.LocationName" },
      "style": { "align": "right" }
    }
  ]
}
```

### 2. Intermediate Representation (IR)

The IR is the core data model that all transformations operate on:

```csharp
public record HudDocument
{
    public required string Name { get; init; }
    public required ComponentNode Root { get; init; }
    public IReadOnlyDictionary<string, StyleDefinition> Styles { get; init; }
}

public record ComponentNode
{
    public required string Id { get; init; }
    public required ComponentType Type { get; init; }
    public LayoutSpec? Layout { get; init; }
    public IReadOnlyList<BindingSpec> Bindings { get; init; }
    public IReadOnlyList<ComponentNode> Children { get; init; }
    public IReadOnlyDictionary<string, object> Properties { get; init; }
}

public record LayoutSpec
{
    public LayoutType Type { get; init; }
    public DimensionValue? Width { get; init; }
    public DimensionValue? Height { get; init; }
    public Spacing? Gap { get; init; }
    public Alignment? Align { get; init; }
}

public record BindingSpec
{
    public required string Property { get; init; }
    public required string Path { get; init; }
    public BindingMode Mode { get; init; } = BindingMode.OneWay;
    public string? Converter { get; init; }
}
```

### 3. Backend Generators

Each backend implements `IBackendGenerator`:

```csharp
public interface IBackendGenerator
{
    string TargetFramework { get; }
    CapabilityManifest Capabilities { get; }
    
    GenerationResult Generate(HudDocument document, GenerationOptions options);
}

public record GenerationResult
{
    public IReadOnlyList<GeneratedFile> Files { get; init; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; }
}

public record GeneratedFile
{
    public required string Path { get; init; }
    public required string Content { get; init; }
    public GeneratedFileType Type { get; init; }
}
```

### 4. Capability System

Frameworks have different capabilities. We declare these explicitly:

```csharp
public record CapabilityManifest
{
    public IReadOnlySet<ComponentCapability> Components { get; init; }
    public IReadOnlySet<LayoutCapability> Layouts { get; init; }
    public IReadOnlySet<BindingCapability> Bindings { get; init; }
    public IReadOnlyDictionary<string, CapabilityLevel> Features { get; init; }
}

public enum CapabilityLevel
{
    Native,      // Framework supports natively
    Emulated,    // Can be emulated with some overhead
    Limited,     // Partial support with caveats
    Unsupported  // Not available
}
```

Example capability differences:

| Capability | Terminal.Gui | Avalonia | MAUI |
|------------|--------------|----------|------|
| ProgressBar | Native | Native | Native |
| RichText | Limited | Native | Native |
| Animation | Unsupported | Native | Native |
| SVG Icons | Unsupported | Native | Native |
| Cell-based Layout | Native | Emulated | Emulated |

## Project Structure

```
boom-hud/
├── schemas/
│   └── boom-hud.schema.json       # JSON Schema for DSL
├── dotnet/
│   ├── src/
│   │   ├── BoomHud.Abstractions/  # Core types, IR
│   │   │   ├── IR/
│   │   │   │   ├── HudDocument.cs
│   │   │   │   ├── ComponentNode.cs
│   │   │   │   ├── LayoutSpec.cs
│   │   │   │   └── BindingSpec.cs
│   │   │   ├── Capabilities/
│   │   │   │   ├── ICapabilityManifest.cs
│   │   │   │   └── CapabilityLevel.cs
│   │   │   └── Generation/
│   │   │       ├── IBackendGenerator.cs
│   │   │       └── GenerationResult.cs
│   │   │
│   │   ├── BoomHud.Dsl/           # DSL parsing
│   │   │   ├── YamlParser.cs
│   │   │   ├── JsonParser.cs
│   │   │   └── SchemaValidator.cs
│   │   │
│   │   ├── BoomHud.Gen.TerminalGui/
│   │   │   ├── TerminalGuiCapabilities.cs
│   │   │   ├── TerminalGuiGenerator.cs
│   │   │   └── Emitters/
│   │   │       ├── ComponentEmitter.cs
│   │   │       └── LayoutEmitter.cs
│   │   │
│   │   ├── BoomHud.Gen.Avalonia/
│   │   │   ├── AvaloniaCapabilities.cs
│   │   │   ├── AvaloniaGenerator.cs
│   │   │   └── Emitters/
│   │   │       ├── AxamlEmitter.cs
│   │   │       └── ViewModelEmitter.cs
│   │   │
│   │   └── BoomHud.Cli/           # Command-line tool
│   │       └── Program.cs
│   │
│   └── tests/
│       ├── BoomHud.Tests.Dsl/
│       ├── BoomHud.Tests.TerminalGui/
│       └── BoomHud.Tests.Avalonia/
│
└── samples/
    ├── status-bar.hud.yaml
    ├── inventory-panel.hud.yaml
    └── character-sheet.hud.yaml
```

## Success Metrics

1. **Reduction in Code**: 50%+ reduction in HUD code maintenance
2. **Correctness**: Generated code compiles and runs without modification
3. **Performance**: Generated code within 5% of hand-written performance
4. **Coverage**: Support 80%+ of common game HUD patterns

## Related RFCs

- RFC-0002: Component Model
- RFC-0003: Layout System
- RFC-0004: Data Binding
- RFC-0005: Backend Adapters
- RFC-0006: Capability Policies
- RFC-0007: Theming & Styles

## Open Questions

1. **Source Generator vs CLI Tool**: Should generation be a Roslyn source generator or a standalone CLI?
   - Leaning CLI for flexibility and debugging ease
   
2. **Figma Import Fidelity**: How much Figma structure to preserve vs. normalize?

3. **Hot Reload**: Can we support design-time preview/hot reload?
