namespace BoomHud.Abstractions.IR;

public sealed record ThemeDocument
{
    public required string Name { get; init; }

    public IReadOnlyDictionary<string, Color> Colors { get; init; } = new Dictionary<string, Color>();

    public IReadOnlyDictionary<string, double> Dimensions { get; init; } = new Dictionary<string, double>();

    public IReadOnlyDictionary<string, double> FontSizes { get; init; } = new Dictionary<string, double>();
}
