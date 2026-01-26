# RFC-0017: Design Lint System

- **Status**: Proposed (Partial Implementation)
- **Created**: 2026-01-26
- **Authors**: BoomHud Contributors
- **Implements**: `boomhud lint`, diagnostic infrastructure

## Summary

This RFC documents the design lint system for BoomHud, providing fast feedback on design file quality before rendering or code generation. The lint system surfaces token violations, naming issues, binding errors, and layout warnings through standardized diagnostics.

## Motivation

### Why Design Lint?

Code linters (ESLint, Roslyn analyzers) catch problems early—before runtime. BoomHud needs equivalent tooling for design files:

1. **Fast feedback**: Catch token misuse, naming issues, and binding errors without invoking Godot
2. **Noise reduction**: Prevent trivial design issues from polluting visual regression diffs
3. **Consistency enforcement**: Ensure design files follow team conventions
4. **Agent guidance**: Give AI agents clear rules about what's allowed

### Current Pain Points

Without lint, issues are discovered late:
- Token misuse discovered during generation (BH0102)
- Style collisions discovered during composition (BH0110)
- Layout issues discovered only in visual snapshots
- Naming hygiene problems accumulate silently

### Goals

1. **Pre-flight validation**: Run before generation/snapshot
2. **Actionable diagnostics**: Clear codes, messages, and fix suggestions
3. **CI integration**: GitHub Actions summary support
4. **Configurable severity**: Warning vs error threshold control
5. **Incremental adoption**: Advisory by default, strict opt-in

### Non-Goals

- Full layout solving (we don't have a complete constraint solver)
- Semantic ViewModel validation (type checking requires external schema)
- Design aesthetic judgments (no "ugly color" rules)

---

## Lint Contract

### CLI Command

```bash
boomhud lint \
  --manifest ui/boom-hud.compose.json \
  --fail-on warning \
  --format github
```

**Options**:

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--manifest` | file | Required | Compose manifest path |
| `--input` | file[] | - | Direct input files (alternative to manifest) |
| `--tokens` | file | Auto-discover | Token registry file |
| `--fail-on` | string | (none) | Fail threshold: `error`, `warning`, `info`, or code |
| `--format` | string | `console` | Output format: `console`, `json`, `github` |
| `--rules` | file | - | Custom rules config (future) |

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | No issues (or only below threshold) |
| 1 | Issues at or above `--fail-on` threshold |
| 2 | Lint execution error (file not found, parse error) |

### Output Formats

**Console (default)**:
```
ui/sources/debug-overlay.pen
  [BH0104] warning: Inline value '#ff0000' used for fill; consider using a token reference (node: ErrorIndicator)
  [BH0120] info: Component name 'debug_overlay' uses snake_case; PascalCase recommended (node: debug_overlay)

ui/sources/minimap.pen
  [BH0102] error: Unresolved token reference: '$colors.undefined-token' (node: MapBorder)

Found 3 issues: 1 error, 1 warning, 1 info
```

**JSON**:
```json
{
  "version": "1.0",
  "timestamp": "2026-01-26T14:00:00.000Z",
  "summary": {
    "total": 3,
    "errors": 1,
    "warnings": 1,
    "info": 1
  },
  "diagnostics": [
    {
      "code": "BH0102",
      "severity": "error",
      "message": "Unresolved token reference: '$colors.undefined-token'",
      "file": "ui/sources/minimap.pen",
      "nodeId": "MapBorder",
      "line": 45,
      "column": 12
    }
  ]
}
```

**GitHub Actions**:
```
::error file=ui/sources/minimap.pen,line=45,col=12::BH0102: Unresolved token reference: '$colors.undefined-token'
::warning file=ui/sources/debug-overlay.pen::BH0104: Inline value '#ff0000' used for fill; consider using a token reference
```

---

## Diagnostic Infrastructure

### BoomHudDiagnostic Record

```csharp
public sealed record BoomHudDiagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    string? SourceFile = null,
    string? NodeId = null,
    int? Line = null,
    int? Column = null)
{
    public override string ToString()
    {
        var location = FormatLocation();
        return $"[{Code}] {Severity.ToString().ToLowerInvariant()}: {Message}{location}";
    }
}
```

### Severity Levels

```csharp
public enum DiagnosticSeverity
{
    Info,     // Suggestion, style preference
    Warning,  // Should fix, but generation proceeds
    Error     // Must fix, generation fails
}
```

### Diagnostic Code Ranges

| Range | Category | Examples |
|-------|----------|----------|
| BH0100-BH0109 | Component/Document Collisions | BH0100 component collision, BH0101 token collision |
| BH0102-BH0109 | Token Errors (legacy range) | BH0102 unresolved, BH0103 deprecated, BH0104 inline |
| BH0110-BH0119 | Style Collisions | BH0110 style collision, BH0111 root not found |
| BH0120-BH0129 | Naming (lint) | BH0120 case convention, BH0121 ID hygiene |
| BH0300-BH0399 | Bindings | BH0300 invalid expression, BH0301 target not found |
| BH0400-BH0499 | Generation | BH0400 unsupported feature |
| BH0500-BH0599 | Layout (lint, future) | BH0500 safe area violation |

> **Note**: Token errors (BH0102-0104) are in the BH01xx range for historical reasons.
> New token-related codes should use BH0105-0109 to maintain compatibility.

---

## MVP Rules

These rules ship in v1.0 of the lint system:

### Token Rules

#### BH0102: Unresolved Token Reference (Error)

A token reference cannot be resolved against the token registry.

```
ERROR [BH0102]: Unresolved token reference: '$colors.undefined-token'
  at ui/sources/minimap.pen, node: MapBorder
  
  Suggestion: Check spelling or add token to tokens.ir.json
