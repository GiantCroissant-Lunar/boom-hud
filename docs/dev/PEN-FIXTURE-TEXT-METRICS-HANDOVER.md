# Pen Fixture Text Metrics Handover

## Status (2026-04-10)

This handover captures the follow-up work after the first fixture fidelity loop. The main changes in this pass were:

- structured findings added to image similarity reports
- typography metrics propagated further from `.pen` into UI IR and both Unity generators
- fixture outputs regenerated for UI Toolkit and uGUI
- compare manifest rerun against the refreshed generated outputs

The working fixture set is still:

- `samples/pencil/party-status-strip.pen`
- `samples/pencil/quest-sidebar.pen`
- `samples/pencil/combat-toast-stack.pen`

The compare sample host is still:

- `samples/UnityFullPenCompare`

Local compare artifacts remain uncommitted:

- `build/`
- `.kilo/`
- `.playwright-mcp/`
- `remotion/build/`
- `samples/UnityFullPenCompare/Assets/Screenshots/`
- `samples/UnityFullPenCompare/ProjectSettings/TimelineSettings.asset`
- `samples/build/`

## What changed

### Structured findings in score reports

`dotnet/src/BoomHud.Cli/Handlers/Baseline/ImageSimilarityHandler.cs` now emits richer report data:

- normalization details
- spatial analysis
- categorized findings
- probable fix area
- suggested action

This turns score output into something actionable instead of only a raw percentage.

Related coverage lives in:

- `dotnet/tests/BoomHud.Tests.Unit/Snapshots/ImageSimilarityHandlerTests.cs`

### Typography metrics in IR and generators

`LineHeight` now exists in UI IR and is carried into the backend generators.

Key files:

- `dotnet/src/BoomHud.Abstractions/IR/StyleSpec.cs`
- `dotnet/src/BoomHud.Dsl.Pencil/PenToIrConverter.cs`
- `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`
- `dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs`

Important behavior changes:

- inline `$typography.*` tokens from `.pen` are resolved into style values
- UI Toolkit emission now applies more explicit text-related style assignments
- uGUI emission now applies text wrap and line spacing policy instead of leaving it implicit

Related test coverage:

- `dotnet/tests/BoomHud.Tests.Unit/Dsl/PenParserTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/UnityGeneratorTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/UGuiGeneratorTests.cs`

## Latest normalized scores

These are the current useful trend numbers after the text-metrics pass.

- `PartyStatusStrip`: UI Toolkit `71.29%`, uGUI `67.07%`
- `QuestSidebar`: UI Toolkit `79.23%`, uGUI `54.66%`
- `CombatToastStack`: UI Toolkit `78.07%`, uGUI `61.24%`

Observed movement from the previous normalized pass:

- `QuestSidebar` UI Toolkit improved from `75.15%` to `79.23%`
- `CombatToastStack` UI Toolkit improved from `74.34%` to `78.07%`
- `PartyStatusStrip` UI Toolkit was effectively unchanged
- uGUI was largely flat, which suggests the next fix should not be another broad typography pass

The reports themselves are written under `build/fixture-scores/` and should continue to stay out of git.

## Current mismatch pattern

The dominant issue is now narrower and more repeatable.

Primary recurring finding:

- `text-or-icon-metrics-mismatch`

Secondary recurring finding on uGUI:

- `edge-alignment-mismatch`

What that means in practice:

- font size and line-height are closer, but not identical
- some text still wraps differently from the `.pen`
- some icon glyphs are box-centered but not optically centered
- uGUI still has some anchor or edge-pressure drift in certain fixture layouts

At this point, the capture pipeline is no longer the main blocker. The remaining gap is mostly generator policy.

## Recommended next pass

The next round should focus on narrow, repeatable fixes instead of another generator-wide sweep.

Priority order:

1. Add icon optical-centering policy for both backends.
2. Tighten uGUI edge and anchor alignment for the sidebar and toast fixtures.
3. Separate text, icon, and layout translation into smaller services instead of growing the current generator monoliths.
4. Extend metadata normalization for multi-frame `.pen` usage so flow/state/keyframe semantics can be recorded in UI IR and Motion IR.

## Verification already completed

Unit tests passed for the affected parser and generator areas:

- `dotnet test dotnet/tests/BoomHud.Tests.Unit/BoomHud.Tests.Unit.csproj --filter "FullyQualifiedName~PenParserTests|FullyQualifiedName~UnityGeneratorTests|FullyQualifiedName~UGuiGeneratorTests" -p:UseSharedCompilation=false`

The fixture compare manifest was also rerun successfully through the Unity sample host after regeneration.
