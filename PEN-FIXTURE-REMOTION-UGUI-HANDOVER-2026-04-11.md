# PEN Fixture / Remotion / uGUI Handover

This handover covers the canonical rule-selector work, the React/Remotion rule-consumption path, the uGUI content-hugging and alignment emission changes, and the Unity 6 fixture-harness investigation that moved the remaining problem from planner expressiveness into capture/layout contract behavior.

## Scope completed

- Added general selector surfaces for planned rules:
  - `semanticClass`
  - `fontFamily`
  - `textGrowth`
  - `sizeBand`
- Added semantic roles now used by planned rules:
  - `pixel-text`
  - `icon-glyph`
  - `icon-shell`
  - `heading-label`
  - `stacked-text-line`
  - `stacked-text-group`
- Threaded canonical rule resolution into the React/Remotion generation path so Remotion is no longer bypassing the planner/rule stack.
- Added general uGUI materialization for:
  - `preferContentHeight`
  - `flexAlignmentPreset`
  - icon/text metric deltas already resolved through policy helpers
- Added Unity 6 fixture-capture support for per-surface capture dimensions on uGUI surfaces, sourced from the reference PNG dimensions during the sweep.

## Main code changes

### Planner / rule stack

- [GeneratorRuleSet.cs](C:\lunar-horse\plate-projects\boom-hud\dotnet\src\BoomHud.Abstractions\Generation\GeneratorRuleSet.cs)
- [RuleResolver.cs](C:\lunar-horse\plate-projects\boom-hud\dotnet\src\BoomHud.Generators\RuleResolver.cs)
- [GeneratorRulePlanner.cs](C:\lunar-horse\plate-projects\boom-hud\dotnet\src\BoomHud.Generators\GeneratorRulePlanner.cs)
- [GeneratorRuleSetTests.cs](C:\lunar-horse\plate-projects\boom-hud\dotnet\tests\BoomHud.Tests.Unit\Generation\GeneratorRuleSetTests.cs)
- [GeneratorRulePlannerTests.cs](C:\lunar-horse\plate-projects\boom-hud\dotnet\tests\BoomHud.Tests.Unit\Generation\GeneratorRulePlannerTests.cs)

Key point: selector expressiveness is no longer the main blocker for quest-sidebar. The planner is selecting the intended general roles correctly.

### React / Remotion

- [ReactGenerator.cs](C:\lunar-horse\plate-projects\boom-hud\dotnet\src\BoomHud.Gen.React\ReactGenerator.cs)
- [ReactGeneratorTests.cs](C:\lunar-horse\plate-projects\boom-hud\dotnet\tests\BoomHud.Tests.Unit\Generation\ReactGeneratorTests.cs)
- [FontReadyGate.tsx](C:\lunar-horse\plate-projects\boom-hud\remotion\src\FontReadyGate.tsx)
- [run-fixture-remotion-rule-sweep.ps1](C:\lunar-horse\plate-projects\boom-hud\scripts\run-fixture-remotion-rule-sweep.ps1)
- [quest-sidebar.remotion.catalog.json](C:\lunar-horse\plate-projects\boom-hud\samples\rules\quest-sidebar.remotion.catalog.json)

Key point: Remotion is now a fast proving ground for the same canonical planning layer. It is no longer measuring a parallel ad hoc React path.

### uGUI generator / sample outputs

- [UGuiGenerator.cs](C:\lunar-horse\plate-projects\boom-hud\dotnet\src\BoomHud.Gen.UGui\UGuiGenerator.cs)
- [UGuiGeneratorTests.cs](C:\lunar-horse\plate-projects\boom-hud\dotnet\tests\BoomHud.Tests.Unit\Generation\UGuiGeneratorTests.cs)
- [UnityGenerator.cs](C:\lunar-horse\plate-projects\boom-hud\dotnet\src\BoomHud.Gen.Unity\UnityGenerator.cs)
- [UnityBackendPlanner.cs](C:\lunar-horse\plate-projects\boom-hud\dotnet\src\BoomHud.Gen.Unity\UnityBackendPlanner.cs)
- [quest-sidebar-ugui-content-hug.catalog.json](C:\lunar-horse\plate-projects\boom-hud\samples\rules\quest-sidebar-ugui-content-hug.catalog.json)
- [quest-sidebar.catalog.json](C:\lunar-horse\plate-projects\boom-hud\samples\rules\quest-sidebar.catalog.json)

Key point: uGUI now emits the intended content-hugging and layout-group alignment behavior. That backend work was a real gain. Small semantic rule deltas are comparatively low leverage now.

### Unity 6 fixture harness / capture contract

