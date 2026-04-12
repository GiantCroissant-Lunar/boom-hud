# RFC-0022: Replayable uGUI Synthesis

- **Status**: Draft
- **Created**: 2026-04-12
- **Authors**: BoomHud Contributors

## Summary

This RFC extends the visual fidelity architecture with a replayable `uGUI` synthesis model.

Instead of treating `uGUI` generation as a single-pass tree emission followed by late heuristic tuning, the compiler should construct `uGUI` as an ordered, deterministic build program over a fixed-size virtual canvas. Each subtree is realized as a sequence of actions with explicit checkpoints so candidate realizations can be replayed, verified, accepted, or rolled back during hierarchical search.

The immediate result of this RFC is a new compiler-owned `UGuiBuildProgram` artifact and planner seam. The longer-term goal is a component-first search loop that proves smaller `uGUI` pieces against Pen references before assembling larger surfaces.

## Motivation

The current fidelity loop has useful diagnostics but the control loop is still too late:

- a full `uGUI` tree is emitted first
- capture and scoring happen after realization
- refinement mostly adjusts rules over an already-chosen structure

That is the wrong shape for exact reconstruction.

For fixed-size Pen fixtures, BoomHud can assume a fixed virtual canvas in Unity and should be able to recreate the visible result exactly, provided the realization path is allowed to:

1. solve small subtrees first
2. checkpoint accepted results
3. backtrack when a larger composition fails
4. replay the accepted action sequence deterministically

The missing concept is not more post-hoc tweaking. The missing concept is a replayable construction trace.

## Goals

- Treat `uGUI` realization as a deterministic sequence of build actions.
- Keep the build trace compiler-owned and serializable.
- Introduce explicit subtree checkpoints for acceptance and rollback.
- Support component-first growth from atom to motif to component to full surface.
- Preserve compatibility with the existing `Visual IR` architecture.
- Land this incrementally without rewriting the entire backend in one step.

## Non-Goals

- replacing `Visual IR`
- replacing `uGUI` layout primitives with a custom runtime layout engine
- shipping the full search and backtracking engine in the first implementation slice
- making rule sweeps the primary long-term control mechanism

## Design

### Overview

The long-term `uGUI` fidelity pipeline becomes:

```text
HudDocument
  -> Visual IR normalization
  -> structural synthesis
  -> UGuiBuildProgram planning
  -> hierarchical candidate search
  -> accepted replayable build program
  -> final uGUI code emission
  -> capture + score + measured layout verification
```

`Visual IR` remains the canonical visual contract. `UGuiBuildProgram` becomes the canonical realization contract for the `uGUI` backend.

### Core Idea

The compiler should no longer think in terms of "emit the whole `uGUI` tree now."

It should instead think in terms of:

- create a node
- choose its realization strategy
- bind edge and metric contracts
- grow verified children
- checkpoint the subtree
- continue upward

If a parent composition fails acceptance, the compiler rolls back to the latest accepted checkpoint, tries another candidate, and replays the program.

### Fixed Virtual Canvas

Pen fixtures use fixed-size frames. For fixture-based fidelity work, the `uGUI` search loop should operate against a fixed virtual canvas derived from the target Pen surface.

This matters because:

- subtree coordinates become stable
- absolute and in-flow placement can be reasoned about on the same surface
- replay becomes deterministic
- acceptance scoring becomes comparable across iterations

The virtual canvas is a compiler-time planning assumption, not a separate runtime UI system.

### UGuiBuildProgram

The compiler should introduce a new artifact:

```csharp
public sealed record UGuiBuildProgram
{
    public required string DocumentName { get; init; }
    public required string BackendFamily { get; init; }
    public required string SourceGenerationMode { get; init; }
    public required string RootStableId { get; init; }
    public IReadOnlyList<UGuiBuildStep> Steps { get; init; } = [];
    public IReadOnlyList<UGuiBuildCheckpoint> Checkpoints { get; init; } = [];
}
```

Each step is deterministic and replayable:

```csharp
public sealed record UGuiBuildStep
{
    public required int Order { get; init; }
    public required string StableId { get; init; }
    public string? ParentStableId { get; init; }
    public required string SolveStage { get; init; }
    public required string ActionType { get; init; }
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
}
```

