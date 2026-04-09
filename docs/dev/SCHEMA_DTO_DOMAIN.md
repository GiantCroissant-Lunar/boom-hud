# Schema → DTO → Domain Pattern

This document describes how BoomHud manages schema-driven data structures.

## Principles

1. **Schemas are source of truth** — JSON Schema files in `schemas/` define the contract
2. **Generated DTOs are isolated** — `*.g.cs` files live in `Generated/` subfolders
3. **Domain wrappers own policy** — Load, validate, resolve paths, emit diagnostics
4. **CLI never touches DTOs** — Command handlers consume domain wrappers only

## Folder Layout

```
schemas/
  compose.schema.json          # boom-hud.compose.json schema
  motion.schema.json           # motion.ir.json schema
  states.schema.json           # *.states.json schema
  tokens.schema.json           # tokens.ir.json schema
  figma-export.schema.json     # External Figma schema

dotnet/src/BoomHud.Abstractions/
  Composition/
    ComposeManifest.cs         # Domain wrapper
    Generated/
      ComposeManifestDto.g.cs  # quicktype output (DO NOT EDIT)
  Motion/
    MotionDocument.cs          # Domain wrapper
    Generated/
      MotionDocumentDto.g.cs   # quicktype output (DO NOT EDIT)
  Snapshots/
    SnapshotStatesManifest.cs  # Domain wrapper
    Generated/
      StatesManifestDto.g.cs   # quicktype output (DO NOT EDIT)
  Tokens/
    TokenRegistry.cs           # Domain wrapper
    Generated/
      TokensDto.g.cs           # quicktype output (DO NOT EDIT)
```

## Namespaces

| Area | DTO Namespace | Wrapper Namespace |
|------|---------------|-------------------|
| Compose | `BoomHud.Abstractions.Composition.Generated` | `BoomHud.Abstractions.Composition` |
| Motion | `BoomHud.Abstractions.Motion.Generated` | `BoomHud.Abstractions.Motion` |
| States | `BoomHud.Abstractions.Snapshots.Generated` | `BoomHud.Abstractions.Snapshots` |
| Tokens | `BoomHud.Abstractions.Tokens.Generated` | `BoomHud.Abstractions.Tokens` |

Wrappers may `using ...Generated;` internally.
CLI must NOT reference `*.Generated` namespaces.

## Regeneration

To regenerate DTOs from schemas:

```bash
task generate:dto
```

This runs quicktype on each schema and outputs to the correct `Generated/` folder.

**Generated files are committed** so CI doesn't need Node/quicktype at build time.

## Adding a New Schema-Driven Type

1. Create `schemas/<name>.schema.json`
2. Add quicktype invocation to `Taskfile.yml` under `generate:dto`
3. Create `Generated/<Name>Dto.g.cs` via quicktype
4. Create domain wrapper `<Name>.cs` that:
   - Holds DTO internally
   - Exposes `Load(path)` / `LoadFromJson(json)`
   - Validates and emits diagnostics
   - Exposes clean domain API

## What Stays Hand-Written

| Model | Reason |
|-------|--------|
| `FigmaDto.cs` | Already generated, external schema, stable |
| `BaselineReport.cs` | Output model (not schema-driven input) |
| `PenDto.cs` | Format evolving, schema-gen would create churn |

## No-Leak Rule

**BoomHud.Cli must not reference `*.Generated` namespaces.**

This is enforced by CI:

```powershell
# scripts/verify-no-dto-leak.ps1
$leaks = Select-String -Path "dotnet/src/BoomHud.Cli/**/*.cs" -Pattern "\.Generated" -Recurse
if ($leaks) {
  $leaks | ForEach-Object { Write-Host "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
  throw "BoomHud.Cli must not reference *.Generated namespaces (use domain wrappers instead)."
}
```

## Why This Pattern?

- **Review clarity**: `git diff` clearly shows Generated vs hand-written changes
- **IntelliSense isolation**: DTOs don't pollute autocomplete in CLI
- **Refactor safety**: Schema changes → regenerate DTOs → update wrapper → CLI unchanged
- **Contract alignment**: DTOs mirror RFC-0015/0016/0017 JSON contracts exactly
