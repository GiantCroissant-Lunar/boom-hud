using BoomHud.Abstractions.Runtime;
using Godot;

namespace BoomHud.Godot.Runtime;

public sealed class RuntimeSurfaceRenderer
{
    private static readonly string[] ButtonStyleSlots = { "normal", "hover", "pressed", "focus", "disabled" };
    private static readonly string[] ButtonColorSlots =
        { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" };

    private readonly RuntimeSurfaceCatalog _catalog;
    private readonly RuntimeSurfaceValidatorOptions _validatorOptions;
    private readonly RuntimeSurfaceActionHandler? _actionHandler;
    private readonly RuntimeSurfaceTheme? _theme;
    private readonly Dictionary<string, Font> _fontCache = new();

    public RuntimeSurfaceRenderer(RuntimeSurfaceRendererOptions? options = null)
    {
        options ??= new RuntimeSurfaceRendererOptions();
        _catalog = options.Catalog ?? RuntimeSurfaceCatalog.Basic;
        _validatorOptions = options.ValidatorOptions ?? new RuntimeSurfaceValidatorOptions();
        _actionHandler = options.ActionHandler;
        _theme = options.Theme;
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

        var style = StyleFor(node, document);
        if (style is { Opacity: < 1.0 } dimmed)
        {
            control.Modulate = new Color(1f, 1f, 1f, (float)dimmed.Opacity);
        }

        var childHost = ApplySpecificProperties(control, node, document);

        // Space-between idiom: when a container holds a `spacer` child, the spacer absorbs the free space
        // (ExpandFill on the main axis) while its siblings hug their content (ShrinkBegin) — this is how the
        // design pushes a trailing pill/badge/button to the right edge. Containers WITHOUT a spacer keep the
        // default equal-fill behavior, so existing surfaces are unchanged. `align: "center"` centers children
        // on the cross axis.
        var hasSpacer = false;
        foreach (var c in node.Children)
        {
            if (string.Equals(c.Type, "spacer", StringComparison.OrdinalIgnoreCase)) { hasSpacer = true; break; }
        }
        var horizontal = string.Equals(node.Layout?.Type, "horizontal", StringComparison.OrdinalIgnoreCase);
        var centerCross = string.Equals(node.Layout?.Align, "center", StringComparison.OrdinalIgnoreCase);

        foreach (var child in node.Children)
        {
            var childControl = CreateControlTree(document, child);
            if (hasSpacer)
            {
                var isSpacer = string.Equals(child.Type, "spacer", StringComparison.OrdinalIgnoreCase);
                var flag = isSpacer ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin;
                if (horizontal) childControl.SizeFlagsHorizontal = flag;
                else childControl.SizeFlagsVertical = flag;
            }
            if (centerCross)
            {
                if (horizontal) childControl.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                else childControl.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            }
            childHost.AddChild(childControl);
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
            "scroll" => new ScrollContainer(),
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
            ScrollContainer sc => ApplyScroll(sc, node, document),
            _ => control
        };

    private Button ApplyButton(Button button, RuntimeComponentNode node, RuntimeSurfaceDocument document)
    {
        button.Text = RuntimeValueResolver.ResolveText(node.Properties, "text", document.DataModel);

        var style = StyleFor(node, document);
        if (style is not null)
        {
            if (BuildStyleBox(style) is { } box)
            {
                foreach (var slot in ButtonStyleSlots)
                {
                    button.AddThemeStyleboxOverride(slot, box);
                }
            }
            if (style.FontColor is not null)
            {
                var color = Color.FromHtml(style.FontColor);
                foreach (var slot in ButtonColorSlots)
                {
                    button.AddThemeColorOverride(slot, color);
                }
            }
            ApplyFont(button, style);
        }

        foreach (var action in node.Actions.Where(action => string.Equals(action.Event, "pressed", StringComparison.OrdinalIgnoreCase)))
        {
            button.Pressed += () => Dispatch(document.SurfaceId, node.Id, action);
        }

        return button;
    }

    private Label ApplyLabel(Label label, RuntimeComponentNode node, RuntimeSurfaceDocument document)
    {
        label.Text = RuntimeValueResolver.ResolveText(node.Properties, "text", document.DataModel);

        // A styled label doubles as a badge/pill: its `normal` stylebox gives the chip background + border +
        // padding, and the text style sets the chip's font color/size. Plain labels (no box props) just take
        // the font style. Untyped/unthemed labels are unchanged.
        var style = StyleFor(node, document);
        if (style is not null)
        {
            if (BuildStyleBox(style) is { } box)
            {
                label.AddThemeStyleboxOverride("normal", box);
            }
            ApplyTextStyle(label, style);
        }

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

    private Container ApplyPanel(PanelContainer panel, RuntimeComponentNode node, RuntimeSurfaceDocument document)
    {
        // A styled panel is the card / header / row box: its `panel` stylebox gives the fill + border +
        // corner radius, and the stylebox content margins give the inner padding. The content container's
        // orientation follows the node's layout type so a panel can host a horizontal row (e.g. a card
        // header: id · spacer · pill · badge · button), not just a vertical stack.
        var style = StyleFor(node, document);
        if (style is not null && BuildStyleBox(style) is { } box)
        {
            panel.AddThemeStyleboxOverride("panel", box);
        }

        var horizontal = string.Equals(node.Layout?.Type, "horizontal", StringComparison.OrdinalIgnoreCase);
        Container content = horizontal
            ? new HBoxContainer { Name = "Content" }
            : new VBoxContainer { Name = "Content" };
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

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

    // A `scroll` node wraps exactly one child (a vertical container) and gives it vertical room the
    // pure-BoomHud render path otherwise lacks: it fills whatever space its parent gives it and scrolls
    // its content vertically only (horizontal scrolling is disabled — content wraps/truncates instead).
    private static ScrollContainer ApplyScroll(ScrollContainer scroll, RuntimeComponentNode node, RuntimeSurfaceDocument document)
    {
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        return scroll;
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

    // ----- Theming (no-op unless a theme is configured and the node carries a matching `variant`) -----

    private RuntimeComponentStyle? StyleFor(RuntimeComponentNode node, RuntimeSurfaceDocument document)
    {
        if (_theme is null)
        {
            return null;
        }

        var variant = RuntimeValueResolver.ResolveText(node.Properties, "variant", document.DataModel, fallback: string.Empty);
        return _theme.Get(variant);
    }

    private static StyleBoxFlat? BuildStyleBox(RuntimeComponentStyle style)
    {
        if (!style.HasBox)
        {
            return null;
        }

        var box = new StyleBoxFlat
        {
            BgColor = style.Fill is not null ? Color.FromHtml(style.Fill) : new Color(0f, 0f, 0f, 0f),
        };

        if (style.CornerRadius > 0)
        {
            box.SetCornerRadiusAll(style.CornerRadius);
        }

        if (style.BorderColor is not null && (style.BorderWidth > 0 || style.BorderBottomOnly))
        {
            box.BorderColor = Color.FromHtml(style.BorderColor);
            var width = style.BorderWidth > 0 ? style.BorderWidth : 1;
            if (style.BorderBottomOnly)
            {
                box.BorderWidthBottom = width;
            }
            else
            {
                box.SetBorderWidthAll(width);
            }
        }

        if (style.Padding is { Count: 4 } pad)
        {
            box.ContentMarginTop = pad[0];
            box.ContentMarginRight = pad[1];
            box.ContentMarginBottom = pad[2];
            box.ContentMarginLeft = pad[3];
        }

        return box;
    }

    private void ApplyTextStyle(Control control, RuntimeComponentStyle style)
    {
        if (style.FontColor is not null)
        {
            control.AddThemeColorOverride("font_color", Color.FromHtml(style.FontColor));
        }

        ApplyFont(control, style);
    }

    private void ApplyFont(Control control, RuntimeComponentStyle style)
    {
        if (style.FontSize > 0)
        {
            control.AddThemeFontSizeOverride("font_size", style.FontSize);
        }

        if (style.FontFamily is not null)
        {
            control.AddThemeFontOverride("font", GetFont(style.FontFamily, style.FontWeight));
        }
    }

    private Font GetFont(string family, int weight)
    {
        var key = $"{family}:{weight}";
        if (_fontCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var font = new SystemFont { FontNames = new[] { family } };
        if (weight > 0)
        {
            font.FontWeight = weight;
        }

        _fontCache[key] = font;
        return font;
    }
}
