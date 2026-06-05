using BoomHud.Abstractions.IR;

namespace BoomHud.Generators;

/// <summary>
/// Shared emission for the per-document <c>*_Compose</c> class. The disposable helper types
/// and the component-instance tree walk are identical across backends; only the <c>Apply</c>
/// body (how a view model is attached to a view) is framework-specific and stays in each
/// generator.
/// </summary>
public static class ComposeEmitter
{
    /// <summary>
    /// Appends the <c>IChildVmResolver</c> interface and the <c>DisposableAction</c> /
    /// <c>CompositeDisposable</c> helper types into the open Compose class body. Emits the
    /// same source across every backend, so a fix applies everywhere at once.
    /// </summary>
    public static void AppendHelperTypes(CodeBuilder cb)
    {
        cb.AppendLine("public interface IChildVmResolver");
        cb.OpenBlock();
        cb.AppendLine("T Resolve<T>(object parentVm, string slotKey) where T : class;");
        cb.CloseBlock();
        cb.AppendLine();

        cb.AppendLine("private sealed class DisposableAction : IDisposable");
        cb.OpenBlock();
        cb.AppendLine("private readonly Action _dispose;");
        cb.AppendLine("public DisposableAction(Action dispose) { _dispose = dispose; }");
        cb.AppendLine("public void Dispose() { _dispose(); }");
        cb.CloseBlock();
        cb.AppendLine();

        cb.AppendLine("private sealed class CompositeDisposable : IDisposable");
        cb.OpenBlock();
        cb.AppendLine("private readonly List<IDisposable> _items = new();");
        cb.AppendLine("public void Add(IDisposable d) { _items.Add(d); }");
        cb.AppendLine("public void Dispose() { for (var i = _items.Count - 1; i >= 0; i--) _items[i].Dispose(); }");
        cb.CloseBlock();
        cb.AppendLine();
    }

    /// <summary>
    /// Collects <c>(node, definition)</c> pairs for every component-reference instance in the
    /// tree, depth-first.
    /// </summary>
    public static void CollectComponentInstances(ComponentNode node, IReadOnlyDictionary<string, HudComponentDefinition> components, List<(ComponentNode Node, HudComponentDefinition Def)> results)
    {
        if (node.ComponentRefId != null && components.TryGetValue(node.ComponentRefId, out var def))
        {
            results.Add((node, def));
        }

        foreach (var child in node.Children)
        {
            CollectComponentInstances(child, components, results);
        }
    }
}
