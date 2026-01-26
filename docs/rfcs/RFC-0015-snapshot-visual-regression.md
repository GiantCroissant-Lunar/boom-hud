# RFC-0015: Snapshot & Visual Regression System

- **Status**: Implemented
- **Created**: 2026-01-26
- **Authors**: BoomHud Contributors
- **Implements**: `boomhud snapshot`, `boomhud baseline compare`, `boomhud baseline diff`

## Summary

This RFC documents the snapshot and visual regression system for BoomHud, enabling deterministic PNG capture of UI states and baseline comparison for detecting visual drift in CI/CD pipelines.

## Motivation

### Why Snapshots?

BoomHud generates UI code from design files, but generated code quality is only verifiable through visual inspection. Manual review doesn't scale for:

1. **Agent-driven iteration**: When AI agents modify designs, humans need fast feedback
2. **PR review**: Reviewers need to see "what changed visually" without running the app
3. **Regression detection**: Catch accidental visual breaks before merge
4. **Determinism verification**: Ensure same inputs produce same outputs

### Why Baseline Comparison?

Raw snapshots are useful but comparing against a "known good" baseline enables:

- **Pass/fail CI gates**: Actionable feedback on visual changes
- **Noise filtering**: Tolerate minor anti-aliasing differences
- **Dimension tracking**: Detect viewport/layout shifts
- **Version-aware gating**: Ignore changes when Godot version differs

### Goals

1. **Deterministic capture**: Same inputs → same PNG bytes (or explain why not)
2. **CI-first design**: Works headless, produces machine-readable reports
3. **Agent-friendly**: Supports `--dry-run` for pipeline testing without Godot
4. **Diff visualization**: Generate visual diff images for human review

### Non-Goals

- **General UI test framework**: This is not Playwright/Cypress for UIs
- **Cross-backend pixel parity**: Godot is the canonical renderer; Avalonia/TUI won't match
- **Runtime screenshot API**: This is build-time only

---

## Concepts

### Snapshot

A **snapshot** is a deterministic PNG image captured from a rendered UI in a specific ViewModel state.

Properties:
- Captured at a fixed viewport size
- Rendered after layout settles (configurable frame wait)
- Named by state index and name (e.g., `000_Default.png`)
- Hashed for change detection (SHA-256)

### Baseline

A **baseline** is a blessed set of snapshots representing the "known good" visual state. Baselines are:

- Stored as CI artifacts (not committed to git by default)
- Versioned by Godot version for compatibility tracking
- Updated via manual workflow dispatch with documented reason

### Actionable vs Non-Actionable Drift

Not all visual changes require human attention:

| Change Type | Actionable | Reason |
|-------------|------------|--------|
| Hash mismatch, same Godot version | ✅ Yes | Real visual change |
| Hash mismatch, different Godot version | ❌ No | Rendering engine changed |
| Dimension mismatch | ❌ No | Viewport config changed |
| Missing in baseline | ⚠️ Warning | New state added |
| Missing in current | ⚠️ Warning | State removed |

This distinction prevents CI noise when upgrading Godot or changing viewport configs.

---

## Inputs

### Compose Manifest (`--manifest`)

The snapshot system uses the compose manifest (see RFC-0016) to locate:
- Generated scene files
- Source file hashes for input tracking
- Output directory conventions

```bash
boomhud snapshot --manifest ui/boom-hud.compose.json
```

### States Manifest (`*.states.json`)

Defines the viewport configuration and ViewModel states to capture.

**Schema** (`version: "1.0"`):

```json
{
  "$schema": "https://boom-hud.dev/schemas/states.schema.json",
  "version": "1.0",
  "viewport": {
    "width": 1280,
    "height": 720,
    "scale": 1.0
  },
  "defaults": {
    "waitFrames": 2,
    "background": "#1a1a2e"
  },
  "states": [
    {
      "name": "Default",
      "description": "Initial state with no data loaded",
      "vm": {
        "FpsValue": "60",
        "MemoryUsed": "128 MB",
        "ActiveEntities": "0"
      }
    },
    {
      "name": "HighLoad",
      "description": "Simulated high load scenario",
      "vm": {
        "FpsValue": "24",
        "MemoryUsed": "3.2 GB",
        "ActiveEntities": "10,432",
        "IsPaused": true
      },
      "waitFrames": 5
    }
  ]
}
```

