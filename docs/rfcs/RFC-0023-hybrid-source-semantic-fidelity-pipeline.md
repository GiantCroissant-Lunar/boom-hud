# RFC-0023: Hybrid Source-Semantic Fidelity Pipeline

- **Status**: Draft
- **Created**: 2026-04-14
- **Authors**: BoomHud Contributors

## Summary

This RFC proposes a hybrid fidelity pipeline for BoomHud.

The core idea is:

- keep BoomHud's current layered compiler architecture
- enrich the pipeline with a new source-semantic extraction stage inspired by importer-first systems such as the D.A. Assets Figma converters
- strengthen backend-native realization, especially for Unity `uGUI`

This RFC does not propose replacing BoomHud's `UI IR`, `Motion IR`, `Visual IR`, rules, or planning layers. It proposes refactoring the seams around them so fidelity-critical source semantics survive long enough to improve backend realization.

## Motivation

Recent fidelity work shows that BoomHud already has several long-term advantages:

- a compiler-owned `Visual IR`
- backend-shared metric and rule surfaces
- replayable and refinement-oriented planning for `uGUI`
- source-agnostic architecture that is not locked to Figma

At the same time, a direct review of the D.A. Assets pipeline shows that it is stronger in a different area:

- it preserves raw source semantics aggressively
- it classifies nodes early
- it stores rich per-node derived state
- it uses backend-native realization logic instead of relying on a mostly neutral generator

That combination matters because the remaining fidelity gap is not only an optimizer problem. It is also a representation problem. Some design truth is being lost before backend realization.

BoomHud should not copy the D.A. Assets architecture wholesale. That would give up real advantages in cross-backend planning and closed-loop refinement. But BoomHud should copy the parts that are clearly helping fidelity:

- early source-semantic classification
- richer derived node state
- asset realization classification
- backend-native geometry and typography realization

## Goals

- Preserve `HudDocument` as the semantic/source authoring contract.
- Preserve `Motion IR` as the motion authoring and export contract.
- Preserve `Visual IR` as the compiler-owned visual contract.
- Introduce a new source-semantic enrichment stage before `Visual IR` construction.
- Introduce a richer compiler-owned derived node state model for fidelity-critical source facts.
- Improve backend-native realization for `uGUI` and Unity UI Toolkit.
- Keep rules, actions, and planning surfaces compatible with the current optimizer and refinement work.
- Land this incrementally without rewriting the entire pipeline.

## Non-Goals

- replacing `HudDocument` with a raw Figma-style node graph
- replacing `Visual IR`
- replacing `Motion IR`
- deleting the current rule and action model
- replacing the existing GOAP or planner stack with a vendor-style importer state machine
- introducing runtime-heavy abstraction layers

## Design

### Overview

The proposed long-term pipeline becomes:

```text
Design Source (.pen / future sources)
    -> Semantic UI IR (HudDocument)
    -> Source-Semantic Enrichment
    -> Visual IR normalization
    -> Structural synthesis
    -> Backend realization planning
    -> Backend-native generation
    -> Capture + recursive scoring + refinement
```

The new stage is `Source-Semantic Enrichment`.

Its job is to preserve fidelity-critical source facts that the current semantic tree does not encode strongly enough on its own.

### Core Position

BoomHud does not need a wholesale architectural rewrite.

BoomHud does need targeted refactoring in three places:

1. before `Visual IR`
2. inside backend realization
3. in how refinement reasons about source-semantic evidence

BoomHud does not need a rewrite of:

- `UI IR`
- `Motion IR`
- `Visual IR`
- the rule model
- the action model

Those layers are still the right shape. The weak area is the fidelity of the data flowing into them and the backend-native strength of the realizers consuming them.

### New Layer: Source-Semantic Enrichment

Introduce a compiler-only enrichment stage that derives fidelity-critical facts from the semantic/source tree and attached metadata.

Illustrative responsibilities:

- classify text nodes by role
- classify icon nodes by role
- classify image nodes by realization mode
- derive compact-row and edge-pressure hints
- derive source typography intent in normalized form
- derive reuse signatures and stable subtree fingerprints
- preserve fidelity-relevant facts that should survive into `Visual IR`

Illustrative shape:

```csharp
public sealed record SourceSemanticDocument
{
    public required string DocumentName { get; init; }
    public required SourceSemanticNode Root { get; init; }
}

public sealed record SourceSemanticNode
{
    public required string StableId { get; init; }
    public required string SourceId { get; init; }
    public required string SemanticRole { get; init; }
    public AssetRealizationKind AssetRealization { get; init; }
    public SourceTypographyEvidence? TypographyEvidence { get; init; }
    public SourceGeometryEvidence? GeometryEvidence { get; init; }
    public SourceReuseSignature? ReuseSignature { get; init; }
    public IReadOnlyDictionary<string, object?> Facts { get; init; } = new Dictionary<string, object?>();
    public IReadOnlyList<SourceSemanticNode> Children { get; init; } = [];
}
```

This is not a new authoring format. It is a compiler-owned derived artifact, similar in spirit to `Visual IR`, but earlier and more source-oriented.

### Relationship to Existing IR Layers

#### UI IR

Keep it.

`HudDocument` remains the semantic interchange contract. It is still the correct authoring-facing layer because BoomHud must remain source-agnostic and schema-first.

What changes:

- the compiler derives more evidence from `HudDocument`
- more source facts are normalized into metadata that later layers can consume

