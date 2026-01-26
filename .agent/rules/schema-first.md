# Schema-First Design Rules

## Principle

**Always define the DSL schema before implementing code.**

The JSON Schema (`schemas/boom-hud.schema.json`) is the single source of truth for the DSL format.

## Rules

### 1. Schema Before Code

- [ ] New component type? Add to schema first
- [ ] New property? Add to schema first
- [ ] New layout option? Add to schema first

### 2. Schema Validation

All DSL input must be validated against the schema before parsing:

```csharp
// Good: Validate first
var validation = parser.Validate(content);
if (!validation.IsValid)
    return ValidationResult.Fail(validation.Errors);

var document = parser.Parse(content);
```

### 3. Schema Evolution

When modifying the schema:

1. **Additive changes** are safe (new optional properties)
2. **Breaking changes** require version bump
3. **Deprecation** - mark as deprecated before removal

### 4. IR Must Match Schema

The IR types in `BoomHud.Abstractions/IR/` must be able to represent everything in the schema:

| Schema Element | IR Type |
|----------------|---------|
| `componentNode` | `ComponentNode` |
| `layoutSpec` | `LayoutSpec` |
| `styleSpec` | `StyleSpec` |
| `binding` | `BindingSpec` |
| `dimension` | `Dimension` |

### 5. Schema Documentation

Every schema element should have:
- `description` field explaining purpose
- `examples` for non-obvious formats
- `default` values where applicable

## Example Workflow

Adding a new component property:

1. Add to schema:
```json
"newProperty": {
  "type": "string",
  "description": "Description of the property",
  "default": "defaultValue"
}
```

2. Update IR if needed:
```csharp
public string? NewProperty { get; init; }
```

3. Update parser to handle property
4. Update generators to emit property
5. Add tests