```

#### BH0103: Deprecated Token (Warning)

A token marked as deprecated in the registry is being used.

```
WARNING [BH0103]: Token 'colors.old-primary' is deprecated
  at ui/sources/debug-overlay.pen, node: Header
  
  Suggestion: Use 'colors.primary' instead
```

#### BH0104: Inline Token Warning (Warning)

A literal value (color, spacing) is used where a token reference would be preferred.

```
WARNING [BH0104]: Inline value '#ff0000' used for fill; consider using a token reference
  at ui/sources/debug-overlay.pen, node: ErrorIndicator
  
  Suggestion: Define $colors.error in tokens.ir.json
```

**Configuration**: This rule can be elevated to error for strict token-only policies.

### Naming Rules

#### BH0120: Component Name Convention (Info)

Component names should follow PascalCase convention.

```
INFO [BH0120]: Component name 'debug_overlay' uses snake_case; PascalCase recommended
  at ui/sources/debug-overlay.pen, node: debug_overlay
  
  Suggestion: Rename to 'DebugOverlay'
```

#### BH0121: ID Hygiene (Info)

Node IDs should be meaningful and not auto-generated placeholders.

```
INFO [BH0121]: Node ID 'Frame123' appears auto-generated; consider using meaningful name
  at ui/sources/minimap.pen, node: Frame123
  
  Suggestion: Rename to describe purpose (e.g., 'MapContainer')
```

### Binding Rules

> **Scope**: Binding lint validates **syntax** and **IR-level target existence** only.
> It does NOT validate that a ViewModel property exists or has the correct type—that
> requires an external schema (see Non-Goals).

#### BH0300: Invalid Binding Expression (Error)

A binding expression has invalid syntax.

```
ERROR [BH0300]: Invalid binding expression: '{Binding .FpsValue}'
  at ui/sources/debug-overlay.pen, node: FpsLabel
  
  Suggestion: Remove leading dot: '{Binding FpsValue}'
