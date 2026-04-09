# BoomHud Contributor Guide

This guide describes the current repo mental model for contributors. It focuses on ownership boundaries, where code belongs, and which checks must stay green when you change the architecture.

## Mental Model

BoomHud is organized around one canonical flow:

1. Parse input sources such as Figma JSON, Pencil `.pen`, or IR JSON.
2. Normalize them into BoomHud Foundation types.
3. Apply composition, diagnostics, and token resolution.
4. Generate backend-native output.

Do not add direct source-to-backend shortcuts that bypass Foundation IR. If a feature changes how multiple backends behave, the default assumption is that it belongs in Foundation or in the shared generator layer, not inside one backend generator.

## Repository Roles

- `dotnet/` is the product and test workspace.
- `ui/` is the repo's dogfood consumer workspace.
- `samples/` contains examples and fixtures, not the main operational workflow.

Current sample layout:

- `samples/dotnet/` contains the .NET sample hosts and generation sample
- `samples/GodotSample/` contains the standalone Godot runtime sample
- `samples/pencil/` contains Pencil input fixtures and pilot assets

Important `ui/` conventions:

- compose manifest: `ui/boom-hud.compose.json`
- generated output convention: `ui/generated`
- Godot snapshot runner: `ui/godot/SnapshotRunner.gd`
- snapshot state inputs: `ui/states`
- token registry: `ui/tokens.ir.json`

Artifact location convention:

- repo-owned generated UI artifacts should live under `ui/generated`
- build and package artifacts should live under `build/_artifacts/boom-hud`
- do not reintroduce `dotnet/_out` as a parallel artifact location

Package artifact convention:

- NuGet/tool packages should be emitted under `build/_artifacts/boom-hud/nuget`
- local docs and package-oriented verification should point at `build/_artifacts/boom-hud`

If a change affects repo-level generation behavior, verify it against `ui/boom-hud.compose.json` before treating the work as complete.

## Package Ownership

The first-pass separation contract is:

- `BoomHud.Foundation`
  - owns IR, diagnostics, composition, manifests, tokens, and core interfaces
- `BoomHud.Foundation.Generators`
  - owns backend-agnostic generation infrastructure
- `BoomHud.Input.Figma`
  - owns Figma parsing and related input concerns
- `BoomHud.Input.Pencil`
  - owns Pencil `.pen` parsing and related DTO-to-IR conversion
- `BoomHud.TerminalGui`
  - owns Terminal.Gui output generation
- `BoomHud.Godot`
  - owns Godot output generation and Godot-specific generation concerns
- `BoomHud.Tool`
  - owns CLI orchestration, command wiring, and backend selection

Current nuance:

- Avalonia is still implemented and used in-source.
- The package-graph guardrails do not yet enforce an Avalonia package identity.
- Do not use Avalonia's current in-source shape as justification to weaken the Foundation, input, Godot, Terminal.Gui, or tool boundaries.

## Test Ownership

Tests are split by ownership, not by convenience.

- `dotnet/tests/BoomHud.Tests.Unit`
  - Foundation-owned tests
  - parser contract tests
  - MVVM generator tests that do not require backend/tool orchestration
- `dotnet/tests/BoomHud.Tests.Backends`
  - backend generation tests
  - CLI tests
  - integration-style generation tests
  - baseline compare/diff handler tests

If you add a test that needs CLI internals, backend generators, emitted backend syntax, or end-to-end generation behavior, it belongs in `BoomHud.Tests.Backends`.

If you add a test that only needs Foundation contracts, composition, tokens, diagnostics, or parser normalization behavior, it belongs in `BoomHud.Tests.Unit`.

## Required Verification

Run these commands for any boundary-affecting change:

```powershell
task verify:no-dto-leak
task verify:package-graph
task verify:test-graph
dotnet test dotnet/BoomHud.sln --configuration Release
```

Useful focused commands:

```powershell
dotnet test dotnet/tests/BoomHud.Tests.Unit/BoomHud.Tests.Unit.csproj --configuration Release
dotnet test dotnet/tests/BoomHud.Tests.Backends/BoomHud.Tests.Backends.csproj --configuration Release
dotnet run --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj --configuration Release -- \
  generate --manifest ui/boom-hud.compose.json --target godot --output artifacts/godot
```

CI also enforces the package graph and test graph in `.github/workflows/ci.yml`. Local success should match that shape.

## Editing Rules For Contributors

- Keep generated-output conventions under `ui/`, not at repo root.
- Prefer changing one ownership boundary at a time. If you touch Foundation and a backend together, state why.
- Do not move backend-specific helpers into Foundation to make a reference graph easier.
- Do not let the CLI become the implicit architecture definition. Backend registration belongs in explicit command or catalog seams.
- When changing schemas, update generated DTOs and the consuming code in the same slice.

## Where To Start

Read these in order when you need architectural context:

1. `docs/rfcs/RFC-0001-core-architecture.md`
2. `docs/rfcs/RFC-0014-pencil-dev-integration.md`
3. `docs/rfcs/RFC-0016-compose-manifest.md`
4. `docs/USAGE-CONTRACTS-AND-COMPOSE.md`

For current infrastructure and agent workflow notes, also see `AGENTS.md` and `.agent/README.md`.