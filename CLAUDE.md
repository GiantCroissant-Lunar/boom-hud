# BoomHud - Claude Quick Reference

## What is BoomHud?

A build-time code generator that transforms declarative UI definitions (JSON DSL via Figma) into framework-specific implementations (Terminal.Gui, Avalonia, MAUI).

## Key Commands

```bash
# Build
dotnet build dotnet/BoomHud.sln

# Test
dotnet test dotnet/BoomHud.sln

# Generate from DSL (Figma JSON)
dotnet run --project dotnet/src/BoomHud.Cli -- generate samples/dotnet/BoomHud.Sample.Generation/design/status-bar.json --target terminalGui
```

## Project Structure

- `schemas/` - JSON Schema for DSL validation
- `dotnet/src/BoomHud.Abstractions/` - Core IR types
- `dotnet/src/BoomHud.Dsl/` - JSON parser
- `dotnet/src/BoomHud.Gen.*` - Backend generators

## Design Principles

1. **Schema-First**: DSL schema defines the contract
2. **IR-Centric**: Parse → IR → Generate (never direct transformation)
3. **Capability-Aware**: Handle framework limitations explicitly

## Common Tasks

| Task | Location |
|------|----------|
| Add component type | `schemas/`, `Abstractions/`, each `Gen.*` |
| Add backend | New `BoomHud.Gen.{Framework}` project |
| Modify DSL | Update `schemas/json/boom-hud.schema.json` first |

## RFCs

- RFC-0001: Core Architecture
- RFC-0002: Component Model
- RFC-0003: Layout System
- RFC-0004: Data Binding
- RFC-0005: Backend Adapters

See [`docs/rfcs/`](docs/rfcs/) for details.
