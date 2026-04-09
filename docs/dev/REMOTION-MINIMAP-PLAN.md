# Remotion Minimap Plan

This document defines the next focused Remotion slice for BoomHud: a dedicated minimap composition.

The goal is not just to preview the minimap in Remotion. The goal is to establish a clean, portable path:

`Pencil .pen -> UI IR -> generated React view -> Remotion preview/render -> shared Motion JSON -> Unity metadata/export`

## Goal

Create a dedicated Remotion surface for the minimap component so that:

1. the minimap can be reviewed in isolation without the rest of the HUD
2. motion can be authored against the shared Motion JSON contract rather than hard-coded Remotion frame logic
3. the same staged motion metadata can later flow into Unity Timeline without re-authoring

## Current State

The relevant pieces already exist in the repo:

1. Remotion already has focused and screen-level demo compositions:
   - `remotion/src/GeneratedMotionDemo.tsx`
   - `remotion/src/GeneratedFullPenDemo.tsx`
2. The reusable minimap component already exists in `samples/pencil/full.pen` as `Component/Minimap`.
3. Unity generation already produces a standalone minimap surface:
   - `samples/UnityFullPenCompare/Assets/Resources/BoomHudGenerated/MinimapView.gen.cs`
4. Shared motion metadata already supports:
   - `defaultSequenceId`
   - `sequences`
   - per-item `startFrame`
   - per-item `durationFrames`
   - per-item `fillMode`
5. Unity already consumes this metadata through generated motion helpers and `BoomHudMotionTimelineSceneBuilder`.

What is missing is a minimap-specific React/Remotion surface and a minimap-specific motion sample.

## Decision

The first minimap Remotion slice should use the existing reusable component inside `samples/pencil/full.pen`.

Short-term decision:

1. use `samples/pencil/full.pen` as the input source
2. use CLI root selection to generate only the minimap component
3. keep the first Remotion slice focused on render path and stable targeting, not fancy effects

Do not create a dedicated `ui/sources/pencil/minimap.pen` yet unless the component needs to diverge from the full-screen HUD source. The reusable component in `full.pen` is already the current design truth.

## Deliverables

The minimap slice should land these artifacts:

1. `remotion/src/generated/MinimapView.tsx`
2. `remotion/src/generated/IMinimapViewModel.g.ts`
3. `remotion/src/GeneratedMinimapDemo.tsx`
4. `remotion/src/motion-samples/minimap.motion.json`
5. `remotion/src/Root.tsx` registration for `GeneratedMinimapDemo`
6. `remotion/README.md` update after the slice is implemented

## Recommended Generation Path

Use the existing CLI root-selection support.

Example command:

```bash
dotnet run --project dotnet/src/BoomHud.Cli -- generate samples/pencil/full.pen --root Minimap --target react --output remotion/src/generated
```

Notes:

1. `--root` already supports selecting a reusable component from a single input document.
2. If `Minimap` is not the exact resolved component name, the CLI will print the available roots.
3. The selected root should produce a focused generated React view instead of the full `ExploreHudView` surface.

## Composition Shape

Add a dedicated Remotion composition that mirrors the existing focused-demo pattern used by `GeneratedMotionDemo`.

Recommended shape:

1. parse a dedicated `minimap.motion.json`
2. resolve the default sequence from shared motion metadata
3. compute composition duration from `getSequenceDurationFrames(...)`
4. expose `animated: boolean` props, defaulting to `false`
5. when `animated` is `false`, render the final resolved motion state for fidelity inspection
6. when `animated` is `true`, render through `MotionScene`

Recommended frame size:

1. keep the authored minimap itself at its generated size
2. render it centered inside a slightly larger review surface such as `512x512` or `640x480`
3. avoid scaling the minimap by default, since this is a fidelity/debug surface

## Motion Contract

The minimap must use the shared Motion JSON contract. Do not author minimap behavior as custom Remotion-only hooks or arbitrary `useCurrentFrame()` logic.

That means:

1. motion lives in `remotion/src/motion-samples/minimap.motion.json`
2. Remotion consumes it through `parseMotionDocument(...)`, `getRequiredMotionSequence(...)`, and `MotionScene`
3. Unity later consumes the same authored sequence metadata through generated motion exports

If the minimap needs staged playback, define:

