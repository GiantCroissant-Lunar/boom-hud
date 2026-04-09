# Unity Full Pen Compare

This Unity project is a minimal consumer for the generated UI Toolkit output from the real `full.pen` file.

What it contains:

- a local UPM reference to [unity-packages/com.boomhud.unity](c:/lunar-horse/plate-projects/boom-hud/unity-packages/com.boomhud.unity)
- generated UI Toolkit files under `Assets/Resources/BoomHudGenerated`
- an editor setup entrypoint: `BoomHud.Compare.Editor.BoomHudCompareProjectSetup.SetupScene`
- a runtime presenter that clones `ExploreHudView.uxml`, applies `ExploreHudView.uss`, and binds the generated `ExploreHudView`

Regenerate the generated assets with:

```powershell
dotnet run --project dotnet/src/BoomHud.Cli -- generate "samples\pencil\full.pen" --target unity --output "samples\UnityFullPenCompare\Assets\Resources\BoomHudGenerated" --namespace Generated.Hud
dotnet run --project dotnet/src/BoomHud.Cli -- generate "samples\pencil\full.pen" --root CharPortrait --motion "remotion\src\motion-samples\char-portrait.motion.json" --target unity --output "samples\UnityFullPenCompare\Assets\Resources\BoomHudGenerated" --namespace Generated.Hud
```

Set up the comparison scene headlessly with:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe" -batchmode -projectPath "c:\lunar-horse\plate-projects\boom-hud\samples\UnityFullPenCompare" -executeMethod BoomHud.Compare.Editor.BoomHudCompareProjectSetup.SetupScene -quit -logFile -
```

Then open the project in Unity 6.4, load `Assets/BoomHudCompare/Scenes/ExploreHudCompare.unity`, and compare it visually against the source `full.pen`.

For the motion bridge sample, run `Tools/BoomHud/Setup Char Portrait Motion Timeline Scene` after the second generation command. That creates a Timeline scene driven by `CharPortraitMotionHost`, sourced from the same `full.pen` component used by the Remotion demo.