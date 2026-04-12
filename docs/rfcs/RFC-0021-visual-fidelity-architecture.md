# RFC-0021: Visual Fidelity Architecture

- **Status**: Draft
- **Created**: 2026-04-11
- **Authors**: BoomHud Contributors

## Summary

This RFC defines the long-term architecture for reconstructing UI from `.pen` and future design sources so generated output can converge toward the visual result shown in the design surface, not merely a semantically similar widget tree.

The proposal introduces a compiler-only `Visual IR` layer between the current semantic UI IR and backend generation. `Visual IR` becomes the canonical contract for:

1. structural synthesis of repeated motifs and panels
2. explicit edge behavior and wrap pressure
3. backend-calibrated text and icon metric profiles
4. recursive fidelity scoring and closed-loop refinement

The current `HudDocument` and `ComponentNode` model remains the semantic/source contract. `Visual IR` is derived from it and is used by planners, verifiers, and backend realizers.

## Motivation

The current pipeline is good enough to prove end-to-end generation, but it still has an architectural gap:

- the semantic/source IR contains enough information for Remotion to get close to the design
- Unity backends, especially `uGUI`, remain sensitive to structure, edge pressure, and font/icon metric drift
- the current planning model selects rules over the existing tree, but it does not yet own a canonical representation of what is visually true

That leads to recurring fidelity failures:

- repeated motifs are not always represented as reusable structure
- fill, hug, stretch, and absolute placement interact differently per backend
- left-edge alignment pressure appears as a recurring failure mode
- text and icon drift require backend-specific compensation, but there is no first-class calibration model
- recursive scoring exists as diagnostics, but it is not yet a planning input

If the long-term goal is "generated UI identical to what the pen file shows", the pipeline needs a canonical visual contract and a closed-loop planner that operates on it.

## Goals

- Preserve the current semantic/source IR as the importer-facing contract.
- Add a compiler-only `Visual IR` that captures render-relevant truth more precisely than the current semantic tree.
- Make repeated motif synthesis, edge behavior, and text/icon metric calibration first-class concepts.
- Feed recursive fidelity scores back into refinement planning instead of using them as diagnostics only.
- Keep the architecture shared across Remotion, Unity UI Toolkit, and `uGUI`.
- Provide a staged migration path that can be implemented incrementally without blocking current generation.

## Non-Goals

- replacing the current semantic/source IR with a completely new authoring contract
- guaranteeing exact fidelity in one migration step
- introducing runtime reflection-heavy UI abstraction
- forcing Pencil authors to define components manually
- using image similarity as the only planner signal

## Design

### Overview

The long-term pipeline becomes:

```text
Design Source (.pen / future source)
    -> semantic/source IR (HudDocument)
    -> Visual IR normalization
    -> structural synthesis
    -> backend realization plan
    -> backend generation
    -> capture + recursive scoring
    -> refinement loop
```

The current `HudDocument` remains valuable because it is the semantic interchange contract. The proposed `Visual IR` exists because the semantic tree is not rich enough to act as the canonical fidelity contract by itself.

### Layer Responsibilities

#### 1. Semantic / Source IR

The current IR remains responsible for:

- component identity
- high-level widget type
- authored layout/style intent
- data binding contract
- design-source metadata

This layer should remain stable enough for importers and hand-authored documents.

#### 2. Visual IR

`Visual IR` becomes the canonical compiler representation of what should be visible after synthesis and normalization.

This layer is responsible for:

- visual boxes and their hierarchy
- positioning mode and edge behavior
- synthesized component structure
- text and icon metric intent
- clipping and overflow behavior
- backend-independent fidelity diagnostics

`Visual IR` is not a new authoring format. It is a derived compiler artifact.

#### 3. Structural Synthesis

Structural synthesis rewrites the normalized visual tree before backend realization.

This layer is responsible for:

- exact synthetic componentization
- later parametric componentization
- motif and panel decomposition
- edge contract inference
- choosing reusable structure that improves backend realization

#### 4. Backend Realization

Each backend consumes the same `Visual IR` plus the same structural synthesis output, then applies backend-specific calibration.

This layer is responsible for:

- translating edge contracts into backend-specific layout primitives
- applying text/icon metric profiles
- choosing safe fallback strategies for unsupported features
- emitting native framework output

#### 5. Closed-Loop Verification

Verification captures backend output, compares it against design references, and emits recursive scores.

