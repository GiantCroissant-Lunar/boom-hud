# BoomHud

BoomHud is a build-time UI generation toolchain. It converts design-oriented inputs into a shared IR and emits framework-native output for selected targets without a runtime abstraction layer.

Today the repo supports a canonical pipeline of:

- Figma JSON or Pencil `.pen` input
- shared BoomHud IR, composition, diagnostics, and token resolution
- backend generation for Godot, Terminal.Gui, Avalonia, Unity UI Toolkit, and an initial React backend for Remotion-friendly output

The current package-separation work formalizes the active boundaries around Foundation, Figma input, Pencil input, Godot, Terminal.Gui, and the CLI tool. Avalonia remains implemented in-source, but it is not yet part of the first-pass package guardrails.

## Quick Start

Generate from the repo dogfood manifest:

```powershell
dotnet run --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj --configuration Release -- \
  generate --manifest ui/boom-hud.compose.json --target godot --output artifacts/godot
```

Generate from a single input file:

```powershell
dotnet run --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj --configuration Release -- \
  generate samples/dotnet/BoomHud.Sample.Generation/design/status-bar.json \
  --target terminalgui \
  --output build/_artifacts/boom-hud/terminalgui \
  --namespace MyApp.Ui.Hud
```

## Current Architecture

```text
Figma JSON / Pencil .pen / IR JSON
                |
                v
      Parser + annotations layer
                |
                v
      BoomHud Foundation IR
  (composition, tokens, diagnostics)
                |
                v
         Backend generation
    Godot / Terminal.Gui / Avalonia / Unity / React
```

Key principles:

- Schema-first inputs and contracts
- IR-centric transformation instead of source-to-backend shortcuts
- capability-aware backend behavior
- build-time generation over runtime indirection

## Repository Layout

- `dotnet/`: product code, CLI, generators, tests, and packable projects
- `ui/`: in-repo dogfood consumer workspace, compose manifest, tokens, states, snapshots, and generated output conventions
- `samples/`: all sample apps and fixtures, including `samples/dotnet/` for .NET sample hosts
- `schemas/json/`: JSON schemas that define input and manifest contracts
- `schemas/yaml/`: upstream YAML schema sources such as the Figma OpenAPI input
- `schemas/scripts/`: schema conversion and maintenance scripts
- `unity-packages/`: Unity Package Manager package sources for Unity-side BoomHud integration
- `docs/`: RFCs, consuming docs, contributor-facing guidance, and workflow notes

Important current conventions:

- repo compose output lives under `ui/generated`
- Godot snapshot runner lives at `ui/godot/SnapshotRunner.gd`
- `ui/boom-hud.compose.json` is the operational manifest used by repo-level generation flows

## Package Model

First-pass split-ready package identities:

- `BoomHud.Foundation`: IR, diagnostics, composition, tokens, and core contracts
- `BoomHud.Foundation.Generators`: shared emission infrastructure used by backend packages
- `BoomHud.Input.Figma`: Figma JSON parser
- `BoomHud.Input.Pencil`: Pencil `.pen` parser
- `BoomHud.TerminalGui`: Terminal.Gui backend generator
- `BoomHud.Godot`: Godot backend generator
- `BoomHud.React`: React and Remotion-friendly backend generator
- `BoomHud.Tool`: CLI orchestration tool

The CLI still wires Avalonia in-source, but the package graph verifier currently enforces the split above.

Unity consumer package source also now lives in the repo at [unity-packages/com.boomhud.unity](unity-packages/com.boomhud.unity) for Unity Package Manager consumption inside Unity projects. This is distinct from the NuGet generator package `BoomHud.Unity`.

## Verification

Common verification commands:

```powershell
task verify:no-dto-leak
task verify:package-graph
task verify:test-graph
dotnet test dotnet/BoomHud.sln --configuration Release
```

CI runs the package-graph and test-graph checks automatically in `.github/workflows/ci.yml`.

## Documentation

- [docs/CONSUMING.md](./docs/CONSUMING.md): consumer usage patterns for the CLI and package APIs
- [docs/dev/CONTRIBUTOR-GUIDE.md](./docs/dev/CONTRIBUTOR-GUIDE.md): repo structure, ownership rules, and contributor checks
- [docs/USAGE-CONTRACTS-AND-COMPOSE.md](./docs/USAGE-CONTRACTS-AND-COMPOSE.md): contract IDs, compose output, and host integration details
- [docs/IMPLEMENTATION-PLAN.md](./docs/IMPLEMENTATION-PLAN.md): roadmap and current work streams
- [docs/rfcs/](./docs/rfcs/): design rationale and architecture RFCs

Recommended RFCs to start with:

- [docs/rfcs/RFC-0001-core-architecture.md](./docs/rfcs/RFC-0001-core-architecture.md)
- [docs/rfcs/RFC-0013-native-godot-tscn-backend.md](./docs/rfcs/RFC-0013-native-godot-tscn-backend.md)
- [docs/rfcs/RFC-0014-pencil-dev-integration.md](./docs/rfcs/RFC-0014-pencil-dev-integration.md)
- [docs/rfcs/RFC-0016-compose-manifest.md](./docs/rfcs/RFC-0016-compose-manifest.md)
- [docs/rfcs/RFC-0018-react-remotion-motion-pipeline.md](./docs/rfcs/RFC-0018-react-remotion-motion-pipeline.md)

## Status

BoomHud is beyond the original design-only phase. The repo contains working code generation, dogfood compose flows, snapshot/baseline tooling, and split-boundary verification for the first package-separation pass.

## License

MIT (see LICENSE)
