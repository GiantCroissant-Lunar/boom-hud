# BoomHud Agent Infrastructure

This directory contains agent configuration, rules, and workflows for the BoomHud project.

## Quick Start

1. **Understand the architecture**: Read [RFC-0001](../docs/rfcs/RFC-0001-core-architecture.md)
2. **Follow the rules**: See `rules/` for development guidelines
3. **Use workflows**: See `workflows/` for step-by-step processes

## Directory Structure

```
.agent/
├── README.md           # This file
├── adapters/           # Tool-specific sync configuration
├── rules/              # Development rules and guidelines
│   ├── schema-first.md          # Schema-first design patterns
│   ├── code-generation.md       # Generator implementation rules
│   └── capability-mapping.md    # Framework capability rules
├── skills/             # AgentSkills-format skills (SKILL.md)
├── workflows/          # Multi-step processes
│   ├── add-component.md         # Adding new component types
│   └── add-backend.md           # Adding new backend generators
```

## Core Principles

### 1. Schema-First Design

- Define DSL schema (`schemas/boom-hud.schema.json`) before implementing parsers
- Schema is the contract between DSL and IR
- Validate all input against schema

### 2. IR-Centric Architecture

- All transformations go through the Intermediate Representation
- Never generate directly from DSL to output
- IR enables cross-backend analysis and optimization

### 3. Capability-Aware Generation

- Each backend declares its capabilities via `ICapabilityManifest`
- Generator checks capabilities before emitting code
- Missing capabilities handled via configurable policies

### 4. Build-Time Generation

- No runtime reflection or overhead
- Generated code should be readable and debuggable
- Prefer CLI tool over Roslyn source generators for flexibility

## Key Patterns

### Adding a New Component

1. Add to DSL schema (`schemas/boom-hud.schema.json`)
2. Add IR type if needed (`BoomHud.Abstractions/IR/`)
3. Add to each backend's capability manifest
4. Implement emitter for each backend
5. Add tests

### Adding a New Backend

1. Create `BoomHud.Gen.{Framework}` project
2. Implement `ICapabilityManifest`
3. Implement `IBackendGenerator`
4. Add component emitters
5. Add integration tests

## Build Commands

```bash
# From dotnet/ directory
dotnet restore BoomHud.sln
dotnet build BoomHud.sln
dotnet test BoomHud.sln
```

## Sync Commands

This repo can generate tool-specific agent directories (for Windsurf/Cursor/Cline/etc.) from `.agent/`.

```bash
task agent:sync
```

## Related Documentation

- [RFC-0001: Core Architecture](../docs/rfcs/RFC-0001-core-architecture.md)
- [RFC-0002: Component Model](../docs/rfcs/RFC-0002-component-model.md)
- [Implementation Plan](../docs/IMPLEMENTATION-PLAN.md)
