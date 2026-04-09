# BoomHud Agent Infrastructure

Quick overview of the agent infrastructure and project conventions for the **boom-hud** repo.

> **Complete infrastructure docs**: See [`.agent/README.md`](.agent/README.md).

## What Is BoomHud?

BoomHud is a build-time code generator that transforms declarative UI definitions (JSON DSL via Figma/Pencil) into framework-specific implementations such as Terminal.Gui, Avalonia, MAUI, Unity UI Toolkit, and related backends.

## Source Of Truth

- Treat `.agent/` as the source of truth for agent customization content.
- Treat `.kilo/` as generated sync output for Kilo Code. Do not hand-edit files there.
- Put Kilo-specific source content under `.agent/adapters/kilo/` and sync it into `.kilo/`.
- Use this `AGENTS.md` as the canonical project-level agent guidance. `CLAUDE.md` is only a compatibility pointer.

## Quick Start

Start here when you are new to the repo:

1. [`.agent/README.md`](.agent/README.md) - Complete infrastructure overview
2. [`docs/rfcs/RFC-0001-core-architecture.md`](docs/rfcs/RFC-0001-core-architecture.md) - Core architecture
3. [`docs/IMPLEMENTATION-PLAN.md`](docs/IMPLEMENTATION-PLAN.md) - Implementation roadmap

## Key Commands

```bash
# Build
dotnet build dotnet/BoomHud.sln

# Test
dotnet test dotnet/BoomHud.sln

# Generate from DSL (Figma JSON)
dotnet run --project dotnet/src/BoomHud.Cli -- generate samples/dotnet/BoomHud.Sample.Generation/design/status-bar.json --target terminalGui

# Sync agent adapter outputs
task agent:sync
```

## Directory Structure

```text
.agent/
├── README.md           # Complete overview (start here)
├── adapters/           # Tool-specific sync configuration and overlays
├── rules/              # Shared development rules
├── skills/             # AgentSkills-format skills (SKILL.md)
├── commands/           # Shared command/workflow sources
└── workflows/          # Multi-step development processes
```

## Project Structure

- `schemas/` - JSON Schema for DSL validation and generation contracts
- `dotnet/src/BoomHud.Abstractions/` - Core IR and abstractions
- `dotnet/src/BoomHud.Dsl/` - JSON parser and DSL handling
- `dotnet/src/BoomHud.Gen.*` - Backend generators
- `samples/` - Reference samples, comparison projects, and fixtures

## Core Principles

1. **Schema-First Design** - Define DSL schema before implementing parsers or generators.
2. **IR-Centric** - All transformations go through the Intermediate Representation.
3. **Capability-Aware** - Handle framework limitations explicitly.
4. **Build-Time Generation** - No runtime reflection or generator overhead.

## Common Tasks

### Add A New Component

1. Define the component in `schemas/json/boom-hud.schema.json`.
2. Add IR representation in `BoomHud.Abstractions`.
3. Implement generation for each target backend.
4. Add tests and samples.

### Add A New Backend

1. Create a capability manifest for the framework.
2. Implement `IBackendGenerator`.
3. Handle capability gaps with explicit policies.
4. Add integration tests.

### Modify The DSL

1. Update `schemas/json/boom-hud.schema.json` first.
2. Keep the IR aligned with the schema.
3. Update parsers and generators only after the schema contract is clear.

## Platform Support

- **Kilo Code** - Uses generated `.kilo/` output synced from `.agent/`.
- **Claude Code** - Reads this `AGENTS.md`; `CLAUDE.md` is a pointer only.
- **Windsurf** - Uses `.agent/` rules.
- **GitHub Copilot** - Uses parent platform configuration.
- **Cursor** - Uses parent platform configuration.

## Resources

- [`docs/rfcs/RFC-0001-core-architecture.md`](docs/rfcs/RFC-0001-core-architecture.md) - Architecture overview
- [`docs/rfcs/RFC-0002-component-model.md`](docs/rfcs/RFC-0002-component-model.md) - Component model
- [`docs/rfcs/RFC-0004-data-binding.md`](docs/rfcs/RFC-0004-data-binding.md) - Data binding
- [`.agent/README.md`](.agent/README.md) - Agent infrastructure details
- [`../../../AGENTS.md`](../../../AGENTS.md) - Parent project agent infrastructure
