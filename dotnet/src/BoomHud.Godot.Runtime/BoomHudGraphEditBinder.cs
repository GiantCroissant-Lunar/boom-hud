using System.Collections;
using System.Collections.Specialized;
using System.Reflection;
using Godot;

namespace BoomHud.Godot.Runtime;

public sealed class BoomHudGraphEditBinder : IDisposable
{
    private readonly GraphEdit _graph;
    private readonly PropertyInfo _nodesProperty;
    private readonly PropertyInfo? _wiresProperty;
    private readonly MethodInfo? _wireMethod;
    private readonly MethodInfo? _unwireMethod;
    private readonly MethodInfo? _removeMethod;
    private readonly List<Action> _disconnects = new();
    private readonly Dictionary<string, GraphNode> _spawned = new(StringComparer.Ordinal);
    private readonly HashSet<string> _wired = new(StringComparer.Ordinal);

    private object? _viewModel;
    private bool _disposed;

    private BoomHudGraphEditBinder(
        GraphEdit graph,
        object viewModel,
        PropertyInfo nodesProperty,
        PropertyInfo? wiresProperty)
    {
        _graph = graph;
        _viewModel = viewModel;
        _nodesProperty = nodesProperty;
        _wiresProperty = wiresProperty;

        var vmType = viewModel.GetType();
        _wireMethod = vmType.GetMethod("WireNodes", new[] { typeof(string), typeof(int), typeof(string), typeof(int) });
        _unwireMethod = vmType.GetMethod("UnwireNodes", new[] { typeof(string), typeof(int), typeof(string), typeof(int) });
        _removeMethod = vmType.GetMethod("RemoveNode", new[] { typeof(string) });
    }

    public static BoomHudGraphEditBinder? TryBind(
        GraphEdit graph,
        object viewModel,
        string nodesPropertyName = "Nodes",
        string wiresPropertyName = "Wires")
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(viewModel);

        var vmType = viewModel.GetType();
        var nodesProperty = vmType.GetProperty(nodesPropertyName, BindingFlags.Public | BindingFlags.Instance);
        if (nodesProperty is null || !nodesProperty.CanRead || !IsCollectionType(nodesProperty.PropertyType))
        {
            return null;
        }

        var wiresProperty = vmType.GetProperty(wiresPropertyName, BindingFlags.Public | BindingFlags.Instance);
        if (wiresProperty is not null && (!wiresProperty.CanRead || !IsCollectionType(wiresProperty.PropertyType)))
        {
            wiresProperty = null;
        }

