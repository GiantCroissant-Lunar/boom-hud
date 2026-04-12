# PEN Fixture / uGUI Fidelity Handover (2026-04-12, Session 2)

This handover covers the proof-subtree/search scaffolding, the Unity `uGUI` object-crop fix, and the follow-up generator adjustment that made component-ref children contribute intrinsic size during shell overflow compaction.

## Scope completed

- Added replayable `uGUI` build-program artifacts and planner scaffolding.
- Added `rules proof-subtree`, `rules scaffold-subtree-candidates`, and `rules select-subtree-candidate` CLI flows with unit coverage.
- Fixed the `uGUI` fixture capture path so `ScreenSpaceCamera` crops from centered `PixelAdjustRect` instead of broken viewport bounds.
- Fixed the `uGUI` generator so component-ref children contribute intrinsic size during parent overflow estimation.
- Regenerated the tracked `PartyStatusStrip` `uGUI` sample after the generator fix.

## Main code changes

### Replayable `uGUI` synthesis / proof-subtree

- [docs/rfcs/RFC-0022-replayable-ugui-synthesis.md](C:/lunar-horse/plate-projects/boom-hud/docs/rfcs/RFC-0022-replayable-ugui-synthesis.md)
- [docs/rfcs/README.md](C:/lunar-horse/plate-projects/boom-hud/docs/rfcs/README.md)
- [dotnet/src/BoomHud.Generators/VisualIR/UGuiBuildProgram.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Generators/VisualIR/UGuiBuildProgram.cs)
- [dotnet/src/BoomHud.Generators/VisualIR/UGuiBuildProgramPlanner.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Generators/VisualIR/UGuiBuildProgramPlanner.cs)
- [dotnet/src/BoomHud.Generators/GenerationDocumentPreprocessor.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Generators/GenerationDocumentPreprocessor.cs)
- [dotnet/src/BoomHud.Generators/VisualIR/VisualBackendAdapters.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Generators/VisualIR/VisualBackendAdapters.cs)
- [dotnet/src/BoomHud.Abstractions/Generation/IBackendGenerator.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Abstractions/Generation/IBackendGenerator.cs)
- [dotnet/src/BoomHud.Cli/Program.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Cli/Program.cs)
- [dotnet/src/BoomHud.Cli/Commands/Rules/RulesProofSubtreeCommand.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Cli/Commands/Rules/RulesProofSubtreeCommand.cs)
- [dotnet/src/BoomHud.Cli/Commands/Rules/RulesScaffoldSubtreeCandidatesCommand.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Cli/Commands/Rules/RulesScaffoldSubtreeCandidatesCommand.cs)
- [dotnet/src/BoomHud.Cli/Commands/Rules/RulesSelectSubtreeCandidateCommand.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Cli/Commands/Rules/RulesSelectSubtreeCandidateCommand.cs)
- [dotnet/src/BoomHud.Cli/Handlers/Rules/UGuiSubtreeProofHandler.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Cli/Handlers/Rules/UGuiSubtreeProofHandler.cs)
- [dotnet/src/BoomHud.Cli/Handlers/Rules/UGuiSubtreeCandidateScaffoldHandler.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Cli/Handlers/Rules/UGuiSubtreeCandidateScaffoldHandler.cs)
- [dotnet/src/BoomHud.Cli/Handlers/Rules/UGuiSubtreeCandidateSelectionHandler.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Cli/Handlers/Rules/UGuiSubtreeCandidateSelectionHandler.cs)
- [dotnet/src/BoomHud.Cli/Handlers/Baseline/BaselineCompareHandler.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Cli/Handlers/Baseline/BaselineCompareHandler.cs)

### Generator / capture fixes

- [dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs)
- [samples/UnityFullPenCompare/Assets/Editor/BoomHudFidelityCapture.cs](C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/Editor/BoomHudFidelityCapture.cs)
- [samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui/PartyStatusStripView.ugui.cs](C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui/PartyStatusStripView.ugui.cs)
- [samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui/SyntheticContainerAF9ECAF6View.ugui.cs](C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui/SyntheticContainerAF9ECAF6View.ugui.cs)

### Tests

