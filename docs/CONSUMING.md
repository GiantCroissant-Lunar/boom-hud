# Consuming BoomHud (Avalonia-first)

This doc describes the **supported, repeatable** ways to consume BoomHud output in another repo (e.g. `fanta-world-window`).

BoomHud currently produces **generated, framework-native files** (AXAML + code-behind + ViewModel interface) from a **design source** (currently: Figma JSON + optional Figma variables for theme tokens).

## Option A (recommended): Generate files via the BoomHud CLI

This is the lowest-friction approach: treat BoomHud as a codegen tool that outputs files you commit into the consuming repo.

### 1) Prepare inputs

- Design file: Figma JSON export (see `dotnet/samples/BoomHud.Sample.Generation/design/*.json` for examples)
- Optional: Figma variables JSON for theme tokens

Bindings:

- BoomHud supports explicit bindings via a simple node-name convention.
- Add a trailing bracket segment like: `Health Value [bind: Health]`
- For `TEXT` nodes, this converts the static text into a binding and will generate the corresponding ViewModel interface property.

### 2) Run the generator

From the BoomHud repo:

```powershell
Set-Location D:\lunar-snake\personal-work\plate-projects\boom-hud\dotnet

# Run the CLI directly from source
dotnet run -c Release --project .\src\BoomHud.Cli\BoomHud.Cli.csproj -- generate .\samples\BoomHud.Sample.Generation\design\status-bar.json --target avalonia --output .\_out\boom-hud\avalonia --namespace MyApp.Ui.Hud

# Optional: include Figma variables for theme tokens
# dotnet run -c Release --project .\src\BoomHud.Cli\BoomHud.Cli.csproj -- generate .\path\to\design.json --target avalonia --output .\_out\boom-hud\avalonia --namespace MyApp.Ui.Hud --variables .\path\to\variables.json
```

Outputs:

- `*.axaml`
- `*.axaml.cs`
- `I*ViewModel.g.cs`

If you want to install the CLI as a local tool for convenience, you can pack and install it from a local source:

```powershell
Set-Location D:\lunar-snake\personal-work\plate-projects\boom-hud\dotnet
dotnet pack -c Release .\src\BoomHud.Cli\BoomHud.Cli.csproj -o .\_out\packages
dotnet tool install --global BoomHud.Tool --add-source .\_out\packages

# Then run:
boom-hud generate .\path\to\design.json --target avalonia --output .\_out\boom-hud\avalonia --namespace MyApp.Ui.Hud
```

### 3) Copy/commit outputs into your app repo

In your consuming Avalonia app repo, place the generated files under a stable folder, e.g.

- `src/Ui/Hud/Generated/...`

and include them in your `.csproj` as `AvaloniaXaml` + `Compile`.

Notes:

- The generated AXAML references `x:Class="<Namespace>.<Name>View"`; set `GenerationOptions.Namespace` to your app namespace.
- BoomHud emits a `I<Name>ViewModel.g.cs` interface; your app implements that interface and assigns it as the view’s DataContext / ViewModel.

### 4) Hook into your app

Minimal pattern:

- Create a ViewModel implementing `I<Name>ViewModel`
- Instantiate the generated `*View`
- Assign DataContext / ViewModel
- Add to your visual tree

## Option B: Call BoomHud generator APIs from your own tooling

If you prefer to run generation inside your own repo/pipeline, create a small tool project that references:

- `BoomHud.Dsl` (parsing)
- `BoomHud.Gen.Avalonia` (AXAML emission)

Pseudocode shape:

1) Parse design source into `HudDocument`
2) Create `GenerationOptions { Namespace = "...", Theme = ... }`
3) Call `new AvaloniaGenerator().Generate(document, options)`
4) Write `GeneratedFile.Content` to disk

This avoids copying BoomHud’s sample project and lets you:

- run codegen in CI
- enforce output paths
- pin generation options (namespace, nullable, comments)

## Compatibility notes (Avalonia)

- BoomHud targets Avalonia AXAML and emits code-behind plus ViewModel interfaces.
- BoomHud emits `x:DataType="vm:I<Name>ViewModel"` on the generated root control, so it works cleanly with `AvaloniaUseCompiledBindingsByDefault=true`.

## Current limitations (important)

- The “JSON DSL” described in the top-level README is aspirational; the implemented path today is **Figma JSON → IR → generators**.
- Generated component instance overrides are currently warned about (not fully applied) in `BoomHud.Gen.Avalonia`.
