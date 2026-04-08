# BoomHud Implementation Plan

## Overview

This document outlines the implementation roadmap for BoomHud, organized into phases with clear milestones.

## Phase 0: Foundation (Weeks 1-2)

### Goals
- Establish project structure
- Define core IR types
- Create DSL JSON Schema
- Basic JSON parser

### Tasks

- [ ] **P0.1**: Project scaffolding
  - Create solution and project structure
  - Set up Directory.Build.props
  - Configure NuGet dependencies

- [ ] **P0.2**: JSON Schema for DSL
  - Define schema for components
  - Define schema for layouts
  - Define schema for bindings
  - Schema validation tooling

- [ ] **P0.3**: Core IR Types
  - `HudDocument`
  - `ComponentNode`
  - `LayoutSpec`
  - `BindingSpec`
  - `StyleSpec`

- [ ] **P0.4**: JSON Parser
  - Parse DSL files to IR
  - Validate against schema
  - Helpful error messages

### Deliverables
- `BoomHud.Abstractions` with IR types
- `BoomHud.Dsl` with parser
- `schemas/json/boom-hud.schema.json`
- Basic unit tests

---

## Phase 1: Terminal.Gui Backend (Weeks 3-5)

### Goals
- End-to-end generation for Terminal.Gui v2
- Support core components
- Imperative C# output

### Tasks

- [ ] **P1.1**: Terminal.Gui Capability Manifest
  - Document supported components
  - Document layout capabilities
  - Document binding support (manual refresh)

- [ ] **P1.2**: Code Generator Infrastructure
  - `IBackendGenerator` interface
  - `TerminalGuiGenerator` implementation
  - C# code emitter utilities

- [ ] **P1.3**: Component Emitters
  - Label → `Label`
  - Button → `Button`
  - ProgressBar → `ProgressBar`
  - Container → `View`
  - Panel → `FrameView`

- [ ] **P1.4**: Layout Emitters
  - Horizontal stack → manual X positioning
  - Vertical stack → manual Y positioning
  - Fixed dimensions
  - Fill/stretch

- [ ] **P1.5**: Sample: StatusBar
  - Generate working StatusBar
  - Compare with hand-written version
  - Performance validation

### Deliverables
- `BoomHud.Gen.TerminalGui`
- Generated StatusBar matching manual implementation
- Integration tests

---

## Phase 2: Avalonia Backend (Weeks 6-8)

### Goals
- AXAML generation for Avalonia
- ViewModel generation for bindings
- Leverage native Avalonia features

### Tasks

- [ ] **P2.1**: Avalonia Capability Manifest
  - Full component mapping
  - Native binding support
  - XAML layout primitives

- [ ] **P2.2**: AXAML Emitter
  - XML generation utilities
  - Namespace handling
  - Proper formatting

- [ ] **P2.3**: Component Emitters
  - Label → `TextBlock`
  - Button → `Button`
  - ProgressBar → `ProgressBar`
  - Container → `Border`/`Panel`
  - Grid layout → `Grid`
  - Stack layout → `StackPanel`

- [ ] **P2.4**: ViewModel Generation
  - Generate ViewModel base classes
  - Property change notification
  - Command bindings

- [ ] **P2.5**: Sample: StatusBar
  - Generate AXAML + ViewModel
  - Compare with hand-written version

### Deliverables
- `BoomHud.Gen.Avalonia`
- Generated StatusBar with full bindings
- Integration tests

---

## Phase 3: Data Binding (Weeks 9-10)

### Goals
- Unified binding syntax
- Platform-appropriate binding generation
- Converters and formatters

### Tasks

- [ ] **P3.1**: Binding Syntax
  - Path expressions
  - Binding modes (OneWay, TwoWay, OneTime)
  - String formatting

- [ ] **P3.2**: Terminal.Gui Binding
  - Generate refresh methods
  - Property change subscriptions
  - Manual update triggers

- [ ] **P3.3**: Avalonia Binding
  - Native XAML bindings
  - Compiled bindings option
  - Converter generation

- [ ] **P3.4**: Built-in Converters
  - Boolean to visibility
  - Number formatters
  - Collection to count

### Deliverables
- RFC-0004 implementation
- Cross-platform binding examples
- Converter library

---

## Phase 4: Theming & Styles (Weeks 11-12)

### Goals
- Consistent styling system
- Theme support
- Platform-appropriate style generation

### Tasks

- [ ] **P4.1**: Style DSL
  - Inline styles
  - Named styles
  - Style inheritance

- [ ] **P4.2**: Theme System
  - Theme definition format
  - Light/dark themes
  - Color palette abstraction

- [ ] **P4.3**: Terminal.Gui Styles
  - ColorScheme mapping
  - Border styles
  - Attribute mapping

- [ ] **P4.4**: Avalonia Styles
  - Resource dictionary generation
  - Style classes
  - Themes as AXAML resources

### Deliverables
- RFC-0007 implementation
- Theme samples
- Style documentation

---

## Phase 5: Figma Import (Weeks 13-15)

### Goals
- Import Figma JSON exports
- Convert to BoomHud DSL
- Handle design-to-code gaps

### Tasks

- [ ] **P5.1**: Figma JSON Parser
  - Parse Figma export format
  - Extract component hierarchy
  - Extract styles and colors

- [ ] **P5.2**: DSL Converter
  - Map Figma nodes to BoomHud components
  - Layout inference
  - Style extraction

- [ ] **P5.3**: Gap Handling
  - Unsupported feature warnings
  - Manual annotation points
  - Binding placeholder generation

### Deliverables
- `BoomHud.Import.Figma`
- Figma-to-DSL CLI command
- Import documentation

---

## Phase 6: Polish & Documentation (Weeks 16-17)

### Goals
- CLI tool polish
- Comprehensive documentation
- Sample gallery

### Tasks

- [ ] **P6.1**: CLI Tool
  - `boom generate` command
  - `boom validate` command
  - `boom init` scaffolding
  - Watch mode for development

- [ ] **P6.2**: Documentation
  - Getting started guide
  - Component reference
  - Backend comparison guide
  - Migration guide from manual code

- [ ] **P6.3**: Samples
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

## Future Phases (Backlog)

### MAUI Backend
- XAML generation for MAUI
- Mobile-specific considerations

### Hot Reload
- Design-time preview
- Live DSL editing

### Visual Designer
- VS Code extension
- WYSIWYG editing

### Animation System
- Declarative animations
- Platform-specific generation

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