```

#### BH0301: Binding Target Not Found (Warning)

A binding references an **element path** that doesn't exist in the component's IR tree.
This detects typos like `ElementToFill: "NonExistent"` when no such node exists.

> **Note**: This is NOT ViewModel path validation. BH0301 checks IR node references,
> not `{Binding PropertyName}` paths. VM path validation requires external schema.

```
WARNING [BH0301]: Binding target 'NonExistent' not found in component tree
  at ui/sources/debug-overlay.pen, node: StatusLabel
  
  Suggestion: Check spelling or add target element
```

### Composition Rules

#### BH0110: Style Collision Guidance (Warning)

Surfaces style collision information from composition as lint warning.

```
WARNING [BH0110]: Style 'DebugText' collision detected during composition
  Winner: ui/sources/debug-overlay.pen
  Loser: ui/sources/minimap.pen
  
  Suggestion: Define shared styles in a single theme file or scope styles inside components
```

> **Implementation Note**: To detect BH0110, `boomhud lint --manifest` internally runs
> `MultiSourceComposer.Compose()` when the manifest has multiple sources. This means lint
> sees cross-source collisions. If linting a single file without `--manifest`, cross-source
> rules like BH0110 will not fire.

### Layout Rules (Future)

#### BH0500: Safe Area Violation (Warning)

A component exceeds safe area bounds for the target platform.

```
WARNING [BH0500]: Component 'BottomBar' may overlap mobile safe area (bottom: 20px inset expected)
  at ui/sources/main-hud.pen, node: BottomBar
  
  Suggestion: Add 20px bottom padding or use safe area binding
```

This rule requires platform-specific safe area knowledge and is planned for v1.1.

---

## Rule Configuration

### Default Behavior

All rules are enabled at their default severity. Lint is **advisory** by default (exit code 0 unless errors).

### Severity Override via CLI

```bash
# Fail on warnings
boomhud lint --manifest ui/boom-hud.compose.json --fail-on warning

# Fail only on specific code
boomhud lint --manifest ui/boom-hud.compose.json --fail-on BH0104
```

### Configuration File (Future)

A `.boomhud-lint.json` config file is planned for v1.1:

```json
{
  "version": "1.0",
  "rules": {
    "BH0104": "error",
    "BH0120": "off",
    "BH0121": "warning"
  },
  "exclude": [
    "legacy/**"
  ]
}
```

---

## Integration Points

### Pre-Generation Hook

Lint runs automatically before code generation when enabled:

```bash
boomhud generate --manifest ui/boom-hud.compose.json --lint
```

With `--lint` flag:
1. Run lint pass
2. If errors found, abort generation
3. If only warnings, continue with report

### Review Workflow Integration

The `boomhud review` command includes lint:

```bash
boomhud review --manifest ui/boom-hud.compose.json
```

Flow:
1. **Lint** → Report issues
2. **Generate** → Code generation
3. **Snapshot** → Visual capture
4. **Baseline compare** → Diff detection
5. **Video** → Preview generation

### CI Integration

**GitHub Actions workflow**:

```yaml
- name: Lint design files
  run: |
    boomhud lint \
      --manifest ui/boom-hud.compose.json \
      --format github
  continue-on-error: true  # Advisory by default

- name: Lint (strict)
  if: github.event_name == 'pull_request'
  run: |
    boomhud lint \
      --manifest ui/boom-hud.compose.json \
      --fail-on warning \
      --format github
```

**Job Summary**:

```yaml
- name: Write lint summary
  if: always()
  run: |
    echo "## Design Lint Results" >> $GITHUB_STEP_SUMMARY
    boomhud lint --manifest ui/boom-hud.compose.json --format github >> $GITHUB_STEP_SUMMARY
```

---

## Implementation Architecture

### Lint Pipeline

```
Input Files
    │
    ▼
