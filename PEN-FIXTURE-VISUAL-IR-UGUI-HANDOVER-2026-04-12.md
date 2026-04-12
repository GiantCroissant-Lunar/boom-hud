# PEN Fixture / Visual IR / uGUI Handover (2026-04-12)

This handover covers the post-V1/V2/V3 fidelity work that pushed the pipeline from exact synthetic componentization into Visual IR planning, recursive refinement artifacts, measured layout diagnostics, and the latest Unity capture normalization fix.

## Scope completed

- Implemented V2 compiler-only Visual IR foundation:
  - `VisualDocument`
  - `EdgeContract`
  - `MetricProfileDefinition`
  - opt-in `*.visual-ir.json` emission
- Implemented V3 Visual IR planning and refinement scaffolding:
  - `VisualSynthesisPlanner`
  - `VisualRefinementPlanner`
  - `*.visual-synthesis.json`
  - `*.visual-refinement.json`
- Added real recursive scorer integration so `baseline score` can consume a generated Visual IR artifact and emit refinement actions from the actual score tree.
- Added measured-layout export and issue classification for Unity captures:
  - shell drift
  - stretch / preferred-size mismatch
  - transform scale mismatch
- Fixed multiple `uGUI` realization bugs:
  - absolute overlay text can content-hug instead of falling back to `100x100`
  - icon metric baseline adjustment no longer overwrites absolute inset
  - synthetic component roots avoid duplicated parent-side layout application
- Fixed a critical capture/export mismatch:
  - Unity cropped artifacts are now normalized back to the manifest-requested dimensions before PNG write
  - this removed the false “small UI” signal caused by half-resolution cropped exports

## Main code changes

### Visual IR / planning

- [dotnet/src/BoomHud.Generators/GenerationDocumentPreprocessor.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Generators/GenerationDocumentPreprocessor.cs)
- [dotnet/src/BoomHud.Generators/VisualIR/VisualDocument.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Generators/VisualIR/VisualDocument.cs)
- [dotnet/src/BoomHud.Generators/VisualIR/VisualDocumentBuilder.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Generators/VisualIR/VisualDocumentBuilder.cs)
- [dotnet/src/BoomHud.Generators/VisualIR/VisualSynthesisPlanner.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Generators/VisualIR/VisualSynthesisPlanner.cs)
- [dotnet/src/BoomHud.Generators/VisualIR/VisualRefinementPlanner.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Generators/VisualIR/VisualRefinementPlanner.cs)

Key point: Visual IR is now a real compiler/planning layer, not only a diagnostics artifact.

### Scoring / measured layout

- [dotnet/src/BoomHud.Cli/Handlers/Baseline/ImageSimilarityHandler.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Cli/Handlers/Baseline/ImageSimilarityHandler.cs)
- [dotnet/src/BoomHud.Cli/Commands/Baseline/BaselineScoreCommand.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Cli/Commands/Baseline/BaselineScoreCommand.cs)
- [samples/UnityFullPenCompare/Assets/Editor/BoomHudFidelityCapture.cs](C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/Editor/BoomHudFidelityCapture.cs)
- [scripts/run-fixture-rule-sweep.ps1](C:/lunar-horse/plate-projects/boom-hud/scripts/run-fixture-rule-sweep.ps1)
- [scripts/run-pen-remotion-unity-fidelity.ps1](C:/lunar-horse/plate-projects/boom-hud/scripts/run-pen-remotion-unity-fidelity.ps1)

Key point: we now have both recursive image-phase diagnostics and numeric layout diagnostics. The latest addition is artifact-size normalization after crop, so capture output matches the reference artifact dimensions again.

### uGUI realization

- [dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs)
- [samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui/PartyStatusStripView.ugui.cs](C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui/PartyStatusStripView.ugui.cs)
- [samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui/QuestSidebarView.ugui.cs](C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui/QuestSidebarView.ugui.cs)
- [samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui/CombatToastStackView.ugui.cs](C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui/CombatToastStackView.ugui.cs)

Key point: the remaining `uGUI` drift is no longer dominated by obvious shell collapse or hidden transform-scale issues. It is now mostly actual layout/metric drift.

## Latest verified results

Primary static verification lane:

- [build/fixture-rule-sweeps/v3_2_capture_size_normalization_2026_04_12/leaderboard.json](C:/lunar-horse/plate-projects/boom-hud/build/fixture-rule-sweeps/v3_2_capture_size_normalization_2026_04_12/leaderboard.json)

Important numbers from that lane:

