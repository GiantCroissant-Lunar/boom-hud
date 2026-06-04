# RFC-0025: Runtime Agent Surfaces

- **Status**: Draft
- **Created**: 2026-06-04
- **Authors**: BoomHud Contributors

## Summary

Add a small runtime surface contract for agent-authored UI payloads. Agents emit a trusted-catalog component tree plus a data model. A host such as `orc-bot` mounts a resident renderer once and updates surfaces from JSON at runtime instead of rebuilding a Godot bundle for each result.

## Existing Surfaces Audited

- `BoomHud.Cli` and the backend generators already own design-source parsing and build-time output. Runtime agent surfaces do not replace `.pen`/Figma ingestion or generated Godot scenes.
- `BoomHud.Abstractions` already owns shared IR and contracts. The runtime surface contract lives there as `BoomHud.Abstractions.Runtime` so it can be consumed without referencing `BoomHud.Cli`.
- `BoomHud.Gen.Godot` already owns generated Godot output. A future resident renderer should reuse its component mapping decisions where practical, not fork a second component vocabulary.
- `orc-bot` already owns the local agent bridge, task events, and bundle host. It should mount a renderer and forward validated payloads into it, not introduce a new transport.

## Motivation

Static product UI is well served by the existing build-time path:

```text
Pencil/Figma/IR -> BoomHud CLI -> generated Godot scene/C# -> app bundle
```

Dynamic per-result UI is different. Rebuilding a Godot PCK for every agent result is too heavy, and direct `.pen` parsing in the app would duplicate BoomHud's parser and backend responsibility. The runtime path should be:

```text
agent JSON -> BoomHud runtime surface DTO -> resident Godot renderer -> mounted host UI
```

## Goals

- Define a stable JSON DTO for runtime surfaces.
- Require a `catalogId` so the renderer can reject unknown component vocabularies.
- Keep state as a separate data model addressed by JSON Pointer bindings.
- Support action descriptors that route back to the host's existing command/task surface.
- Keep the contract independent of Godot so non-Godot renderers can exist later.

## Non-Goals

- Runtime `.pen` parsing.
- Runtime Godot scene or C# code generation.
- A new HTTP, WebSocket, or agent bridge.
- Arbitrary component execution from agent output.

## Design

`BoomHud.Abstractions.Runtime` defines:

- `RuntimeSurfaceDocument`: `surfaceId`, `catalogId`, component root, optional data model, metadata.
- `RuntimeComponentNode`: stable component id, catalog component type, layout, properties, bindings, actions, children.
- `RuntimeBinding`: JSON Pointer path into the data model.
- `RuntimeDataModelUpdate`: incremental data model mutation envelope.
- `RuntimeSurfaceCatalog`: trusted component/property/action allowlist.
- `RuntimeSurfaceValidator`: pre-render validation for ids, catalog match, allowed properties, allowed events, JSON Pointer paths, node count, and depth.

The first catalog is intentionally small: containers, panels, labels, badges, buttons, progress bars, lists, and spacers. The first Godot renderer should render only this catalog, then expand by adding catalog entries and tests.

## Backward Compatibility

This is additive. Existing build-time documents, generators, and CLI flows are unchanged.

## Security / Performance Considerations

The host must validate every agent payload against a trusted catalog before rendering. The validator also bounds node count and tree depth so malformed agent output cannot build an unbounded UI tree.

Actions are descriptors, not executable code. The host maps allowed commands to its existing command/task surface.

## Alternatives Considered

- Direct `.pen` JSON rendering in `orc-bot`: rejected because it duplicates BoomHud's design parser and backend mapping.
- Per-result generated Godot bundles: rejected for dynamic result UI because it adds build/export latency and bundle churn.
- A new agent/UI server: rejected because `orc-bot` already owns bridge and task surfaces.

## Related RFCs

- RFC-0001: Core Architecture
- RFC-0013: Native Godot Scene Backend
- RFC-0024: Spatial (3D) Layout Dimension