This layer is responsible for:

- subtree-level scores
- phase-specific scores
- backend regression signals
- planner feedback for refinement

### Proposed Visual IR Contract

`Visual IR` should be introduced as a new abstractions namespace or compiler-only model, leaving the current IR untouched during migration.

Illustrative shape:

```csharp
public sealed record VisualDocument
{
    public required string Name { get; init; }
    public required VisualNode Root { get; init; }
    public IReadOnlyDictionary<string, VisualComponentDefinition> Components { get; init; } = new Dictionary<string, VisualComponentDefinition>();
    public IReadOnlyDictionary<string, MetricProfileDefinition> MetricProfiles { get; init; } = new Dictionary<string, MetricProfileDefinition>();
}

public sealed record VisualNode
{
    public required string StableId { get; init; }
    public required VisualNodeKind Kind { get; init; }
    public VisualBox Box { get; init; } = new();
    public EdgeContract EdgeContract { get; init; } = new();
    public TypographyContract? Typography { get; init; }
    public IconContract? Icon { get; init; }
    public string? ComponentRefId { get; init; }
    public IReadOnlyDictionary<string, object?> InstanceOverrides { get; init; } = new Dictionary<string, object?>();
    public IReadOnlyList<VisualNode> Children { get; init; } = [];
}
```

This model intentionally does not replace `ComponentNode`. It augments the compiler with a fidelity-oriented representation.

### Edge Contract

The current `LayoutSpec` is not sufficient to model recurring alignment-pressure failures. A dedicated `EdgeContract` is needed.

`EdgeContract` should define, per node:

- whether width/height behavior is `fill`, `hug`, `fixed`, or `absolute`
- which edges are pinned
- where effective insets come from
- whether wrap pressure is allowed
- whether overflow should clip, scroll, or remain visible
- whether the node participates in parent layout flow or is an overlay

Illustrative shape:

```csharp
public sealed record EdgeContract
{
    public AxisSizing WidthSizing { get; init; } = AxisSizing.Hug;
    public AxisSizing HeightSizing { get; init; } = AxisSizing.Hug;
    public EdgePin HorizontalPin { get; init; } = EdgePin.Start;
    public EdgePin VerticalPin { get; init; } = EdgePin.Start;
    public WrapPressurePolicy WrapPressure { get; init; } = WrapPressurePolicy.Allow;
    public OverflowBehavior OverflowX { get; init; } = OverflowBehavior.Visible;
    public OverflowBehavior OverflowY { get; init; } = OverflowBehavior.Visible;
    public LayoutParticipation Participation { get; init; } = LayoutParticipation.NormalFlow;
}
```

This is the correct long-term place to solve left-edge alignment pressure. Backend generators should stop inferring this purely from scattered layout heuristics.

### Metric Profiles

Text and icon fidelity should not be modeled as one-off backend heuristics. They need explicit metric profiles.

Profiles should capture:

- text semantic role
- backend family
- font family and fallback
- calibrated font size delta
- line height delta
- letter spacing delta
- wrap mode
- alignment and baseline behavior
- icon box size policy
- icon baseline offset
- optical centering preference

Illustrative shape:

```csharp
public sealed record MetricProfileDefinition
{
    public required string Id { get; init; }
    public required string SemanticClass { get; init; }
    public required string BackendFamily { get; init; }
    public TextMetricProfile? Text { get; init; }
    public IconMetricProfile? Icon { get; init; }
}
```

These profiles should be selected from semantic/source information and later refined from fixture calibration.

### Structural Synthesis

Structural synthesis becomes an explicit compiler stage before backend planning.

#### V1

- exact synthetic componentization
- deterministic subtree signatures
- overlap-safe component extraction
- backend-safe rewrite rules

#### V2

- parametric componentization
- "same shape, different text/icon/value" extraction
- instance override preservation and specialization

#### V3

- panel decomposition
- motif clustering
- inside-out and outside-in solve passes

The synthesis stage should operate on `Visual IR`, not directly on backend emit plans.

### Recursive Fidelity Scoring

The current recursive score shape is the correct direction, but long term it must become planner input.

Scoring should remain phase-based:

- structural match
- outer frame match
- inner layout match
- text/icon metrics
- polish offsets

Scoring should remain recursive:

- screen/frame
- panel
- card/cluster
- atomic motif

But long term, the planner should use these scores to choose the next refinement action.

