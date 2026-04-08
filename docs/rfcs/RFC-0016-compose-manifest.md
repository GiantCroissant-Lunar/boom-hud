# RFC-0016: Compose Manifest & Multi-source Composition

- **Status**: Implemented
- **Created**: 2026-01-26
- **Authors**: BoomHud Contributors
- **Implements**: `boom-hud.compose.json`, `MultiSourceComposer`, `--manifest` CLI option

## Summary

This RFC documents the compose manifest system for BoomHud, enabling declarative configuration of multi-source composition where design files from different sources (Figma JSON, Pencil .pen) are merged into a single IR document for code generation.

## Motivation

### Why a Compose Manifest?

As BoomHud evolved to support multiple input sources (RFC-0014: Pencil.dev integration), several pain points emerged:

1. **CLI Complexity**: Specifying multiple inputs, tokens, targets, and output paths via CLI flags is error-prone and hard to reproduce
2. **Agent Ergonomics**: AI agents need "one file, one command" patterns—not long CLI invocations
3. **Reproducibility**: Build commands should be version-controlled alongside design files
4. **Gradual Migration**: Teams need per-component source ownership during Figma → Pencil transitions

### Goals

1. **Single configuration file** that captures all generation inputs
2. **CLI flag overrides** for situational customization
3. **Deterministic composition** with explicit collision handling
4. **Token authority** via registry reference
5. **IDE discoverability** via JSON schema

### Non-Goals

- Runtime configuration (this is build-time only)
- Bidirectional sync between sources
- Auto-discovery of all design files (explicit source list required)

---

## Manifest Schema

### File Location

By convention, the compose manifest lives at:

```
ui/boom-hud.compose.json
```

Other locations are valid when specified via `--manifest`.

### Full Schema

```json
{
  "$schema": "https://boom-hud.dev/schemas/compose.schema.json",
  "version": "1.0",
  "root": "DebugOverlay",
  "sources": [
    "sources/debug-overlay.pen",
    "sources/minimap.pen",
    "figma/status-bar.figma.json"
  ],
  "tokens": "tokens.ir.json",
  "targets": ["godot", "avalonia"],
  "output": "generated",
  "namespace": "FantaSim.Hud.Generated"
}
```

### Field Definitions

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `$schema` | string | No | - | JSON Schema URL for IDE validation |
| `version` | string | Yes | - | Schema version ("1.0") |
| `root` | string | No | First source | Root component name for composition |
| `sources` | string[] | Yes | - | Ordered list of input file paths |
| `tokens` | string | No | Auto-discover | Path to token registry file |
| `targets` | string[] | No | CLI required | Target backends to generate |
| `output` | string | No | Current dir | Output directory for generated files |
| `namespace` | string | No | "Generated" | Namespace for generated code |

### Path Resolution Rules

All paths in the manifest are **relative to the manifest file location**:

```
ui/
├── boom-hud.compose.json    # Manifest
├── tokens.ir.json           # tokens: "tokens.ir.json"
├── generated/               # output: "generated"
├── sources/
│   ├── debug-overlay.pen    # sources: ["sources/debug-overlay.pen"]
│   └── minimap.pen
└── figma/
    └── status-bar.figma.json
```

Paths are resolved at load time using `Path.GetFullPath(Path.Combine(manifestDir, relativePath))`.

---

## CLI Integration

### Basic Usage

```bash
# Use manifest for all settings
boomhud generate --manifest ui/boom-hud.compose.json

# Override specific options
boomhud generate --manifest ui/boom-hud.compose.json --target godot --output ./custom-out
```

### Precedence Rules

CLI flags **always override** manifest values:

| Setting | Resolution Order |
|---------|------------------|
| `sources` | CLI `--input` files → manifest `sources` |
| `root` | CLI `--root` → manifest `root` → first source |
| `tokens` | CLI `--tokens` → manifest `tokens` → auto-discover |
| `targets` | CLI `--target` → manifest `targets` |
| `output` | CLI `--output` → manifest `output` → current dir |
| `namespace` | CLI `--namespace` (if not default) → manifest `namespace` |

### Backward Compatibility

The positional input argument and `--input` options continue to work without a manifest:

```bash
# Single file (original behavior)
boomhud generate design.figma.json --target godot

# Multiple files via CLI
boomhud generate --input a.pen --input b.pen --root MainHud --target godot
```

When both manifest and CLI inputs are provided, CLI inputs take precedence.

### Generate Command Integration

```csharp
// Simplified flow in HandleGenerate()
if (manifest != null)
{
    loadedManifest = ComposeManifest.LoadFromFile(manifest.FullName);
}

// Merge inputs: CLI args take precedence, then manifest
var inputs = new List<FileInfo>();
if (inputSingle != null) inputs.Add(inputSingle);
inputs.AddRange(inputMultiple);

if (inputs.Count == 0 && loadedManifest != null)
{
    var manifestPaths = loadedManifest.ResolveSourcePaths(manifest.FullName);
    foreach (var path in manifestPaths)
        inputs.Add(new FileInfo(path));
}
```

