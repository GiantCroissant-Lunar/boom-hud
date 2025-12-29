# RFC-0009: Runtime Theming (Multi-Variant)

- **Status**: Accepted
- **Created**: 2025-12-12
- **Authors**: BoomHud Contributors

## Summary

Define how BoomHud supports runtime theme switching across multiple variants (not limited to light/dark), including:

- Theme variant identification and selection
- Token key stability
- Backend-specific runtime application

## Motivation

BoomHud needs to support theme switching at runtime for:

- accessibility
- user preference
- game state / seasonal events

## Goals

- Support many variants per project
- Make token usage in generated UI update when theme changes
- Keep the runtime API minimal and backend-appropriate

## Non-Goals

- Cross-fade animations between themes
- Arbitrary theme merging at runtime

## Design

### Theme Variants

A theme source may provide multiple variants:

- Figma Variables: `collection + mode` maps to a variant
- DesignTokens JSON: `spread.<variant>` maps to a variant

Variants have stable names (string identifiers).

### Runtime API

Generated output should expose a minimal API:

- `GetAvailableThemes(): IReadOnlyList<string>`
- `SetTheme(string variantName)`

Theme application supports two scopes:

- **Global scope**: affects all generated views that use the global theme provider.
- **Per-view instance scope**: a specific view instance can override the global theme.

### Avalonia

- Emit one `ResourceDictionary` per variant.
- Use dynamic resource references for token-backed values.
- `SetTheme` swaps the active dictionary (merged dictionary replacement).

For per-view instance scope, generated views may host a local `ResourceDictionary` (or merged dictionary) that takes precedence over application-level resources.

### Terminal.Gui

- Use a runtime `ThemeManager` that maps token keys to Terminal.Gui colors.
- `SetTheme` triggers re-application of `ColorScheme`/styles and redraw.

For per-view instance scope, views may accept an optional `IThemeProvider` that overrides the global provider.

### Token Key Rules

- Token keys are stable across variants.
- Missing keys in a variant are handled via fallback rules:
  - error / warn / ignore (policy-controlled)

## Backward Compatibility

- If only a single theme is provided, runtime switching is optional.

## Alternatives Considered

- StaticResource-only Avalonia tokens (rejected: does not update on runtime switch)

## Open Questions

- Where should the default theme be configured (CLI option, MSBuild property, runtime call)?
- How should per-view theme override interact with global changes (inherit vs lock to a specific variant)?

## Related RFCs

- RFC-0007: Theming & Styles
- RFC-0008: Dual Generation Architecture