        var binder = new BoomHudGraphEditBinder(graph, viewModel, nodesProperty, wiresProperty);
        binder.Bind();
        return binder;
    }

    private void Bind()
    {
        _graph.ConnectionRequest += OnConnection;
        _graph.DisconnectionRequest += OnDisconnection;
        _graph.DeleteNodesRequest += OnDeleteNodes;
        _disconnects.Add(() =>
        {
            if (!GodotObject.IsInstanceValid(_graph))
            {
                return;
            }

            _graph.ConnectionRequest -= OnConnection;
            _graph.DisconnectionRequest -= OnDisconnection;
            _graph.DeleteNodesRequest -= OnDeleteNodes;
        });

        if (_nodesProperty.GetValue(_viewModel) is INotifyCollectionChanged nodesChanged)
        {
            NotifyCollectionChangedEventHandler handler = (_, _) => Defer(SyncAll);
            nodesChanged.CollectionChanged += handler;
            _disconnects.Add(() => nodesChanged.CollectionChanged -= handler);
        }

        if (_wiresProperty?.GetValue(_viewModel) is INotifyCollectionChanged wiresChanged)
        {
            NotifyCollectionChangedEventHandler handler = (_, _) => Defer(SyncAll);
            wiresChanged.CollectionChanged += handler;
            _disconnects.Add(() => wiresChanged.CollectionChanged -= handler);
        }

        Defer(SyncAll);
    }

    private void OnConnection(StringName from, long fromPort, StringName to, long toPort)
    {
        if (_viewModel is null || !GodotObject.IsInstanceValid(_graph))
        {
            return;
        }

        _wireMethod?.Invoke(_viewModel, new object?[] { from.ToString(), (int)fromPort, to.ToString(), (int)toPort });
        _graph.ConnectNode(from, (int)fromPort, to, (int)toPort);
    }

    private void OnDisconnection(StringName from, long fromPort, StringName to, long toPort)
    {
        if (_viewModel is null || !GodotObject.IsInstanceValid(_graph))
        {
            return;
        }

        _unwireMethod?.Invoke(_viewModel, new object?[] { from.ToString(), (int)fromPort, to.ToString(), (int)toPort });
        _graph.DisconnectNode(from, (int)fromPort, to, (int)toPort);
    }

    private void OnDeleteNodes(global::Godot.Collections.Array<StringName> nodes)
    {
        if (_viewModel is null)
        {
            return;
        }

        foreach (var node in nodes)
        {
            _removeMethod?.Invoke(_viewModel, new object?[] { node.ToString() });
        }
    }

    private void SyncAll()
    {
        SyncNodes();
        SyncWires();
    }

    private void SyncNodes()
    {
        if (_viewModel is null || !GodotObject.IsInstanceValid(_graph))
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        if (_nodesProperty.GetValue(_viewModel) is IEnumerable items)
        {
            foreach (var item in items)
            {
                var nodeId = ReadString(item, "NodeId");
                if (string.IsNullOrEmpty(nodeId))
                {
                    index++;
                    continue;
                }

                seen.Add(nodeId);
                if (!_spawned.ContainsKey(nodeId))
                {
                    var title = ReadString(item, "TypeId") ?? nodeId;
                    var graphNode = CreateGraphNode(
                        nodeId,
                        title,
                        ReadInt(item, "InputCount", 1),
                        ReadInt(item, "OutputCount", 1),
                        index);
                    _graph.AddChild(graphNode);
                    _spawned[nodeId] = graphNode;
                }

                index++;
            }
        }

        foreach (var pair in _spawned.Where(pair => !seen.Contains(pair.Key)).ToList())
        {
            if (GodotObject.IsInstanceValid(pair.Value))
            {
                pair.Value.QueueFree();
            }

            _spawned.Remove(pair.Key);
        }
    }

    private void SyncWires()
    {
        if (_wiresProperty is null || _viewModel is null || !GodotObject.IsInstanceValid(_graph))
        {
            return;
        }

        var desired = new HashSet<string>(StringComparer.Ordinal);
        if (_wiresProperty.GetValue(_viewModel) is IEnumerable items)
        {
            foreach (var item in items)
            {
                var fromId = ReadString(item, "FromNodeId");
                var toId = ReadString(item, "ToNodeId");
                var fromSlot = ReadInt(item, "FromSlot", 0);
                var toSlot = ReadInt(item, "ToSlot", 0);
                if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId))
                {
                    continue;
                }

                if (!_spawned.ContainsKey(fromId) || !_spawned.ContainsKey(toId))
                {
                    continue;
                }

                var key = BuildWireKey(fromId, fromSlot, toId, toSlot);
                desired.Add(key);
                if (_wired.Add(key))
                {
                    _graph.ConnectNode(fromId, fromSlot, toId, toSlot);
                }
            }
        }

        foreach (var key in _wired.Where(key => !desired.Contains(key)).ToList())
        {
            _wired.Remove(key);
            if (TryParseWireKey(key, out var fromId, out var fromSlot, out var toId, out var toSlot)
                && GodotObject.IsInstanceValid(_graph))
            {
                _graph.DisconnectNode(fromId, fromSlot, toId, toSlot);
            }
        }
    }

    private static GraphNode CreateGraphNode(string nodeId, string title, int inputCount, int outputCount, int index)
    {
        var graphNode = new GraphNode
        {
            Name = nodeId,
            Title = title,
            PositionOffset = new Vector2(40 + (index % 4) * 240, 40 + (index / 4) * 200),
        };

        var rows = Math.Max(inputCount, outputCount);
        for (var row = 0; row < rows; row++)
        {
            graphNode.AddChild(new Label { Text = SlotRowLabel(row, inputCount, outputCount) });
            graphNode.SetSlot(
                row,
                row < inputCount,
                0,
                new Color(0.4f, 0.7f, 1f),
                row < outputCount,
                0,
                new Color(1f, 0.7f, 0.3f));
        }

        return graphNode;
    }

    private static string SlotRowLabel(int row, int inputCount, int outputCount)
    {
        var left = row < inputCount ? $"in{row}" : "";
        var right = row < outputCount ? $"out{row}" : "";
        return string.IsNullOrEmpty(right) ? left : string.IsNullOrEmpty(left) ? right : $"{left}    {right}";
    }

    private static string? ReadString(object? item, string propertyName)
        => item?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(item)?.ToString();

    private static int ReadInt(object? item, string propertyName, int fallback)
        => item?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(item) is int value
            ? value
            : fallback;

    private static string BuildWireKey(string fromId, int fromSlot, string toId, int toSlot)
        => $"{fromId}:{fromSlot}->{toId}:{toSlot}";

    private static bool TryParseWireKey(
        string key,
        out string fromId,
        out int fromSlot,
        out string toId,
        out int toSlot)
    {
        fromId = "";
        toId = "";
        fromSlot = 0;
        toSlot = 0;

        var arrow = key.IndexOf("->", StringComparison.Ordinal);
        if (arrow < 0)
        {
            return false;
        }

        var left = key[..arrow];
        var right = key[(arrow + 2)..];
        var leftSeparator = left.LastIndexOf(':');
        var rightSeparator = right.LastIndexOf(':');
        if (leftSeparator < 0 || rightSeparator < 0)
        {
            return false;
        }

        if (!int.TryParse(left[(leftSeparator + 1)..], out fromSlot)
            || !int.TryParse(right[(rightSeparator + 1)..], out toSlot))
        {
            return false;
        }

        fromId = left[..leftSeparator];
        toId = right[..rightSeparator];
        return true;
    }

    private void Defer(Action action)
        => Callable.From(() =>
        {
            if (!_disposed)
            {
                action();
            }
        }).CallDeferred();

    private static bool IsCollectionType(Type type)
        => type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var disconnect in _disconnects)
        {
            disconnect();
        }

        _disconnects.Clear();
        _spawned.Clear();
        _wired.Clear();
        _viewModel = null;
    }
}
