# RFC-0010: Figma Component Reuse (COMPONENT / INSTANCE)

- **Status**: Accepted
- **Created**: 2025-12-12
- **Authors**: BoomHud Contributors

## Summary

Support reusable HUD components driven by Figma `COMPONENT` and `INSTANCE` nodes by:

- Extracting component definitions from Figma documents
- Generating reusable view classes per component
- Supporting instance overrides and variant properties

## Motivation

Current BoomHud generation produces monolithic views, which makes reuse and maintenance difficult. Figma already models reusable elements via components and instances; BoomHud should reflect that structure in generated output.

## Goals

- Represent Figma components as reusable generated UI types
- Allow instances to override text/value/props where possible
- Keep component names stable and deterministic

## Non-Goals

- Full fidelity reproduction of all Figma component property types
- Cross-file published library component resolution (initially)

## Design

### Figma Inputs

We rely on the Figma REST export shape:

- `FigmaNode.Type` in `{ COMPONENT, INSTANCE }`
- `FigmaNode.ComponentId`
- `FigmaNode.ComponentProperties` / `Overrides` / `VariantProperties` (when present)

### IR Representation

Introduce a component library concept in IR:

- `HudDocument` (or a new `HudProject`) contains:
  - `Root` (main tree)
  - `Components` (map of componentId/name → component root node)

Instances reference a component definition and carry:

- overrides (e.g. text)
- variant selection

### Code Generation

#### File Output

Generate one output file per reusable component plus the root view:

- `SearchInputView.g.cs`
- `UserMenuView.g.cs`
- `AppMainFrameView.g.cs`

#### Instance Expansion vs Composition

Prefer composition:

- Root view instantiates component view types
- Overrides are applied via properties / parameters where feasible

Fallback: if a backend cannot represent a component cleanly, it may expand the component inline (policy-controlled).

### Override Handling

Initial supported override types:

- text overrides
- simple value overrides

Unsupported overrides produce diagnostics.

## Backward Compatibility

- If no Figma components are present, generation remains monolithic.

## Alternatives Considered

- Always inline/expand instances (rejected: defeats reuse)

## Open Questions

- How should overrides map to strongly typed properties vs a generic dictionary?
- How should component parameters be surfaced in generated code across backends?

## Related RFCs

- RFC-0002: Component Model
- RFC-0008: Dual Generation Architecture
- RFC-0009: Runtime Theming
