# BoomHud Implementation Plan

> **STATUS: HISTORICAL — superseded.** This is the original v0 roadmap (Phases 0–6).
> **All six phases have shipped**, and the project has since expanded well beyond this
> plan (six rendering backends plus a motion pipeline, visual-regression, design-lint,
> 3D layout, and runtime agent surfaces). It is retained for historical context only.
> For the current architecture and status see the RFC index in
> [`docs/rfcs/`](./rfcs/README.md) and the latest handover
> [`docs/HANDOVER-2026-06-05.md`](./HANDOVER-2026-06-05.md).
> _Last reconciled with the codebase: 2026-06-05._

## Overview

This document outlines the implementation roadmap for BoomHud, organized into phases with clear milestones.

## Phase 0: Foundation (Weeks 1-2) — ✅ DELIVERED

### Goals
- Establish project structure
- Define core IR types
- Create DSL JSON Schema
- Basic JSON parser

### Tasks

- [x] **P0.1**: Project scaffolding
  - Create solution and project structure
  - Set up Directory.Build.props
  - Configure NuGet dependencies

- [x] **P0.2**: JSON Schema for DSL
  - Define schema for components
  - Define schema for layouts
  - Define schema for bindings
  - Schema validation tooling

- [x] **P0.3**: Core IR Types
  - `HudDocument`
  - `ComponentNode`
  - `LayoutSpec`
  - `BindingSpec`
  - `StyleSpec`

- [x] **P0.4**: JSON Parser
  - Parse DSL files to IR
  - Validate against schema
  - Helpful error messages

### Deliverables
- `BoomHud.Abstractions` with IR types
- `BoomHud.Dsl` with parser
- `schemas/json/boom-hud.schema.json`
- Basic unit tests

---

## Phase 1: Terminal.Gui Backend (Weeks 3-5) — ✅ DELIVERED

### Goals
- End-to-end generation for Terminal.Gui v2
- Support core components
- Imperative C# output

### Tasks

- [x] **P1.1**: Terminal.Gui Capability Manifest
  - Document supported components
  - Document layout capabilities
  - Document binding support (manual refresh)

- [x] **P1.2**: Code Generator Infrastructure
  - `IBackendGenerator` interface
  - `TerminalGuiGenerator` implementation
  - C# code emitter utilities

- [x] **P1.3**: Component Emitters
  - Label → `Label`
  - Button → `Button`
  - ProgressBar → `ProgressBar`
  - Container → `View`
  - Panel → `FrameView`

- [x] **P1.4**: Layout Emitters
  - Horizontal stack → manual X positioning
  - Vertical stack → manual Y positioning
  - Fixed dimensions
  - Fill/stretch

- [x] **P1.5**: Sample: StatusBar
  - Generate working StatusBar
  - Compare with hand-written version
  - Performance validation

### Deliverables
- `BoomHud.Gen.TerminalGui`
- Generated StatusBar matching manual implementation
- Integration tests

---

## Phase 2: Avalonia Backend (Weeks 6-8) — ✅ DELIVERED

### Goals
- AXAML generation for Avalonia
- ViewModel generation for bindings
- Leverage native Avalonia features

### Tasks

- [x] **P2.1**: Avalonia Capability Manifest
  - Full component mapping
  - Native binding support
  - XAML layout primitives

- [x] **P2.2**: AXAML Emitter
  - XML generation utilities
  - Namespace handling
  - Proper formatting

- [x] **P2.3**: Component Emitters
  - Label → `TextBlock`
  - Button → `Button`
  - ProgressBar → `ProgressBar`
  - Container → `Border`/`Panel`
  - Grid layout → `Grid`
  - Stack layout → `StackPanel`

- [x] **P2.4**: ViewModel Generation
  - Generate ViewModel base classes
  - Property change notification
  - Command bindings

- [x] **P2.5**: Sample: StatusBar
  - Generate AXAML + ViewModel
  - Compare with hand-written version

### Deliverables
- `BoomHud.Gen.Avalonia`
- Generated StatusBar with full bindings
- Integration tests

---

## Phase 3: Data Binding (Weeks 9-10) — ✅ DELIVERED

### Goals
- Unified binding syntax
- Platform-appropriate binding generation
- Converters and formatters

### Tasks

- [x] **P3.1**: Binding Syntax
  - Path expressions
  - Binding modes (OneWay, TwoWay, OneTime)
  - String formatting

- [x] **P3.2**: Terminal.Gui Binding
  - Generate refresh methods
  - Property change subscriptions
  - Manual update triggers

- [x] **P3.3**: Avalonia Binding
  - Native XAML bindings
  - Compiled bindings option
  - Converter generation

- [x] **P3.4**: Built-in Converters
  - Boolean to visibility
  - Number formatters
  - Collection to count

### Deliverables
- RFC-0004 implementation
- Cross-platform binding examples
- Converter library

---

## Phase 4: Theming & Styles (Weeks 11-12) — ✅ DELIVERED

### Goals
- Consistent styling system
- Theme support
- Platform-appropriate style generation

### Tasks

- [x] **P4.1**: Style DSL
  - Inline styles
  - Named styles
  - Style inheritance

- [x] **P4.2**: Theme System
  - Theme definition format
  - Light/dark themes
  - Color palette abstraction

