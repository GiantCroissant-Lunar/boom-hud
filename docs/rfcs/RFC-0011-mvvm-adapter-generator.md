# RFC-0011: MVVM Adapter Generator (Roslyn)

- **Status**: Accepted
- **Created**: 2025-12-12
- **Authors**: BoomHud Contributors

## Summary

Introduce an optional Roslyn incremental source generator that produces MVVM-framework-specific “adapter” code for BoomHud-generated views.

- Layer A (CLI) remains responsible for Design → IR → UI generation.
- Layer B (this RFC) generates **MVVM glue** for the consuming application based on configuration.

## Motivation

BoomHud needs to be usable across a variety of MVVM patterns:

- Avalonia apps commonly use **ReactiveUI** or **CommunityToolkit.Mvvm**.
- Terminal.Gui apps often use simple POCO view models or custom game state adapters.
- Some users may want ECS-driven binding models.

If Layer A emits direct dependencies on any MVVM library, it reduces portability. A dedicated adapter generator allows the UI generation contract to stay stable (interfaces), while per-project MVVM wiring is selectable.

## Goals

- Preserve an MVVM-framework-agnostic contract from Layer A.
- Support multiple MVVM “flavors” selected per consuming project.
- Keep the generator optional and additive.
- Provide a path to generate boilerplate-free ViewModel implementations where desired.

Recommended default behavior:

- Prefer **user-authored** (handwritten) partial classes as the adaptation target.
- Treat **auto-generation of concrete ViewModel implementations** as an explicit opt-in.

## Non-Goals

- Replacing Layer A CLI generation.
- Writing non-C# outputs (AXAML files) from Roslyn.
- Automatically inferring full domain model types from binding paths.

## Design

### Layer Boundary / Stable Contract

Layer A must emit (at minimum):

- `I{ViewName}ViewModel` interfaces
- Optionally, metadata describing required bindings:
  - property names
  - expected binding modes (one-way, two-way)
  - command names/parameters (if present)

Layer B consumes the above contract and emits MVVM framework-specific types.

### Selection & Configuration

The adapter generator is configured via MSBuild properties (similar to UnifyEcs):

- `BoomHudMvvmFlavor`:
  - `None` (default)
  - `ReactiveUI`
  - `CommunityToolkit`
  - `Custom`

Optional additional properties may include:

- `BoomHudMvvmGenerateConcreteViewModels = true|false` (default: `false`)
- `BoomHudMvvmNamespace = <string>`

### Discovery of Views to Adapt

The generator supports one or both approaches.

Default recommendation:

- Use **attribute-based** discovery as the primary mechanism to avoid surprising codegen.
- Convention-based scanning can be enabled later if desired.

1) **Convention-based**: scan for `I*ViewModel` interfaces that match BoomHud naming conventions.
2) **Attribute-based**: users mark their partial classes:

```csharp
[BoomHudViewModelFor("StatusBar")]
public partial class StatusBarViewModel { }
```

Rationale:

- Keeps ownership of ViewModel shape with the application.
- Avoids generating “too much” code until the binding/type inference story is proven.
- Works equally well with `ReactiveUI`, `CommunityToolkit`, and custom/ECS-based patterns.

### Output Types

Depending on flavor:

#### Flavor: None

- No additional outputs.

#### Flavor: ReactiveUI

- Generate `partial class {ViewName}ViewModel : ReactiveUI.ReactiveObject, I{ViewName}ViewModel`
- Generate properties using `RaiseAndSetIfChanged`.

#### Flavor: CommunityToolkit

- Generate `partial class {ViewName}ViewModel : ObservableObject, I{ViewName}ViewModel`
- Use `[ObservableProperty]` where feasible.

#### Flavor: Custom

- Emit only helpers and leave concrete implementation to the user.

### Diagnostics

The generator emits diagnostics for:

- Missing MVVM package reference for selected flavor
- Missing partial class targets when `BoomHudMvvmGenerateConcreteViewModels=true`
- Conflicts (duplicate adapters for same view model interface)

## MSBuild Integration

Pattern mirrors `unify-ecs`:

- Generator project targets `netstandard2.1`, `IncludeBuildOutput=false`.
- Consuming project references the generator DLL via `<Analyzer Include=...>`.
- Configuration values are exposed via `<CompilerVisibleProperty Include="BoomHudMvvmFlavor" />` etc.

## Backward Compatibility

- Default is `BoomHudMvvmFlavor=None`, meaning existing projects are unchanged.
- Layer A continues to emit interfaces as today.

## Alternatives Considered

- Generating concrete view models in Layer A CLI (rejected: couples to MVVM frameworks)
- Requiring a single MVVM framework (rejected: reduces portability)

## Open Questions

- Should we standardize a `BoomHudViewModelBase` dependency-free base class as a fallback flavor?
- Should adapters also generate command types or rely on existing `ICommand`/ReactiveCommand patterns?

## Related RFCs

- RFC-0008: Dual Generation Architecture (Design→UI + MVVM Adapters)
- RFC-0004: Data Binding
