using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using BoomHud.Abstractions.Runtime;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Runtime;

public sealed class RuntimeSurfaceReconcilerTests
{
    private static RuntimeComponentNode Node(string id, string type, string? text = null, params RuntimeComponentNode[] children)
        => new()
        {
            Id = id,
            Type = type,
            Properties = text is null
                ? new Dictionary<string, RuntimeValue>()
                : new Dictionary<string, RuntimeValue> { ["text"] = new RuntimeValue { Literal = JsonValue.Create(text) } },
            Children = children,
        };

    [Fact]
    public void Diff_IdenticalTree_ProducesNoPatches()
    {
        var oldRoot = Node("root", "container", null, Node("a", "label", "x"), Node("b", "label", "y"));
        var newRoot = Node("root", "container", null, Node("a", "label", "x"), Node("b", "label", "y"));

        RuntimeSurfaceReconciler.Diff(oldRoot, newRoot).Should().BeEmpty();
    }

    [Fact]
    public void Diff_TextChange_EmitsSingleUpdateForThatNode()
    {
        var oldRoot = Node("root", "container", null, Node("a", "label", "x"), Node("b", "label", "y"));
        var newRoot = Node("root", "container", null, Node("a", "label", "x"), Node("b", "label", "CHANGED"));

        var patches = RuntimeSurfaceReconciler.Diff(oldRoot, newRoot);

        patches.Should().ContainSingle();
        patches[0].Should().BeOfType<UpdateNodePatch>().Which.Id.Should().Be("b");
    }

    [Fact]
    public void Diff_PrependChild_EmitsInsertAtZero_NoMovesNoUpdates()
    {
        // The activity-ledger case: a new entry arrives at the top; the rest are untouched.
        var oldRoot = Node("root", "container", null, Node("a", "label", "x"), Node("b", "label", "y"));
        var newRoot = Node("root", "container", null, Node("new", "label", "z"), Node("a", "label", "x"), Node("b", "label", "y"));

        var patches = RuntimeSurfaceReconciler.Diff(oldRoot, newRoot);

        patches.Should().ContainSingle();
        var insert = patches[0].Should().BeOfType<InsertChildPatch>().Subject;
        insert.ParentId.Should().Be("root");
        insert.Index.Should().Be(0);
        insert.Node.Id.Should().Be("new");
    }

    [Fact]
    public void Diff_PrependAndTrim_EmitsInsertAndRemove_OnlyForTheEnds()
    {
        // Ring-buffer behaviour: prepend newest, drop oldest. The middle rows must be preserved as-is.
        var oldRoot = Node("root", "container", null, Node("e2", "label", "2"), Node("e1", "label", "1"), Node("e0", "label", "0"));
        var newRoot = Node("root", "container", null, Node("e3", "label", "3"), Node("e2", "label", "2"), Node("e1", "label", "1"));

        var patches = RuntimeSurfaceReconciler.Diff(oldRoot, newRoot);

        patches.Should().HaveCount(2);
        patches.OfType<RemoveChildPatch>().Should().ContainSingle(p => p.ChildId == "e0");
        patches.OfType<InsertChildPatch>().Should().ContainSingle(p => p.Node.Id == "e3" && p.Index == 0);
        patches.OfType<MoveChildPatch>().Should().BeEmpty();
        patches.OfType<UpdateNodePatch>().Should().BeEmpty();
    }

    [Fact]
    public void Diff_RemovedChild_EmitsRemove()
    {
        var oldRoot = Node("root", "container", null, Node("a", "label"), Node("b", "label"), Node("c", "label"));
        var newRoot = Node("root", "container", null, Node("a", "label"), Node("c", "label"));

        var patches = RuntimeSurfaceReconciler.Diff(oldRoot, newRoot);

        patches.Should().ContainSingle();
        patches[0].Should().BeOfType<RemoveChildPatch>().Which.ChildId.Should().Be("b");
    }

    [Fact]
    public void Diff_TypeChange_EmitsReplaceAndDoesNotRecurse()
    {
        var oldRoot = Node("root", "container", null, Node("a", "label", "x", Node("inner", "label", "deep")));
        var newRoot = Node("root", "container", null, Node("a", "button", "x", Node("inner", "label", "deep")));

        var patches = RuntimeSurfaceReconciler.Diff(oldRoot, newRoot);

        patches.Should().ContainSingle();
        patches[0].Should().BeOfType<ReplaceNodePatch>().Which.Id.Should().Be("a");
    }

    [Fact]
    public void Diff_Reordered_EmitsMoves()
    {
        var oldRoot = Node("root", "container", null, Node("a", "label"), Node("b", "label"), Node("c", "label"));
        var newRoot = Node("root", "container", null, Node("c", "label"), Node("b", "label"), Node("a", "label"));

        var patches = RuntimeSurfaceReconciler.Diff(oldRoot, newRoot);

        patches.Should().OnlyContain(p => p is MoveChildPatch);
        patches.OfType<MoveChildPatch>().Select(m => m.ChildId).Should().Equal("c", "b", "a");
    }

    [Fact]
    public void Diff_NestedChange_RecursesIntoSurvivingChild()
    {
        var oldRoot = Node("root", "container", null, Node("card", "panel", null, Node("title", "label", "old")));
        var newRoot = Node("root", "container", null, Node("card", "panel", null, Node("title", "label", "new")));

        var patches = RuntimeSurfaceReconciler.Diff(oldRoot, newRoot);

        patches.Should().ContainSingle();
        patches[0].Should().BeOfType<UpdateNodePatch>().Which.Id.Should().Be("title");
    }
}