- [x] **P4.3**: Terminal.Gui Styles
  - ColorScheme mapping
  - Border styles
  - Attribute mapping

- [x] **P4.4**: Avalonia Styles
  - Resource dictionary generation
  - Style classes
  - Themes as AXAML resources

### Deliverables
- RFC-0007 implementation
- Theme samples
- Style documentation

---

## Phase 5: Figma Import (Weeks 13-15) — ✅ DELIVERED

### Goals
- Import Figma JSON exports
- Convert to BoomHud DSL
- Handle design-to-code gaps

### Tasks

- [x] **P5.1**: Figma JSON Parser
  - Parse Figma export format
  - Extract component hierarchy
  - Extract styles and colors

- [x] **P5.2**: DSL Converter
  - Map Figma nodes to BoomHud components
  - Layout inference
  - Style extraction

- [x] **P5.3**: Gap Handling
  - Unsupported feature warnings
  - Manual annotation points
  - Binding placeholder generation

### Deliverables
- `BoomHud.Import.Figma`
- Figma-to-DSL CLI command
- Import documentation

---

## Phase 6: Polish & Documentation (Weeks 16-17) — ✅ DELIVERED

### Goals
- CLI tool polish
- Comprehensive documentation
- Sample gallery

### Tasks

- [x] **P6.1**: CLI Tool
  - `boom generate` command
  - `boom validate` command
  - `boom init` scaffolding
  - Watch mode for development (not pursued)

- [x] **P6.2**: Documentation
  - Getting started guide
  - Component reference
  - Backend comparison guide
  - Migration guide from manual code

- [x] **P6.3**: Samples
  - StatusBar (all backends)
  - InventoryPanel
  - CharacterSheet
  - DialoguePanel
  - Full game HUD

### Deliverables
- Production-ready CLI
- Documentation site/markdown
- Sample gallery

---

## What actually came next (beyond the original plan)

The original backlog anticipated a MAUI backend as the next phase. **MAUI was never pursued** — no `BoomHud.Gen.Maui` project exists. Instead, the project delivered the following additional backends and capabilities.

### Additional backends delivered

- **Native Godot TSCN backend** — [RFC-0013](./rfcs/RFC-0013-native-godot-tscn-backend.md) — projects `BoomHud.Gen.Godot`, `BoomHud.Godot.Runtime`
- **Unity UI Toolkit backend** — [RFC-0012](./rfcs/RFC-0012-unity-uitoolkit-backend.md) — project `BoomHud.Gen.Unity`
- **Replayable uGUI synthesis** — [RFC-0022](./rfcs/RFC-0022-replayable-ugui-synthesis.md) — project `BoomHud.Gen.UGui`
- **React backend + Remotion motion pipeline** — [RFC-0018](./rfcs/RFC-0018-react-remotion-motion-pipeline.md), [RFC-0019](./rfcs/RFC-0019-remotion-motion-ir-authoring.md), [RFC-0020](./rfcs/RFC-0020-ui-motion-ir-contract-hardening.md) — projects `BoomHud.Gen.React`, `BoomHud.Gen.Remotion`
- **Pencil.dev agentic-UI integration** — [RFC-0014](./rfcs/RFC-0014-pencil-dev-integration.md) — projects `BoomHud.Dsl.Pencil`, `BoomHud.Gen.Pencil`

### Additional capabilities delivered

- **Dual-generation architecture** (Design→UI + MVVM adapters) — [RFC-0008](./rfcs/RFC-0008-dual-generation-architecture.md)
- **Runtime theming, multi-variant** — [RFC-0009](./rfcs/RFC-0009-runtime-theming.md)
- **Snapshot visual regression** — [RFC-0015](./rfcs/RFC-0015-snapshot-visual-regression.md)
- **Compose manifest** — [RFC-0016](./rfcs/RFC-0016-compose-manifest.md)
- **Design lint** — [RFC-0017](./rfcs/RFC-0017-design-lint.md)
- **Visual fidelity architecture** — [RFC-0021](./rfcs/RFC-0021-visual-fidelity-architecture.md)
- **Hybrid source/semantic fidelity pipeline** — [RFC-0023](./rfcs/RFC-0023-hybrid-source-semantic-fidelity-pipeline.md)
- **Spatial (3D) layout dimension** — [RFC-0024](./rfcs/RFC-0024-spatial-3d-layout-dimension.md)
- **Runtime agent surfaces** — [RFC-0025](./rfcs/RFC-0025-runtime-agent-surfaces.md)
- **Shared generator infrastructure** extracted into `BoomHud.Generators` (GeneratorSourceId, GeneratorEmit, ComposeEmitter) during the 2026-06-05 cleanup pass.

---

## Dependencies

```
Phase 0 ──► Phase 1 ──► Phase 3
              │
              ▼
           Phase 2 ──► Phase 4
              │
              ▼
           Phase 5 ──► Phase 6
```

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Layout semantic gap (cells vs pixels) | Abstract to logical units, let backends interpret |
| Binding complexity | Start simple (OneWay), add complexity incrementally |
| Figma format changes | Version-lock Figma plugin/export format |
| Performance overhead | Benchmark early and often vs hand-written code |
