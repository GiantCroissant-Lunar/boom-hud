# Pen Fixture GOAP + uGUI Fidelity Handover

Date: 2026-04-11
Branch: `main`
Remote: `origin`

## Scope

This handover covers three connected workstreams:

1. Shared declarative rule infrastructure for generator planning and execution across UIToolkit, uGUI, and Motion/Timeline.
2. Unity-side fixture compare harness repairs so uGUI fidelity scores are trustworthy.
3. A first size-delta rule experiment for `quest-sidebar.pen` after the harness repair.

The current system is now:

`Pen -> IR / Motion IR -> planned action chain -> deterministic backend emit`

The planner selects and orders bounded actions. The generators stay deterministic and phase-ordered.

## Delivered

### 1. Shared planning and rule infrastructure

Implemented a backend-agnostic rule/action layer with selectors, phases, costs, preconditions, effects, and executable planned rulesets.

Key files:

- `dotnet/src/BoomHud.Abstractions/Generation/GeneratorRuleSet.cs`
- `dotnet/src/BoomHud.Abstractions/Generation/GeneratorRulePlan.cs`
- `dotnet/src/BoomHud.Generators/GeneratorRuleExecutionCompiler.cs`
- `dotnet/src/BoomHud.Generators/GeneratorRulePlanner.cs`
- `dotnet/src/BoomHud.Generators/RuleResolver.cs`
- `dotnet/src/BoomHud.Generators/MotionPolicyService.cs`

Notable behavior:

- Rule selectors support backend, document, normalized IR node id, original `.pen` id, component type, metadata, and motion selectors.
- The planner emits:
  - plan summary JSON
  - executable planned ruleset JSON
- Rules are applied by phase before emission rather than as scattered backend conditionals.

### 2. CLI and manifest plumbing

Added planning/sweep workflow support to the CLI and compose manifest path resolution.

Key files:

- `dotnet/src/BoomHud.Cli/Program.cs`
- `dotnet/src/BoomHud.Cli/Commands/Rules/RulesPlanCommand.cs`
- `dotnet/src/BoomHud.Cli/Commands/Rules/RulesSweepCommand.cs`
- `dotnet/src/BoomHud.Abstractions/Composition/ComposeManifest.cs`
- `dotnet/src/BoomHud.Abstractions/Composition/Generated/ComposeManifestDto.g.cs`

Added support for:

- `boom generate --rules <path>`
- `boom rules plan`
- `boom rules sweep`
- compose manifest `rules` entry

### 3. Unity package rule authoring mirror

Added Unity editor authoring support for the canonical JSON ruleset.

Key files:

- `unity-packages/com.boomhud.unity/Editor/BoomHudGenerationRuleSetAsset.cs`
- `unity-packages/com.boomhud.unity/Editor/BoomHudGenerationRuleSetUtility.cs`
- `unity-packages/com.boomhud.unity/Editor/BoomHudProjectSettings.cs`
- `unity-packages/com.boomhud.unity/Editor/BoomHudUnityPackageMenu.cs`

The Unity asset mirrors JSON and supports import/export. JSON remains the source of truth.

### 4. Backend integration

Both UI backends and the motion export path now consume resolved planned actions.

Key files:

- `dotnet/src/BoomHud.Gen.Unity/UnityBackendPlanner.cs`
- `dotnet/src/BoomHud.Gen.Unity/UnityBackendPlan.cs`
- `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`
- `dotnet/src/BoomHud.Gen.Unity/UnityMotionExporter.cs`
- `dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs`
- `dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.Motion.cs`

Implemented action surfaces include:

- text metrics and wrap
- icon metrics
- layout gap, padding, offsets
- anchor/pivot/rect transform presets for uGUI
- position/flex alignment presets for UITK
- motion duration quantization, easing remap, fill/fallback policy

### 5. uGUI fixture compare harness repair

The prior uGUI quest-sidebar scoring was distorted by scaling/fit behavior in the compare harness. The harness is now repaired enough to trust the scores.

Tracked sample changes:

- `samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/BoomHudUGuiHost.cs`
- `samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/FixtureUGuiPresenter.cs`
- `samples/UnityFullPenCompare/Assets/Editor/BoomHudFidelityCapture.cs`
- `samples/UnityFullPenCompare/Assets/BoomHudCompare/Scenes/FixtureCompare.unity`
- `samples/UnityFullPenCompare/Assets/BoomHudCompare/Scenes/FixtureCompareUGui.unity`

Important harness decisions:

- Keep uGUI fixture compare on `CanvasScaler.ConstantPixelSize`.
- Do not reintroduce overlay-forcing in the presenter.
- Do not auto-fit the target root during capture.

This moved the baseline `quest-sidebar-ugui` score from the misleading mid-50s range into a stable `60.28`.

### 6. Size-delta rule surface

Added preferred size deltas so planned rules can change fixed panel/card sizes without backend code edits.

Key files:

- `dotnet/src/BoomHud.Abstractions/Generation/GeneratorRuleSet.cs`
- `dotnet/src/BoomHud.Generators/RuleResolver.cs`
- `dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs`
- `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`

New layout action fields:

- `preferredWidthDelta`
- `preferredHeightDelta`

### 7. Rule catalogs and generated samples

Added and iterated several sample rule catalogs:

- `samples/rules/party-hud-mixed.catalog.json`
- `samples/rules/party-status-strip.catalog.json`
- `samples/rules/combat-toast-stack.catalog.json`
- `samples/rules/quest-sidebar.catalog.json`
- `samples/rules/quest-sidebar-ugui-objectives-tight.catalog.json`
- `samples/rules/quest-sidebar-ugui-text-tight.catalog.json`
- `samples/rules/quest-sidebar-ugui-layout-tight.catalog.json`
- `samples/rules/quest-sidebar-ugui-layout-tighter.catalog.json`