┌─────────────────┐
│  Parse to IR    │
│  (Figma/Pencil) │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Load Tokens    │
│  Registry       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Run Lint Rules │
│  ┌─────────────┐│
│  │TokenRules   ││
│  │NamingRules  ││
│  │BindingRules││
│  │LayoutRules ││
│  └─────────────┘│
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Collect        │
│  Diagnostics    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Format Output  │
│  (console/json/ │
│   github)       │
└─────────────────┘
```

### Rule Interface

```csharp
public interface ILintRule
{
    string Code { get; }
    DiagnosticSeverity DefaultSeverity { get; }
    string Description { get; }
    
    IEnumerable<BoomHudDiagnostic> Analyze(
        HudDocument document,
        SourceIdentity source,
        TokenRegistry? tokens);
}
```

### Rule Registration

```csharp
public static class LintRules
{
    public static IReadOnlyList<ILintRule> All { get; } =
    [
        new UnresolvedTokenRule(),      // BH0102
        new DeprecatedTokenRule(),       // BH0103
        new InlineTokenRule(),           // BH0104
        new ComponentNameRule(),         // BH0120
        new IdHygieneRule(),             // BH0121
        new InvalidBindingRule(),        // BH0300
        new BindingTargetRule(),         // BH0301
    ];
}
```

---

## Error Suppression (Future)

### Inline Suppression

For v1.1, support inline suppression via design file comments:

**Pencil format**:
```json
{
  "id": "ErrorIndicator",
  "fill": "#ff0000",
  "_boomhud_suppress": ["BH0104"]
}
```

**Figma annotations**:
```json
{
  "nodeId": "1:23",
  "suppress": ["BH0104"]
}
```

### File-Level Suppression

Via config file:

```json
{
  "suppress": {
    "BH0104": ["legacy/old-design.pen"],
    "BH0120": ["*"]
  }
}
```

---

## Metrics & Reporting

### Lint Summary Statistics

```json
{
  "summary": {
    "total": 15,
    "errors": 2,
    "warnings": 8,
    "info": 5,
    "suppressed": 3,
    "byCode": {
      "BH0102": 2,
      "BH0104": 6,
      "BH0120": 4,
      "BH0121": 3
    },
    "byFile": {
      "ui/sources/debug-overlay.pen": 8,
      "ui/sources/minimap.pen": 7
    }
  }
}
```

### Trend Tracking (Future)

CI can track lint metrics over time:

```yaml
- name: Record lint metrics
  run: |
    boomhud lint --manifest ui/boom-hud.compose.json --format json > lint-results.json
    # Upload to metrics service
```

---

## Migration Path

### Phase 1: Advisory (Current)

- Lint available via `boomhud lint` command
- Not run by default during generation
- CI uses `continue-on-error: true`

### Phase 2: Integrated

- `boomhud generate --lint` runs lint before generation
- `boomhud review` includes lint step
- CI fails on errors, warns on warnings

### Phase 3: Strict

- `--lint` is default for generate
- Teams configure `--fail-on warning` in CI
- Custom rules via config file

---

## Non-Goals (Explicit)

### No Semantic VM Validation

We don't validate that ViewModel properties exist or have correct types:

```json
{
  "vm": {
    "NonExistentProperty": "value"
  }
}
```

This requires external schema definition and is out of scope.

### No Layout Solving

We don't solve constraint systems or detect overflow:

```
Component A overlaps Component B by 12px
```

This requires a full layout engine and is out of scope.

### No Aesthetic Rules

We don't judge design quality:

```
Color '#ff0000' has low contrast with '#ff1111'
```

This is subjective and better handled by design tools.

---

## References

- RFC-0014: Pencil.dev Integration (lint mentioned in Phase 0B)
- RFC-0015: Snapshot & Visual Regression (lint reduces diff noise)
- RFC-0016: Compose Manifest (lint uses manifest for input discovery)
- BoomHud.Abstractions/Diagnostics/BoomHudDiagnostic.cs
- BoomHud.Abstractions/Diagnostics/DiagnosticCodes (partial list)
