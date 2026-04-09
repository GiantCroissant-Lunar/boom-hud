# RFC-0020: UI and Motion IR Contract Hardening

- **Status**: Draft
- **Created**: 2026-04-10
- **Authors**: BoomHud Contributors

## Summary

This RFC hardens the shared contracts between importers, IR, and runtime/export backends so the same `.pen` or future component inputs can flow into Remotion and Unity without backend-specific reinterpretation.

The main changes are:

1. move absolute placement and clipping into neutral UI IR
2. preserve reusable component instances in UI IR instead of flattening them
3. define one canonical motion target contract across Remotion and Unity
4. narrow Motion IR values to the subset we can export consistently, or implement the missing semantics explicitly
5. add cross-backend golden tests that exercise the same UI IR and Motion IR fixtures

## Motivation

The current pipeline works for the curated samples, but several contracts are still importer-specific or backend-specific:

- Pencil absolute placement is carried in `InstanceOverrides` metadata instead of neutral UI IR.
- Pencil component instances are expanded into inline trees even though `ComponentRefId` already exists in core IR.
- Remotion and Unity interpret motion targets differently.
- Motion schema accepts values that current consumers do not handle consistently.

That means the current system is not yet stable enough to claim:

```text
new .pen/components + same converter/builder => correct Remotion + correct Unity
```

## Goals

- Make UI IR sufficient to reproduce layout and composition without Pencil-only metadata.
- Make motion targeting stable across Remotion and Unity.
- Keep the IR exportable to additional backends without parsing framework-specific artifacts.
- Preserve backward compatibility while the generators migrate.
- Add regression coverage that proves the same fixture works across both backends.

## Non-Goals

- redesigning the full component model
- supporting arbitrary Remotion-only motion semantics
- solving every fidelity issue in Unity UI Toolkit
- introducing a runtime scene graph separate from existing UI IR

## Design

### Overview

The shared contract must live in BoomHud IR, not in importer metadata or backend assumptions.

The rollout should happen in this order:

1. UI layout neutrality
2. component instance preservation
3. motion target unification
4. motion value contract tightening
5. cross-backend golden coverage

### Detailed Design

### 1. Neutral layout placement and clipping

`LayoutSpec` should carry the placement semantics that are currently hidden in Pencil metadata:

- absolute offsets (`left`, `top`)
- optional clipping intent (`clipContent`)

Rules:

- importers should populate these fields directly
- generators should prefer these fields over importer metadata
- existing metadata keys may remain temporarily as migration fallback only
- a node should not require `boomhud:pencilLeft`, `boomhud:pencilTop`, or `boomhud:pencilClip` to render correctly

This makes UI IR self-sufficient for new importers and future component sources.

### 2. Preserve component instances in UI IR

When an importer sees a reusable component instance, it should emit:

- `ComponentRefId`
- instance-local overrides
- optional overridden child subtrees when the source format supports them

It should not eagerly flatten the instance into a plain inline tree as the primary representation.

Rules:

- reusable definitions remain in `HudDocument.Components`
- instances point at those definitions via `ComponentRefId`
- backends may still expand instances internally if needed, but that expansion becomes an implementation detail rather than the IR contract

This gives Remotion, Unity, and future backends the same component graph.

### 3. Canonical motion target contract

Motion tracks need one target-id rule that every backend uses.

Decision:

- every track keeps `targetKind`
- every track also carries a non-empty `targetId`
- `targetId` always refers to a stable BoomHud node id in UI IR
- the root track for a screen/component targets the root node id of that screen/component, not an implicit backend-specific object

Implications:

- generated React views must expose the same stable ids through `data-boomhud-id`
- Unity exporters must resolve the same ids through generated bindings
- component instances must not rewrite target ids differently per backend

### 4. Motion value contract tightening

The schema currently accepts value shapes that not all exporters support.

Decision:

- the default portable contract is `number | boolean | string | color`
- vector values are either:
  - removed from the portable schema, or
  - mapped explicitly to supported channel families with shared evaluation rules

No backend should silently accept a document that another backend cannot interpret.

If a value kind is allowed by schema, Remotion and Unity must both either:

- support it, or
- reject it with a diagnostic before generation

### 5. Cross-backend contract tests

Add golden tests that use the same fixtures for both backends:

- `.pen` -> UI IR -> React/Remotion
- `.pen` -> UI IR -> Unity UI Toolkit
- `motion.json` -> Remotion runtime
- `motion.json` -> Unity motion export

Required fixture coverage:

- absolute root and absolute child placement
- clipped container
- reusable component instance with overrides
- root-level motion target
- nested element motion target
- unsupported motion value diagnostics

## MSBuild / CLI Integration

The CLI and build tasks should keep current behavior while the migration is in progress, but the long-term target is:

- both React/Remotion and Unity consume the same hardened IR contracts
- motion input is no longer described as Unity/Godot-only when Remotion is part of the same supported path

## Backward Compatibility

The migration should be additive first:

- add neutral layout fields
- keep reading old Pencil metadata as fallback
- update importers to populate the new fields
- update generators to prefer the new fields
- remove metadata dependency after tests cover the new contract

For component reuse:

- keep current component definitions
- start emitting `ComponentRefId` for importers that can preserve reuse
- keep temporary expansion fallback only where a backend cannot yet consume refs directly

## Security / Performance Considerations

- preserving component refs reduces importer-specific tree duplication
- stable contracts reduce backend-specific parsing and conditional logic
- extra validation adds small upfront cost but avoids late-stage export failures

## Alternatives Considered

### Keep importer-specific metadata as the canonical source

Rejected because it makes every new backend learn Pencil/Figma quirks instead of reading BoomHud IR.

### Let each backend define its own motion target rules

Rejected because one motion file would not be reliably portable.

### Keep vector motion values broad and backend-defined

Rejected because it creates schema-valid documents that are not actually shared-contract documents.

## Open Questions

1. Should clipping live directly on `LayoutSpec`, or in a smaller dedicated visual/layout interaction type?
2. Should instance overrides become structured instead of `Dictionary<string, object?>` in the next phase?
3. Should non-portable motion constructs be rejected at schema validation time or at export validation time?

## Recommended Implementation Order

1. Add neutral placement/clipping fields to UI IR and migrate Pencil conversion.
2. Update React and Unity generators to prefer the neutral fields.
3. Preserve component refs in Pencil conversion and update generators/tests.
4. Tighten motion target and value validation.
5. Add cross-backend golden tests.

## Related RFCs

- [RFC-0018](./RFC-0018-react-remotion-motion-pipeline.md)
- [RFC-0019](./RFC-0019-remotion-motion-ir-authoring.md)
