# Consuming BoomHud

This document describes the supported ways to consume BoomHud today. The short version is:

- most consumers should use the CLI and commit generated output
- advanced consumers can reference parser and backend packages directly
- the repo's `ui/` folder is a dogfood workspace, not the package you consume

## Recommended Path: Use The CLI

Treat BoomHud as a code-generation tool that turns input sources plus a manifest into framework-native output.

Supported inputs in the current pipeline:

- Figma JSON
- Pencil `.pen`
- IR JSON
- optional annotations and token registry files

Typical CLI usage from this repo:

```powershell
dotnet run --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj --configuration Release -- \
	generate --manifest ui/boom-hud.compose.json --target godot --output artifacts/godot
```

Single-input example:

```powershell
dotnet run --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj --configuration Release -- \
	generate samples/dotnet/BoomHud.Sample.Generation/design/status-bar.json \
	--target terminalgui \
	--output out/terminalgui \
	--namespace MyApp.Ui.Hud
```

The CLI owns:

- input loading and format detection
- multi-source composition
- token resolution
- backend target resolution
- file emission and command orchestration

## Tool Packaging

The CLI packs as the local tool package `BoomHud.Tool`.

Example local pack/install flow:

```powershell
dotnet pack dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj --configuration Release --output build/_artifacts/boom-hud/nuget
dotnet tool install --global BoomHud.Tool --add-source build/_artifacts/boom-hud/nuget
```

Then run:

```powershell
boom-hud generate --manifest ui/boom-hud.compose.json --target terminalgui --output artifacts/terminalgui
```

## Package-Level Consumption

If you want to run generation inside your own tooling or build pipeline, reference the package boundary that matches your need.

Current split-ready package identities:

- `BoomHud.Foundation`
- `BoomHud.Foundation.Generators`
- `BoomHud.Input.Figma`
- `BoomHud.Input.Pencil`
- `BoomHud.TerminalGui`
- `BoomHud.Godot`
- `BoomHud.Tool`

Typical direct-reference combinations:

- parse only: `BoomHud.Foundation` + one input package
- custom generator host: `BoomHud.Foundation` + `BoomHud.Foundation.Generators` + one backend package
- normal repo consumption: `BoomHud.Tool`

Current nuance:

- Avalonia generation exists in-source and is available through the CLI.
- Avalonia is not yet part of the first-pass package graph contract documented above.

## Compose Manifest Workflow

For repeatable generation, prefer a compose manifest over ad hoc command-line input lists.

The repo dogfood manifest is `ui/boom-hud.compose.json`. It defines the current convention for:

- source list
- token registry path
- output path defaults
- namespace
- targets

The repo convention is to keep generated output under `ui/generated`. External consumers should keep the same idea: pick a stable generated folder inside the app repo and commit it intentionally.

## Generated Output Strategy

BoomHud emits framework-native files. The exact file set depends on the backend and options, but the common expectation is:

- generated source files live under a stable folder in your app repo
- your app treats them as normal source assets
- your app implements or consumes the generated ViewModel contracts as needed

For compose and contract-oriented flows, see `docs/USAGE-CONTRACTS-AND-COMPOSE.md`.

## When To Use APIs Directly

Use the packages directly when you need one of these:

- generation embedded in your own build orchestration
- strict control over output paths and file writing
- custom tooling around `HudDocument`
- backend-specific post-processing in the same pipeline

The programmatic shape is still the same core pipeline:

1. Parse input into `HudDocument`.
2. Apply composition and tokens.
3. Build `GenerationOptions`.
4. Invoke the selected backend generator.
5. Write generated files to disk.

## Repo-Specific Clarification

`ui/` is not the product source tree. It is the repo's consumer-style workspace used to exercise the toolchain end to end.

That distinction matters:

- `dotnet/` is where BoomHud itself is built
- `ui/` is where BoomHud is consumed inside this repo
- `samples/` are examples, not the primary operational flow

## Current Limitations

- the split package graph is currently enforced for Foundation, Figma, Pencil, Terminal.Gui, Godot, and the tool
- Avalonia is present but not yet brought into the same package-separation guardrails
- consumer docs should assume the CLI path is the most stable entrypoint unless there is a specific reason to host generation yourself