**Field Definitions**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `version` | string | Yes | Schema version ("1.0") |
| `viewport.width` | int | No | Viewport width in pixels (default: 1280) |
| `viewport.height` | int | No | Viewport height in pixels (default: 720) |
| `viewport.scale` | float | No | Scale factor (default: 1.0) |
| `defaults.waitFrames` | int | No | Frames to wait before capture (default: 2) |
| `defaults.background` | string | No | Background color hex (e.g., "#1a1a2e") |
| `states[].name` | string | Yes | State identifier (used in filename) |
| `states[].description` | string | No | Human-readable description |
| `states[].vm` | object | No | ViewModel property values to apply |
| `states[].waitFrames` | int | No | Override default wait frames |

**State discovery** (when `--states` is omitted):
1. Look for `ui/states/*.states.json` relative to manifest
2. If exactly one file found, use it
3. If multiple files found, error with guidance
4. If none found, error

---

## Snapshot Generation Pipeline

### CLI Command

```bash
boomhud snapshot \
  --manifest ui/boom-hud.compose.json \
  --states ui/states/debug-overlay.states.json \
  --target godot \
  --out ui/snapshots \
  --timeout 60 \
  --verbose
```

**Options**:

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--manifest` | file | Required | Compose manifest path |
| `--states` | file | Auto-discover | States manifest path |
| `--target` | string | `godot` | Target backend (only `godot` supported) |
| `--out` | dir | `ui/snapshots` | Output directory |
| `--godot-exe` | file | Auto-detect | Path to Godot executable |
| `--runner-path` | file | Auto-detect | Path to SnapshotRunner.gd |
| `--timeout` | int | 60 | Timeout in seconds |
| `--verbose` | flag | false | Enable verbose output |
| `--dry-run` | flag | false | Generate placeholders without Godot |

### Godot Headless Runner Contract

The snapshot system invokes Godot in headless mode with a custom runner script.

**CLI Invocation Shape**:

```bash
godot --headless \
  --quit-after <timeout> \
  --script <runner_script_path> \
  -- \
  --scene <generated_scene.tscn> \
  --states <states_json_path> \
  --out <output_dir> \
  [--verbose]
