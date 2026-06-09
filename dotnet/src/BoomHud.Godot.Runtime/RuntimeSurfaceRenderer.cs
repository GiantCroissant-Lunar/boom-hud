using BoomHud.Abstractions.Runtime;
using Godot;

namespace BoomHud.Godot.Runtime;

public sealed class RuntimeSurfaceRenderer
{
    private readonly RuntimeSurfaceCatalog _catalog;
    private readonly RuntimeSurfaceValidatorOptions _validatorOptions;
    private readonly RuntimeSurfaceActionHandler? _actionHandler;

    public RuntimeSurfaceRenderer(RuntimeSurfaceRendererOptions? options = null)
    {
        options ??= new RuntimeSurfaceRendererOptions();
        _catalog = options.Catalog ?? RuntimeSurfaceCatalog.Basic;
        _validatorOptions = options.ValidatorOptions ?? new RuntimeSurfaceValidatorOptions();
        _actionHandler = options.ActionHandler;
    }

    public Control Render(RuntimeSurfaceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var validation = RuntimeSurfaceValidator.Validate(document, _catalog, _validatorOptions);
        if (!validation.IsValid)
        {
            throw new RuntimeSurfaceRenderException(validation.Diagnostics);
        }

        return CreateControlTree(document, document.Root);
    }

    public Control Mount(Control parent, RuntimeSurfaceDocument document, bool clearExistingChildren = false)
    {
        ArgumentNullException.ThrowIfNull(parent);

        if (clearExistingChildren)
        {
            foreach (var child in parent.GetChildren())
            {
                parent.RemoveChild(child);
                child.QueueFree();
            }
        }

        var rendered = Render(document);
        parent.AddChild(rendered);
        return rendered;
    }

    private Control CreateControlTree(RuntimeSurfaceDocument document, RuntimeComponentNode node)
    {
        var control = CreateControl(node);
        control.Name = node.Id;
        ApplyLayout(control, node.Layout);
        ApplyCommonProperties(control, node, document);

        var childHost = ApplySpecificProperties(control, node, document);
        foreach (var child in node.Children)
        {
            childHost.AddChild(CreateControlTree(document, child));
        }

        return control;
    }

    private static Control CreateControl(RuntimeComponentNode node)
        => node.Type switch
        {
            "badge" => CreateLabel(),
            "button" => new Button { ClipText = true },
            "container" => CreateContainer(node.Layout?.Type),
            "label" => CreateLabel(),
            "list" => new ItemList(),
            "nodeGraph" => new GraphEdit(),
            "panel" => new PanelContainer(),
            "progressBar" => new ProgressBar { ShowPercentage = false },
            "spacer" => new Control(),
            _ => new Control()
        };

    private static Control CreateContainer(string? layoutType)
        => layoutType switch
        {
            "grid" => new GridContainer(),
            "horizontal" => new HBoxContainer(),
            "vertical" => new VBoxContainer(),
            _ => new Control()
        };

