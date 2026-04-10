# Pen Fixture Fidelity Handover

## Status (2026-04-10)

This note captures the current state of the fixture-based `.pen` to Unity fidelity loop before the next round of generator work.

Current compare fixtures:

- `samples/pencil/party-status-strip.pen`
- `samples/pencil/quest-sidebar.pen`
- `samples/pencil/combat-toast-stack.pen`

Current compare sample host:

- `samples/UnityFullPenCompare`

Current capture outputs:

- `build/fixture-refs/*`
- `build/fixture-captures/*`
- `build/fixture-scores/*`

## Current behavior

The compare loop now has a reliable enough capture path to iterate on both backends against the new fixture set.

Working improvements already in place:

- generic fixture compare scenes exist for both UI Toolkit and uGUI
- UI Toolkit capture now forces panel repaint before crop
- uGUI capture now uses camera render target cropping instead of unstable screen capture
- UI Toolkit generator now emits layout-critical static assignments in generated controller code
- Pencil `textGrowth` metadata is carried into UI IR and used to decide wrapping behavior for generated labels

This means the remaining major differences are no longer dominated by a broken screenshot pipeline. They are mostly generator and text/icon fidelity problems.

## Score snapshot

Raw pen-vs-current scores from the latest fixture run:

- `PartyStatusStrip`: UI Toolkit `27.48%`, uGUI `33.56%`
- `QuestSidebar`: UI Toolkit `25.41%`, uGUI `32.51%`
- `CombatToastStack`: UI Toolkit `28.47%`, uGUI `39.29%`

Important caveat:

- these are raw pixel scores
- the current reports still show dimension mismatch
- use them for trend tracking only, not as absolute fidelity quality

To make the score more trustworthy, the compare pipeline should move to one of these:

1. canonical full-canvas capture for both reference and generated outputs
2. normalized scoring after candidate-to-reference rescale/crop alignment
3. hybrid reporting that separates layout, text, icon, and pixel similarity

## Recurring mismatch patterns

The same categories are showing up across fixtures:

### 1. Text metrics mismatch

- generated font size feels too large or too small compared with the `.pen`
- line height does not match the design intent
- text width measurement differs enough to move line breaks

This is why labels wrap differently, body text occupies a different height, and some rows feel visually off even when the container bounds are close.

### 2. Wrap policy mismatch

- some nodes should stay single-line but wrap
- some nodes should wrap but are treated like fixed-width single-line labels

Carrying `textGrowth` into metadata was the first required step, but it is not sufficient by itself. The generator also needs explicit line-height and wrap-mode policy.

### 3. Icon optical alignment mismatch

- icons are box-centered but not visually centered
- glyph baseline and side bearing differences make them look off-center even when the layout math is correct

This should be treated as an icon metrics problem, not only a container alignment problem.

### 4. Backend-specific fill/stretch mismatch

- nested `fill` or `fill_container` nodes do not always expand the same way across UI Toolkit and uGUI
- absolute children mixed into flow layouts still require careful backend-specific handling

Recent Unity generator fixes improved this significantly, but the logic is still too scattered.

### 5. Stylesheet/runtime split mismatch

- some generated UI Toolkit `.uss` assets import with zero effective rules in Unity
- the current working path relies on generated runtime layout assignments in `.gen.cs`

This is good enough to keep moving, but it is not a stable long-term architecture.

## What the next reporting pass should add

Pixel score alone is not enough. The compare output should also emit structured findings per fixture.

Recommended finding categories:

- `text-metrics-mismatch`
- `wrap-mismatch`
- `icon-centering-mismatch`
- `absolute-offset-mismatch`
- `fill-stretch-mismatch`
- `container-padding-gap-mismatch`
- `style-import-mismatch`

Each finding should record:

- fixture id
- backend (`uitk` or `ugui`)
- target node or region if available
- short description
- probable fix area
- suggested owning subsystem

This will let us detect repeated failure patterns and stop treating every screenshot diff as a unique problem.

## Refactor direction

The current generator path is still too prototype-heavy. The immediate goal should be to split translation and emission responsibilities into smaller typed modules.

Recommended extraction targets:

- `PenProjectReader`
- `PenFrameExtractor`
- `UiMetadataNormalizer`
- `LayoutTranslationService`
- `TextStyleTranslationService`
- `IconStyleTranslationService`
- `UxmlEmitter`
- `UssEmitter`
- `UiToolkitRuntimeEmitter`
- `UGuiRuntimeEmitter`
- `FidelityScoringService`
- `FidelityFindingExtractor`

Primary monolith hotspots today:

- `dotnet/src/BoomHud.Dsl.Pencil/PenToIrConverter.cs`
- `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`
- `dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs`

The immediate split to prioritize is:

1. move layout translation logic out of backend generator monoliths
2. move text/icon policy into explicit services
3. make scoring and findings a first-class compare output instead of ad hoc scripts/artifacts

## Pen project and metadata model

Folders should organize related UI, but folders alone should not define semantics.

Recommended interpretation:

- folder = project grouping or feature grouping
- top-level frame = candidate screen/state/variant/keyframe
- metadata = authoritative semantic meaning

UI IR should record UI structure and screen/state relationships.

Motion IR should record motion-specific semantics such as sequence membership and keyframe ordering.

Recommended metadata keys to normalize into IR:

- `screenId`
- `flowId`
- `stateId`
- `variantGroup`
- `sequenceId`
- `keyframeIndex`
- `transitionId`
- `lineHeight`
- `wrapMode`
- `iconBaselineOffset`

This is the path that will let a single `.pen` file contain:

- multiple related screens
- several frames representing a flow
- keyframes for animation states
- variant sets for the same UI

without forcing the backend generators to guess intent from folder structure or frame names alone.

## Recommended implementation order

1. Add structured fidelity findings to the compare pipeline.
2. Canonicalize score normalization so the percentage becomes more meaningful.
3. Extract text metrics and icon alignment policy from the Unity/uGUI generators.
4. Extract layout translation into dedicated services shared by the generators where possible.
5. Extend Pencil-to-IR metadata normalization for multi-frame and motion-aware authoring.
6. Define how UI IR and Motion IR share frame identity and node identity across a `.pen` project.

## Exit condition for the next pass

Do not consider the fixture compare system healthy until it can answer both of these:

1. What is the score?
2. What specific categories caused the mismatch, and where should they be fixed?