1. `defaultSequenceId`
2. `sequences`
3. portable clip ids
4. portable fill modes

This is the metadata Unity can already use.

## Stable Targeting Rules

This is the most important constraint for minimap motion.

The current generated minimap contains:

1. stable row-level ids such as `R0` through `R9`
2. many opaque tile ids derived from source ids, such as `N6x8c6`, `O6YM3`, and similar

For the first motion slice, target only stable, readable ids.

Recommended initial targets:

1. minimap root
2. row containers `r0` to `r9` or their generated equivalents
3. any explicit semantic child ids if they are later introduced

Avoid binding the first exported motion to opaque tile ids unless those ids are stabilized semantically in source.

Reason:

1. row-level targeting is understandable in both Remotion and Unity
2. raw auto-derived tile ids are brittle and hard to maintain
3. exportability is more important than highly granular first-pass effects

## Recommended First Motion Slice

The first minimap animation should prove the contract, not maximize visual ambition.

Recommended initial clips:

1. `minimapIntro`
   - root opacity from `0` to `1`
   - slight `positionY` or `scale` settle
2. `rowRevealTop`
   - reveal top rows in order
3. `rowRevealBottom`
   - reveal remaining rows in order

Recommended initial sequence:

1. hold the intro end state
2. stage row reveals with explicit `startFrame`
3. keep all effects limited to exportable properties already supported by Motion IR

Avoid in v1:

1. shader-like scanlines
2. arbitrary noise functions
3. Remotion-only procedural drawing logic
4. effects that cannot map cleanly into Unity Timeline playback

## Implementation Order

### Step 1: Prove focused React generation

1. generate the minimap-only React view from `samples/pencil/full.pen`
2. verify the output lands under `remotion/src/generated/`
3. confirm the generated component renders cleanly in isolation

### Step 2: Add a static Remotion composition

1. create `remotion/src/GeneratedMinimapDemo.tsx`
2. render the generated `MinimapView` without motion first
3. register it in `remotion/src/Root.tsx`
4. verify it in Remotion Studio

### Step 3: Add shared motion JSON

1. create `remotion/src/motion-samples/minimap.motion.json`
2. keep clip ids readable and exportable
3. add `defaultSequenceId` and `sequences`
4. wire the composition through `MotionScene`

### Step 4: Verify metadata portability

1. ensure the minimap motion document can round-trip through `MotionDocument`
2. ensure Unity motion export emits `DefaultSequenceId`, `SequenceIds`, and `GetSequenceItems(...)`
3. ensure Timeline scheduling can be built from that metadata without Remotion-specific parsing

## Validation Checklist

The slice is not complete until all of these are true:

1. the CLI can generate a focused minimap React view from `full.pen`
2. Remotion Studio shows a dedicated `GeneratedMinimapDemo` composition
3. the static final-state minimap renders correctly with no dependency on the rest of the HUD
4. the minimap motion sample validates against `schemas/json/motion.schema.json`
5. the composition duration comes from `getSequenceDurationFrames(...)`, not a hard-coded frame count
6. the authored sequence metadata remains consumable by Unity through the existing exporter/tooling path

## Non-Goals

This slice should not attempt to solve all future minimap behavior.

Out of scope for the first pass:

1. live gameplay data integration
2. dynamic runtime map generation
3. per-tile semantic animation across the entire grid
4. preview-only Remotion tricks that cannot be exported
5. redesigning the minimap source component structure unless targeting stability requires it

## Follow-Up Work After v1

Once the first minimap Remotion slice is working, the next useful upgrades are:

1. introduce semantic ids for important minimap markers if per-cell animation is needed
2. split the minimap source into a dedicated `.pen` only if it starts diverging from `full.pen`
3. add a Unity comparison scene or Timeline sample for minimap motion, matching the char portrait workflow
4. update `remotion/README.md` with the minimap composition and generation command once the implementation lands

## Summary

The minimap Remotion slice should be implemented as a focused reusable-component demo, not as a cropped fragment of the full HUD.

The source of truth remains:

1. the reusable minimap component from `samples/pencil/full.pen`
2. the shared Motion JSON contract
3. portable sequence metadata that Unity can already consume

That keeps the minimap work aligned with the repo's current architecture instead of creating a new Remotion-only path.