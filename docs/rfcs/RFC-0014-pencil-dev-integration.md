# RFC-0014: Pencil.dev Integration for Agentic UI Development

- **Status**: Draft
- **Created**: 2026-01-26
- **Authors**: BoomHud Contributors

## Summary

This RFC proposes integrating **pencil.dev** as a first-class design input source for BoomHud, enabling an agent-driven UI development workflow where design files live in the repository, are editable by AI agents via MCP, and compile to the same IR as Figma JSON—producing Godot `.tscn`, Avalonia AXAML, and Terminal.Gui v2 C# code.

## Motivation

### Current State

BoomHud currently uses **Figma JSON** as the primary design input:

```
Figma JSON → BoomHud IR → Backend Generators → (tscn/xaml/cs)
```

While this works, it has limitations for agentic workflows:

1. **External Tool Dependency**: Figma is external to the codebase; changes require manual export
2. **No MCP Integration**: AI agents cannot directly edit Figma designs
3. **Version Control Friction**: Figma files don't naturally live in git

### Why Pencil.dev?

Pencil.dev is an **agent-driven MCP canvas** that addresses these limitations:

- **Design as Code**: `.pen` files are JSON-based and live in your repository
- **MCP Bi-directional**: Agents can read AND write design files via MCP tools
- **Git-Native**: Branch, merge, PR review design changes like code
- **Figma Import**: Copy-paste from Figma preserves vectors, text, and styles
- **IDE Integration**: Works in Cursor, VS Code, Claude Code

### Goals

1. **Add `.pen` as an input format** alongside Figma JSON (not replacing it)
2. **UI-IR remains the center** — both inputs compile to the same IR
3. **Enable agentic iteration** on design files via MCP
4. **Component-level ownership** — gradual migration, not big-bang switch
5. **Visual verification** — snapshots and Remotion video previews for PRs

### Non-Goals