```

**Runner Arguments**:

| Argument | Description |
|----------|-------------|
| `--scene` | Path to generated scene to instantiate |
| `--states` | Path to states manifest JSON file |
| `--out` | Directory for PNG output |
| `--verbose` | Enable verbose logging (optional) |

### ApplyVmJson Contract

Generated root nodes **MUST** implement an `ApplyVmJson(string json)` method:

```csharp
// Generated in root component (e.g., DebugOverlayView.g.cs)
public partial class DebugOverlayView : Control
{
    /// <summary>
    /// Applies ViewModel state from JSON for snapshot testing.
    /// </summary>
    public void ApplyVmJson(string json)
    {
        var state = JsonSerializer.Deserialize<DebugOverlayVmState>(json);
        if (state == null) return;
        
        if (state.FpsValue != null) FpsValue = state.FpsValue;
        if (state.MemoryUsed != null) MemoryUsed = state.MemoryUsed;
        // ... etc
    }
}
```

This method:
- Accepts a JSON object matching the ViewModel shape
- Applies only non-null properties (partial updates)
- Is called by the runner before each frame capture

### Frame Waiting Rules

Layout engines need time to settle. The runner:

1. Instantiates the scene
2. Calls `ApplyVmJson()` with state data
3. Waits `waitFrames` render cycles (default: 2)
4. Captures viewport to PNG
5. Advances to next state

The wait frame count is configurable per-state for complex layouts.

---

## Snapshot Output Contract

### PNG Naming Convention

```
snapshots/
├── 000_Default.png
├── 001_HighLoad.png
├── 002_Error.png
└── snapshots.manifest.json
```

Format: `{index:D3}_{sanitized_name}.png`

Name sanitization:
- Replace spaces with underscores
- Remove special characters
- Truncate to 50 characters

### Output Manifest (`snapshots.manifest.json`)

```json
{
  "version": "1.0",
  "generatedAt": "2026-01-26T14:32:00.000Z",
  "toolVersion": "1.2.3",
  "godotVersion": "4.3-stable",
  "target": "godot",
  "viewport": {
    "width": 1280,
    "height": 720,
    "scale": 1.0
  },
  "runnerInfo": {
    "os": "linux",
    "dotnetVersion": "9.0.0",
    "isCI": true,
    "headless": true
  },
  "inputHashes": {
    "manifest": "sha256:abc123...",
    "states": "sha256:def456...",
    "source:debug-overlay.pen": "sha256:789ghi...",
    "runner": "sha256:jkl012..."
  },
  "snapshots": [
    {
      "state": "Default",
      "path": "000_Default.png",
      "sha256": "sha256:xyz789..."
    },
    {
      "state": "HighLoad",
      "path": "001_HighLoad.png",
      "sha256": "sha256:abc012..."
    }
  ]
}
```

**Field Definitions**:

| Field | Description |
|-------|-------------|
| `version` | Manifest schema version |
| `generatedAt` | ISO 8601 timestamp |
| `toolVersion` | BoomHud CLI version |
| `godotVersion` | Godot version used (null for dry-run) |
| `target` | Backend name |
| `viewport` | Viewport configuration used |
| `runnerInfo.os` | Operating system |
| `runnerInfo.dotnetVersion` | .NET runtime version |
| `runnerInfo.isCI` | Whether running in CI environment |
| `runnerInfo.headless` | Whether Godot ran headless |
| `inputHashes` | SHA-256 hashes of all inputs |
| `snapshots[].state` | State name |
| `snapshots[].path` | Relative path to PNG |
| `snapshots[].sha256` | SHA-256 hash of PNG |

---

## Baseline Comparison

### Compare Command

```bash
boomhud baseline compare \
  --current ui/snapshots \
  --baseline ui/snapshots-baseline \
  --out reports/baseline-compare.json \
  --tolerance 8 \
  --min-changed-percent 0.01 \
  --summary
```

**Options**:

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--current` | dir | Required | Current snapshots directory |
| `--baseline` | dir | Required | Baseline snapshots directory |
| `--out` | file | stdout | Output report path |
| `--tolerance` | int | 8 | Per-channel delta tolerance (0-255) |
| `--min-changed-percent` | float | 0.01 | Minimum % changed for significance |
| `--summary` | flag | false | Print summary to console |
| `--fail-on` | string | (none) | Fail condition (see below) |

### Frame Comparison Statuses

```csharp
public enum FrameCompareStatus
{
    Unchanged,           // Hashes match exactly
    Changed,             // Hashes differ, actionable
    MissingBaseline,     // New frame (not in baseline)
    MissingCurrent,      // Removed frame (in baseline, not current)
    ChangedNonActionable,// Changed but Godot version mismatch
    DimensionMismatch    // Viewport dimensions differ
}
```

### Compatibility Rules

Comparisons are marked **Compatible=false** when:

1. **Godot version mismatch**: `baseline.godotVersion != current.godotVersion`
   - All changed frames become `ChangedNonActionable`
   - IncompatibilityReason: "Godot version mismatch (baseline: 4.2, current: 4.3)"

2. **Dimension mismatch**: `baseline.viewport != current.viewport`
   - Affected frames become `DimensionMismatch`
   - Comparison uses max dimensions for diff

When `Compatible=false`, the comparison **does not fail CI by default** (non-blocking).

### Comparison Report

