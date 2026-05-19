# Capability Mapping Rules

## Principle

**Explicitly declare and check framework capabilities. Never assume.**

## Rules

### 1. Capability Manifests Are Authoritative

Each backend must declare its capabilities in an `ICapabilityManifest`:

```csharp
public class TerminalGuiCapabilities : ICapabilityManifest
{
    public IReadOnlyDictionary<string, CapabilityLevel> Features { get; } = new()
    {
        [Capabilities.Animation] = CapabilityLevel.Unsupported,
        [Capabilities.DataBinding] = CapabilityLevel.Emulated,
        // ...
    };
}
```

### 2. Check Before Generate

Always check capabilities before emitting code:

```csharp
var level = capabilities.GetCapabilityLevel(Capabilities.Animation);
if (level == CapabilityLevel.Unsupported)
{
    diagnostics.Add(Diagnostic.Warning("Animation not supported"));
    return; // Skip or use fallback
}
```

### 3. Capability Levels

| Level | Meaning | Action |
|-------|---------|--------|
| `Native` | Framework supports directly | Use native API |
| `Emulated` | Can be simulated | Generate emulation code |
| `Limited` | Partial support | Generate with caveats |
| `Unsupported` | Not available | Skip or error based on policy |

### 4. Policy-Driven Handling

Honor the configured `MissingCapabilityPolicy`:

```csharp
switch (options.MissingCapabilityPolicy)
{
    case MissingCapabilityPolicy.Error:
        return GenerationResult.Fail($"Feature '{feature}' not supported");
    case MissingCapabilityPolicy.Warn:
        diagnostics.Add(Diagnostic.Warning(...));
        break;
    case MissingCapabilityPolicy.Skip:
        return; // Silent skip
}
```

### 5. Document Capability Gaps

When a backend doesn't support a feature, document:

1. What's not supported
2. Why (technical limitation)
3. Workaround if any

## Capability Matrix

| Capability | Terminal.Gui | Avalonia | MAUI |
|------------|--------------|----------|------|
| Data Binding | Emulated | Native | Native |
| Pixel Layout | ❌ | ✅ | ✅ |
| Cell Layout | ✅ | Emulated | ❌ |
| Images | ❌ | ✅ | ✅ |
| Animation | ❌ | ✅ | ✅ |
| Rich Text | Limited | ✅ | ✅ |
| Tooltips | ❌ | ✅ | ✅ |
| Touch Input | ❌ | ✅ | ✅ |

## Emulation Patterns

### Data Binding for Terminal.Gui

Since Terminal.Gui lacks native binding, generate a refresh pattern:

```csharp
public partial class StatusBar
{
    private INotifyPropertyChanged? _dataContext;
    
    public void SetDataContext(INotifyPropertyChanged context)
    {
        if (_dataContext != null)
            _dataContext.PropertyChanged -= OnPropertyChanged;
        
        _dataContext = context;
        _dataContext.PropertyChanged += OnPropertyChanged;
        Refresh();
    }
    
    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Only refresh affected bindings
        if (e.PropertyName == nameof(IStatusBarViewModel.HealthPercent))
            RefreshHealthBar();
    }
}
```
