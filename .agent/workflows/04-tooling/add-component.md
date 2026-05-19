# Workflow: Add New Component Type

This workflow describes how to add a new component type to BoomHud.

## Prerequisites

- Understanding of the component model (RFC-0002)
- Familiarity with target frameworks

## Steps

### 1. Design the Component

Define the component's:
- Purpose and use cases
- Required properties
- Optional properties
- Events/commands
- Capability requirements

### 2. Update DSL Schema

Add to `schemas/json/boom-hud.schema.json`:

```json
// In componentType enum
"myNewComponent"

// In componentNode properties (if component-specific properties)
"myProperty": {
  "type": "string",
  "description": "Description of property"
}
```

### 3. Update IR (if needed)

If the component needs special IR representation:

```csharp
// In ComponentType enum
MyNewComponent

// Or add to Properties dictionary in ComponentNode
```

### 4. Update Capability Manifests

For each backend, declare support level:

```csharp
// TerminalGuiCapabilities.cs
public IReadOnlySet<string> SupportedComponents { get; } = new HashSet<string>
{
    // ...
    "myNewComponent", // if supported
};

// AvaloniaCapabilities.cs  
public IReadOnlySet<string> SupportedComponents { get; } = new HashSet<string>
{
    // ...
    "myNewComponent",
};
```

### 5. Implement Emitters

For each backend, implement the component emitter:

**Terminal.Gui:**
```csharp
private void EmitMyNewComponent(CodeBuilder code, ComponentNode node)
{
    var varName = GetVariableName(node);
    code.AppendLine($"var {varName} = new MyTerminalGuiControl();");
    EmitLayout(code, varName, node.Layout);
    EmitStyle(code, varName, node.Style);
}
```

**Avalonia:**
```csharp
private void EmitMyNewComponent(XmlWriter xml, ComponentNode node)
{
    xml.WriteStartElement("MyAvaloniaControl");
    EmitBindings(xml, node);
    xml.WriteEndElement();
}
```

### 6. Add Tests

```csharp
[Fact]
public void Generate_MyNewComponent_ProducesValidCode()
{
    var document = new HudDocument
    {
        Name = "Test",
        Root = new ComponentNode
        {
            Type = ComponentType.MyNewComponent,
            // ...
        }
    };
    
    var result = generator.Generate(document, new GenerationOptions());
    
    result.Success.Should().BeTrue();
    result.Files.Should().ContainSingle();
}
```

### 7. Add Sample

Create a sample HUD file demonstrating the component:

```json
// samples/my-new-component.hud.json
{
  "component": "MyNewComponentDemo",
  "children": [
    {
      "type": "myNewComponent",
      "id": "example"
      // properties...
    }
  ]
}
```

### 8. Update Documentation

- Add to RFC-0002 component table
- Add to README examples if noteworthy
- Update capability matrix

## Checklist

- [ ] DSL schema updated
- [ ] IR types updated (if needed)
- [ ] Terminal.Gui manifest updated
- [ ] Terminal.Gui emitter implemented
- [ ] Avalonia manifest updated
- [ ] Avalonia emitter implemented
- [ ] Unit tests added
- [ ] Sample file created
- [ ] Documentation updated