What does not change:

- `HudDocument` is not replaced by a Figma-native raw tree
- semantic component modeling remains the entry contract

#### Motion IR

Keep it.

The motion system is orthogonal to this RFC. Motion fidelity may eventually benefit from richer source-semantic evidence, but `Motion IR` itself does not need redesign here.

What changes:

- motion planning may later consume better structural decomposition from `Visual IR`

What does not change:

- motion authoring and export contracts

#### Visual IR

Keep it and strengthen it.

`Visual IR` remains the canonical visual contract. This RFC only changes what feeds it.

What changes:

- `Visual IR` builder consumes source-semantic evidence
- semantic class assignment becomes more evidence-driven
- edge and metric contracts become less heuristic

What does not change:

- `Visual IR` remains the backend-shared planning surface
- `Visual IR` remains compiler-owned, not author-facing

### Backend Realization Refactor

The next weak seam is backend realization.

BoomHud currently has the right abstraction shape, but the realizers still need to become more backend-native where fidelity depends on renderer behavior.

#### uGUI

`uGUI` should gain a stronger realization layer for:

- source-aware geometry resolution
- compact-row and right-edge handling
- asset realization choice
- text metric application
- icon optical alignment

This should remain compiler-owned and deterministic.

It should not become a manual importer workflow.

#### Unity UI Toolkit

UI Toolkit should keep the current plan-driven generation shape, but should become more explicit about:

- shared versus local styles
- template-worthy repeated structures
- asset realization mode
- downloadable image box versus layout box handling

### Rules, Actions, and GOAP

These layers should be evolved, not replaced.

#### Rules

Keep the current rule system.

What changes:

- selectors gain access to richer source-semantic classes and evidence
- actions can target better-realized backend seams

What does not change:

- rule files remain the calibration and override surface

#### Actions

Keep the current action vocabulary direction.

What changes:

- action scopes can target richer semantic roles
- some actions become more source-aware because backend realizers have better evidence

What does not change:

- actions remain compiler-owned policy mutations, not ad hoc backend hacks

#### GOAP / planning

No immediate rewrite is required.

The planner is not the main bottleneck today. The bottleneck is the quality of the evidence and realization surface it is operating on.

What changes:

- planning should consume source-semantic diagnostics and richer visual evidence
- repair selection can become more accurate because hot regions will be classified better

What does not change:

- the high-level planning and refinement architecture

### Data Flow

The intended data flow is:

```text
HudDocument
    -> SourceSemanticDocument
    -> VisualDocument
    -> Visual synthesis
    -> backend plan
    -> backend generator
```

The compiler should expose artifacts for the new stage in the same spirit as the current visual artifacts, so the pipeline remains inspectable.

### Implementation Plan

#### Phase 1

- introduce `SourceSemanticDocument` and `SourceSemanticNode`
- derive source-semantic evidence from `HudDocument` plus existing metadata
- feed semantic classes, asset realization hints, and geometry hints into `VisualDocumentBuilder`

#### Phase 2

- refactor `uGUI` realization to consume source geometry and asset realization evidence
- refactor UI Toolkit realization to consume stronger template, style, and asset evidence

#### Phase 3

- expose source-semantic diagnostics in refinement artifacts
- allow planner and subtree repair scaffolding to target source-semantic hot spots directly

## Backward Compatibility

This RFC is intended to be backward-compatible.

- existing `.pen` authoring remains valid
- existing `HudDocument` contracts remain valid
- existing `Motion IR` contracts remain valid
- existing `Visual IR` contracts remain valid, though richer evidence may populate them
- existing rule sets remain valid, though new selector opportunities will be added

## Security / Performance Considerations

The added enrichment stage increases compile-time work.

This is acceptable because:

- BoomHud is a build-time system
- fidelity work is already dominated by generation and scoring cost
- better evidence should reduce wasted search in later phases

The main performance risk is over-deriving or over-indexing evidence that is never used. The implementation should prefer compact, directly actionable evidence over maximal source mirroring.

## Alternatives Considered

### 1. Replace BoomHud with a vendor-style importer architecture

Rejected.

This would sacrifice:

- cross-backend planning
- source-agnostic architecture
- compiler-owned visual contracts
- refinement-oriented tooling

### 2. Keep the current architecture and only improve optimizers

Rejected as insufficient.

The remaining fidelity gap is not only a search problem. It is also a representation and realization problem.

### 3. Replace `HudDocument` with a raw Figma-like source tree

Rejected.

BoomHud should stay schema-first and source-agnostic.

## Open Questions

- Should the new source-semantic artifact be persisted by default or only under a debug flag?
- Should asset realization kind live in source-semantic evidence only, or also become a first-class `Visual IR` field?
- How much source-semantic evidence should be backend-neutral versus backend-specific?
- Should subtree reuse signatures be derived at the source-semantic stage or remain purely a visual-synthesis concern?

## Related RFCs

- [RFC-0001: Core Architecture](/C:/lunar-horse/plate-projects/boom-hud/docs/rfcs/RFC-0001-core-architecture.md)
- [RFC-0021: Visual Fidelity Architecture](/C:/lunar-horse/plate-projects/boom-hud/docs/rfcs/RFC-0021-visual-fidelity-architecture.md)
- [RFC-0022: Replayable uGUI Synthesis](/C:/lunar-horse/plate-projects/boom-hud/docs/rfcs/RFC-0022-replayable-ugui-synthesis.md)
