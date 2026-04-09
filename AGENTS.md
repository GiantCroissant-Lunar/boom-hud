# BoomHud Agent Infrastructure

Quick overview of the agent infrastructure for the **boom-hud** project.

> **Complete documentation**: See [`.agent/README.md`](.agent/README.md) for comprehensive infrastructure.

## Quick Start

**New to boom-hud?** Start here:

1. **[`.agent/README.md`](.agent/README.md)** - Complete infrastructure overview
2. **[`docs/rfcs/RFC-0001-core-architecture.md`](docs/rfcs/RFC-0001-core-architecture.md)** - Architecture overview
3. **[`docs/IMPLEMENTATION-PLAN.md`](docs/IMPLEMENTATION-PLAN.md)** - Implementation roadmap

## Directory Structure

```
.agent/
├── README.md           # Complete overview (START HERE)
├── adapters/           # Tool-specific sync configuration
├── rules/              # Development rules
│   ├── schema-first.md          # DSL schema patterns
│   ├── code-generation.md       # Generator patterns
│   └── capability-mapping.md    # Framework capability rules
├── skills/             # AgentSkills-format skills (SKILL.md)
├── workflows/          # Multi-step development processes
│   ├── add-component.md         # Adding new component types
│   └── add-backend.md           # Adding new backend generators
```

## Core Principles

1. **Schema-First Design** - Define DSL schema before implementing parsers/generators
2. **IR-Centric** - All transformations go through the Intermediate Representation
3. **Capability-Aware** - Explicit handling of framework limitations
4. **Build-Time Generation** - No runtime reflection or overhead

## Key Patterns

### Adding a New Component

1. Define component in DSL schema (`schemas/json/boom-hud.schema.json`)
2. Add IR representation (`BoomHud.Abstractions`)
3. Implement generator for each backend
4. Add tests and samples

### Adding a New Backend

1. Create capability manifest for the framework
2. Implement `IBackendGenerator` interface
3. Handle capability gaps with policies
4. Add integration tests

## Platform Support

- **Claude Code** - See [`CLAUDE.md`](CLAUDE.md)
- **Windsurf** - Uses `.agent/` rules
- **GitHub Copilot** - Uses parent platform configuration
- **Cursor** - Uses parent platform configuration

## Resources

- **[RFC-0001](docs/rfcs/RFC-0001-core-architecture.md)** - Architecture overview
- **[Parent AGENTS.md](../../../AGENTS.md)** - Parent project agent infrastructure