### Closed-Loop Refinement

Long term, refinement should be a bounded search loop:

1. generate from `Visual IR`
2. capture backend output
3. score recursively against reference
4. choose highest-value failing subtree
5. apply one structural or metric refinement
6. re-render affected subtree or fixture
7. keep change only if score improves

This is where GOAP remains useful:

- selecting synthesis actions
- selecting backend compensation actions
- ordering outside-in and inside-out refinements

GOAP should not own the whole architecture. It should operate over `Visual IR`, edge contracts, metric profiles, and recursive scores.

### Backend Strategy

#### Remotion

Remotion remains the fast-lane visual verifier and a strong baseline for the canonical contract.

Long term role:

- reference realization
- fast capture loop
- contract verification for synthesis output

#### Unity UI Toolkit

UI Toolkit should remain the primary high-fidelity Unity target.

Long term role:

- preferred Unity fidelity backend
- primary target for structural synthesis validation
- first recipient of backend-calibrated metric profiles

#### Unity uGUI

`uGUI` should remain supported, but it needs stronger compensation and a realistic backend strategy.

Long term role:

- compatibility backend
- explicit edge-contract realization
- explicit metric-profile compensation

If exact text fidelity remains blocked by legacy `UnityEngine.UI.Text`, the project should evaluate a `TextMeshPro` realization path for `uGUI`.

### Backward Compatibility

Migration should be additive and staged.

Rules:

- keep `HudDocument` and `ComponentNode` as the importer-facing contract
- derive `Visual IR` after current normalization
- let backends migrate to `Visual IR` one at a time
- keep existing generation working while the new compiler stages are introduced

The current semantic/source IR should not be invalidated during this transition.

### MSBuild / CLI Integration

The CLI should gain optional artifacts for:

- normalized `Visual IR`
- structural synthesis summary
- metric profile selection summary
- recursive fidelity score reports
- planner/refinement action trace

These artifacts should be machine-readable so fixture sweeps can compare them across runs.

### Security / Performance Considerations

- `Visual IR` adds compiler complexity, but keeps that complexity out of importers and backend emitters
- structural synthesis can reduce repeated backend output size
- recursive scoring and closed-loop refinement are expensive, so they should be fixture-gated and subtree-scoped
- Remotion should remain the fast verification lane before expensive Unity verification

## Alternatives Considered

### Keep expanding the current rule planner only

Rejected because the current planner operates on an already-fixed tree and does not own a canonical visual representation.

### Replace the current IR entirely

Rejected because the current semantic/source IR is still the right importer-facing and author-facing contract.

### Use image similarity as the only truth source

Rejected because visual scores without structural contracts lead to unstable search and poor explainability.

### Force authors to define all reusable components in Pencil

Rejected because the compiler must be able to synthesize repeated structure even when authors choose not to formalize it.

## Open Questions

1. Should `Visual IR` live in `BoomHud.Abstractions` or remain compiler-internal until the contract stabilizes?
2. Should metric profiles be document-local, backend-global, or both?
3. At what point should `uGUI` move from legacy `Text` to `TextMeshPro` for fidelity-critical surfaces?
4. Which parts of edge contract inference are deterministic enough to live in normalization, and which should remain planner decisions?

## Recommended Implementation Order

1. Introduce compiler-only `Visual IR` and `VisualDocument` snapshot artifacts.
2. Add explicit `EdgeContract` derivation from the current semantic/source IR.
3. Add `MetricProfileDefinition` selection for text and icons.
4. Migrate exact synthetic componentization to operate over `Visual IR`.
5. Preserve original instance placement overrides during synthetic rewrite.
6. Feed edge contracts into `uGUI` and UI Toolkit realization.
7. Feed metric profiles into text/icon emission.
8. Promote recursive scoring from diagnostics into planner input.
9. Add bounded closed-loop refinement for fixture sweeps.

## Related RFCs

- [RFC-0001](./RFC-0001-core-architecture.md)
- [RFC-0010](./RFC-0010-figma-component-reuse.md)
- [RFC-0012](./RFC-0012-unity-uitoolkit-backend.md)
- [RFC-0015](./RFC-0015-snapshot-visual-regression.md)
- [RFC-0018](./RFC-0018-react-remotion-motion-pipeline.md)
- [RFC-0020](./RFC-0020-ui-motion-ir-contract-hardening.md)