```json
{
  "version": "1.0",
  "generatedAt": "2026-01-26T14:35:00.000Z",
  "toolVersion": "1.2.3",
  "baseline": {
    "path": "ui/snapshots-baseline",
    "godotVersion": "4.3-stable",
    "toolVersion": "1.2.2",
    "target": "godot",
    "inputHash": "sha256:combined...",
    "snapshotCount": 3
  },
  "current": {
    "path": "ui/snapshots",
    "godotVersion": "4.3-stable",
    "toolVersion": "1.2.3",
    "target": "godot",
    "inputHash": "sha256:combined...",
    "snapshotCount": 3
  },
  "summary": {
    "total": 3,
    "unchanged": 2,
    "changed": 1,
    "changedNonActionable": 0,
    "dimensionMismatch": 0,
    "missingBaseline": 0,
    "missingCurrent": 0,
    "compatible": true,
    "incompatibilityReason": null,
    "actionableChanges": 1,
    "tolerance": 8,
    "minChangedPercent": 0.01,
    "maxChangedPercent": 12.5,
    "framesExceedingThreshold": 0
  },
  "frames": [
    {
      "index": 0,
      "name": "Default",
      "status": "Unchanged",
      "actionable": true,
      "baselineHash": "sha256:abc...",
      "currentHash": "sha256:abc...",
      "baselinePath": "000_Default.png",
      "currentPath": "000_Default.png"
    },
    {
      "index": 1,
      "name": "HighLoad",
      "status": "Changed",
      "actionable": true,
      "baselineHash": "sha256:def...",
      "currentHash": "sha256:ghi...",
      "baselinePath": "001_HighLoad.png",
      "currentPath": "001_HighLoad.png",
      "diffMetrics": {
        "baselineWidth": 1280,
        "baselineHeight": 720,
        "currentWidth": 1280,
        "currentHeight": 720,
        "totalPixels": 921600,
        "changedPixels": 115200,
        "changedPercent": 12.5,
        "maxDelta": 45,
        "meanDelta": 8.2,
        "dimensionsMatch": true
      }
    }
  ]
}
```

---

## Diff Images

### Diff Command

```bash
boomhud baseline diff \
  --current ui/snapshots \
  --baseline ui/snapshots-baseline \
  --out ui/diffs \
  --tolerance 8 \
  --highlight-color "#ff0080"
```

**Options**:

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--current` | dir | Required | Current snapshots directory |
| `--baseline` | dir | Required | Baseline snapshots directory |
| `--out` | dir | `diffs/` | Output directory for diff PNGs |
| `--tolerance` | int | 8 | Per-channel delta tolerance |
| `--highlight-color` | string | `#ff0080` | Color for changed pixels |

### Diff Output

For each changed frame, generates:

```
diffs/
├── 001_HighLoad_diff.png      # Highlighted diff overlay
├── 001_HighLoad_baseline.png  # Copy of baseline (for side-by-side)
├── 001_HighLoad_current.png   # Copy of current (for side-by-side)
└── diff-manifest.json         # Diff generation metadata
```

### Tolerance & Noise Filtering

Per-channel delta tolerance accounts for:
- Anti-aliasing differences between renders
- Minor floating-point color variations
- Font rendering differences

A pixel is considered "changed" if `max(|R1-R2|, |G1-G2|, |B1-B2|, |A1-A2|) > tolerance`.

Frames with `changedPercent < minChangedPercent` are considered "effectively unchanged" (noise).

---

## CI Workflows

### PR Workflow (ci.yml)