Tracked generated sample outputs were updated in:

- `samples/UnityFullPenCompare/Assets/Resources/BoomHudGenerated/*.gen.cs`
- `samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui/*.ugui.cs`

## Verification

Focused verification that passed:

```powershell
dotnet test dotnet/tests/BoomHud.Tests.Unit/BoomHud.Tests.Unit.csproj --filter "FullyQualifiedName~ComposeManifestTests|FullyQualifiedName~PenParserTests|FullyQualifiedName~GeneratorRuleSetTests|FullyQualifiedName~GeneratorRulePlannerTests|FullyQualifiedName~UGuiGeneratorTests|FullyQualifiedName~UnityGeneratorTests|FullyQualifiedName~UnityMotionExporterTests" -p:UseSharedCompilation=false
```

Result:

- 55 passing tests
- 0 failures

Key test files:

- `dotnet/tests/BoomHud.Tests.Unit/Composition/ComposeManifestTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Dsl/PenParserTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/GeneratorRuleSetTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/GeneratorRulePlannerTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/UGuiGeneratorTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/UnityGeneratorTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Motion/UnityMotionExporterTests.cs`

The fixture sweep script was also exercised repeatedly:

```powershell
pwsh -File scripts/run-fixture-rule-sweep.ps1 -UnityProjectPath build/UnityFullPenCompareSweep -UnityExe "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe" -RuleManifestGlob "samples/rules/quest-sidebar-ugui-*.catalog.json" -OutputRoot build/fixture-rule-sweeps/quest-sidebar-size-tight
```

## Latest sweep result

Primary leaderboard:

- `build/fixture-rule-sweeps/quest-sidebar-size-tight/leaderboard.json`

Baseline after harness repair:

- `quest-sidebar-ugui`: `60.28`
- average overall similarity: `68.7783`

Result of new size-delta catalogs:

- `quest-sidebar-ugui-layout-tight.catalog` -> `quest-sidebar-ugui`: `59.96`
- `quest-sidebar-ugui-layout-tighter.catalog` -> `quest-sidebar-ugui`: `59.85`

Important conclusion:

- The new `preferredHeightDelta` rules definitely landed.
- The planner selected the right actions for:
  - `QSB43` `ObjectiveCard`
  - `QSB63` `ResourceCard`
- But uniform card/row shrinking made fidelity slightly worse.

Useful artifacts:

- baseline capture:
  - `build/fixture-rule-sweeps/quest-sidebar-size-tight/baseline-no-rules/captures/quest-sidebar-ugui.png`
- baseline diff:
  - `build/fixture-rule-sweeps/quest-sidebar-size-tight/baseline-no-rules/scores/quest-sidebar-ugui.diff.png`
- layout-tight capture:
  - `build/fixture-rule-sweeps/quest-sidebar-size-tight/quest-sidebar-ugui-layout-tight.catalog/captures/quest-sidebar-ugui.png`
- layout-tight diff:
  - `build/fixture-rule-sweeps/quest-sidebar-size-tight/quest-sidebar-ugui-layout-tight.catalog/scores/quest-sidebar-ugui.diff.png`
- selected action chain:
  - `build/fixture-rule-sweeps/quest-sidebar-size-tight/quest-sidebar-ugui-layout-tight.catalog/plans/quest-sidebar-ugui-layout-tight.catalog.plan.json`

## Current understanding of the remaining gap

The next missing rule surface is not “more global shrinking.” The remaining uGUI drift looks more like:

1. Per-edge padding or inset control rather than uniform `paddingDelta`.
2. Inner content width/inset control for text stacks and bars.
3. Possibly per-child layout element suppression or different flexible/preferred sizing choices inside the objective and resource cards.
4. More specific anchor/pivot/inset policies for the bottom-heavy mismatch region.

The rule engine is working. The remaining limitation is expressiveness of the layout actions, not the planner wiring.

## Known issues

1. The sweep still ends with a UITK batchmode capture exception after producing artifacts:
   - `InvalidOperationException: Unity UI capture did not produce meaningful content.`
   - This shows up in `Editor.log`, but the sweep still produces captures, scores, and leaderboard output.

2. A few non-source artifact folders remain intentionally uncommitted:
   - `.kilo/`
   - `.playwright-mcp/`
   - `build/`
   - `remotion/build/`
   - `samples/build/`
   - `samples/UnityFullPenCompare/Assets/Screenshots/`
   - `samples/UnityFullPenCompare/ProjectSettings/TimelineSettings.asset`

## Recommended next session

1. Add finer-grained layout actions:
   - per-edge padding
   - per-edge inset
   - preferred size on specific axes with child-specific selectors
   - fill-width / preferred-width overrides for nested text containers

2. Re-run a narrow quest-sidebar uGUI sweep only after adding those new actions.

3. Keep the constant-pixel harness and do not reintroduce overlay forcing or capture-time fitting.

4. If needed, add rules that target the bars and labels separately instead of shrinking the containing cards uniformly.

## Commit stack

Commits created in this session before this handover doc:

1. `8787f71` `feat(generator): add planned rule catalogs for unity fidelity`
2. `aba6afa` `fix(unity): stabilize fixture compare capture for ugui`
3. `acee8ac` `feat(samples): add planned rule catalogs for pen fixtures`

This handover document is committed separately so the next session can start by reading a single file and then jump into the exact commit stack above.
