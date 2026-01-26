# BoomHud Godot Sample

This sample demonstrates how to use BoomHud with **Godot 4.x (C#)**.

## Overview

- **Schema**: `hud.json` (Figma-like structure)
- **Generator**: `BoomHud.Gen.Godot` (generates C# code to build UI programmatically)
- **Runtime**: `GodotSample.csproj` (Godot .NET project)

## How it works

1.  **Generation**: The `BoomHud.Cli` tool reads `hud.json` and generates:
    -   `Generated/GameHudView.g.cs`: A `Control` subclass that builds the UI tree in `_Ready()`.
    -   `Generated/IGameHudViewModel.g.cs`: An interface for the ViewModel.

2.  **Runtime Binding**:
    -   `GameHudViewModel.cs`: Implements the logic (health, mana, score).
    -   `Main.cs`: The entry point scene script that instantiates the View and ViewModel and binds them.

## Running the Sample

1.  **Build the CLI**:
    ```bash
    dotnet build ../../dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj
    ```

2.  **Regenerate UI (Optional)**:
    ```bash
    dotnet run --project ../../dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- generate hud.json --target godot --output Generated --namespace GodotSample
    ```

3.  **Run in Godot**:
    -   Open this folder (`samples/GodotSample`) in the **Godot Editor**.
    -   Build the project (Build button or MSBuild).
    -   Run the `Main.tscn` scene.

    *Note: Since this uses C# code to build UI, you won't see the controls in the Godot Editor's 2D view unless you run the game.*

## Project Structure

```
GodotSample/
├── hud.json              # UI Definition
├── project.godot         # Godot Project
├── GodotSample.csproj    # .NET Project
├── Main.tscn             # Entry Scene
├── Main.cs               # Entry Script
├── GameHudViewModel.cs   # Logic
└── Generated/            # BoomHud Output
    ├── GameHudView.g.cs
    └── IGameHudViewModel.g.cs
```
