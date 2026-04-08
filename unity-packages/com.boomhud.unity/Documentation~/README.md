# BoomHud Unity Integration

`com.boomhud.unity` is a Unity Package Manager package that provides the Unity-side hosting surface for BoomHud output.

## What It Covers

- UI Toolkit hosting helpers for generated `.uxml`, `.uss`, and `.gen.cs` views.
- Shared runtime base behavior for binding and rebinding generated views.
- uGUI hosting scaffolding so consumer projects have a stable package location for future Canvas-based generation.

## Current Status

- UI Toolkit generation is implemented in BoomHud today.
- uGUI generation is not implemented yet in the BoomHud generator, but this package includes the Unity-side host surface so the package layout is ready when that backend lands.

## Package Layout

- `Runtime/Common`: shared `BoomHudViewHost` lifecycle base class.
- `Runtime/UIToolkit`: `BoomHudUiToolkitHost` for `UIDocument`-based hosts.
- `Runtime/uGUI`: `BoomHudUguiHost` for `Canvas` and `RectTransform` hosts.
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