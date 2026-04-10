# uGUI Timeline Handover

This handover is for the next session, which should focus on timeline support and timeline-driven verification for generated Unity `uGUI`.

## Current state

### Backend

- `uGUI` backend exists in [UGuiGenerator.cs](/C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs).
- CLI supports `--target ugui`.
- Generated `uGUI` views now support both:
  - construction from scratch with `new <View>(parent, viewModel)`
  - binding onto an existing prefab hierarchy with `<View>.Bind(root, viewModel)`

### Unity sample host

- `uGUI` compare scenes exist:
  - [ComponentLabUGui.unity](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scenes/ComponentLabUGui.unity)
  - [ExploreHudCompareUGui.unity](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scenes/ExploreHudCompareUGui.unity)
- Main host/presenter files:
  - [BoomHudUGuiHost.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/BoomHudUGuiHost.cs)
  - [UGuiComponentLabPresenter.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/UGuiComponentLabPresenter.cs)
  - [UGuiExploreHudPresenter.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/UGuiExploreHudPresenter.cs)
  - [UGuiHudPreviewComposer.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/UGuiHudPreviewComposer.cs)

### Prefabs

- `uGUI` component prefabs are built by:
  - [BoomHudUGuiPrefabBuilder.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/Editor/BoomHudUGuiPrefabBuilder.cs)
- Output location:
  - [BoomHudUGuiPrefabs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/Resources/BoomHudUGuiPrefabs)
- The composer now prefers prefab-backed instantiation and falls back to generated construction only if a prefab is missing.

## CharPortrait status

- `CharPortrait` is now running on the prefab path.
- The portrait-specific postprocess in [UGuiHudPreviewComposer.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/UGuiHudPreviewComposer.cs) currently:
  - disables root auto layout for `CharPortrait`
  - places `Face`, `Name`, `Hp`, `Mp`, `Stats`, and `ActionGrid` explicitly
  - tightens action-slot sizing and row spacing
  - keeps HP/MP overlays attached through `StatBarView.Bind(...)` when present in the prefab

Latest intended geometry:

```text
CharPortraitContent: 130x160
Face: pos=(37,0) size=(56,56)
Name: pos=(0,-62) size=(130,12)
Hp: pos=(0,-84) size=(130,10)
Mp: pos=(0,-98) size=(130,8)
Stats: pos=(11,-112) size=(108,10)
ActionGrid: pos=(5,-132) size=(120,27)
```

## Verification already done

- `uGUI` generator tests pass:
  - `dotnet test dotnet/tests/BoomHud.Tests.Unit/BoomHud.Tests.Unit.csproj --filter UGuiGeneratorTests`
- Unity compiles cleanly after the prefab/bind changes.
- Prefab rebuild succeeds through:
  - `Tools/BoomHud/Build uGUI Component Prefabs`
- `D.A. Assets` import no longer blocks compilation. The local fixes are in the imported package and package config.

## Known issues

1. `uGUI` game-view screenshot capture is flaky in the current Unity session.
   - Scene-view capture is usable for structural inspection.
   - Do not trust a black game-view PNG as evidence of broken UI.

2. `CharPortrait` still needs real fidelity scoring on the current prefab-backed layout.
   - The geometry pass is in place.
   - A clean, isolated capture still needs to be taken and scored.

3. `uGUI` still has no motion export or timeline playback integration.

## Next session target

The next session should focus on using timeline with generated `uGUI`.

Recommended order:

1. Define the intended `uGUI` timeline path.
   - Decide whether `uGUI` motion should:
     - reuse the same generated motion JSON/metadata as Remotion and UI Toolkit
     - drive RectTransform/Image/Text properties through generated runtime binders
     - or use a Unity Timeline wrapper that invokes generated `uGUI` views

2. Start with `CharPortrait`.
   - It already has the best isolated component coverage.
   - It already has a known motion scene on the UI Toolkit side for reference:
     - [CharPortraitMotionTimeline.unity](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scenes/CharPortraitMotionTimeline.unity)
     - [CharPortraitMotionTimeline.playable](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Timelines/CharPortraitMotionTimeline.playable)

3. Keep the source of truth shared.
   - Do not invent a `uGUI`-only motion format if it can be avoided.
   - Reuse the same sequence ids, clip ids, frame spans, fill modes, and easing intent already used by Remotion/UI Toolkit.

4. Add a dedicated `uGUI` motion sample scene.
   - Prefer a separate scene instead of overloading the static `ComponentLabUGui`.
   - Mount the `CharPortrait` prefab-backed view there.

5. Only after the first motion path works:
   - fold it into the fidelity harness
   - add frame sampling similar to the existing Remotion/UI Toolkit timeline plan

## Files most likely to matter next

- [UGuiGenerator.cs](/C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs)
- [UGuiHudPreviewComposer.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/UGuiHudPreviewComposer.cs)
- [BoomHudUGuiHost.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/BoomHudUGuiHost.cs)
- [BoomHudUGuiPrefabBuilder.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/Editor/BoomHudUGuiPrefabBuilder.cs)
- [BoomHudFidelityCapture.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/Editor/BoomHudFidelityCapture.cs)

## Avoid next session

- Do not restart from a scene-only code path for `uGUI`.
- Do not hand-edit generated `uGUI` output in `Assets/BoomHudGeneratedUGui`.
- Do not use build artifacts or screenshots under `build/` as committed source of truth.