---

## Composition Semantics

### Multi-Source Composition

When multiple sources are specified, `MultiSourceComposer` merges them at the IR level:

```
Source A (.pen)     Source B (.figma.json)
     │                     │
     ▼                     ▼
  HudDocument A        HudDocument B
     │                     │
     └──────────┬──────────┘
                ▼
        MultiSourceComposer
                │
                ▼
         Composed HudDocument
                │
                ▼
           Generators
```

### Hard Rules (Errors)

These rules are **strictly enforced** and cause generation to fail:

#### BH0100: Component Name Collision

Two components with the same ID (case-insensitive) across different sources:

```
ERROR [BH0100]: Component 'DebugOverlay' defined in both:
  - ui/sources/debug-overlay.pen
  - ui/figma/legacy-debug.figma.json
Each component must have exactly one source.
```

**Resolution**: Remove one definition or rename one component.

#### BH0111: Root Not Found

The specified root component doesn't exist in any source:

```
ERROR [BH0111]: Root component 'MainHud' not found in any input document.
Available: DebugOverlay, Minimap, StatusBar
```

**Resolution**: Check spelling or omit `root` to use first source's document.

### Soft Rules (Warnings)

These rules generate warnings but allow composition to proceed:

#### BH0110: Style Collision (First-Wins)

Two styles with the same name across different sources:

```
WARNING [BH0110]: Style 'DebugText' collision:
  - Winner: ui/sources/debug-overlay.pen
  - Loser: ui/figma/legacy-debug.figma.json
Consider defining styles inside components or in a single theme file.
```

**Behavior**: First source wins (order matters in `sources` array).

**Best Practice**: Define shared styles in a single theme file or scope styles inside components.

---

## Token Registry Integration

### Token Authority

The token registry (`tokens.ir.json`) is the **single source of truth** for design tokens:

```
tokens.ir.json (authority)
       │
       ▼
┌──────────────────┐
│  Token Registry  │
│  - colors.*      │
│  - spacing.*     │
│  - typography.*  │
└──────────────────┘
       │
       ├─────────────────┬────────────────┐
       ▼                 ▼                ▼
   Source A          Source B         Source C
   ($token refs)     ($token refs)    ($token refs)
```

### Resolution Order

Token references are resolved in this order:

1. **Registry lookup**: Check `tokens.ir.json` for the token ID
2. **Inline fallback**: If token uses inline value, warn (BH0104)
3. **Error**: If token is unresolved, fail (BH0102)

### Token Discovery

When `--tokens` is not specified and manifest has no `tokens` field:

1. Look for `ui/tokens.ir.json` relative to the first input file
2. If found, load and use
3. If not found, proceed without token validation

> **CI Recommendation**: In CI pipelines, always specify `--tokens` explicitly (or include
> `tokens` in your compose manifest) to ensure missing tokens fail early. The auto-discovery
> fallback is convenient for local experimentation but can mask broken token references
> in automated builds.

### Token Diagnostic Codes

| Code | Severity | Description |
|------|----------|-------------|
| BH0102 | Error | Unresolved token reference |
| BH0103 | Warning | Deprecated token used |
| BH0104 | Warning | Inline value where token preferred |

---

## Source Identity Tracking

### Purpose

When diagnostics are emitted, they need to reference the original source file and location for actionability.

### SourceIdentity Record

```csharp
public sealed record SourceIdentity(
    string FilePath,
    string? NodeId = null,
    int? Line = null,    // Future: populated by parsers with position tracking
    int? Column = null)  // Future: populated by parsers with position tracking
{
    public override string ToString() => 
        NodeId != null ? $"{FilePath} (node: {NodeId})" : FilePath;
}
```