- baseline average overall similarity: `72.0667`
- candidate average overall similarity: `72.0833`
- lane delta: `+0.0166`
- `party-status-strip-ugui`: `54.82`
- `quest-sidebar-ugui`: `71.95`
- `combat-toast-stack-ugui`: `75.82`

Important artifact correctness checks:

- `party-status-strip-ugui.png` now exports at `2560x640`
- `combat-toast-stack-ugui.png` now exports at `1920x1440`
- the earlier half-resolution crop problem is gone

Verification completed in this session:

- full unit suite:
  - `dotnet test C:\lunar-horse\plate-projects\boom-hud\dotnet\tests\BoomHud.Tests.Unit\BoomHud.Tests.Unit.csproj -p:UseSharedCompilation=false`
  - result: `385` passed, `0` failed
- focused Unity-first sweep:
  - `build/fixture-rule-sweeps/v3_2_capture_size_normalization_2026_04_12`

## What is actually fixed now

- Visual IR planning/refinement infrastructure is in place and emitting usable artifacts.
- The scorer can now feed real recursive score trees into refinement artifact generation.
- Measured-layout reports now catch shell/stretch issues and transform-scale mismatches.
- The `CombatToastStack` overlay-text width bug is fixed.
- The `RoleIcon` absolute inset bug is fixed.
- The capture/export path no longer produces misleading undersized artifacts after crop.

## What is still open

The remaining static fidelity gap is now narrower and clearer:

- `party-status-strip-ugui` is still the main weak fixture.
- The dominant findings are no longer “mystery small UI”.
- The remaining visible drift is mostly:
  - edge alignment pressure
  - row/card density drift
  - text/icon metric drift

Current interpretation:

- the capture layer is no longer the primary blocker
- the next work should be generator-level `uGUI` realization and metric calibration
- do not spend the next session re-debugging root scale or crop dimensions unless a new regression appears

## Recommended next steps

1. Use `party-status-strip-ugui` as the primary acceptance fixture.
2. Keep `quest-sidebar-ugui` as the non-regression fixture.
3. Focus the next slice on `uGUI` realization and metrics, not capture plumbing:
   - bar thickness / density
   - portrait / card shell density
   - row-edge padding and stack alignment
   - font/icon metric calibration
4. Keep using:
   - `*.visual-refinement.json`
   - `*.measured-layout.json`
   - the normalized full-size artifacts

## Useful artifacts for the next session

- [build/fixture-rule-sweeps/v3_2_capture_size_normalization_2026_04_12/baseline-no-rules/captures/party-status-strip-ugui.png](C:/lunar-horse/plate-projects/boom-hud/build/fixture-rule-sweeps/v3_2_capture_size_normalization_2026_04_12/baseline-no-rules/captures/party-status-strip-ugui.png)
- [build/fixture-refs/party-status-strip/PSS01.png](C:/lunar-horse/plate-projects/boom-hud/build/fixture-refs/party-status-strip/PSS01.png)
- [build/fixture-rule-sweeps/v3_2_capture_size_normalization_2026_04_12/baseline-no-rules/scores/party-status-strip-ugui.json](C:/lunar-horse/plate-projects/boom-hud/build/fixture-rule-sweeps/v3_2_capture_size_normalization_2026_04_12/baseline-no-rules/scores/party-status-strip-ugui.json)
- [build/fixture-rule-sweeps/v3_2_capture_size_normalization_2026_04_12/baseline-no-rules/scores/party-status-strip-ugui.measured-layout.json](C:/lunar-horse/plate-projects/boom-hud/build/fixture-rule-sweeps/v3_2_capture_size_normalization_2026_04_12/baseline-no-rules/scores/party-status-strip-ugui.measured-layout.json)
- [build/fixture-rule-sweeps/v3_2_capture_size_normalization_2026_04_12/quest-sidebar-ugui-content-hug.catalog/scores/party-status-strip-ugui.visual-refinement.json](C:/lunar-horse/plate-projects/boom-hud/build/fixture-rule-sweeps/v3_2_capture_size_normalization_2026_04_12/quest-sidebar-ugui-content-hug.catalog/scores/party-status-strip-ugui.visual-refinement.json)

## Intentionally untouched / uncommitted folders

I left the heavy artifact and tooling folders alone, including:

- `build/`
- `.playwright-mcp/`
- `.kilo/`
- `remotion/build/`
- `samples/build/`
- `samples/UnityFullPenCompare/Assets/Screenshots/`
- `samples/UnityFullPenCompare/ProjectSettings/TimelineSettings.asset`

These should stay out of the commit sequence unless there is a deliberate packaging or fixture-refresh step.