- [BoomHudUGuiHost.cs](C:\lunar-horse\plate-projects\boom-hud\samples\UnityFullPenCompare\Assets\BoomHudCompare\Scripts\BoomHudUGuiHost.cs)
- [FixtureUGuiPresenter.cs](C:\lunar-horse\plate-projects\boom-hud\samples\UnityFullPenCompare\Assets\BoomHudCompare\Scripts\FixtureUGuiPresenter.cs)
- [BoomHudFidelityCapture.cs](C:\lunar-horse\plate-projects\boom-hud\samples\UnityFullPenCompare\Assets\Editor\BoomHudFidelityCapture.cs)
- [run-fixture-rule-sweep.ps1](C:\lunar-horse\plate-projects\boom-hud\scripts\run-fixture-rule-sweep.ps1)

Key point: the remaining uGUI mismatch was partly a harness problem. The sweep was rendering all Unity captures through a fixed `1920x1080` path even when the fixture references were `840x1920`, `2560x640`, or `1920x1440`. The current script now stamps uGUI surfaces with reference dimensions and the Unity editor capture consumes those dimensions for the uGUI capture path.

## Latest verified results

Reference lane to treat as current truth:

- sweep output:
  - [leaderboard.json](C:\lunar-horse\plate-projects\boom-hud\build\fixture-rule-sweeps\quest-sidebar-ugui-content-hug-unity6-v8\leaderboard.json)
- Unity editor:
  - `C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe`
- Unity project:
  - `C:\lunar-horse\plate-projects\boom-hud\build\UnityFullPenCompareSweep`

Important numbers from `v8`:

- `quest-sidebar-uitk`: `79.23`
- `quest-sidebar-ugui`: baseline `70.67`
- `quest-sidebar-ugui`: candidate catalog `70.94`
- average overall similarity:
  - baseline `71.4467`
  - candidate `71.4917`
  - delta `+0.045`

Interpretation:

- The per-surface uGUI capture-size contract is a real improvement.
- The uGUI semantic catalog still helps only slightly.
- The remaining quest-sidebar uGUI gap is still mostly left-edge pressure and text/icon metrics.
- `party-status-strip-ugui` is now the main warning sign. The generalized uGUI capture contract improved quest-sidebar and combat-toast-stack, but `party-status-strip-ugui` still sits low (`53.70`) and likely needs a surface-specific or axis-aware capture/layout treatment rather than more quest-sidebar tuning.

## Recommended next steps

1. Keep Unity 6 as the only verification lane.
2. Do not add more quest-sidebar-specific planner rules right now.
3. Add explicit manifest-level capture policy for uGUI surfaces instead of relying only on inferred reference dimensions.
4. Separate “screen-like full-frame fixtures” from “narrow strip / panel fixtures” in the uGUI capture contract.
5. After that, revisit uGUI left-edge alignment and text/icon metrics with the current semantic selectors unchanged.

## Verification commands

Focused .NET tests:

```powershell
dotnet test C:\lunar-horse\plate-projects\boom-hud\dotnet\tests\BoomHud.Tests.Unit\BoomHud.Tests.Unit.csproj -p:UseSharedCompilation=false --filter "FullyQualifiedName~GeneratorRuleSetTests|FullyQualifiedName~GeneratorRulePlannerTests|FullyQualifiedName~ReactGeneratorTests|FullyQualifiedName~UGuiGeneratorTests"
```

Remotion TypeScript check:

```powershell
Set-Location C:\lunar-horse\plate-projects\boom-hud\remotion
npx tsc --noEmit
```

Unity 6 sweep:

```powershell
dotnet run --project C:\lunar-horse\plate-projects\boom-hud\dotnet\src\BoomHud.Cli\BoomHud.Cli.csproj -- rules sweep --rules-glob C:\lunar-horse\plate-projects\boom-hud\samples\rules\quest-sidebar-ugui-content-hug.catalog.json --unity-project C:\lunar-horse\plate-projects\boom-hud\build\UnityFullPenCompareSweep --unity-exe "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe" --out C:\lunar-horse\plate-projects\boom-hud\build\fixture-rule-sweeps\quest-sidebar-ugui-content-hug-unity6-v8
```

## Known issues

- The Unity batch run still ends with the same late `CaptureFromCommandLine` exception after artifacts are written.
- The localhost image `404` lines still appear during the sweep, but the leaderboard and scored outputs are still produced.
- The current uGUI capture contract is better, not final.

## Intentionally uncommitted folders

These are artifact-heavy and should stay out of source control unless there is a deliberate packaging step:

- `build/`
- `remotion/build/`
- `samples/build/`
- `samples/UnityFullPenCompare/Assets/Screenshots/`
- local tooling scratch folders such as `.playwright-mcp/`

One exception to review deliberately: [press-start-2p-latin-400-normal.woff2](C:\lunar-horse\plate-projects\boom-hud\remotion\public\fonts\press-start-2p-latin-400-normal.woff2) is a real runtime asset rather than a generated artifact and should be committed with the Remotion rule-path work.
