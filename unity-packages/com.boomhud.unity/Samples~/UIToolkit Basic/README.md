# UI Toolkit Basic Sample

This sample shows how to use `BoomHudUiToolkitHost` without requiring generated BoomHud files yet.

## Included Files

- `BoomHudSampleStatusViewModel.cs`: small MonoBehaviour-backed sample view model.
- `BoomHudSampleStatusPresenter.cs`: presenter derived from `BoomHudUiToolkitHost`.
- `StatusPanel.uxml`: minimal UI Toolkit view tree.
- `StatusPanel.uss`: matching style sheet.

## How To Use

1. Import the sample from the Unity Package Manager samples list.
2. Create a `UIDocument` in your scene.
3. Assign `StatusPanel.uxml` to the document's source asset and add `StatusPanel.uss` as a style sheet.
4. Add `BoomHudSampleStatusPresenter` to the same GameObject.
5. Add `BoomHudSampleStatusViewModel` to any GameObject and assign it to the presenter.

This sample uses the same hosting pattern that generated UI Toolkit views will use when wired through `BoomHudUiToolkitHost`.
