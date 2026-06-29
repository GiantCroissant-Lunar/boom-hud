using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace BoomHud.Abstractions.Runtime;

/// <summary>A minimal change to apply to a rendered surface to bring it from one document to the next.</summary>
public abstract record RuntimeSurfacePatch;

/// <summary>The node changed type — its whole subtree must be rebuilt in place.</summary>
public sealed record ReplaceNodePatch(string Id, RuntimeComponentNode Node) : RuntimeSurfacePatch;

/// <summary>Same id + type, but the node's own layout/properties/actions changed — re-apply them (NOT children).</summary>
public sealed record UpdateNodePatch(string Id, RuntimeComponentNode Node) : RuntimeSurfacePatch;

/// <summary>Insert a new child subtree under <see cref="ParentId"/> at <see cref="Index"/>.</summary>
public sealed record InsertChildPatch(string ParentId, int Index, RuntimeComponentNode Node) : RuntimeSurfacePatch;

/// <summary>Remove child <see cref="ChildId"/> from <see cref="ParentId"/> (frees its subtree).</summary>
public sealed record RemoveChildPatch(string ParentId, string ChildId) : RuntimeSurfacePatch;

/// <summary>Move existing child <see cref="ChildId"/> to <see cref="Index"/> under <see cref="ParentId"/>.</summary>
public sealed record MoveChildPatch(string ParentId, string ChildId, int Index) : RuntimeSurfacePatch;

/// <summary>
/// Pure, engine-agnostic reconciler: diffs two runtime-surface trees into a minimal patch list, matching
/// nodes by their stable <c>id</c>. A retained renderer applies the patches to mutate only what changed —
/// preserving unchanged controls (scroll position, focus, no flicker) instead of rebuilding the whole
/// surface on every revision.
///
/// REQUIRES STABLE IDS: a logical element (a bundle row, a ledger entry) must keep the same id across
/// rebuilds (e.g. <c>"bundle:assist"</c>, not a positional <c>"n7"</c>) or it is seen as remove + insert.
/// Inserts/removes alone shift sibling positions but preserve relative order, so they emit no moves; a
/// <see cref="MoveChildPatch"/> is emitted only when the relative order of surviving children actually changes.
/// </summary>
public static class RuntimeSurfaceReconciler
{
    /// <summary>Diff two documents' roots into a patch list. The roots are assumed to share an id (e.g. "root").</summary>
    public static IReadOnlyList<RuntimeSurfacePatch> Diff(RuntimeComponentNode oldRoot, RuntimeComponentNode newRoot)
    {
        if (oldRoot is null) throw new ArgumentNullException(nameof(oldRoot));
        if (newRoot is null) throw new ArgumentNullException(nameof(newRoot));

        var patches = new List<RuntimeSurfacePatch>();
        DiffNode(oldRoot, newRoot, patches);
        return patches;
    }

    private static void DiffNode(RuntimeComponentNode old, RuntimeComponentNode @new, List<RuntimeSurfacePatch> patches)
    {
        if (!string.Equals(old.Type, @new.Type, StringComparison.Ordinal))
        {
            patches.Add(new ReplaceNodePatch(@new.Id, @new));
            return;
        }

        if (!ShellEquals(old, @new))
        {
            patches.Add(new UpdateNodePatch(@new.Id, @new));
        }

        ReconcileChildren(@new.Id, old.Children, @new.Children, patches);
    }

    private static void ReconcileChildren(
        string parentId,
        IReadOnlyList<RuntimeComponentNode> oldChildren,
        IReadOnlyList<RuntimeComponentNode> newChildren,
        List<RuntimeSurfacePatch> patches)
    {
        var oldById = new Dictionary<string, RuntimeComponentNode>(StringComparer.Ordinal);
        foreach (var child in oldChildren)
        {
            oldById[child.Id] = child;
        }

        var newIds = new HashSet<string>(newChildren.Select(c => c.Id), StringComparer.Ordinal);

        // Removals first, so the parent's child set is correct before inserts/moves.
        foreach (var child in oldChildren)
        {
            if (!newIds.Contains(child.Id))
            {
                patches.Add(new RemoveChildPatch(parentId, child.Id));
            }
        }

        // Inserts at their target index (in the new ordering).
        for (var i = 0; i < newChildren.Count; i++)
        {
            if (!oldById.ContainsKey(newChildren[i].Id))
            {
                patches.Add(new InsertChildPatch(parentId, i, newChildren[i]));
            }
        }

        // Reorder only when the relative order of surviving children actually changed.
        var survivors = oldChildren.Where(c => newIds.Contains(c.Id)).Select(c => c.Id).ToList();
        var newCommon = newChildren.Where(c => oldById.ContainsKey(c.Id)).Select(c => c.Id).ToList();
        if (!survivors.SequenceEqual(newCommon, StringComparer.Ordinal))
        {
            for (var i = 0; i < newChildren.Count; i++)
            {
                if (oldById.ContainsKey(newChildren[i].Id))
                {
                    patches.Add(new MoveChildPatch(parentId, newChildren[i].Id, i));
                }
            }
        }

        // Recurse into surviving common children.
        foreach (var child in newChildren)
        {
            if (oldById.TryGetValue(child.Id, out var oldChild))
            {
                DiffNode(oldChild, child, patches);
            }
        }
    }

    /// <summary>True when two nodes' OWN content (type, layout, properties, bindings, actions) is equal — children excluded.</summary>
    public static bool ShellEquals(RuntimeComponentNode a, RuntimeComponentNode b)
        => string.Equals(a.Type, b.Type, StringComparison.Ordinal)
            && Equals(a.Layout, b.Layout)
            && PropertiesEqual(a.Properties, b.Properties)
            && ActionsEqual(a.Actions, b.Actions)
            && BindingsEqual(a.Bindings, b.Bindings);

    private static bool PropertiesEqual(IReadOnlyDictionary<string, RuntimeValue> a, IReadOnlyDictionary<string, RuntimeValue> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (key, valueA) in a)
        {
            if (!b.TryGetValue(key, out var valueB) || !ValueEquals(valueA, valueB))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValueEquals(RuntimeValue a, RuntimeValue b)
        => JsonNode.DeepEquals(a.Literal, b.Literal) && BindingEquals(a.Binding, b.Binding);

    private static bool BindingEquals(RuntimeBinding? a, RuntimeBinding? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return string.Equals(a.Property, b.Property, StringComparison.Ordinal)
            && string.Equals(a.Path, b.Path, StringComparison.Ordinal)
            && string.Equals(a.Mode, b.Mode, StringComparison.Ordinal)
            && string.Equals(a.Format, b.Format, StringComparison.Ordinal)
            && JsonNode.DeepEquals(a.Fallback, b.Fallback);
    }

    private static bool ActionsEqual(IReadOnlyList<RuntimeActionDescriptor> a, IReadOnlyList<RuntimeActionDescriptor> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i].Event, b[i].Event, StringComparison.Ordinal)
                || !string.Equals(a[i].Command, b[i].Command, StringComparison.Ordinal)
                || !JsonNode.DeepEquals(a[i].Payload, b[i].Payload))
            {
                return false;
            }
        }

        return true;
    }

    private static bool BindingsEqual(IReadOnlyList<RuntimeBinding> a, IReadOnlyList<RuntimeBinding> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!BindingEquals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }
}