Each checkpoint marks an acceptance boundary:

```csharp
public sealed record UGuiBuildCheckpoint
{
    public required int Order { get; init; }
    public required string StableId { get; init; }
    public required string SolveStage { get; init; }
    public required int LastStepOrder { get; init; }
    public required string Purpose { get; init; }
}
```

### Solve Stages

Planning and search should operate hierarchically:

- `atom`: a single visual primitive or text/icon box
- `motif`: a small repeated pattern such as portrait-plus-label or icon cell
- `component`: a reusable local assembly such as a member card or stat bar
- `surface`: a fixture or screen-level subtree

The first implementation slice may classify these stages heuristically from subtree size. Later slices may promote stage assignment into explicit structural synthesis output.

### Action Vocabulary

The long-term action vocabulary should include:

- `create-node`
- `bind-edge-contract`
- `bind-typography-contract`
- `bind-icon-contract`
- `choose-layout-strategy`
- `choose-sizing-strategy`
- `attach-child`
- `seal-subtree`
- `checkpoint`

The first implementation slice only needs enough actions to serialize the intended shape of the replayable construction trace.

### Search Strategy

The recommended long-term search strategy is hierarchical DFS with beam pruning.

Why not plain BFS:

- the branching factor is too high
- failures usually happen locally inside one subtree
- a component-first solve should backtrack locally, not globally

Recommended loop:

1. choose the next subtree to solve
2. enumerate a bounded set of realization candidates
3. replay each candidate onto the virtual tree
4. render and score the subtree
5. keep the best `k` candidates
6. promote one accepted checkpoint into the parent solve
7. backtrack when parent acceptance fails

This makes `uGUI` synthesis reproducible:

- same input
- same candidate set
- same scoring function
- same beam width
- same accepted build program

### Relationship to Existing Visual IR Planning

This RFC does not replace:

- `VisualDocument`
- `VisualSynthesisPlanner`
- `VisualRefinementPlanner`

It extends them.

The intended relationship is:

- `VisualDocument`: canonical visual truth
- `VisualSynthesisPlanner`: structural decomposition and reuse
- `VisualRefinementPlanner`: candidate mutation hints
- `UGuiBuildProgram`: replayable realization trace for the `uGUI` backend

The current refinement action labels are useful as candidate-generation hints, but they are not yet sufficient to act as the full replayable construction model.

## Implementation Plan

### Slice 1: Build Program Scaffold

- add `UGuiBuildProgram`, `UGuiBuildStep`, and `UGuiBuildCheckpoint`
- add a deterministic `UGuiBuildProgramPlanner`
- classify subtrees into `atom`/`motif`/`component`/`surface`
- emit a serialized build-program artifact for `uGUI`
- add unit tests proving deterministic action order and checkpoints

This slice is intentionally descriptive, not yet search-driven.

### Slice 2: Checkpointed Subtree Verification

- render individual subtree candidates on a fixed virtual canvas
- gate acceptance per checkpoint
- record accepted and rejected candidate metadata

### Slice 3: Candidate Enumeration

- promote layout and metric choices into candidate strategies
- bind refinement actions to concrete candidate generation
- add local rollback from failed parent compositions

### Slice 4: Hierarchical Search

- add DFS with beam pruning
- store accepted checkpoints per subtree
- emit final `uGUI` code from the accepted build program instead of direct one-pass tree emission

## Risks

- a naive candidate generator will explode combinatorially
- capture nondeterminism can corrupt acceptance decisions
- subtree boundaries chosen too early may lock in bad structure
- the replayable program can become an extra artifact with no control effect if code emission never consumes it

## Open Questions

- should stage classification remain heuristic or become part of structural synthesis output
- should rollback be represented by immutable snapshots only, or also by explicit inverse actions
- should `uGUI` build programs be emitted for all `uGUI` generations or only fidelity lanes
- how much of the existing generator rule system should be lifted into build-program candidates versus retained as post-planning policy

## Acceptance Criteria

This RFC is meaningfully implemented when:

- the compiler emits a deterministic replayable build-program artifact for `uGUI`
- subtree checkpoints are explicit and stable
- a first fixture can be reasoned about in terms of accepted checkpointed subtrees
- future search work can build on the same artifact instead of inventing a parallel planner