> **Note**: `Line` and `Column` are reserved for future parser enhancements. Currently,
> diagnostics include `FilePath` and `NodeId` only. Line/column support requires parsers
> to track source positions during parsing.
```

### SourcedDocument Wrapper

```csharp
public sealed record SourcedDocument(HudDocument Document, SourceIdentity Source);
```

### Diagnostic Output

```
[BH0100] error: Component 'DebugOverlay' defined in both sources (at ui/sources/debug-overlay.pen, node: DebugOverlay)
[BH0110] warning: Style 'DebugText' collision: using 'ui/sources/debug-overlay.pen' (winner) (at ui/figma/legacy-debug.figma.json, node: DebugText)
```

---

## Examples

### Minimal Manifest

```json
{
  "version": "1.0",
  "sources": ["design.pen"],
  "targets": ["godot"]
}
```

### Multi-Source with Tokens

```json
{
  "$schema": "https://boom-hud.dev/schemas/compose.schema.json",
  "version": "1.0",
  "root": "DebugOverlay",
  "sources": [
    "sources/debug-overlay.pen",
    "sources/minimap.pen",
    "sources/status-bar.pen"
  ],
  "tokens": "tokens.ir.json",
  "targets": ["godot", "avalonia", "terminalgui"],
  "output": "generated",
  "namespace": "FantaSim.Hud.Generated"
}
```

### Mixed Figma + Pencil

```json
{
  "version": "1.0",
  "root": "MainHud",
  "sources": [
    "pencil/debug-overlay.pen",
    "pencil/minimap.pen",
    "figma/main-hud.figma.json",
    "figma/settings-panel.figma.json"
  ],
  "tokens": "tokens.ir.json",
  "targets": ["godot"]
}
```

### CLI Commands

```bash
# Generate all targets from manifest
boomhud generate --manifest ui/boom-hud.compose.json

# Override target for quick iteration
boomhud generate --manifest ui/boom-hud.compose.json --target godot

# Override output for CI artifacts
boomhud generate --manifest ui/boom-hud.compose.json --output ./artifacts/generated

# Combine with snapshot generation
boomhud snapshot --manifest ui/boom-hud.compose.json

# Full review workflow
boomhud review --manifest ui/boom-hud.compose.json --states ui/states/all.states.json
```

---

## Implementation Details

### ComposeManifest Class

```csharp
public sealed record ComposeManifest
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    [JsonPropertyName("root")]
    public string? Root { get; init; }

    [JsonPropertyName("sources")]
    public IReadOnlyList<string> Sources { get; init; } = [];

    [JsonPropertyName("tokens")]
    public string? Tokens { get; init; }

    [JsonPropertyName("targets")]
    public IReadOnlyList<string>? Targets { get; init; }

    [JsonPropertyName("output")]
    public string? Output { get; init; }

    [JsonPropertyName("namespace")]
    public string? Namespace { get; init; }

    public static ComposeManifest LoadFromFile(string filePath);
    public IReadOnlyList<string> ResolveSourcePaths(string manifestPath);
    public string? ResolveTokensPath(string manifestPath);
    public string? ResolveOutputPath(string manifestPath);
}
```

### MultiSourceComposer Flow

```csharp
public static CompositionResult Compose(
    IReadOnlyList<SourcedDocument> sources, 
    string? rootComponentName = null)
{
    // 1. Track components and sources for collision detection
    // 2. Detect BH0100 (component collision) → hard error
    // 3. Detect BH0110 (style collision) → warning, first wins
    // 4. Determine root document (BH0111 if not found)
    // 5. Return composed HudDocument with merged components/styles
}
```

---

## Migration Guide

### From CLI-only to Manifest

Before:
```bash
boomhud generate \
  --input ui/design-a.pen \
  --input ui/design-b.pen \
  --root MainHud \
  --tokens ui/tokens.ir.json \
  --target godot \
  --output generated \
  --namespace FantaSim.Hud
```

After:
```json
// ui/boom-hud.compose.json
{
  "version": "1.0",
  "root": "MainHud",
  "sources": ["design-a.pen", "design-b.pen"],
  "tokens": "tokens.ir.json",
  "targets": ["godot"],
  "output": "generated",
  "namespace": "FantaSim.Hud"
}
```

```bash
boomhud generate --manifest ui/boom-hud.compose.json
```

### From Single Source to Multi-Source

1. Create `boom-hud.compose.json` with single source
2. Add new sources to `sources` array
3. Run generation and fix any collision errors (BH0100)
4. Review collision warnings (BH0110) and refactor if needed

---

## JSON Schema

A JSON Schema for IDE autocompletion is provided at:

```
schemas/json/compose.schema.json
```

Reference it in your manifest:

```json
{
  "$schema": "../schemas/json/compose.schema.json",
  "version": "1.0",
  ...
}
```

---

## Security Considerations

- Manifest paths are resolved relative to manifest location only (no absolute paths recommended)
- Token registry may contain sensitive brand colors (review before open-sourcing)
- Source files listed in manifest should be version-controlled

---

## Future Considerations

### Possible Extensions

- **Glob patterns**: `sources: ["sources/*.pen"]`
- **Conditional sources**: Include/exclude based on target
- **Per-source overrides**: Custom namespace per source
- **Extends**: Inherit from base manifest

These are explicitly **not** in v1.0 to keep the schema simple.

---

## References

- RFC-0014: Pencil.dev Integration (motivation for multi-source)
- RFC-0015: Snapshot & Visual Regression (uses compose manifest)
- BoomHud.Abstractions/Composition/ComposeManifest.cs
- BoomHud.Abstractions/IR/MultiSourceComposer.cs
