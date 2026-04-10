# Pencil-to-Unity Layout Fidelity Implementation

## Status (2026-04-10)

- Review completed on the current Pen-to-IR and Unity generator flow.
- Priority: focus first on layout intent fidelity (squish/absolute behavior) before parity checks with Remotion and Godot.

## What is working today

- IR-based pipeline is in place (`.pen` -> converter -> IR -> backend generators).
- Unity sample host loads generated stylesheets and applies them at runtime.
- `layout: none`/`absolute` path is represented by `LayoutType.Absolute` in converter.

## High-confidence issues to fix (current pass)

1. Absolute metadata leak into non-absolute flow
- Legacy position metadata (`x`, `y`) should not influence layout unless node has explicit absolute intent.
- Unclear intent currently made coordinate metadata available in non-absolute contexts and contributed to squish in generated Unity layout.

2. Absolute intent ambiguity from converter metadata
- `LayoutType.Absolute` was not always carried into the backend absolute-placement flag in a canonical way.
- This created inconsistent behavior, especially when layout was specified as string mode `absolute`.

3. Fallback-to-`Vertical` without visibility
- Unsupported/missing layout mode currently maps to `LayoutType.Vertical` silently.
- That masks authoring intent and makes Unity diffs hard to diagnose.

## Step 1 (in progress)

- [x] Add converter-level canonical absolute intent (`IsAbsolutePositioned = layout.Type == LayoutType.Absolute`).
- [x] Fix Unity absolute placement path to treat `LayoutType.Absolute` as explicit absolute positioning and avoid using numeric coordinates unless explicit absolute context exists.
- [x] Add explicit diagnostic when fallback-to-`Vertical` is applied on container nodes.
- [x] Restrict coordinate metadata (`x`, `y`) and resolved offsets to explicit absolute nodes only; add warning for legacy `x/y` without explicit absolute intent.
- [x] Normalize React absolute placement style handling to remove dead branch and consistently default missing absolute offsets to `0px` when absolute intent is explicit.
- [x] Implement absolute placement emission in Godot layout setup (`Anchor`/`Offset` for `Left`/`Top`).

## Step 2 (next)

- Add targeted compare fixtures for a compact set of `.pen` nodes:
  - `frame` with no `layout.mode`
  - `group` and `absolute` variants
  - `fill`/`fill_container` children under mixed parent layouts
  - nested legacy `x/y` in non-absolute contexts
- Add a small Unity-vs-Remotion parity check (layout-only) to catch layout intent drift.

## Step 3 (later)

- Normalize dimension source precedence (`top-level width/height` vs nested `layout` dimensions) into a documented rule.
- Add missing backend alignment handling for mixed `position=absolute` + missing coordinates.

## Remaining known gaps

- Dedicated Remotion backend project now exists under `dotnet/src/BoomHud.Gen.Remotion`, but it still needs parity tuning against `.pen` layouts once fixture coverage is added.
- Godot currently emits only `left/top` absolute offsets (no `right/bottom` emission path yet). This is sufficient for the observed squish case but should be expanded if pen files start using full anchored offsets.

## Notes

- Keep defaulting behavior unchanged for now to reduce blast radius; warnings should be added before changing default layout semantics.
