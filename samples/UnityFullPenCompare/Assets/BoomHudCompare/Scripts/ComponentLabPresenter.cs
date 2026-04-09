using BoomHud.Unity.UIToolkit;
using Generated.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace BoomHud.Compare
{
    [ExecuteAlways]
    public sealed class ComponentLabPresenter : BoomHudUiToolkitHost
    {
        private const string GeneratedBasePath = "BoomHudGenerated/";

        protected override void BindView(VisualElement root)
        {
            root.Clear();
            root.style.width = Length.Percent(100f);
            root.style.height = Length.Percent(100f);
            root.style.flexGrow = 1f;
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingLeft = 24f;
            root.style.paddingTop = 24f;
            root.style.paddingRight = 24f;
            root.style.paddingBottom = 24f;
            root.style.backgroundColor = new Color(0.07f, 0.07f, 0.07f, 1f);

            var header = new Label("BoomHud Component Lab");
            header.style.fontSize = 24f;
            header.style.color = new Color(0.96f, 0.96f, 0.96f, 1f);
            header.style.marginBottom = 8f;
            root.Add(header);

            var subtitle = new Label("Generated components mounted individually in code for fidelity tuning.");
            subtitle.style.fontSize = 14f;
            subtitle.style.color = new Color(0.70f, 0.70f, 0.70f, 1f);
            subtitle.style.marginBottom = 18f;
            root.Add(subtitle);

            var scrollView = new ScrollView();
            scrollView.style.width = Length.Percent(100f);
            scrollView.style.height = Length.Percent(100f);
            scrollView.style.flexGrow = 1f;
            scrollView.style.alignSelf = Align.Stretch;
            root.Add(scrollView);

            var gallery = new VisualElement();
            gallery.style.width = Length.Percent(100f);
            gallery.style.flexDirection = FlexDirection.Row;
            gallery.style.flexWrap = Wrap.Wrap;
            gallery.style.alignItems = Align.FlexStart;
            scrollView.Add(gallery);

            AddActionButtonPreview(gallery);
            AddStatusIconPreview(gallery);
            AddStatBarPreview(gallery);
            AddMessageLogPreview(gallery);
            AddCharPortraitPreview(gallery);
            AddMinimapPreview(gallery);
        }

        private static void AddActionButtonPreview(VisualElement gallery)
        {
            var surface = CreatePreviewCard(gallery, "ActionButton", 220f, 120f);
            var componentRoot = InstantiateGeneratedRoot(surface, "ActionButtonView");
            if (componentRoot == null)
            {
                return;
            }

            _ = new ActionButtonView(componentRoot);
        }

        private static void AddStatusIconPreview(VisualElement gallery)
        {
            var surface = CreatePreviewCard(gallery, "StatusIcon", 220f, 120f);
            var componentRoot = InstantiateGeneratedRoot(surface, "StatusIconView");
            if (componentRoot == null)
            {
                return;
            }

            _ = new StatusIconView(componentRoot);
        }

        private static void AddStatBarPreview(VisualElement gallery)
        {
            var surface = CreatePreviewCard(gallery, "StatBar", 280f, 120f);
            var componentRoot = InstantiateGeneratedRoot(surface, "StatBarView");
            if (componentRoot == null)
            {
                return;
            }

            var view = new StatBarView(componentRoot);
            view.Fill.style.width = 80f;
        }

        private static void AddMessageLogPreview(VisualElement gallery)
        {
            var surface = CreatePreviewCard(gallery, "MessageLog", 360f, 180f);
            var componentRoot = InstantiateGeneratedRoot(surface, "MessageLogView");
            if (componentRoot == null)
            {
                return;
            }

            var view = new MessageLogView(componentRoot);
            view.Line1.text = "You hear distant footsteps.";
            view.Line2.text = "Aelric blocks the attack.";
            view.Line3.text = "Lyra channels Firebolt.";
            view.Line4.text = "Critical hit for 28 damage.";
            view.Line5.text = "Treasure chest spotted.";
        }

        private static void AddCharPortraitPreview(VisualElement gallery)
        {
            var surface = CreatePreviewCard(gallery, "CharPortrait", 280f, 220f);
            var componentRoot = InstantiateGeneratedRoot(surface, "CharPortraitView");
            if (componentRoot == null)
            {
                return;
            }

            var view = new CharPortraitView(componentRoot);
            view.Name.text = "Name";
            view.Stat1.text = "ATK 10";
            view.Stat2.text = "DEF 8";
            view.HpFill.style.width = 90f;
            view.MpFill.style.width = 60f;
        }

        private static void AddMinimapPreview(VisualElement gallery)
        {
            var surface = CreatePreviewCard(gallery, "Minimap", 360f, 380f);
            var componentRoot = InstantiateGeneratedRoot(surface, "MinimapView");
            if (componentRoot == null)
            {
                return;
            }

            _ = new MinimapView(componentRoot);
        }

        private static VisualElement CreatePreviewCard(VisualElement gallery, string title, float width, float height)
        {
            var card = new VisualElement();
            card.style.width = width;
            card.style.minHeight = height;
            card.style.paddingLeft = 14f;
            card.style.paddingTop = 14f;
            card.style.paddingRight = 14f;
            card.style.paddingBottom = 14f;
            card.style.backgroundColor = new Color(0.11f, 0.11f, 0.11f, 1f);
            card.style.borderLeftWidth = 1f;
            card.style.borderTopWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            card.style.borderTopColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            card.style.borderRightColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            card.style.borderBottomColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            card.style.borderTopLeftRadius = 6f;
            card.style.borderTopRightRadius = 6f;
            card.style.borderBottomLeftRadius = 6f;
            card.style.borderBottomRightRadius = 6f;
            card.style.marginRight = 16f;
            card.style.marginBottom = 16f;

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 14f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.94f, 0.94f, 0.94f, 1f);
            titleLabel.style.marginBottom = 10f;
            card.Add(titleLabel);

            var surface = new VisualElement();
            surface.style.flexGrow = 1f;
            surface.style.minHeight = height - 50f;
            surface.style.paddingLeft = 10f;
            surface.style.paddingTop = 10f;
            surface.style.paddingRight = 10f;
            surface.style.paddingBottom = 10f;
            surface.style.backgroundColor = Color.black;
            surface.style.alignItems = Align.Center;
            surface.style.justifyContent = Justify.Center;
            card.Add(surface);

            gallery.Add(card);
            return surface;
        }

        private static VisualElement? InstantiateGeneratedRoot(VisualElement surface, string viewName)
        {
            var visualTree = Resources.Load<VisualTreeAsset>(GeneratedBasePath + viewName);
            if (visualTree == null)
            {
                surface.Add(CreateMissingAssetLabel(viewName + ".uxml"));
                return null;
            }

            var styleSheet = Resources.Load<StyleSheet>(GeneratedBasePath + viewName);
            if (styleSheet != null && !surface.styleSheets.Contains(styleSheet))
            {
                surface.styleSheets.Add(styleSheet);
            }

            var staging = new VisualElement();
            visualTree.CloneTree(staging);
            if (staging.childCount == 0)
            {
                surface.Add(CreateMissingAssetLabel(viewName + " has no root element."));
                return null;
            }

            var componentRoot = staging.ElementAt(0);
            surface.Add(componentRoot);
            return componentRoot;
        }

        private static Label CreateMissingAssetLabel(string assetName)
        {
            var label = new Label("Missing generated asset: " + assetName);
            label.style.color = new Color(1f, 0.45f, 0.45f, 1f);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return label;
        }
    }
}
