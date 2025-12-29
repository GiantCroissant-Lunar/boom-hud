namespace BoomHud.Abstractions.IR;

/// <summary>
/// Specification for a data binding.
/// </summary>
public sealed record BindingSpec
{
    /// <summary>
    /// Target property on the component.
    /// </summary>
    public required string Property { get; init; }

    /// <summary>
    /// Source path to bind to (e.g., "Player.Health").
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Binding mode.
    /// </summary>
    public BindingMode Mode { get; init; } = BindingMode.OneWay;

    /// <summary>
    /// Name of converter to apply.
    /// </summary>
    public string? Converter { get; init; }

    /// <summary>
    /// Parameter for the converter.
    /// </summary>
    public object? ConverterParameter { get; init; }

    /// <summary>
    /// String format to apply (e.g., "{0:N0} HP").
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Fallback value if binding fails.
    /// </summary>
    public object? Fallback { get; init; }
}

/// <summary>
/// Binding mode options.
/// </summary>
public enum BindingMode
{
    /// <summary>
    /// Source to target only, updates when source changes.
    /// </summary>
    OneWay,

    /// <summary>
    /// Bidirectional binding.
    /// </summary>
    TwoWay,

    /// <summary>
    /// Source to target, one-time at initialization.
    /// </summary>
    OneTime
}

/// <summary>
/// A value that can be either a static value or a binding.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
#pragma warning disable CA1000
public readonly record struct BindableValue<T>
{
    /// <summary>
    /// Static value (if not bound).
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// Binding path (if bound).
    /// </summary>
    public string? BindingPath { get; init; }

    /// <summary>
    /// Binding mode (if bound).
    /// </summary>
    public BindingMode Mode { get; init; }

    /// <summary>
    /// String format (if bound).
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Whether this value is bound to a data source.
    /// </summary>
    public bool IsBound => BindingPath != null;

    /// <summary>
    /// Creates a static value.
    /// </summary>
    public static implicit operator BindableValue<T>(T value) => new() { Value = value };

    /// <summary>
    /// Creates a binding.
    /// </summary>
    public static BindableValue<T> Bind(string path, BindingMode mode = BindingMode.OneWay, string? format = null)
        => new() { BindingPath = path, Mode = mode, Format = format };

    public override string ToString() => IsBound ? $"{{Binding {BindingPath}}}" : Value?.ToString() ?? "null";
}
#pragma warning restore CA1000