- [dotnet/tests/BoomHud.Tests.Unit/Generation/UGuiGeneratorTests.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/tests/BoomHud.Tests.Unit/Generation/UGuiGeneratorTests.cs)
- [dotnet/tests/BoomHud.Tests.Unit/Generation/VisualPlanningTests.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/tests/BoomHud.Tests.Unit/Generation/VisualPlanningTests.cs)
- [dotnet/tests/BoomHud.Tests.Unit/Snapshots/UGuiSubtreeProofHandlerTests.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/tests/BoomHud.Tests.Unit/Snapshots/UGuiSubtreeProofHandlerTests.cs)
- [dotnet/tests/BoomHud.Tests.Unit/Snapshots/UGuiSubtreeCandidateScaffoldHandlerTests.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/tests/BoomHud.Tests.Unit/Snapshots/UGuiSubtreeCandidateScaffoldHandlerTests.cs)
- [dotnet/tests/BoomHud.Tests.Unit/Snapshots/UGuiSubtreeCandidateSelectionHandlerTests.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/tests/BoomHud.Tests.Unit/Snapshots/UGuiSubtreeCandidateSelectionHandlerTests.cs)

## Latest verified results

Primary comparable lane:

- [build/proof-subtree/post-shell-fix-unity-2560x640/party-status-strip-ugui.final.score.json](C:/lunar-horse/plate-projects/boom-hud/build/proof-subtree/post-shell-fix-unity-2560x640/party-status-strip-ugui.final.score.json)
- [build/proof-subtree/post-shell-fix-unity-2560x640/captures/party-status-strip-ugui.png](C:/lunar-horse/plate-projects/boom-hud/build/proof-subtree/post-shell-fix-unity-2560x640/captures/party-status-strip-ugui.png)
- [build/proof-subtree/post-shell-fix-unity-2560x640/party-status-strip-ugui.final.diff.png](C:/lunar-horse/plate-projects/boom-hud/build/proof-subtree/post-shell-fix-unity-2560x640/party-status-strip-ugui.final.diff.png)

Important numbers:

- `OverallSimilarityPercent`: `68.92`
- previous comparable post-crop-fix baseline in this session: `68.00`
- delta from component-ref shell fix: `+0.92`

Important measured-layout confirmations:

- [build/proof-subtree/post-shell-fix-unity-2560x640/captures/party-status-strip-ugui.layout.actual.json](C:/lunar-horse/plate-projects/boom-hud/build/proof-subtree/post-shell-fix-unity-2560x640/captures/party-status-strip-ugui.layout.actual.json)
- `MemberA` stays at `216`
- `HeroRow` now resolves at `76` instead of collapsing below its preferred height
- generated `MemberA`/`MemberB`/`MemberC` shells now emit:
  - `ApplyVerticalLayout(... 8f, 12, 12, 8, 8, ..., childControlHeight: false)`

## What is actually improved now

- The replayable `uGUI` proof/search loop is committed in code instead of living only in artifacts.
- The `uGUI` comparable fixture lane now crops the target object correctly.
- The real `PartyStatusStrip` sample no longer ignores synthetic/component-ref child size during shell compaction.
- The outer card shell and `HeroRow` sizing are materially closer to Pen than at the start of the session.

## What is still open

The dominant remaining gap is no longer crop or outer-shell collapse. It is now mostly lower card content:

- bar/body lower-half drift
- status icon cell rendering and spacing
- text/icon metrics and offsets inside the center/lower bands

Current interpretation:

- the next useful work should target `StatusRow`, bar text placement, and icon-cell realization
- do not spend the next session re-debugging object crop unless a new framing regression appears

## Verification completed

Passed in this session:

```powershell
dotnet test C:\lunar-horse\plate-projects\boom-hud\dotnet\tests\BoomHud.Tests.Unit\BoomHud.Tests.Unit.csproj -p:UseSharedCompilation=false
```

Result:

- `412` passed
- `0` failed

Unity verification lane exercised:

- `FixtureCompareUGui`
- manifest: [build/proof-subtree/post-shell-fix-unity-2560x640/fixture-compare.party-status-strip-ugui.json](C:/lunar-horse/plate-projects/boom-hud/build/proof-subtree/post-shell-fix-unity-2560x640/fixture-compare.party-status-strip-ugui.json)

## Intentionally untouched / uncommitted folders

I left the heavy artifact and unrelated tool folders alone, including:

- `build/`
- `.playwright-mcp/`
- `.kilo/`
- `remotion/build/`
- `samples/build/`
- `samples/UnityFullPenCompare/Assets/Screenshots/`
- `samples/UnityFullPenCompare/ProjectSettings/TimelineSettings.asset`
