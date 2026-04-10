# BoomHud Unity Integration

`com.boomhud.unity` is a Unity Package Manager package that provides the Unity-side hosting surface for BoomHud output.

## What It Covers

- UI Toolkit hosting helpers for generated `.uxml`, `.uss`, and `.gen.cs` views.
- Timeline track helpers that drive generated BoomHud motion hosts through Unity Playables.
- Shared runtime base behavior for binding and rebinding generated views.
- uGUI hosting scaffolding and motion hosts for Canvas-based generation.

## Current Status

- UI Toolkit generation is implemented in BoomHud today.
- uGUI generation is implemented in BoomHud today.
- Timeline support now targets a shared BoomHud motion host contract so UI Toolkit and uGUI can consume the same Motion IR sequence and clip metadata.

## Package Layout

- `Runtime/Common`: shared `BoomHudViewHost` lifecycle base class.
- `Runtime/UIToolkit`: `BoomHudUiToolkitHost` for `UIDocument`-based hosts.
- `Runtime/Timeline`: Timeline clips and tracks that scrub `IBoomHudMotionHost` instances.
- `Runtime/uGUI`: `BoomHudUguiHost` and `BoomHudUguiMotionHost` for `Canvas` and `RectTransform` hosts.
- `Editor`: small editor utility hook for package discovery.
- `Samples~`: importable Unity Package Manager samples for UI Toolkit and uGUI hosts.

## Basic UI Toolkit Usage

Create a Unity `MonoBehaviour` that inherits from `BoomHudUiToolkitHost` and wires your generated view/controller:

```csharp
using BoomHud.Unity.UIToolkit;
using UnityEngine.UIElements;

public sealed class StatusHudPresenter : BoomHudUiToolkitHost
{
    private StatusHudView? _view;

    protected override void BindView(VisualElement root)
    {
        _view ??= new StatusHudView(root);
        _view.ViewModel = ResolveViewModel();
    }

    private IStatusHudViewModel? ResolveViewModel()
    {
        return FindAnyObjectByType<StatusHudViewModel>();
    }

    protected override void Unbind()
    {
        if (_view != null)
        {
            _view.ViewModel = null;
        }
    }
}
```

Attach the presenter to the same `GameObject` as a `UIDocument`, or assign the `UIDocument` explicitly.

## Timeline Motion Usage

Generated `*MotionHost` classes already know how to evaluate Motion JSON clips at a specific time. The package now includes a generic Timeline track that binds directly to those hosts:

1. Add a generated `*MotionHost` component to the same `GameObject` as your `UIDocument`.
2. Add a `PlayableDirector`.
3. Create a Timeline asset and add a `BoomHud Motion Track`.
4. Bind the track to your generated `*MotionHost`.
5. Add a `BoomHud Motion Clip` and set its `Clip Id` to a Motion JSON clip such as `intro`.

The Timeline bridge calls `Evaluate(clipId, timeSeconds)` on the host, so Timeline becomes the time owner without introducing a second playback implementation.

## Project Settings

The package now registers `Project Settings > GiantCroissant > BoomHud` so consumer projects can define their BoomHud roots in one place.

The settings currently cover:

- Pen source root
- Motion source root
- UI Toolkit generated output path
- uGUI generated output path
- Timeline scene output root
- Timeline playable output root
- Timeline `PanelSettings` asset path

Use `Tools/BoomHud/Open Project Settings` to jump directly to that page.

Generated UI Toolkit motion hosts no longer assume `Resources.Load`. Consumer projects should provide the generated `VisualTreeAsset` through the `UIDocument` on the same GameObject, or by assigning the inherited `BoomHudUiToolkitHost` fields in the inspector.

## Generic Timeline Scene Tooling

The Unity package now includes a generic editor action for generated motion hosts:

1. Select a generated `*MotionHost` script in the Project window, or a GameObject with that host attached.
2. Run `Tools/BoomHud/Create Timeline Scene From Selected Motion Host`.

The tool will:

- resolve the generated `VisualTreeAsset`
- resolve the generated motion class and its available clip ids
- create a Timeline asset with one `BoomHud Motion Clip` per clip
- create a scene with `UIDocument`, `PlayableDirector`, camera, and the selected motion host bound to the Timeline track

By default the generated assets land under the locations configured in `Project Settings > GiantCroissant > BoomHud`.

## Basic uGUI Usage

The package also exposes a simple uGUI host base so Canvas-based presenters have a consistent home once the generator adds uGUI output:

```csharp
using BoomHud.Unity.UGUI;
using UnityEngine;

public sealed class InventoryHudPresenter : BoomHudUguiHost
{
    protected override void BindView(Canvas canvas, RectTransform root)
    {
        // Wire your Canvas-based view here.
    }
}
```

## Suggested Consumer Layout

In a Unity project, place the package under:

```text
Packages/com.boomhud.unity
```

Then keep generated UI Toolkit output alongside your game code, for example:

```text
Assets/UI/Generated/BoomHud/
  StatusHudView.uxml
  StatusHudView.uss
  StatusHudView.gen.cs
```

## Included Samples

After installing the package in Unity, open the Package Manager entry for `BoomHud Unity Integration` and import:

- `UIToolkit Basic`: minimal `UIDocument` + presenter sample.
- `uGUI Basic`: minimal `Canvas` + `Text` presenter sample.

## Relationship To .NET Packages

This UPM package is separate from the NuGet package `BoomHud.Unity`.

- `BoomHud.Unity` is the generator/backend package used by the BoomHud CLI and .NET toolchain.
- `com.boomhud.unity` is the Unity-consumer package used inside a Unity project.
