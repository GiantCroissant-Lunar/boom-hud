# Fidelity Corpus Policy

## Purpose

BoomHud needs a repeatable quality floor for `pen -> IR -> backend` generation, not one-off manual fixes against a single HUD.

The immediate objective is:

- every current component fixture in the active corpus must score at least `80%`
- every new `.pen` fixture added for converter validation must join that corpus with an explicit fidelity manifest
- once the corpus is stable at `80%`, the floor can be raised to `85%`, then `90%+`

## Current Phase

The active enforced gate is component-first, not full-screen-first.

That means we currently optimize for:

- isolated component parity against `.pen`
- repeatable generation for Unity UI Toolkit and Remotion
- converter correctness across multiple `.pen` fixtures

Whole-screen parity is still useful, but it is not yet a safe global gate because screen-level noise is still higher than component-level noise.

## Corpus Rules

Every corpus manifest must:

- declare its `sourcePen`
- render the same surfaces from Pen, Remotion, and Unity when those paths exist
- set a threshold appropriate to the current phase
- write artifacts to a dedicated output folder under `build/fidelity/`

The first enforced corpus manifest is:

- [fullpen-components.json](/C:/lunar-horse/plate-projects/boom-hud/fidelity/corpus/fullpen-components.json)

## Fixture Rules

When adding a new `.pen` verification fixture:

- keep it focused on one component family or one layout behavior
- prefer reusable component refs when the design is intended to be built from smaller parts
- add at least one fidelity manifest entry for the new fixture
- keep sample values deterministic so screenshots are comparable

Good fixture categories:

- reusable component composition
- absolute vs flow placement
- `fill_container` behavior in horizontal and vertical parents
- icon rendering
- borders, padding, and typography edge cases

## Composition Requirement

The architectural target is that larger components are built from smaller reusable components.

Today, the IR already preserves reusable reference intent through `ComponentRefId`, but Unity compose helpers are not implemented yet. That means:

- composition intent must be preserved in IR and covered by tests now
- backend-side emitted composition is still a follow-up item for Unity and future uGUI work

Do not hide this gap by flattening the requirement in docs. Treat it as an explicit backlog item.

## Commands

- Run one manifest: [run-pen-remotion-unity-fidelity.ps1](/C:/lunar-horse/plate-projects/boom-hud/scripts/run-pen-remotion-unity-fidelity.ps1)
- Run the current corpus: [run-fidelity-corpus.ps1](/C:/lunar-horse/plate-projects/boom-hud/scripts/run-fidelity-corpus.ps1)
- Task wrapper: `task verify:fidelity:corpus`

## Exit Criteria For Raising The Floor

Raise the floor from `80%` only when:

- the whole active corpus passes consistently
- the failing diffs are micro-adjustment issues instead of structural mismatches
- new fixture onboarding is routine instead of manual firefighting
