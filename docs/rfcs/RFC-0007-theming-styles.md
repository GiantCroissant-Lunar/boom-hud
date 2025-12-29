# RFC-0007: Theming & Styles

- **Status**: Accepted
- **Created**: 2025-12-12
- **Authors**: BoomHud Contributors

## Summary

Define a theming and styling system for BoomHud that supports:

- Named style classes and inline style overrides
- Design tokens (colors, dimensions, font sizes)
- Runtime theme switching with multiple variants (not limited to light/dark)

## Motivation

BoomHud already has IR support for theme tokens (`ThemeDocument`, `StyleSpec.*Token`) and partial generator support (Avalonia resource emission). We need a consistent cross-backend theming model that can be driven by Figma Variables / design token exports and switched at runtime.

## Goals

- Define token types and naming conventions
- Define how styles reference tokens
- Define runtime theme switching semantics
- Define backend responsibilities (Avalonia vs Terminal.Gui)

## Non-Goals

- Full CSS-like cascading/inheritance system
- Animation/transition theming
- A visual theme editor

## Design

### Token Model

A theme is a set of typed tokens:

- **Colors**: `Dictionary<string, Color>`
- **Dimensions**: `Dictionary<string, double>` (interpreted per backend)
- **FontSizes**: `Dictionary<string, double>`

Token keys are normalized to dot-separated identifiers (e.g. `brand.primary`, `surface.card`, `font.size.body`).

### Style Model

Styles can be expressed as:

- **Inline values** (e.g. literal hex color)
- **Token references** (e.g. `ForegroundToken = "surface.text"`)

Generators must prefer token references when provided and fall back to literal values.

### Theme Variants & Runtime Switching

A project may define multiple theme variants (e.g. `Light`, `Dark`, `HighContrast`, `Halloween`).

- Theme switching is a runtime concern.
- Generated output must expose a minimal API for switching the active theme.

### Backend Strategy

#### Avalonia

- Emit theme resources into `ResourceDictionary` instances.
- Use **dynamic resource references** (e.g. `DynamicResource`) for token-backed values so that runtime switching updates the UI.
- Provide a generated helper (e.g. `ThemeManager`) that swaps dictionaries / merged dictionaries.

#### Terminal.Gui

- Terminal.Gui has no native resource system.
- Generated views should resolve token-backed colors via a runtime `IThemeProvider`/`ThemeManager` and apply them to `ColorScheme`.
- Switching themes triggers re-application of styles and a redraw.

### Figma Variables Integration

Figma variables JSON is mapped to token keys. Modes map to variants.

- Collection name + mode name/id select a variant.
- Multiple modes should be imported into multiple variants when requested.

## Backward Compatibility

- Existing literal-only style definitions remain valid.
- Token references are additive.

## Alternatives Considered

- Single global theme only (rejected: user requires multi-variant runtime switching)
- Backend-specific theming DSL (rejected: breaks portability)

## Open Questions

- Do we add token references to layout values (padding/margin/gap) or keep those literal-only initially?
- Should theme switching be global or scoped per view instance?

## Related RFCs

- RFC-0001: Core Architecture
- RFC-0002: Component Model
- RFC-0004: Data Binding
- RFC-0008: Dual Generation Architecture
- RFC-0009: Runtime Theming
