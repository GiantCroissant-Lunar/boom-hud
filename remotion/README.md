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

## Motion JSON

BoomHud motion authoring for Remotion should use the shared JSON contract, not a custom chained DSL.

The Remotion workspace now exposes typed helpers under `src/motion/`:

- `schema.ts`: Zod schemas and TypeScript types mirroring `schemas/json/motion.schema.json`
- `authoring.ts`: object-literal helpers like `defineMotionDocument()` and `parseMotionDocument()`
- `runtime.ts`: frame evaluation helpers like `resolveClipStateAtFrame()` and `useMotionTargetState()`
- `MotionScene.tsx`: wrapper that injects `motionTargets` into generated BoomHud React views

Example:

```ts
import { defineMotionDocument, numberValue } from "./src/motion";

const document = defineMotionDocument({
  name: "HudMotion",
  clips: [
    {
      id: "intro",
      name: "Intro",
      durationFrames: 20,
      tracks: [
        {
          id: "portrait",
          targetId: "char-portrait",
          channels: [
            {
              property: "opacity",
              keyframes: [
                { frame: 0, value: numberValue(0) },
                { frame: 20, value: numberValue(1), easing: "easeOut" },
              ],
            },
          ],
        },
      ],
    },
  ],
});
```

This keeps JSON as the source of truth while still giving Remotion typed authoring and playback utilities.

Generated BoomHud React views can now be driven declaratively:

```tsx
import { MotionScene } from "./src/motion";
import { CharPortraitView } from "./generated/CharPortraitView";

<MotionScene
  document={document}
  clipId="intro"
  component={CharPortraitView}
  viewModel={{}}
/>;
```

## Studio

Remotion Studio is available locally in this workspace after install.

From [remotion](C:/lunar-horse/plate-projects/boom-hud/remotion):

```bash
npm run preview
```

The demo composition added in this repo is `GeneratedMotionDemo`. It renders:

- a generated BoomHud React view at `src/generated/CharPortraitView.tsx`
- a Motion JSON document at `src/motion-samples/char-portrait.motion.json`
- through the `MotionScene` bridge in `src/motion/MotionScene.tsx`
