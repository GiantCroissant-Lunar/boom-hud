# RFC-0019: Remotion as Motion IR Authoring Surface

- **Status**: Draft
- **Created**: 2026-04-09
- **Authors**: BoomHud Contributors

## Summary

This RFC defines how Remotion should participate in BoomHud motion authoring.

Decision:

1. Remotion is the primary authoring surface for motion.
2. Motion is authored as BoomHud-owned JSON documents, not arbitrary React code.
3. Remotion helpers may assist with creation/editing, but they emit that JSON contract directly.
4. Remotion also consumes that same Motion IR for preview and final render.

In short, Remotion should dogfood Motion IR, but it should not become Motion IR.

## Motivation

Agents are strong at producing and iterating on Remotion code because:

- the feedback loop is fast
- the model is visual and time-based
- React-style composition is expressive
- preview and export are already part of the toolchain

But arbitrary Remotion code is not a stable interchange format for:

- Godot `AnimationPlayer`
- Unity Timeline
- future non-React motion backends

If we treat free-form Remotion code as the source of truth, we will eventually need to parse and interpret arbitrary user-authored React logic. That is too brittle.

## Core Decision

### Remotion is an authoring adapter over Motion IR JSON

The architecture should be:

```text
BoomHud UI IR + Motion IR
        |
        +--> Remotion preview/render
        +--> Godot AnimationPlayer export
        +--> Unity Timeline export
```

The source of truth should be a JSON document validated by `motion.schema.json`, for example:

```json
{
  "version": "1.0",
  "name": "HudMotion",
  "framesPerSecond": 30,
  "clips": [
    {
      "id": "charPortraitIntro",
      "name": "Char Portrait Intro",
      "durationFrames": 20,
      "tracks": [
        {
          "id": "portrait",
          "targetId": "char-portrait",
          "channels": [
            {
              "property": "opacity",
              "keyframes": [
                { "frame": 0, "value": { "kind": "number", "number": 0 } },
                { "frame": 20, "value": { "kind": "number", "number": 1 }, "easing": "easeOut" }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

Remotion-side helpers are acceptable only if they create or transform this JSON contract deterministically.

## Dogfooding Model

### What “dogfood” should mean here

Remotion should dogfood Motion IR in two ways:

1. **Authoring dogfood**
   - the Remotion author works against Motion IR JSON, either directly or through helpers that emit it
2. **Playback dogfood**
   - the Remotion renderer should be able to render directly from Motion IR

That means Remotion is both:

- the first authoring frontend for Motion IR
- the first runtime adapter for Motion IR

### What it should not mean

Remotion should not dogfood Motion IR by forcing us to recover IR from arbitrary TSX, hooks, closures, or frame logic after the fact.

This is explicitly out of bounds:

```text
arbitrary Remotion code -> AST/parser pass -> inferred Motion IR
```

That path becomes fragile immediately and makes export quality depend on coding style rather than declared motion intent.

## Layering

### Recommended layering

```text
UI IR
  - static component tree
  - stable node ids

Motion IR
  - clips
  - tracks
  - targets
  - keyframes
  - easing
  - events / visibility / swaps

Authoring layer
  - JSON documents validated by schema
  - optional TypeScript helpers for agents and humans
  - constrained enough to emit Motion IR deterministically

Adapters
  - Remotion playback adapter
  - Godot AnimationPlayer exporter
  - Unity Timeline exporter
```

### Stable targeting

Motion IR should target stable BoomHud ids, not framework-specific names.

Examples:

- `char-portrait`
- `health-bar`
- `message-log`

These ids should map to:

- React `data-boomhud-id`
- Godot node path metadata or generated node names
- Unity element names or generated bindings

This is what makes one motion spec portable across all targets.

## Motion IR Scope

The first version of Motion IR should stay narrow and exportable.

### Include

- clip id
- duration or frame span
- tracks
- target id
- property id
- keyframes
- easing
- visibility toggles
- simple enter/exit timing

### Exclude initially

- arbitrary callback code
- runtime branching based on free-form JS conditions
- side effects outside the animation model
- custom shader/material logic
- anything that cannot map cleanly to Godot or Unity

## Authoring Constraints

The Remotion authoring surface should be expressive, but constrained.

Good constraints:

- target by stable id
- animate only known property names
- use standard easing identifiers
- use explicit frame ranges
- prefer declarative composition over imperative frame callbacks

Allowed escape hatches should be clearly marked non-exportable.

For example:

- `previewOnly(...)`
- `remotionOnly(...)`

These can exist, but exported Motion IR should either reject them or strip them with diagnostics.

## Export Semantics

### Remotion

Remotion should render by interpreting Motion IR against the generated React view tree.

That gives us:

- preview parity with authoring
- deterministic video output
- a fast review path for motion work

### Godot

Godot export should translate Motion IR into:

- `AnimationPlayer` tracks
- node/property keyframes
- visibility and modulation changes

### Unity

Unity export should translate Motion IR into either:

- Timeline assets
- or an intermediate runtime track representation if Timeline asset authoring is too heavy at first

The long-term target remains Timeline-compatible output.

## Open Questions

1. Should the primary editing surface be raw JSON, a typed object literal, or both?
2. Should Motion IR live in the same assembly/namespace as UI IR or as a sibling package?
3. Do we need a separate “exportability validator” that rejects Remotion-only constructs before Godot/Unity export?
4. Should Motion IR use frame-based timing only, or support seconds plus fps normalization?

## Recommendation

Implement in this order:

1. Motion IR contracts
2. Remotion authoring helpers that emit Motion IR JSON
3. Remotion playback adapter that consumes Motion IR
4. Godot exporter
5. Unity exporter

This keeps Remotion as the fastest place for agents to create motion, while ensuring the source of truth remains BoomHud-owned and portable.
