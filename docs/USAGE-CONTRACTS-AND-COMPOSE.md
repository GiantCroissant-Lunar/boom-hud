# BoomHud: Using Contracts + Composition (Updated Workflow)

This document explains how to use the updated **BoomHud** code generator to:

- Bind generated views to **stable ViewModel contracts** (interfaces) provided by another assembly/namespace.
- Decouple contract member names from runtime property paths via `binding.key`.
- Compose a HUD tree via **slot-key driven composition** (`*Compose.g.cs`).
- Embed drift detection IDs (`BoomHudSourceId`, `BoomHudContractId`) into generated views.

This is intended for other agents and contributors who need to consume BoomHud from a host repo (Avalonia / Terminal.Gui / Godot).

---

## Concepts (What Changed)

### Stable ViewModel contracts (external interfaces)
BoomHud can generate views that reference ViewModel interfaces from a separate namespace (e.g. `FantaSim.Hud.Contracts`) rather than always generating `I*ViewModel.g.cs` in the output.

- **`ViewModelNamespace`** controls where the generator expects the `I*ViewModel` interfaces to live.
- **`EmitViewModelInterfaces`** controls whether BoomHud emits its own `I*ViewModel.g.cs` files.

### `binding.key` (contract member name) vs `binding.path` (runtime path)
Each binding has:

- **`Path`**: the runtime binding path (what the view reads from the VM / how it refreshes bindings).
- **`Key`**: the contract member name to generate against.

If `Key` is provided, it becomes the canonical contract member name (and drives interface member naming) while `Path` remains the runtime path.

### `slotKey` (stable child mount key)
When a node is a component instance (`ComponentRefId`), `slotKey` is used as its stable identity in generated composition.

This affects:

- How the compose layer finds the child view.
- The `slotKey` passed to the child ViewModel resolver.

### Compose output (`*Compose.g.cs`)
When enabled, each backend emits `*View.Compose.g.cs` which wires:

- The **root** `I{Root}ViewModel` into the root view.
- Each **child** view’s ViewModel by calling a `resolver.Resolve<IChildViewModel>(parentVm, slotKey)`.
- Unbinding via `IDisposable` (so hosts can clean up).

### Drift detection IDs
Generated views embed:

- `public const string BoomHudSourceId = "sha256:...";`
- `public const string BoomHudContractId = "...";` (if provided)

`BoomHudSourceId` is computed from the document structure (node type/id/slotKey/componentRef + bindings including `binding.key`).

---

## CLI Usage

BoomHud CLI entrypoint is `BoomHud.Cli`.

### Generate

```
BoomHud.Cli generate <input.json> \
  --target terminalGui|avalonia|all \
  --output <dir> \
  --namespace <CSharpNamespace> \
  --viewmodel-namespace <ViewModelInterfaceNamespace> \
  --no-vm-interfaces \
  --compose \
  --contract-id <string> \
  --annotations <annotations.json>
```

Where:

- `--namespace`
  - Namespace for generated code output.
- `--viewmodel-namespace`
  - Namespace for generated (or externally-provided) `I*ViewModel` interfaces.
  - If omitted, generators default to using `--namespace`.
- `--no-vm-interfaces`
  - Do **not** emit `I*ViewModel.g.cs` files (assume interfaces exist elsewhere).
- `--compose`
  - Emit `*View.Compose.g.cs` (composition helpers).
- `--contract-id`
  - Optional string embedded into generated views as `BoomHudContractId`.
- `--annotations`
  - Optional BoomHud annotations JSON (bindings, slotKey, etc.) applied after parsing.

---

## Annotations JSON: `binding.key` and `slotKey`

Annotations are applied by `BoomHud.Dsl.Figma.FigmaAnnotations`.

### Binding shape
Bindings can be specified as a string (path only) or as an object with `{ path, key }`.

Example (conceptual):

- Bind view property `text` to runtime path `Player.Name` but contract member `PlayerName`.

