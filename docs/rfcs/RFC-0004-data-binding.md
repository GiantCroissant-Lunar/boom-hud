# RFC-0004 Data Binding (Draft)

This RFC describes BoomHud's **MVVM-neutral** data binding model and how it is consumed by backend generators (Terminal.Gui, Avalonia). The goal is to make generated views easy to bind to **any** .NET MVVM stack (ReactiveUI, MVVM Toolkit, custom `INotifyPropertyChanged`, etc.) without introducing hard dependencies on a specific framework.

## Goals

- **MVVM-neutral**: Generators emit bindings that rely only on .NET/BCL concepts (`INotifyPropertyChanged`, `ICommand`), not on any particular MVVM library.
- **IR-centric**: All bindings are expressed in the BoomHud IR (`BindingSpec`, `BindableValue<T>`, `ComponentNode.Bindings`). Backends read IR, they do not infer bindings from framework-specific types.
- **Backend-specific but consistent**:
  - Avalonia: generates standard XAML bindings (e.g. `Text="{Binding Health}"`).
  - Terminal.Gui: generates a `ViewModel` property and an explicit `RefreshBindings()` method that pulls values from the ViewModel into controls.
- **Opt-in**: Designs without bindings still work as purely static views. Adding bindings should never be required just to render UI.

## IR Binding Model

Bindings live entirely in the IR layer:

- `BindingSpec`
  - `Property` – target component property name (e.g. `"text"`, `"value"`, `"items"`).
  - `Path` – source path on the ViewModel (e.g. `"Player.Health"`, `"Health"`).
  - `Mode` – `OneWay`, `TwoWay`, `OneTime`.
  - `Converter` / `ConverterParameter` – logical converter name + parameter; interpretation is backend-specific.
  - `Format` – optional string format (e.g. `"{0:N0} HP"`).
  - `Fallback` – value to use when the binding cannot be resolved.

- `BindableValue<T>`
  - Wraps either a static value (`Value`) or a binding (`BindingPath`, `Mode`, `Format`).
  - Used for things like `ComponentNode.Visible`, `Enabled`, `Tooltip`, and type-specific `Properties` (e.g. `"text"`, `"value"`).

- `ComponentNode`
  - `Properties: IReadOnlyDictionary<string, BindableValue<object?>>`
  - `Bindings: IReadOnlyList<BindingSpec>` – explicit bindings to arbitrary component properties.
  - `Visible`, `Enabled`, and some other fields are themselves `BindableValue<T>`.

Backends are responsible for mapping these IR concepts to their own binding stories.

## Avalonia Backend

### Markup Generation

`AvaloniaGenerator` consumes IR bindings as follows:

- For each `BindingSpec` in `ComponentNode.Bindings`:
  - Uses `MapBindingProperty(binding.Property, node.Type)` to map logical properties (e.g. `"text"`, `"value"`, `"items"`) to Avalonia properties:
    - `"text"` → `Text`
    - `"value"` → `Value` / `IsChecked` / etc., depending on control type
    - `"items"` → `ItemsSource`
  - Emits standard Avalonia binding expressions, e.g.:

    ```xml
    <TextBlock Text="{Binding Health}" />
    ```

  - `BindingMode` is mapped to Avalonia `Mode` when not `OneWay` (e.g. `TwoWay`, `OneTime`).
  - `Converter`, `Format`, and `Fallback` are translated into `Converter`, `StringFormat`, and `FallbackValue` parts of the binding expression.

- `BindableValue<bool>` wrappers for `Visible` and `Enabled` are mapped similarly:
  - Static `Value == false` → `IsVisible="False"` / `IsEnabled="False"`.
  - `IsBound` values → `IsVisible="{Binding ...}"` / `IsEnabled="{Binding ...}"`.

The generator does **not** assume any concrete ViewModel type; it only emits `{Binding Path}` markup.

### ViewModel Interface Generation

The Avalonia backend optionally generates a **pure interface** for the ViewModel:

```csharp
public interface IStatusBarViewModel : INotifyPropertyChanged
{
    // One property per distinct binding path in the document
    string? Health { get; }
    string? Mana { get; }
    // ... etc.
}
``

- The interface lives in the same namespace as the generated view (e.g. `BoomHud.Sample.Generated`).
- Property names are derived from binding paths (dots stripped for now, e.g. `Player.Health` → `PlayerHealth`).
- Property types are inferred heuristically based on where the binding is used (e.g. `text` → `string?`, `value` → `double`).

This interface is **optional glue** for consumers:

- A consumer project is free to:
  - Implement the interface using **any** MVVM toolkit (`ReactiveObject`, `ObservableObject`, custom POCO + `INotifyPropertyChanged`, etc.), or
  - Ignore the interface completely and just ensure the DataContext exposes the binding paths used in XAML.

## Terminal.Gui Backend

Terminal.Gui v2 has no built-in binding engine, so the backend implements an **explicit binding-refresh pattern**.

### Generated View Class

For a component named `StatusBar`, the generator emits a `StatusBarView` class that derives from `View` and exposes a ViewModel property:

```csharp
public partial class StatusBarView : View
{
    private IStatusBarViewModel? _viewModel;

    public IStatusBarViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            RefreshBindings();
        }
    }

    public void RefreshBindings()
    {
        if (_viewModel == null) return;
        // For each bound component:
        //   - read properties from _viewModel
        //   - push values into Terminal.Gui controls (Text, Fraction, Visible, Enabled, etc.)
    }
}
```

- For each `BindingSpec` in the IR, `RefreshBindings()` generates assignment code that:
  - Computes a property name from the binding path (e.g. `Player.Health` → `PlayerHealth`).
  - Reads from `_viewModel.PlayerHealth` (or similar) and assigns into the appropriate control property.
- For `BindableValue<T>` fields like `Visible` / `Enabled`, the same method updates `Visible` and `Enabled` on the generated controls.

### ViewModel Interface

The Terminal.Gui backend also generates a **pure interface**:

```csharp
public interface IStatusBarViewModel
{
    // One property per distinct binding path (type currently `object?`)
    object? PlayerHealth { get; }
}
```

This interface is deliberately minimal:

- It does **not** impose any base class or MVVM framework.
- Consumers are free to:
  - Implement it with ReactiveUI, MVVM Toolkit, or a custom class.
  - Add `INotifyPropertyChanged` support themselves.

In the future, the backend may optionally detect when the ViewModel implements `INotifyPropertyChanged` and auto-call `RefreshBindings()` in response to change notifications, but that is **out of scope** for the initial implementation and would remain MVVM-neutral.

## Consumer Responsibilities

Regardless of backend, the consumer project is responsible for:

- **Creating ViewModels** that expose the properties referenced by bindings.
- **Assigning ViewModels** to generated views:
  - Avalonia: `statusBarView.DataContext = new StatusBarViewModel();`
  - Terminal.Gui: `statusBarView.ViewModel = new StatusBarViewModel();`
- Choosing any MVVM toolkit (or none). BoomHud only requires that:
  - Avalonia can resolve binding paths on the `DataContext`.
  - Terminal.Gui bindings can read properties via the generated interface.

## Current Status

- IR support (`BindingSpec`, `BindableValue<T>`, `ComponentNode.Bindings`) – **implemented**.
- Avalonia backend:
  - Emits `{Binding ...}` expressions from IR bindings – **implemented**.
  - Generates `I{DocumentName}ViewModel` interface from binding paths – **implemented**.
- Terminal.Gui backend:
  - Generates `ViewModel` property and `RefreshBindings()` – **implemented**.
  - Generates `I{DocumentName}ViewModel` interface from binding paths – **implemented**.
- Figma importer:
  - Currently treats text as **static** (`Properties["text"].Value = characters`).
  - Does **not** yet populate `BindingSpec` or `BindableValue<T>.BindingPath` from Figma metadata.

### Planned Work

- Define conventions for expressing bindings in design sources (DSL + Figma):
  - e.g. explicit binding annotations in JSON, or naming/metadata conventions in Figma.
- Implement a transformation layer that turns those annotations into IR `BindingSpec`/`BindableValue<T>` instances.
- Extend samples to demonstrate **dynamic** HUDs where game state flows through ViewModels into generated views.
