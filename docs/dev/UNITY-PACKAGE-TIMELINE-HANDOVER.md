# Unity Package Timeline Handover

This handover covers the work completed for unified Timeline support across Unity UI Toolkit and uGUI, plus the new Unity package project settings surface for consumer projects.

## What landed

### Shared Timeline host contract

- Timeline no longer targets a UI Toolkit-only host.
- Shared motion host contract lives in:
  - [IBoomHudMotionHost.cs](/C:/lunar-horse/plate-projects/boom-hud/unity-packages/com.boomhud.unity/Runtime/Common/IBoomHudMotionHost.cs)
- Shared Timeline runtime now binds against `BoomHudViewHost` + `IBoomHudMotionHost`:
  - [BoomHudMotionTrack.cs](/C:/lunar-horse/plate-projects/boom-hud/unity-packages/com.boomhud.unity/Runtime/Timeline/BoomHudMotionTrack.cs)
  - [BoomHudMotionPreviewBootstrap.cs](/C:/lunar-horse/plate-projects/boom-hud/unity-packages/com.boomhud.unity/Runtime/Timeline/BoomHudMotionPreviewBootstrap.cs)
- Both backend hosts now raise a shared `MotionApplied` event:
  - [BoomHudUiToolkitMotionHost.cs](/C:/lunar-horse/plate-projects/boom-hud/unity-packages/com.boomhud.unity/Runtime/UIToolkit/BoomHudUiToolkitMotionHost.cs)
  - [BoomHudUguiMotionHost.cs](/C:/lunar-horse/plate-projects/boom-hud/unity-packages/com.boomhud.unity/Runtime/uGUI/BoomHudUguiMotionHost.cs)

### uGUI motion generation and sample flow

- uGUI motion generation now emits:
  - `*Motion.gen.cs`
  - `*MotionHost.gen.cs`
- Main generator files:
  - [UGuiGenerator.cs](/C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs)
  - [UGuiGenerator.Motion.cs](/C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.Motion.cs)
- Shared sequence metadata now lines up with UI Toolkit:
  - `FramesPerSecond`
  - `DefaultClipId`
  - `ClipIds`
  - `DefaultSequenceId`
  - `SequenceIds`
  - `GetSequenceItems(...)`
- Sample uGUI motion assets and setup:
  - [BoomHudUGuiMotionTimelineSetup.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/Editor/BoomHudUGuiMotionTimelineSetup.cs)
  - [CharPortraitMotionTimelineUGui.unity](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scenes/CharPortraitMotionTimelineUGui.unity)
  - [CharPortraitMotionTimelineUGui.playable](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Timelines/CharPortraitMotionTimelineUGui.playable)
  - [CharPortraitMotion.gen.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui/CharPortraitMotion.gen.cs)
  - [CharPortraitMotionHost.gen.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui/CharPortraitMotionHost.gen.cs)

### CharPortrait sync fix

- The old uGUI partial host extension was removed because duplicate script assets for the same `MonoBehaviour` class broke Unity scene serialization.
- Replacement sidecar sync component:
  - [CharPortraitUguiMotionSync.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/CharPortraitUguiMotionSync.cs)
- The preview composer sync hook still lives in:
  - [UGuiHudPreviewComposer.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/UGuiHudPreviewComposer.cs)

### Package project settings

- Consumer-facing package settings now live under:
  - `Project Settings > GiantCroissant > BoomHud`
- Package settings implementation:
  - [BoomHudProjectSettings.cs](/C:/lunar-horse/plate-projects/boom-hud/unity-packages/com.boomhud.unity/Editor/BoomHudProjectSettings.cs)
- Menu shortcut:
  - `Tools/BoomHud/Open Project Settings`
  - [BoomHudUnityPackageMenu.cs](/C:/lunar-horse/plate-projects/boom-hud/unity-packages/com.boomhud.unity/Editor/BoomHudUnityPackageMenu.cs)
- Shared Timeline scene builder now reads defaults from settings:
  - [BoomHudMotionTimelineSceneBuilder.cs](/C:/lunar-horse/plate-projects/boom-hud/unity-packages/com.boomhud.unity/Editor/BoomHudMotionTimelineSceneBuilder.cs)
- Settings currently cover:
  - pen source root
  - motion source root
  - UI Toolkit generated output
  - uGUI generated output
  - Timeline scene output root
  - Timeline playable output root
  - Timeline `PanelSettings` asset path

### UI Toolkit no longer assumes Resources

- Generated UI Toolkit motion hosts no longer call `Resources.Load`.
- The generated host now expects the `UIDocument` or inherited host fields to already provide the generated visual tree.
- Main generator file:
  - [UnityMotionExporter.cs](/C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Gen.Unity/UnityMotionExporter.cs)
- Regenerated sample host:
  - [CharPortraitMotionHost.gen.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/Resources/BoomHudGenerated/CharPortraitMotionHost.gen.cs)

## Validation

- .NET tests passed:
  - `dotnet test dotnet/tests/BoomHud.Tests.Unit/BoomHud.Tests.Unit.csproj --filter "UGuiGeneratorTests|UnityMotionExporterTests|GeneratorMotionContractTests"`
- Unity refresh/compile completed cleanly after the final changes.
- The following menu actions ran without console errors:
  - `Tools/BoomHud/Build uGUI Component Prefabs`
  - `Tools/BoomHud/Setup Char Portrait Motion Timeline Scene (uGUI)`
  - `Tools/BoomHud/Open Project Settings`

## Important constraints now

- UI Toolkit and uGUI Timeline paths are unified at the Motion IR / host contract / sequence metadata level.
- Backend-specific differences remain only in:
  - generated property application
  - scene/container setup
  - presenter-side composition sync
- Package settings exist now, but the CLI / importer pipeline does not yet read Unity `ProjectSettings/BoomHudSettings.asset`. The settings currently drive Unity editor tooling and establish the package-side contract.

## Recommended next step

Use the new package settings as the single source of truth for Unity-side paths, then teach the Unity-facing import/generation workflow to consume them directly so consumer projects do not need sample-specific setup scripts for path selection.
