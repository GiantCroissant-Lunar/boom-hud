# uGUI Basic Sample

This sample shows how to use `BoomHudUguiHost` with a simple Canvas-based presenter.

## Included Files

- `BoomHudSampleUguiViewModel.cs`: a small MonoBehaviour-backed sample view model.
- `BoomHudSampleUguiPresenter.cs`: presenter derived from `BoomHudUguiHost`.

## How To Use

1. Import the sample from the Unity Package Manager samples list.
2. Create a `Canvas` with a child `Text` element.
3. Add `BoomHudSampleUguiPresenter` to the root object that owns the `RectTransform`.
4. Add `BoomHudSampleUguiViewModel` to any GameObject and assign it to the presenter.
5. Optionally assign the `Text` reference directly, or let the presenter discover it under the root.

This sample demonstrates the runtime host shape intended for future BoomHud uGUI generation.
