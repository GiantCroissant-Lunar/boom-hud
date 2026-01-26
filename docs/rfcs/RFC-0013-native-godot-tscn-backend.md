# RFC-0013: Native Godot Scene (.tscn) Generation

- **Status**: Draft
- **Created**: 2026-01-01
- **Authors**: BoomHud Contributors

## Summary

This RFC proposes adding a secondary backend for Godot that generates native **PackedScene (`.tscn`)** files instead of imperative C# construction code. This allows the generated UI to be inspected and previewed directly in the Godot Editor.

## Motivation

The current Godot backend (`BoomHud.Gen.Godot`) generates C# code that builds the UI tree programmatically at runtime. While robust and type-safe, this approach has a significant drawback: **Opacity**.

- Developers cannot see the UI layout in the Godot Editor.
- Debugging layout issues requires running the game.
- It feels "foreign" to Godot developers accustomed to the Scene system.

By generating `.tscn` files, BoomHud would treat the Godot Editor as a first-class citizen, allowing the generated artifacts to be visual and familiar.

## Goals

- Generate valid `.tscn` text files representing the component hierarchy.
- Generate companion C# scripts that inherit from the scene root and handle bindings.
- Support Godot 4.x text-based scene format.
- Ensure 100% parity with the imperative C# generator.

## Non-Goals

- Two-way sync (editing the `.tscn` in Godot and syncing back to BoomHud/Figma). The generator is one-way.
- Binary `.scn` format support (text-based is preferred for version control and generation).

## Design

### 1. Architecture

The generator will produce two artifacts per component:

1.  **`MyView.tscn`**: The visual scene definition.
2.  **`MyView.cs`**: The logic script attached to the scene root.

### 2. The TSCN Format Challenge

The `.tscn` format is essentially an INI-style file with strict reference requirements:

```ini
[gd_scene load_steps=3 format=3 uid="uid://unique_id_here"]

[ext_resource type="Script" path="res://MyView.cs" id="1_script"]
[ext_resource type="Texture2D" path="res://icon.svg" id="2_icon"]

[node name="MyView" type="VBoxContainer"]
script = ExtResource("1_script")

[node name="Header" type="Label" parent="."]
layout_mode = 2
text = "Hello World"
```

**Challenges:**
- **UID Generation**: Every resource needs a unique ID. We may need to generate deterministic UIDs based on file paths to avoid churn.
- **Inheritance**: Sub-resources (Styles, Themes) need to be embedded or referenced correctly.
- **Versioning**: The format (`format=3`) changes between Godot versions.

### 3. C# "Code-Behind"

The C# script will need to look up nodes to bind them. Unlike the imperative approach where we *have* the variable, here we must find it.

**Option A: `GetNode` (Brittle)**
```csharp
private Label _label;
public override void _Ready() {
    _label = GetNode<Label>("Header"); // Runtime string lookup
}
```

**Option B: `[Export]` / `[BindNode]` (Better)**
If we generate the C# script, we can't easily "assign" the references in the `.tscn` automatically without complex ID matching. 
However, Godot 4.x supports **Scene Unique Names** (`%NodeName`).

```csharp
_label = GetNode<Label>("%Header");
```

This is robust even if the hierarchy changes, as long as the node name is unique in the scene.

### 4. Implementation Strategy

1.  **TscnWriter**: A helper class to write the `.tscn` syntax (headers, nodes, resources).
2.  **Node Graph**: Build an in-memory graph of the scene.
3.  **Serialization**: Traverse the graph and emit the `[node]` blocks.

## Open Questions

1.  **Theme Resources**: Should we generate a `.tres` Theme file alongside the scene?
2.  **Godot Project Integration**: The generator needs to know the `res://` path structure to write valid `[ext_resource]` paths. This implies the generator might need to know the Godot project root.

## Alternatives Considered

- **Godot Editor Plugin**: A plugin inside Godot that imports the intermediate JSON and builds scenes using the Godot API.
    - *Pros*: Uses native serialization API.
    - *Cons*: Requires running Godot to generate code; hard to integrate into CI/CD pipelines outside of Godot.

## References

- [Godot TSCN Format Documentation](https://docs.godotengine.org/en/stable/contributing/development/file_formats/tscn.html)
