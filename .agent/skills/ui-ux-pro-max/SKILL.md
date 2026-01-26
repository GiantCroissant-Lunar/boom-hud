---
name: ui-ux-pro-max
description: Generate UI/UX design system recommendations (style, colors, typography, UX guidelines) and apply them to BoomHud/Figma variables and generated UI. Use when designing HUD/UI, choosing palettes, auditing UX quality, or preparing theme tokens.
license: MIT
compatibility: Requires Python 3.x. Intended to be used from the boom-hud repo root.
---

# UI/UX Pro Max

## When to use this skill

Use this skill when you are:

- Designing or refining a HUD/UI in BoomHud
- Choosing a style direction (e.g., HUD/FUI, Minimalism, Glassmorphism)
- Selecting color palettes and typography pairings
- Auditing UX quality (accessibility, interaction affordances)
- Creating or validating a design system before implementing UI

## Preconditions

This BoomHud skill requires the UI/UX Pro Max python tooling to be available locally.

## How to run

All commands below assume you are in the `boom-hud` repo root.

### 1) Generate a full design system (recommended)

```powershell
dotnet run -c Release --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- uipro "hud sci-fi game ui" --design-system -f markdown -p "BoomHud"
```

### 2) Search a specific domain

```powershell
dotnet run -c Release --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- uipro "glassmorphism" --domain style
dotnet run -c Release --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- uipro "wcag focus states" --domain ux
dotnet run -c Release --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- uipro "fintech palette" --domain color
```

### 3) Stack guidelines

Even though BoomHud generates for multiple UI frameworks, stack-specific UX guidance can still be useful:

```powershell
dotnet run -c Release --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- uipro "forms" --stack html-tailwind
```

## How to apply results to BoomHud

- **Preferred path**: apply colors/typography in Figma variables, then use BoomHud `--variables` to generate theme tokens.
- **BoomHud CLI**: you can also use `boom-hud uipro ...` (if installed) to run this tool via the BoomHud CLI.

## Notes

- This skill is a wrapper around the upstream toolkit.
- BoomHud currently supports theme tokens via Figma variables (`--variables`) and emits token resources in Avalonia.