- Bidirectional sync between Figma JSON and `.pen` (too complex, too leaky)
- Replacing Figma entirely (some teams need Figma for brand/design system)
- Runtime design file loading (we're build-time only)

## Design Overview

### Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                      Design Sources                                 │
│  ┌─────────────────┐              ┌─────────────────┐               │
│  │   Figma JSON    │              │   Pencil .pen   │               │
│  │  (existing)     │              │     (new)       │               │
│  └────────┬────────┘              └────────┬────────┘               │
│           │                                │                        │
│           ▼                                ▼                        │
│  ┌─────────────────┐              ┌─────────────────┐               │
│  │  FigmaParser    │              │   PenParser     │               │
│  └────────┬────────┘              └────────┬────────┘               │
└───────────┼────────────────────────────────┼────────────────────────┘
            │                                │
            └───────────────┬────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    BoomHud IR (Unchanged)                           │
│   ┌────────────┬───────────┬───────────┬───────────┬──────────┐     │
│   │ Components │  Layout   │ Bindings  │  Styles   │  Tokens  │     │
│   └────────────┴───────────┴───────────┴───────────┴──────────┘     │
└─────────────────────────────────────────────────────────────────────┘
                            │
            ┌───────────────┼───────────────┬───────────────┐
            ▼               ▼               ▼               ▼
┌───────────────┐ ┌───────────────┐ ┌───────────────┐ ┌───────────────┐
│ Godot .tscn   │ │ Avalonia AXAML│ │ Terminal.Gui  │ │ Unity UXML    │
│ + C# scripts  │ │ + ViewModels  │ │ v2 C# code    │ │ (future)      │
└───────────────┘ └───────────────┘ └───────────────┘ └───────────────┘
```

### Key Principle: Component-Level Ownership

During transition (and potentially forever), different components can have different sources:

| Component | Source | Owner |
|-----------|--------|-------|
| `MainHud` | Figma JSON | Design team |
| `DebugOverlay` | `.pen` | Agent-iterated |
| `Minimap` | `.pen` | Agent-iterated |
| `StatusBar` | Figma JSON | Design team |

Rules:
- **One source per component** — never both Figma AND .pen for the same component
- **IR is the composition point** — multiple sources merge into one IR document
- **Component instances** can reference definitions from either source

### Hard Rules (Non-Negotiable)

These rules are enforced at compile time with hard errors:

1. **Component Name Collision → Fail**
   ```
   ERROR BH0100: Component 'DebugOverlay' defined in both:
     - ui/figma/debug.json
     - ui/pencil/debug-overlay.pen
   Each component must have exactly one source.
   ```

2. **Token Collision → Fail**
   ```
   ERROR BH0101: Token 'colors.debug-bg' defined with different values:
     - ui/figma/tokens.json: "rgba(0,0,0,0.8)"
     - ui/pencil/debug-overlay.pen: "rgba(0,0,0,0.85)"
   Use a shared token registry or rename one token.
   ```

3. **Unresolved Token Reference → Fail**
   ```
   ERROR BH0102: Token reference '$colors.undefined-token' not found.
   ```

## Detailed Design

### 0. Token Strategy: IR Tokens as Authority

To prevent token drift between Figma and Pencil sources, **IR Tokens are the single authority**:

```
ui/
├── tokens/
│   └── tokens.ir.json        # Authoritative token registry
├── figma/
│   └── main-hud.json         # References tokens by ID
└── pencil/
    └── debug-overlay.pen     # References tokens by ID
```

**Token Registry** (`tokens.ir.json`):
```json
{
  "$schema": "https://boom-hud.dev/schemas/tokens.schema.json",
  "version": "1.0",
  "colors": {
    "debug-bg": { "value": "rgba(0, 0, 0, 0.85)", "description": "Debug overlay background" },
    "debug-text": { "value": "#00ff00", "description": "Debug text color" },
    "debug-warning": { "value": "#ffaa00", "description": "Warning state" },
    "debug-error": { "value": "#ff4444", "description": "Error state" }
  },
  "spacing": {
    "xs": { "value": 2 },
    "sm": { "value": 4 },
    "md": { "value": 8 },
    "lg": { "value": 16 }
  },
  "typography": {
    "mono-sm": { "fontFamily": "monospace", "fontSize": 12 },
    "mono-md": { "fontFamily": "monospace", "fontSize": 14 }
  }
}
```

**Importer Behavior**:
- Figma parser maps Figma variables → IR token IDs
- Pen parser maps `$ref` → IR token IDs
- Composer validates all token references resolve
- Inline tokens in `.pen` files are allowed but flagged with warnings (prefer registry)

### 1. Pencil File Format (`.pen`)

Based on pencil.dev's "fully open file format", `.pen` files are JSON that describe:

```json
{
  "$schema": "https://boom-hud.dev/schemas/pencil.schema.json",
  "version": "1.0",
  "name": "DebugOverlay",
  "canvas": {
    "width": 1920,
    "height": 1080,
    "units": "px",
    "scaleMode": "none",
    "safeArea": { "top": 40, "right": 20, "bottom": 40, "left": 20 }
  },
  "tokens": {
    "colors": {
      "debug-bg": "rgba(0, 0, 0, 0.8)",
      "debug-text": "#00ff00"
    },
    "spacing": {
      "padding-sm": 4,
      "padding-md": 8
    }
  },
  "nodes": [
    {
      "id": "root",
      "type": "frame",
      "name": "DebugOverlay",
      "layout": {
        "mode": "vertical",
        "padding": { "$ref": "tokens.spacing.padding-md" }
      },
      "style": {
        "background": { "$ref": "tokens.colors.debug-bg" }
      },
      "children": [
        {
          "id": "fps-label",
          "type": "text",
          "name": "FpsLabel",
          "content": "FPS: 60",
          "style": {
            "fill": { "$ref": "tokens.colors.debug-text" },
            "fontSize": 14,
            "fontFamily": "monospace"
          },
          "bindings": {
            "content": "DebugInfo.Fps"
          }
        }
      ]
    }
  ]
}
```

### 1.1 Binding Syntax (Canonical)

All parsers emit bindings using the **same dot-path syntax** to IR:

```json
// In .pen file (shorthand)
"bindings": {
  "content": "DebugInfo.Fps"
}

// In .pen file (full form with options)
"bindings": {
  "content": {
    "$bind": "DebugInfo.Fps",
    "mode": "oneWay",
    "format": "{0:F0} fps"
  }
}

// Emitted to IR (BindingSpec)
{
  "property": "content",
  "path": "DebugInfo.Fps",
  "mode": "OneWay",
  "format": "{0:F0} fps"
}
```

**Rules**:
- Path uses dot notation: `ViewModel.Property.SubProperty`
- Mode: `oneWay` (default), `twoWay`, `oneTime`
- Format: .NET string format pattern
- Both Figma and Pen parsers produce identical `BindingSpec` records

### 1.2 Token Resolution (Single Point of Failure)

All token resolution happens in **one shared component** to avoid double-reporting errors:

```csharp
namespace BoomHud.Dsl;

/// <summary>
/// Resolves token references against the registry. BH0102 is thrown ONLY here.
/// </summary>
public sealed class TokenResolver
{
    private readonly TokenRegistry _registry;
    
    public TokenResolver(TokenRegistry registry) => _registry = registry;
    
    /// <summary>
    /// Resolves a token reference (e.g., "colors.debug-bg") to its value.
    /// </summary>
    /// <exception cref="TokenResolutionException">BH0102 if token not found</exception>
    public TokenValue Resolve(TokenRef tokenRef, SourceLocation source)
    {
        if (!_registry.TryGet(tokenRef.Path, out var value))
        {
            throw new TokenResolutionException(
                $"BH0102: Token reference '${tokenRef.Path}' not found.",
                source);
        }
        return value;
    }
}

/// <summary>
/// A reference to a token (not yet resolved).
/// </summary>
public readonly record struct TokenRef(string Path);

/// <summary>
/// Location in a source file for error reporting.
/// </summary>
public readonly record struct SourceLocation(
    string FilePath,
    string? NodeId = null,
    int? Line = null,
    int? Column = null);
```

**Flow**:
1. Parsers emit `TokenRef("colors.debug-bg")` in IR (unresolved)
2. Composer calls `TokenResolver.Resolve()` for each ref
3. BH0102 thrown once per unresolved ref, with precise source location

### 1.3 Source Identity in IR (For Good Diagnostics)

To produce clear error messages (BH0100/BH0101/BH0102), IR nodes carry source provenance:

```csharp
/// <summary>
/// Added to ComponentNode and HudComponentDefinition for error diagnostics.
/// </summary>
public sealed record SourceIdentity
{
    /// <summary>
    /// Path to the source file (relative to project root).
    /// </summary>
    public required string FilePath { get; init; }
    
    /// <summary>
    /// Node ID within the source file (if applicable).
    /// </summary>
    public string? NodeId { get; init; }
    
    /// <summary>
    /// Line number in source file (1-based, for JSON).
    /// </summary>
    public int? Line { get; init; }
    
    /// <summary>
    /// Column number in source file (1-based).
    /// </summary>
    public int? Column { get; init; }
}
```

This enables error messages like:
```
ERROR BH0100: Component 'DebugOverlay' defined in both:
  - ui/figma/debug.json (node: 1:234, line 45)
  - ui/pencil/debug-overlay.pen (node: root, line 12)
Each component must have exactly one source.
```

### 2. PenParser Implementation

New parser in `BoomHud.Dsl`:

```csharp
namespace BoomHud.Dsl.Pencil;

public sealed class PenParser : IHudParser
{
    public HudDocument Parse(string json, PenParserOptions? options = null);
    public HudDocument Parse(Stream stream, PenParserOptions? options = null);
}

public sealed record PenParserOptions
{
    /// <summary>
    /// Optional annotations file (same format as Figma annotations).
    /// </summary>
    public string? AnnotationsPath { get; init; }
    
    /// <summary>
    /// Root node ID to use (if not the document root).
    /// </summary>
    public string? RootNodeId { get; init; }
}
```

### 3. CLI Integration

Extend the existing CLI to detect input format automatically:

```bash
# Explicit format
boomhud generate --format pen design.pen --target godot --out gen/

# Auto-detect by extension
boomhud generate design.pen --target godot --out gen/
boomhud generate design.figma.json --target godot --out gen/

# Compose multiple sources into one output
boomhud generate \
  --input ui/figma/main-hud.json \
  --input ui/pencil/debug-overlay.pen \
  --target godot \
  --out gen/
```

### 4. Multi-Source Composition

When multiple inputs are provided, the CLI:

1. Parses each input to IR
2. Merges component definitions into a unified registry
3. Resolves component references across sources
4. Generates unified output

```csharp
public sealed class MultiSourceComposer
{
    public HudDocument Compose(IEnumerable<HudDocument> sources)
    {
        // Merge component definitions
        // Detect conflicts (same component name, different sources)
        // Build unified document
    }
}
```

### 5. Agentic Workflow Support

#### MCP Tools for Agents

Pencil.dev provides MCP tools that agents can use. BoomHud should document workflows:

```markdown
## Agent Workflow: Update Debug Overlay

1. Agent reads `ui/pencil/debug-overlay.pen` via MCP
2. Agent modifies node properties (spacing, colors, text)
3. Agent writes updated `.pen` file
4. CI triggers: `boomhud generate` → `boomhud verify`
5. PR includes:
   - Design diff (JSON)
   - Generated code diff
   - Snapshot images
```

#### Agent Conventions File

`.pen.agent.json` is **metadata only** — it does NOT affect IR semantics:

| Purpose | Affects IR? | Used By |
|---------|-------------|----------|
| Edit permissions | ❌ No | Agent tooling |
| Validation rules | ❌ No | CLI `--validate` |
| Test states | ❌ No | `boomhud snapshot` |
| Constraints | ❌ No | Lint warnings |

```json
{
  "$schema": "https://boom-hud.dev/schemas/pen.agent.schema.json",
  "constraints": {
    "safe-area": { "top": 40, "bottom": 40 },
    "min-text-size": 12,
    "allowed-colors": ["$debug-bg", "$debug-text", "$warning", "$error"]
  },
  "agent-editable": ["fps-label", "memory-label", "position-label"],
  "agent-readonly": ["root", "header-frame"],
  "testStates": [
    { "name": "default", "viewModel": { "Fps": 60 } },
    { "name": "warning", "viewModel": { "Fps": 15 } }
  ]
}
```

### 6. Visual Verification Pipeline

```
Design Source → IR → Generate → Build → Snapshot → Compare
                                          │
                                          ▼
                                    Remotion Video
                                    (PR Preview)
```

#### Snapshot Generation

```bash
# Generate Godot project, run headless, capture screenshots
boomhud snapshot \
  --manifest ui/boom-hud.compose.json \
  --states states.json \
  --out snapshots/

# states.json defines different UI states to capture
{
  "states": [
    { "name": "default", "viewModel": { "Fps": 60, "Memory": "512 MB" } },
    { "name": "warning", "viewModel": { "Fps": 15, "Memory": "1.8 GB" } }
  ]
}
```

#### Remotion Video Preview

```bash
# Stitch snapshots into a video for PR review
boomhud video \
  --in snapshots/ \
  --out preview.mp4 \
  --format side-by-side  # or: single, timeline
```

## Migration Path

### Phase 0A: Parser + CLI Plumbing (Smallest Vertical Slice)

**Goal**: Single `.pen` file compiles end-to-end, no composer yet.

#### File Deliverables

| File | Purpose | Lines (est.) | Status |
|------|---------|--------------|--------|
| `dotnet/src/BoomHud.Dsl.Pencil/PenDto.cs` | POCO deserialization types matching pencil.schema.json | ~300 | ✅ Done |
| `dotnet/src/BoomHud.Dsl.Pencil/PenParser.cs` | JSON → PenDto → HudDocument | ~130 | ✅ Done |
| `dotnet/src/BoomHud.Dsl.Pencil/PenToIrConverter.cs` | PenDto → IR (uses [PENCIL-IR-MAPPING.md](../PENCIL-IR-MAPPING.md)) | ~550 | ✅ Done |
| `dotnet/src/BoomHud.Dsl.Pencil/BoomHud.Dsl.Pencil.csproj` | Project file | ~15 | ✅ Done |
| `dotnet/tests/BoomHud.Tests.Unit/Dsl/PenParserTests.cs` | Unit tests for parser | ~270 | ✅ Done |

#### Checklist

- [x] Define `.pen` schema for BoomHud (`schemas/json/pencil.schema.json`) ✅
- [x] Implement `PenDto.cs` (deserialization types) ✅
- [x] Implement `PenParser.cs` returning `HudDocument` (IR) ✅
- [x] Implement `PenToIrConverter.cs` (subset: frame/text/image + basic layout + token refs) ✅
- [x] Add unit tests for parser ✅ (11 tests passing)
- [x] Add `--format pen` option + extension auto-detect to CLI ✅
- [x] Pilot: `samples/pencil/debug-overlay.pen` → Godot `.tscn` + Terminal.Gui v2 ✅

**Deliverable**: Agents can iterate on a `.pen` file and see generated UI. ✅ **COMPLETE**

### Phase 0B: Token Registry

**Goal**: Validate token references against a shared registry with actionable error codes.

#### File Deliverables

| File | Purpose | Lines (est.) | Status |
|------|---------|--------------|--------|
| `dotnet/src/BoomHud.Abstractions/Tokens/TokenRegistry.cs` | Token registry loader and resolver | ~360 | ✅ Done |
| `dotnet/src/BoomHud.Abstractions/Diagnostics/BoomHudDiagnostic.cs` | Standardized error codes (BH0101, BH0102, etc.) | ~150 | ✅ Done |
| `dotnet/tests/BoomHud.Tests.Unit/Tokens/TokenRegistryTests.cs` | Unit tests for registry and diagnostics | ~180 | ✅ Done |

#### Checklist

- [x] Define `tokens.ir.json` schema (`schemas/json/tokens.schema.json`) ✅
- [x] Implement `TokenRegistry.cs` - load and resolve tokens ✅
- [x] Implement `BoomHudDiagnostic.cs` - error codes BH0102, BH0104 ✅
- [x] Add `--tokens` CLI option with auto-discovery ✅
- [x] Validate token refs in pipeline, hard fail on BH0102 ✅
- [x] Golden test: BH0102 output format (code, token id, source file, node id) ✅

**Deliverable**: Unresolved token references fail with actionable error messages. ✅ **COMPLETE**

### Phase 1: Multi-Source Composition

**Goal**: Mix Figma + Pencil in one project.

- [ ] Implement `MultiSourceComposer`
  - Merge component registries
  - Resolve cross-component references
  - **Hard fail** on name collisions (BH0100)
  - **Hard fail** on token collisions (BH0101)
- [ ] Add `--input` option for multiple sources
- [ ] Test: MainHud from Figma JSON + DebugOverlay from `.pen`

### Phase 2: Visual Verification (Snapshots)

**Goal**: PNG snapshots for PR review.

- [ ] Implement `boomhud snapshot` command
- [ ] Godot headless render pipeline
- [ ] State-driven snapshots from `testStates` in `.pen.agent.json`
- [ ] PNG diff for regression detection

### Phase 3: Visual Verification (Video)

**Goal**: MP4 previews built from snapshots.

- [ ] Implement `boomhud video` command
- [ ] Remotion integration (stitches PNGs → MP4)
- [ ] Side-by-side diff format for PRs
- [ ] CI workflow for automatic preview generation

### Phase 4: Agent Tooling Polish

- [ ] Document MCP tool usage patterns
- [ ] Validate `.pen.agent.json` constraints at lint time
- [ ] Agent-friendly error messages with fix suggestions

## Project Structure Changes

```
boom-hud/
├── schemas/
│   ├── boom-hud.schema.json          # Existing IR schema
│   ├── figma-export.schema.json      # Existing Figma schema
│   ├── pencil.schema.json            # NEW: Pencil input schema
│   ├── tokens.schema.json            # NEW: Token registry schema
│   └── pen.agent.schema.json         # NEW: Agent metadata schema
├── dotnet/src/
│   └── BoomHud.Dsl/
│       ├── Figma/                    # Existing Figma parser
│       │   ├── FigmaParser.cs
│       │   └── FigmaDto.cs
│       ├── Pencil/                   # NEW: Pencil parser
│       │   ├── PenParser.cs
│       │   ├── PenDto.cs
│       │   └── PenToIrConverter.cs
│       └── Tokens/                   # NEW: Token registry
│           ├── TokenRegistry.cs
│           └── TokenRegistryLoader.cs
├── ui/                               # NEW: Recommended project layout
│   ├── tokens/
│   │   └── tokens.ir.json            # Authoritative token registry
│   ├── figma/
│   │   └── main-hud.json
│   └── pencil/
│       ├── debug-overlay.pen
│       └── debug-overlay.pen.agent.json
└── samples/
    ├── figma/                        # Existing Figma samples
    │   └── status-bar.figma.json
    └── pencil/                       # NEW: Pencil samples
        ├── debug-overlay.pen
        └── debug-overlay.pen.agent.json
```

## Open Questions

1. **Pencil File Format Stability**: Is the `.pen` format documented and stable? Need to monitor for breaking changes.

2. ~~**Design Token Synchronization**: How do we keep tokens consistent between Figma and Pencil when both are in use?~~
   **RESOLVED**: IR Token Registry is authoritative. Both parsers map to IR tokens. Collisions fail hard.

3. **Component Library Sharing**: Can component definitions be exported from Figma to Pencil for consistency?

4. **Remotion Dependency**: Is Remotion the right choice, or should we use a lighter-weight video generation tool?
   - Remotion is kept as a *derived artifact tool* (Phase 3), not blocking core functionality.
   - Snapshots (Phase 2) work without Remotion.

5. **Coordinate System**: Schema now includes `units` (px/dp) and `scaleMode`. Need to validate against real Godot/Avalonia scaling behavior.

## Alternatives Considered

### A. Full Migration to Pencil

**Pros**: Single source of truth
**Cons**: High migration cost, some teams need Figma

**Decision**: Support both with component-level ownership

### B. Figma Plugin for In-IDE Editing

**Pros**: Keeps Figma as single source
**Cons**: Still requires external tool, no MCP bi-directional

**Decision**: Pencil's native MCP support is more aligned with agentic goals

### C. Custom DSL Instead of .pen

**Pros**: Full control over format
**Cons**: No visual editor, no agent tooling ecosystem

**Decision**: Leverage pencil.dev's canvas + MCP tools

## Security Considerations

- `.pen` files are JSON and can contain arbitrary strings; validate before processing
- Agent-editable constraints should be enforced at compile time
- Snapshot generation runs code; use sandboxed Godot instances

## References

- [Pencil.dev](https://www.pencil.dev/) - Agent-driven MCP canvas
- [RFC-0001: Core Architecture](./RFC-0001-core-architecture.md)
- [RFC-0013: Native Godot TSCN Backend](./RFC-0013-native-godot-tscn-backend.md)
- [Remotion](https://www.remotion.dev/) - Programmatic video generation
