# Visual Regression Policy

This document describes the baseline comparison policy for BoomHud UI snapshots.

## Overview

The visual regression system compares UI snapshots between PRs and the main branch baseline, detecting changes and providing actionable feedback in CI.

## Thresholds & Defaults

| Setting | Default | Purpose |
|---------|---------|---------|
| `--tolerance` | 8 | Per-channel delta (0-255). Ignores small anti-aliasing differences |
| `--min-changed-percent` | 0.01 | Noise filter. Frames below this % are considered unchanged |
| `--fail-on` | (none) | Non-blocking by default. Use `any` or `percent:X` to fail CI |
| `--protected` | (none) | Frames that always fail if changed, regardless of threshold |

## Rollout Phases

### Phase 1: Observational (Current)

- CI runs baseline compare on every PR
- Reports changes in GitHub Actions summary
- **Non-blocking** — PRs can merge even with visual changes
- Collect data on baseline stability and false positive rate

### Phase 2: Protected Frames

After 1-2 weeks of observational data:

```yaml
--fail-on percent:0.5 --protected Default,DebugOverlay_On
```

- Critical frames (`Default`, `DebugOverlay_On`) will fail CI if changed
- Other frames remain advisory
- Threshold at 0.5% allows minor layout shifts but catches major regressions

### Phase 3: Strict Mode

Once baseline is stable:

```yaml
--fail-on any --protected Default,DebugOverlay_On,DebugOverlay_Off
```

- Any actionable change fails CI
- Requires explicit baseline update for intentional changes

## Protected Frames

Protected frames are UI states that should never change without explicit approval:

| Frame | Reason |
|-------|--------|
| `Default` | Primary user-facing state |
| `DebugOverlay_On` | Debug mode must be visually distinct |
| `DebugOverlay_Off` | Ensures debug toggle works correctly |

Add frames to `--protected` as they become critical.

## Updating Baselines

### Automatic (on push to main)

When PRs merge to main, the CI workflow uploads new snapshots as the baseline for future PRs.

### Manual (workflow_dispatch)

For intentional visual changes:

1. Go to **Actions** → **Update UI Baseline**
2. Click **Run workflow**
3. Fill in:
   - **Reason**: Why baseline is being updated
   - **Dry-run**: false for real Godot snapshots (requires self-hosted runner)
   - **Godot version**: Must match main CI

This generates a new baseline without requiring a PR merge.

## Interpreting Results

### Status Types

| Status | Actionable | Meaning |
|--------|------------|---------|
| ✅ Unchanged | — | Frame matches baseline |
| ⚠️ Changed | Yes | Visual difference detected, review required |
| 🔇 Non-actionable | No | Godot version mismatch, can't reliably compare |
| 📐 Dimension mismatch | No | Viewport size changed, treat as non-actionable |
| 🆕 New | — | Frame exists in PR but not in baseline |
| ❌ Removed | — | Frame exists in baseline but not in PR |

### Top Offenders Table

The GH Actions summary shows the most-changed frames:

| Frame | Changed % | Pixels | Max Δ | Mean Δ |
|-------|-----------|--------|-------|--------|
| `Example` | 2.34% | 1,234 | 128 | 45.2 |

- **Changed %**: Percentage of pixels affected
- **Pixels**: Absolute count of changed pixels
- **Max Δ**: Largest per-channel difference (0-255)
- **Mean Δ**: Average difference across changed pixels

## Diff Images

For each changed frame, the system generates:

- `{index}_{name}__baseline.png` — Original baseline
- `{index}_{name}__current.png` — Current PR snapshot  
- `{index}_{name}__diff.png` — Visual diff (magenta = changed, gray = unchanged)

Download the `diff-images` artifact to review changes locally.

## Single-Frame Similarity Score

For design-vs-render checks on one frame or one component crop, use the CLI image scorer:

```bash
dotnet run --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- \
  baseline score --reference path/to/reference.png --candidate path/to/current.png \
  --normalize cover --diff ui/diffs/component-diff.png \
  --out ui/image-similarity-report.json --tolerance 8 --fail-below 95
```

The scorer reports three related values:

| Metric | Formula | Meaning |
|--------|---------|---------|
| `Pixel identity %` | `100 - changedPercent` | Percentage of pixels unchanged above tolerance |
| `Delta similarity %` | `100 * (1 - meanDelta / 255)` | How close changed pixels still are in average intensity |
| `Overall similarity %` | `pixelIdentity * 0.7 + deltaSimilarity * 0.3` | Practical single number for review dashboards |

Interpretation:

- `100%` means identical at the chosen tolerance
- `90-99%` usually means small layout, spacing, or text-rendering drift
- `<90%` usually means a materially different frame or crop

Important:

- Scores are most meaningful when the images have the same dimensions and crop.
- Use `--normalize stretch` or `--normalize cover` when the candidate image does not match the reference dimensions.
- `--normalize cover` is the safer default for screenshot-vs-screenshot comparisons because it preserves aspect ratio and center-crops.
- `--fail-below 95` makes the command suitable for CI quality gates without needing a wrapper script.
- This is a deterministic diff score, not a perceptual SSIM implementation.

## Local Development

Run the full review workflow locally:

```bash
# Generate snapshots (dry-run without Godot)
dotnet run --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- \
  snapshot --manifest ui/boom-hud.compose.json --states ui/states/debug-overlay.states.json \
  --out ui/snapshots --dry-run

# Generate preview video
dotnet run --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- \
  video --snapshots ui/snapshots

# Compare against baseline (if you have one locally)
dotnet run --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- \
  baseline compare --current ui/snapshots --baseline ui/snapshots-baseline \
  --out ui/baseline-report.json --summary --tolerance 8

# Generate diff images
dotnet run --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- \
  baseline diff --current ui/snapshots --baseline ui/snapshots-baseline \
  --out ui/diffs --tolerance 8
```

## FAQ

**Q: My PR shows visual changes but I didn't change any UI code.**  
A: Check for Godot version mismatch (non-actionable), or timing-sensitive states. If the change is under the tolerance threshold, it's likely rendering noise.

**Q: How do I intentionally update the baseline?**  
A: Either merge to main (automatic) or use the "Update UI Baseline" workflow dispatch.

**Q: Why is my frame marked "non-actionable"?**  
A: Usually Godot version mismatch. The baseline was generated with a different Godot version, so pixel-level comparison isn't meaningful. This resolves when baselines are regenerated with the same Godot version.

**Q: Can I run real Godot snapshots locally?**  
A: Yes, remove `--dry-run` and ensure Godot is installed and the project is configured. The CLI will auto-detect Godot.