```
{
  "nodes": [
    {
      "match": { "path": ["Root", "PlayerLabel"] },
      "bindings": {
        "text": { "path": "Player.Name", "key": "PlayerName" }
      }
    }
  ]
}
```

### Slot key
Slot keys are set per node under `set.slotKey`.

```
{
  "nodes": [
    {
      "match": { "path": ["Root", "ToolbarHost"] },
      "set": { "slotKey": "hud.toolbar" }
    }
  ]
}
```

Important:

- For component instances, composition uses `slotKey` first, then falls back to `id`, then `componentName`.

---

## Generator Options Mapping (When Calling Generators Programmatically)

The CLI builds `GenerationOptions` like:

- `Namespace`
- `ViewModelNamespace`
- `EmitViewModelInterfaces = !noVmInterfaces`
- `EmitCompose = compose`
- `ContractId`

If you call generators directly (without CLI), ensure you set those fields accordingly.

---

## Output Files (by backend)

### TerminalGui
Typical output (per component):

- `{Name}View.g.cs`
- `I{Name}ViewModel.g.cs` (only if `EmitViewModelInterfaces=true`)
- `{Name}View.Compose.g.cs` (only if `EmitCompose=true`)

Compose expects:

- Root lookup: `root.FindSlot<ChildView>("slot.key")`

### Avalonia
Typical output:

- `{Name}View.axaml`
- `{Name}View.axaml.cs`
- `I{Name}ViewModel.g.cs` (optional)
- `{Name}View.Compose.g.cs` (optional)

Compose expects:

- Child lookup: `root.FindControl<Control>("<x:Name>")`
- Note: Avalonia’s lookup uses a sanitized name derived from `slotKey` (non `[A-Za-z0-9_]` becomes `_`).

### Godot
Typical output:

- `{Name}View.g.cs`
- `I{Name}ViewModel.g.cs` (optional)
- `{Name}View.Compose.g.cs` (optional)

Compose expects:

- Child lookup: `root.GetNodeOrNull<ChildView>("slot.key")`

---

## How to Use `*Compose.g.cs` in a Host App

Each generated compose file contains a static class:

- `{RootName}_Compose`

Inside it, an interface:

- `public interface IChildVmResolver { T Resolve<T>(object parentVm, string slotKey) where T : class; }`

And an entry point:

- `public static IDisposable Apply(RootView root, IRootViewModel vm, IChildVmResolver resolver)`

### Host responsibilities

- Create the root ViewModel (`IRootViewModel`).
- Implement `IChildVmResolver` for wiring child VMs.
- Call `Apply(...)` and keep the returned `IDisposable` to unbind when disposing/unloading the view.

### Resolver contract
The compose layer calls:

- `resolver.Resolve<IChildViewModel>(parentVm, slotKey)`

So your resolver must:

- Accept `parentVm` (the root VM passed to compose).
- Use `slotKey` to decide which child VM to return.

---

## Drift Detection Guidance

- `BoomHudSourceId` changes when document structure/bindings change.
- `BoomHudContractId` is controlled by `--contract-id`.

Recommended usage pattern:

- Keep `--contract-id` stable per “expected contract version” in the consuming repo.
- If `BoomHudSourceId` changes unexpectedly, re-check:
  - slot keys
  - binding keys
  - component hierarchy changes

---

## Recommended “Consumer Repo” Pattern

- Put your viewmodel interfaces in a stable assembly/namespace (example: `FantaSim.Hud.Contracts`).
- Generate UI views with:
  - `--viewmodel-namespace FantaSim.Hud.Contracts`
  - `--no-vm-interfaces`
  - `--compose`
- Implement:
  - concrete ViewModels in the host app
  - `IChildVmResolver` in the composition root

---

## Related Docs in This Repo

- `docs/CONSUMING.md`
- `docs/MVVM-ADAPTER-GENERATOR.md`