    private static Label CreateLabel()
        => new()
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ClipText = true,
        };

    private static void ApplyLayout(Control control, RuntimeLayoutSpec? layout)
    {
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        control.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

        if (layout is null)
        {
            return;
        }

        var width = layout.Width ?? layout.MinWidth ?? 0;
        var height = layout.Height ?? layout.MinHeight ?? 0;
        if (width > 0 || height > 0)
        {
            control.CustomMinimumSize = new Vector2((float)width, (float)height);
        }

        if (layout.Gap is not null && control is Container container)
        {
            container.AddThemeConstantOverride("separation", (int)Math.Round(layout.Gap.Value));
        }

        if (string.Equals(layout.Type, "absolute", StringComparison.OrdinalIgnoreCase))
        {
            control.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        }
    }

    private static void ApplyCommonProperties(
        Control control,
        RuntimeComponentNode node,
        RuntimeSurfaceDocument document)
    {
        control.Visible = RuntimeValueResolver.ResolveBoolean(node.Properties, "visible", document.DataModel);
        control.TooltipText = RuntimeValueResolver.ResolveText(node.Properties, "tooltip", document.DataModel);

        var enabled = RuntimeValueResolver.ResolveBoolean(node.Properties, "enabled", document.DataModel);
        if (control is BaseButton button)
        {
            button.Disabled = !enabled;
        }
        else
        {
            control.MouseFilter = enabled
                ? Control.MouseFilterEnum.Pass
                : Control.MouseFilterEnum.Ignore;
        }
    }

    private Control ApplySpecificProperties(
        Control control,
        RuntimeComponentNode node,
        RuntimeSurfaceDocument document)
        => control switch
        {
            Button button => ApplyButton(button, node, document),
            ItemList itemList => ApplyList(itemList, node, document),
            Label label => ApplyLabel(label, node, document),
            PanelContainer panel => ApplyPanel(panel, node, document),
            ProgressBar progressBar => ApplyProgressBar(progressBar, node, document),
            _ => control
        };

    private Button ApplyButton(Button button, RuntimeComponentNode node, RuntimeSurfaceDocument document)
    {
        button.Text = RuntimeValueResolver.ResolveText(node.Properties, "text", document.DataModel);
        foreach (var action in node.Actions.Where(action => string.Equals(action.Event, "pressed", StringComparison.OrdinalIgnoreCase)))
        {
            button.Pressed += () => Dispatch(document.SurfaceId, node.Id, action);
        }

        return button;
    }

    private static Label ApplyLabel(Label label, RuntimeComponentNode node, RuntimeSurfaceDocument document)
    {
        label.Text = RuntimeValueResolver.ResolveText(node.Properties, "text", document.DataModel);
        return label;
    }

    private ItemList ApplyList(ItemList list, RuntimeComponentNode node, RuntimeSurfaceDocument document)
    {
        var items = RuntimeValueResolver.ResolveStringList(node.Properties, "items", document.DataModel);
        if (items.Count == 0)
        {
            var emptyText = RuntimeValueResolver.ResolveText(node.Properties, "emptyText", document.DataModel);
            if (!string.IsNullOrWhiteSpace(emptyText))
            {
                list.AddItem(emptyText);
            }
        }
        else
        {
            foreach (var item in items)
            {
                list.AddItem(item);
            }
        }

        var selectedItem = RuntimeValueResolver.ResolveText(node.Properties, "selectedItem", document.DataModel);
        if (!string.IsNullOrEmpty(selectedItem))
        {
            var selectedIndex = FindIndex(items, selectedItem);
            if (selectedIndex >= 0)
            {
                list.Select(selectedIndex);
            }
        }

        foreach (var action in node.Actions.Where(action => string.Equals(action.Event, "selected", StringComparison.OrdinalIgnoreCase)))
        {
            list.ItemSelected += _ => Dispatch(document.SurfaceId, node.Id, action);
        }

        return list;
    }

    private static VBoxContainer ApplyPanel(PanelContainer panel, RuntimeComponentNode node, RuntimeSurfaceDocument document)
    {
        var content = new VBoxContainer
        {
            Name = "Content",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };

        if (node.Layout?.Gap is not null)
        {
            content.AddThemeConstantOverride("separation", (int)Math.Round(node.Layout.Gap.Value));
        }

        var title = RuntimeValueResolver.ResolveText(node.Properties, "title", document.DataModel);
        if (!string.IsNullOrWhiteSpace(title))
        {
            content.AddChild(new Label
            {
                Name = "Title",
                Text = title,
                ClipText = true,
            });
        }

        panel.AddChild(content);
        return content;
    }

    private static ProgressBar ApplyProgressBar(ProgressBar progressBar, RuntimeComponentNode node, RuntimeSurfaceDocument document)
    {
        progressBar.MinValue = RuntimeValueResolver.ResolveNumber(node.Properties, "minimum", document.DataModel);
        progressBar.MaxValue = RuntimeValueResolver.ResolveNumber(node.Properties, "maximum", document.DataModel, fallback: 100);
        progressBar.Value = RuntimeValueResolver.ResolveNumber(node.Properties, "value", document.DataModel);
        return progressBar;
    }

    private static int FindIndex(IReadOnlyList<string> items, string selectedItem)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i], selectedItem, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private void Dispatch(string surfaceId, string componentId, RuntimeActionDescriptor action)
        => _actionHandler?.Invoke(new RuntimeSurfaceActionInvocation(surfaceId, componentId, action));
}
