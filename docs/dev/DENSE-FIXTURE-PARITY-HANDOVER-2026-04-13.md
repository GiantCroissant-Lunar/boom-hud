# Dense Fixture Parity Handover

## Status

This handover covers the dense external-reference fixture pass completed on `2026-04-13`.

The work split into four durable areas:

- new `.pen` fixtures based on dense game-interface references
- reference-quality measurement scripts for `.pen` versus source screenshots
- dense fixture parity wiring for UI Toolkit and `uGUI`
- `uGUI` text-generation hardening so TMP-backed output can run in Unity batch capture without crashing

The accepted source changes live in:

- `samples/pencil/`
- `scripts/`
- `fidelity/reference-ui-masks.json`
- `dotnet/src/BoomHud.Gen.UGui/`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/UGuiGeneratorTests.cs`

The `build/` tree is now git-ignored and should remain local-only.

## Fixture Set

### Dense fixtures selected for parity

- `samples/pencil/cyberpunk-crafting.pen`
- `samples/pencil/the-alters-crafting.pen`
- `samples/pencil/fortnite-inventory.pen`

These were chosen because they occupy more than `50%` of the screen and stress broad layout behavior rather than isolated widgets.

### Additional recreated fixtures kept in repo

- `samples/pencil/genshin-quests.pen`
- `samples/pencil/ruiner-skill-menu.pen`
- `samples/pencil/stardew-journal.pen`
- `samples/pencil/the-alters-task-log.pen`

`RuinerSkillMenu` was intentionally dropped from the dense parity set because its measured UI coverage is too low for the dense-screen benchmark, but the recreated `.pen` remains useful as a medium-complexity case.

## Measurement Tooling

### Scripts added

- `scripts/render-pen-fixture-ref.ps1`
- `scripts/measure-pen-ui-complexity.ps1`
- `scripts/measure-pen-reference-similarity.ps1`
- `scripts/build-reference-similarity-leaderboard.ps1`

### Mask manifest

- `fidelity/reference-ui-masks.json`

The measurement model now separates:

- full-screen similarity
- UI-only similarity
- on-screen UI coverage
- structural complexity band

This avoids punishing overlay-heavy screenshots just because the gameplay background is not recreated in `.pen`.

## Reference Recreation Metrics

Primary recreation metric is now `uiOnly` when available.

Current dense fixture source-reference results:

- `CyberpunkCrafting`: full-screen `44.19%`, UI-only `54.33%`
- `TheAltersCrafting`: full-screen `41.96%`, UI-only `58.33%`
- `FortniteInventory`: full-screen `17.70%`, UI-only `53.78%`

Current dense fixture coverage results from the recreated `.pen` files:

- `CyberpunkCrafting`: `74.35%`
- `TheAltersCrafting`: `92.02%`
- `FortniteInventory`: `98.59%`

Interpretation:

- the recreated dense fixtures are sufficiently large to stress generator layout policy
- `FortniteInventory` looks artificially weak on full-screen similarity because the screenshot contains a large amount of live scene/background content
- UI-only similarity is the metric to use for fixture recreation quality

## Dense Backend Parity

Latest parity run:

- `build/fixture-rule-sweeps/dense-reference-parity-2026-04-13-r3/leaderboard.json`

Static fixture scores from the `dense-reference-noop.rules` lane:

- `CyberpunkCrafting`: UI Toolkit `83.12%`, `uGUI` `82.77%`
- `TheAltersCrafting`: UI Toolkit `87.90%`, `uGUI` `81.90%`
- `FortniteInventory`: UI Toolkit `83.03%`, `uGUI` `82.46%`

The current conclusion is:

- dense-screen parity is broadly close between backends
- the only material backend gap in this lane is `TheAltersCrafting`
- that remaining gap is still classified as text/icon policy drift, not structural layout failure

## `uGUI` TMP Hardening

### What changed

`uGUI` text generation now emits TMP-backed text controls instead of legacy `UnityEngine.UI.Text` for labels, badges, and icons.

Key files:

- `dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs`
- `dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.Motion.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/UGuiGeneratorTests.cs`

### Why it was necessary

The first TMP migration attempt caused Unity batch capture to crash because generated code assumed:

- `Resources.Load<TMP_FontAsset>(...)` would always resolve a stored TMP asset
- `TMP_Settings.defaultFontAsset` would exist in batch mode

Neither assumption held in the sample project.

### Current runtime behavior

Generated helpers now:

- prefer stored TMP font assets when they resolve
- fall back to creating TMP font assets from source `.ttf` resources
- cache runtime-created TMP font assets by resource path
- only try TMP default settings behind a null-safe guard
- never throw during text control creation just because a fallback font is unavailable

This fixed the Unity batch capture failure and allowed the dense parity sweep to complete successfully.

### Important limitation

The TMP migration fixed runtime stability, but it did **not** improve dense static `uGUI` fidelity by itself.

The remaining `uGUI` gap is still in:

- font metrics
- wrap behavior
- icon optical centering
- right-edge text treatment in `TheAltersCrafting`

It is not a shell/layout or capture-bootstrap problem anymore.

## Sweep Runner Changes

`scripts/run-fixture-rule-sweep.ps1` was generalized so the compare lane can operate on manifest-defined fixtures instead of the original three hardcoded fixtures.

Important changes:

- manifest-driven fixture generation
- root-name-aware generation for arbitrary fixture manifests
- added dense fixture reference mappings
- removed unsupported `ConvertFrom-Json -Depth` usage in this PowerShell host
- motion-summary averaging hardened so the sweep completes instead of failing at the leaderboard step

## Verification

Verified locally:

- `dotnet test dotnet/tests/BoomHud.Tests.Unit/BoomHud.Tests.Unit.csproj -p:UseSharedCompilation=false`
  - result: `421` passed, `0` failed
- dense reference parity sweep completed successfully
  - output root: `build/fixture-rule-sweeps/dense-reference-parity-2026-04-13-r3`

## Local-Only Artifacts Left Out Of Git

These should stay local and are intentionally not part of the repo history:

- `build/`
- `samples/build/`
- `remotion/build/`
- Unity scene churn under `samples/UnityFullPenCompare/Assets/BoomHudCompare/Scenes/`
- local screenshot and timeline scratch outputs

## Recommended Next Pass

Priority order:

1. Target `TheAltersCrafting` `uGUI` text/icon metrics, especially the right-side column and button/prompt typography.
2. Add fixture-specific visual inspection for the dense lane before introducing any new generator-wide heuristic.
3. Keep using UI-only reference similarity as the primary fixture recreation metric.
4. If dense parity remains stable, move the dense compare manifests out of `build/` into a tracked location so they are easier to reuse in CI.
