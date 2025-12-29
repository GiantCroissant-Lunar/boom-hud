# MVVM Adapter Generator (Layer B)

This document describes how to enable and use the BoomHud **MVVM adapter generator** (Roslyn incremental source generator).

## Overview

BoomHud uses a dual-generation approach:

- **Layer A (CLI)**: generates UI code (Terminal.Gui / Avalonia) and emits the stable MVVM contract (interfaces like `IStatusBarViewModel`).
- **Layer B (this generator)**: generates **MVVM-framework-specific glue** for your app, driven by MSBuild configuration.

Layer B is optional and additive.

## Enabling the generator (in-repo)

In this repo, enabling the MVVM generator is done via a local props file:

- `dotnet/source-generators/MvvmAdapters.BoomHud.props`

### Option A: Enable in the sample host projects

The sample host projects already contain:

- `BoomHud.Sample.TerminalGuiHost`
- `BoomHud.Sample.AvaloniaHost`

They import the props file behind an opt-in flag:

```xml
<PropertyGroup>
  <BoomHud_EnableMvvmAdapters>true</BoomHud_EnableMvvmAdapters>
</PropertyGroup>
```

### Option B: Enable in any other project in this repo

Add the import to your `.csproj` (adjust relative path if needed):

```xml
<Import Project="..\..\source-generators\MvvmAdapters.BoomHud.props"
        Condition="'$(BoomHud_EnableMvvmAdapters)' == 'true'" />
```

Then opt-in:

```xml
<PropertyGroup>
  <BoomHud_EnableMvvmAdapters>true</BoomHud_EnableMvvmAdapters>
</PropertyGroup>
```

## Configuration (MSBuild)

The generator reads MSBuild properties from `build_property.*` (exposed using `CompilerVisibleProperty`).

### Properties

- `BoomHudMvvmFlavor`
  - `None` (default)
  - `Custom`
  - `ReactiveUI`
  - `CommunityToolkit`
- `BoomHudMvvmGenerateConcreteViewModels`
  - `true | false` (default: `false`)
- `BoomHudMvvmNamespace`
  - Reserved for future use (not required for current implementation).

Example:

```xml
<PropertyGroup>
  <BoomHud_EnableMvvmAdapters>true</BoomHud_EnableMvvmAdapters>
  <BoomHudMvvmFlavor>ReactiveUI</BoomHudMvvmFlavor>
  <BoomHudMvvmGenerateConcreteViewModels>true</BoomHudMvvmGenerateConcreteViewModels>
</PropertyGroup>
```

## Marking ViewModels to adapt (attribute)

Layer B uses attribute-based discovery (recommended default).

Add a user-authored partial class and mark it:

```csharp
using BoomHud.Mvvm;

[BoomHudViewModelFor("StatusBar")]
public partial class StatusBarViewModel
{
}
```

The generator will look for the Layer A interface named:

- `I{ViewName}ViewModel` (e.g. `IStatusBarViewModel`)

## Generated output by flavor

The generator only emits concrete implementations when:

- `BoomHudMvvmGenerateConcreteViewModels=true`

### Flavor: Custom

- Emits a partial class that implements:
  - `I{ViewName}ViewModel`
  - `INotifyPropertyChanged`
- Emits backing fields + *settable* properties for each interface property.

### Flavor: ReactiveUI

Requires the project to reference ReactiveUI so the type exists:

- `ReactiveUI.ReactiveObject`

Emits a partial class that:

- Inherits `ReactiveObject`
- Implements `I{ViewName}ViewModel`
- Uses `this.RaiseAndSetIfChanged(ref _field, value)` in property setters.

### Flavor: CommunityToolkit

Requires the project to reference CommunityToolkit so the type exists:

- `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`

Emits a partial class that:

- Inherits `ObservableObject`
- Implements `I{ViewName}ViewModel`
- Uses `SetProperty(ref _field, value)` in property setters.

## Diagnostics

- `BHMVVM001` (warning): Could not find `I{ViewName}ViewModel` interface.
- `BHMVVM002` (error): Target ViewModel class is not `partial`.
- `BHMVVM004` (warning): Multiple `I{ViewName}ViewModel` interfaces found.
- `BHMVVM005` (warning): Selected flavor requires a type that is not present (missing MVVM package reference).
- `BHMVVM006` (warning): Flavor enabled, but `BoomHudMvvmGenerateConcreteViewModels` is not true.

## Notes / current behavior

- **Avalonia bindings**: AXAML generation emits binding expressions for both root and child elements (e.g. `{Binding Player.Name}`, `Mode=TwoWay`, `StringFormat=...`).
- **Terminal.Gui dimensions**: pixel dimensions currently map directly to `Dim.Absolute(<pixels>)` (no scaling). Root layout setup respects explicit `LayoutSpec.Width/Height` when provided.

These behaviors may evolve as the design-to-terminal scaling policy becomes more formalized.
