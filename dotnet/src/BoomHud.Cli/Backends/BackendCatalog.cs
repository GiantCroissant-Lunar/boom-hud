using BoomHud.Abstractions.Generation;
using BoomHud.Gen.Avalonia;
using BoomHud.Gen.Godot;
using BoomHud.Gen.React;
using BoomHud.Gen.TerminalGui;
using BoomHud.Gen.Unity;

namespace BoomHud.Cli.Backends;

internal static class BackendCatalog
{
    private sealed record BackendRegistration(string CanonicalName, IReadOnlyList<string> Aliases, Func<IBackendGenerator> CreateGenerator);

    private static readonly IReadOnlyList<BackendRegistration> _registrations =
    [
        new(
            CanonicalName: "TerminalGui",
            Aliases: ["terminalgui", "terminal-gui", "terminal"],
            CreateGenerator: static () => new TerminalGuiGenerator()),
        new(
            CanonicalName: "Avalonia",
            Aliases: ["avalonia"],
            CreateGenerator: static () => new AvaloniaGenerator()),
        new(
            CanonicalName: "Godot",
            Aliases: ["godot"],
            CreateGenerator: static () => new GodotGenerator()),
        new(
            CanonicalName: "React",
            Aliases: ["react", "reactjs", "remotion"],
            CreateGenerator: static () => new ReactGenerator()),
        new(
            CanonicalName: "Unity",
            Aliases: ["unity", "unity-uitoolkit", "uitoolkit"],
            CreateGenerator: static () => new UnityGenerator())
    ];

    private static readonly Dictionary<string, BackendRegistration> _registrationsByAlias = BuildAliasMap();
    private static readonly Dictionary<string, BackendRegistration> _registrationsByName = _registrations.ToDictionary(
        static registration => registration.CanonicalName,
        StringComparer.Ordinal);

    internal static IReadOnlyList<string> RegisteredBackendNames => _registrations
        .Select(static registration => registration.CanonicalName)
        .ToList();

    internal static List<string> ResolveTargets(string? targets)
    {
        if (string.IsNullOrWhiteSpace(targets))
        {
            return ["TerminalGui"];
        }

        var requestedTargets = targets
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static candidate => candidate.ToLowerInvariant())
            .ToList();

        if (requestedTargets.Count == 0)
        {
            return ["TerminalGui"];
        }

        var resolvedTargets = new List<string>();
        foreach (var requestedTarget in requestedTargets)
        {
            if (requestedTarget == "all")
            {
                resolvedTargets.AddRange(RegisteredBackendNames);
                continue;
            }

            if (!_registrationsByAlias.TryGetValue(requestedTarget, out var registration))
            {
                throw new ArgumentException($"Unknown target: {targets}");
            }

            resolvedTargets.Add(registration.CanonicalName);
        }

        return resolvedTargets
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    internal static IBackendGenerator CreateGenerator(string backend)
    {
        if (!_registrationsByName.TryGetValue(backend, out var registration))
        {
            throw new ArgumentException($"Unknown backend: {backend}");
        }

        return registration.CreateGenerator();
    }

    private static Dictionary<string, BackendRegistration> BuildAliasMap()
    {
        var aliasMap = new Dictionary<string, BackendRegistration>(StringComparer.Ordinal);
        foreach (var registration in _registrations)
        {
            foreach (var alias in registration.Aliases)
            {
                aliasMap[alias] = registration;
            }
        }

        return aliasMap;
    }
}