```yaml
# On pull_request:
- name: Generate snapshots (dry-run or real)
  run: boomhud snapshot --manifest ui/boom-hud.compose.json --dry-run

- name: Download baseline from main
  uses: dawidd6/action-download-artifact@v6
  with:
    workflow: ci.yml
    branch: main
    name: snapshots
    path: ui/snapshots-baseline
  continue-on-error: true

- name: Compare with baseline
  run: |
    boomhud baseline compare \
      --current ui/snapshots \
      --baseline ui/snapshots-baseline \
      --summary

- name: Generate diff images
  if: always()
  run: |
    boomhud baseline diff \
      --current ui/snapshots \
      --baseline ui/snapshots-baseline \
      --out ui/diffs

- name: Generate comparison video
  run: |
    boomhud video \
      --snapshots ui/snapshots \
      --baseline ui/snapshots-baseline

- name: Upload artifacts
  uses: actions/upload-artifact@v4
  with:
    name: visual-regression
    path: |
      ui/snapshots/
      ui/diffs/
      reports/
```

### Main Branch Workflow

```yaml
# On push to main:
- name: Generate snapshots
  run: boomhud snapshot --manifest ui/boom-hud.compose.json

- name: Upload baseline artifact
  uses: actions/upload-artifact@v4
  with:
    name: snapshots
    path: ui/snapshots/
    retention-days: 90
```

### Manual Baseline Update (update-baseline.yml)

```yaml
on:
  workflow_dispatch:
    inputs:
      reason:
        description: 'Reason for baseline update'
        required: true
      godot_version:
        description: 'Godot version tag'
        default: '4.3-stable'
      dry_run:
        description: 'Dry-run mode'
        type: boolean
        default: false

jobs:
  update-baseline:
    runs-on: ubuntu-latest
    steps:
      - name: Generate new baseline
        run: |
          boomhud snapshot --manifest ui/boom-hud.compose.json
          
      - name: Compare with previous
        run: |
          boomhud baseline compare --summary
          
      - name: Upload new baseline
        uses: actions/upload-artifact@v4
        with:
          name: snapshots
          path: ui/snapshots/
```

---

## Failure Policy

### Default Behavior (Non-Blocking)

By default, visual regression does NOT fail CI:

- Comparison reports are generated and uploaded
- Summary is printed to console
- Exit code is 0 unless catastrophic error

This allows teams to adopt gradually without blocking PRs.

### Strict Modes

Enable strict failure with `--fail-on`:

| Value | Behavior |
|-------|----------|
| `any` | Fail if any actionable frame changed |
| `percent:X` | Fail if any frame exceeds X% changed |
| `count:N` | Fail if more than N frames changed |

```bash
# Fail if any frame has >5% pixel changes
boomhud baseline compare --fail-on percent:5

# Fail if any frame changed at all
boomhud baseline compare --fail-on any
```

### Protected Mode

For repositories requiring strict visual stability:

```bash
boomhud baseline compare --protected
```

This:
- Treats `MissingBaseline` as error (new states must be reviewed)
- Treats `MissingCurrent` as error (removed states must be intentional)
- Ignores `ChangedNonActionable` (version mismatch is allowed)

---

## Implementation Notes

### Dry-Run Mode

When `--dry-run` is specified:
- Placeholder PNGs are generated (solid color based on state name hash)
- No Godot invocation
- `godotVersion` is null in manifest
- Useful for testing pipeline without Godot installation

### Runner Environment Tracking

The `runnerInfo` object captures environment details for debugging baseline drift:

```json
{
  "os": "linux",
  "dotnetVersion": "9.0.0",
  "isCI": true,
  "headless": true
}
```

`isCI` is detected via `CI` environment variable.

### Hash Computation

All hashes use SHA-256 with hex encoding:
- File hashes: `SHA256(file_bytes)`
- Combined hash: `SHA256(sorted_concat(all_input_hashes))`

---

## Security Considerations

- Snapshot PNGs may contain sensitive UI text (disable in public repos if needed)
- Baseline artifacts should have appropriate retention policies
- State manifests may contain test data (review before committing)

---

## References

- RFC-0014: Pencil.dev Integration (Phase 2 - Snapshot System)
- RFC-0016: Compose Manifest (input resolution)
- `.github/workflows/ci.yml`: Implementation reference
- `.github/workflows/update-baseline.yml`: Baseline update workflow
