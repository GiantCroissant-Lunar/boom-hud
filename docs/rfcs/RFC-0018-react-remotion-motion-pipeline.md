# RFC-0018: React/Remotion Backend and Motion Pipeline Split

- **Status**: Draft
- **Created**: 2026-04-09
- **Authors**: BoomHud Contributors

## Summary

This RFC proposes two linked decisions:

1. Add a React backend that compiles BoomHud IR into static, Remotion-friendly TSX.
2. Split motion export from UI fidelity work, instead of trying to derive animation semantics from Unity UI Toolkit output.

The immediate goal is `pen -> IR -> React` so Pencil-authored HUDs can render directly inside Remotion. The follow-on goal is a dedicated motion layer that can target Remotion, Godot `AnimationPlayer`, and Unity Timeline.

## Motivation

The current Unity UI Toolkit fidelity loop is useful for fixing generator bugs, but it is an inefficient place to establish design truth. The current handover shows a loop dominated by Unity refresh, play-mode capture, and manual comparison rather than fast design iteration.

That means Unity is currently acting as both:

1. a runtime target
2. the place where we judge whether the design is correct

Those roles should be separated.

## Decision

### 1. React becomes the visual reference backend for `.pen`

For Pencil-driven work, the preferred path becomes:

```text
Pencil .pen -> BoomHud IR -> React TSX -> Remotion preview/render
```

Reasons:

- React/Remotion is closer to Pencil's flex and pixel model
- the preview loop is faster than Unity domain reload plus play-mode capture
- Remotion gives deterministic video and still rendering for reviews
- generated TSX is a readable intermediate artifact for debugging

### 2. Motion is not inferred from arbitrary React code

We should not build this:

```text
pen -> react code -> parse react code -> godot animation / unity timeline
```

That turns BoomHud into a compiler for arbitrary user code. Instead, BoomHud should introduce a Motion IR and export it to multiple runtimes.

Preferred direction:

```text
pen -> UI IR -> React TSX
          \
           -> Motion IR -> Remotion / Godot AnimationPlayer / Unity Timeline
```

## Scope

### In scope now

- React backend that emits static TSX from IR
- Remotion-friendly structure and inline styling
- using React output as the fast design-review path for Pencil input

### Out of scope for this phase

- extracting motion semantics from arbitrary TSX or JSX
- full React interactivity/runtime state management
- Unity Timeline exporter
- Godot `AnimationPlayer` exporter

## Unity Implications

Unity UI Toolkit remains a runtime backend, but it should no longer be the primary place where we solve overall Pencil fidelity.

Recommended Unity strategy:

1. Use the component-lab workflow to fix clear generator defects.
2. Use React/Remotion as the faster visual truth path for overall screen iteration.
3. Validate Unity against stable component contracts and selected screen cases, not every raw Pencil nuance.

## Rollout

### Phase 1

- land React backend
- make `--target react` available in CLI
- use Remotion for Pencil preview and review artifacts

### Phase 2

- define Motion IR schema and contracts
- prototype Motion IR to Remotion

### Phase 3

- add Motion IR exporters for Godot `AnimationPlayer` and Unity Timeline
