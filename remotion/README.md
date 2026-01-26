# BoomHud Video Stitcher

Remotion-based video generator for BoomHud snapshot previews and comparisons.

## Quick Start

```bash
# Install dependencies
npm install

# Render preview video from snapshots
npx ts-node render.ts --snapshots ../ui/snapshots --out preview.mp4

# Render comparison video (baseline vs current)
npx ts-node render.ts --snapshots ../ui/snapshots --baseline ../ui/snapshots-baseline --out compare.mp4
```

## CLI Options

| Option | Default | Description |
|--------|---------|-------------|
| `--snapshots <dir>` | required | Directory containing current snapshots and manifest |
| `--baseline <dir>` | none | Directory containing baseline snapshots (enables comparison mode) |
| `--out <file>` | `preview.mp4` | Output video file |
| `--fps <number>` | `30` | Frames per second |
| `--seconds-per-state <n>` | `1.5` | Duration per state in seconds |
| `--title-overlay on\|off` | `on` | Show state name overlay |

## Video Modes

### Preview Mode (default)

Single-frame sequence showing each state:

```
┌────────────────────────────┐
│                            │
│    [Snapshot Image]        │
│                            │
├────────────────────────────┤
│ State Name           1/N   │
└────────────────────────────┘
```

### Comparison Mode (with --baseline)

Side-by-side view for PR review:

```
┌─────────────┬─────────────┐
│  BASELINE   │   CURRENT   │
├─────────────┼─────────────┤
│             │             │
│  [Baseline] │  [Current]  │
│             │             │
├─────────────┴─────────────┤
│ State Name           1/N   │
└───────────────────────────┘
```

Missing frames show a placeholder: "Missing baseline" / "Missing current"

## How It Works

1. Reads `snapshots.manifest.json` from snapshot directories
2. Copies PNG files to `public/` for Remotion's staticFile()
3. Renders frames using React components
4. Encodes to mp4 using Remotion/FFmpeg

## Integration with BoomHud CLI

```bash
# Preview video
boomhud video --snapshots ui/snapshots

# Comparison video
boomhud video --snapshots ui/snapshots --baseline ui/snapshots-baseline
```

## Preview in Browser

```bash
npm run preview
```

Opens Remotion Studio for interactive preview.
