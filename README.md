# BoomHud

A source-generation-based abstraction layer that enables writing UI/HUD code once and rendering it on multiple UI frameworks (Terminal.Gui, Avalonia, MAUI, etc.) without runtime overhead.

## Vision

Define UI components via Figma designs (JSON), and source generators produce optimized, framework-specific implementations at compile-time.

## Key Features

- **Zero Runtime Overhead**: Build-time code generation, not reflection
- **Declarative DSL**: JSON component definitions (via Figma)
- **Figma Import**: Convert Figma JSON exports to BoomHud IR and generate code
- **Capability-Aware**: Graceful degradation for framework limitations
- **Data Binding**: Common binding syntax across all targets

## CLI (Figma JSON)

Generate Avalonia or Terminal.Gui code from a Figma-style JSON file:

```powershell
dotnet run -c Release --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- generate \
  path\\to\\design.json \
  --target avalonia \
  --output path\\to\\out \
  --namespace My.App.Ui
```

To keep the input JSON "standard" (no custom metadata embedded in node names), you can provide an optional sidecar annotations file:

```powershell
dotnet run -c Release --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- generate \
  path\\to\\design.json \
  --target avalonia \
  --output path\\to\\out \
  --namespace My.App.Ui \
  --annotations path\\to\\design.annotations.json
```

Annotations are applied after parsing and can:

- Override component types (e.g., mark a container as a `Menu`)
- Add bindings (e.g., bind a text node's `text` to a ViewModel property)

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Design Source                            │
│         (BoomHud DSL / Figma JSON / Designer)               │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  BoomHud IR (Intermediate Repr)             │
│   Components, Layout, Bindings, Styles, Constraints         │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ Terminal.Gui v2 │ │  Avalonia AXAML │ │    MAUI XAML    │
│   Generator     │ │    Generator    │ │   Generator     │
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

## Documentation

See [docs/rfcs/](./docs/rfcs/) for detailed design documents:

| RFC | Title |
|-----|-------|
| [RFC-0001](./docs/rfcs/RFC-0001-core-architecture.md) | Core Architecture |
| [RFC-0002](./docs/rfcs/RFC-0002-component-model.md) | Component Model |
| [RFC-0003](./docs/rfcs/RFC-0003-layout-system.md) | Layout System |
| [RFC-0004](./docs/rfcs/RFC-0004-data-binding.md) | Data Binding |
| [RFC-0005](./docs/rfcs/RFC-0005-backend-adapters.md) | Backend Adapters |
| [RFC-0006](./docs/rfcs/RFC-0006-capability-policies.md) | Capability Policies |
| [RFC-0007](./docs/rfcs/RFC-0007-theming-styles.md) | Theming & Styles |
| [RFC-0013](./docs/rfcs/RFC-0013-native-godot-tscn-backend.md) | Native Godot TSCN Backend |
| [RFC-0014](./docs/rfcs/RFC-0014-pencil-dev-integration.md) | Pencil.dev Integration |

## Implementation Plan

See [docs/IMPLEMENTATION-PLAN.md](./docs/IMPLEMENTATION-PLAN.md) for the full implementation roadmap.

### Phases

| Phase | Weeks | Goal |
|-------|-------|------|
| 0: Foundation | 1-2 | Core IR, DSL schema, parser |
| 1: Terminal.Gui Backend | 3-5 | End-to-end working system |
| 2: Avalonia Backend | 6-8 | AXAML generation |
| 3: Data Binding | 9-10 | Cross-platform bindings |
| 4: Theming | 11-12 | Style system |
| 5: Figma Import | 13-15 | Design tool integration |

## Project Structure

```
boom-hud/
├── docs/
│   ├── rfcs/                    # Design documents
│   └── IMPLEMENTATION-PLAN.md
├── schemas/                     # JSON Schema for DSL validation
│   └── boom-hud.schema.json
├── dotnet/
│   ├── src/
│   │   ├── BoomHud.Abstractions/    # Core types, IR, interfaces
│   │   ├── BoomHud.Dsl/             # DSL parser (YAML/JSON)
│   │   ├── BoomHud.Generators/      # Shared generator infrastructure
│   │   ├── BoomHud.Gen.TerminalGui/ # Terminal.Gui code generator
│   │   ├── BoomHud.Gen.Avalonia/    # Avalonia AXAML generator
│   │   └── BoomHud.Gen.Maui/        # MAUI XAML generator
│   └── tests/
│       ├── BoomHud.Tests.Unit/
│       └── BoomHud.Tests.Integration/
└── samples/
    └── BoomHud.Sample.StatusBar/    # Basic status bar sample
```

## Status

🚧 **Design Phase** - RFC drafting in progress.

## License

MIT (see LICENSE)
