# Workflow: Add New Backend

This workflow describes how to add a new code generation backend to BoomHud.

## Prerequisites

- Deep understanding of target framework's UI model
- Familiarity with BoomHud's IR and generation patterns

## Steps

### 1. Research Target Framework

Document:
- Layout system (pixel, cell, constraint-based)
- Data binding model (native, manual, reactive)
- Component hierarchy and naming
- Code patterns (imperative, declarative, XAML)
- Limitations and quirks

### 2. Create Project

```bash
cd dotnet/src
dotnet new classlib -n BoomHud.Gen.{Framework}
```

Update solution:
```bash
cd ..
dotnet sln BoomHud.sln add src/BoomHud.Gen.{Framework}
```

### 3. Implement Capability Manifest

Create `{Framework}Capabilities.cs`:

```csharp
public sealed class {Framework}Capabilities : ICapabilityManifest
{
    public static {Framework}Capabilities Instance { get; } = new();

    public string TargetFramework => "{Framework}";

    public IReadOnlySet<string> SupportedComponents { get; } = new HashSet<string>
    {
        "label", "button", // ... supported components
    };

    public IReadOnlySet<string> SupportedLayouts { get; } = new HashSet<string>
    {
        "horizontal", "vertical", // ... supported layouts
    };

    public IReadOnlyDictionary<string, CapabilityLevel> Features { get; } = new Dictionary<string, CapabilityLevel>
    {
        [Capabilities.DataBinding] = CapabilityLevel.Native, // or Emulated, etc.
        // ... all features
    };

    // Implement interface methods...
}
```

### 4. Implement Generator

Create `{Framework}Generator.cs`:

```csharp
public sealed class {Framework}Generator : IBackendGenerator
{
    public string TargetFramework => "{Framework}";
    public ICapabilityManifest Capabilities => {Framework}Capabilities.Instance;

    public GenerationResult Generate(HudDocument document, GenerationOptions options)
    {
        var files = new List<GeneratedFile>();
        var diagnostics = new List<Diagnostic>();

        // Generate code...

        return new GenerationResult
        {
            Files = files,
            Diagnostics = diagnostics
        };
    }
}
```

### 5. Implement Component Emitters

Create emitters for each component category:

```
Emitters/
├── PrimitiveEmitter.cs    # label, button, progressBar
├── ContainerEmitter.cs    # container, panel, scrollView
├── LayoutEmitter.cs       # horizontal, vertical, grid
└── BindingEmitter.cs      # data binding patterns
```

### 6. Add to CLI

Update `BoomHud.Cli/Program.cs`:

```csharp
var targetOption = new Option<string>(
    "--target",
    () => "terminalGui",
    "Target framework (terminalGui, avalonia, {framework})"); // Add new option
```

### 7. Create Tests

```csharp
// BoomHud.Tests.{Framework}/
public class {Framework}GeneratorTests
{
    [Fact]
    public void Generate_SimpleComponent_ProducesValidOutput()
    {
        // ...
    }

    [Fact]
    public void Generate_WithBindings_ProducesCorrectPattern()
    {
        // ...
    }
}
```

### 8. Document

Create `docs/backends/{framework}.md`:

- Capability matrix
- Generated code patterns
- Known limitations
- Framework-specific options

## Checklist

- [ ] Project created and added to solution
- [ ] Capability manifest implemented
- [ ] All capability levels documented
- [ ] Generator implemented
- [ ] Primitive component emitters
- [ ] Container component emitters
- [ ] Layout emitters
- [ ] Binding emitters (if applicable)
- [ ] CLI updated
- [ ] Unit tests
- [ ] Integration tests
- [ ] Documentation
- [ ] Sample generation verified
