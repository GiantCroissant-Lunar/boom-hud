using System.Collections.Generic;
using Generated.Hud.UGui;
using UnityEngine;
using UnityEngine.UI;

namespace BoomHud.Compare
{
    public static class UGuiHudPreviewComposer
    {
        private const string PrefabResourcePrefix = "BoomHudUGuiPrefabs/";
        public const float PartyMemberWidth = 130f;
        public const float PartyMemberHeight = 160f;
        public const float DefaultHpWidth = 90f;
        public const float DefaultMpWidth = 60f;

        private static readonly Color HpFillColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        private static readonly Color MpFillColor = new Color(0.47f, 0.47f, 0.47f, 1f);

        public static readonly PartyMemberSpec ReferenceCharPortrait =
            new PartyMemberSpec("Name", "shield", "ATK 10", "DEF 8", DefaultHpWidth, DefaultMpWidth);

        public static readonly PartyMemberSpec[] PartyMembers =
        {
            new PartyMemberSpec("Aelric", "sword", "ATK 12", "DEF 8", 96f, 60f),
            new PartyMemberSpec("Lyra", "sparkles", "ATK 6", "MAG 14", 70f, 90f),
            new PartyMemberSpec("Theron", "shield", "ATK 14", "DEF 12", 110f, 40f),
            new PartyMemberSpec("Selene", "moon", "ATK 5", "MAG 16", 100f, 80f),
            new PartyMemberSpec("Elara", "cross", "ATK 4", "MAG 11", 80f, 100f),
            new PartyMemberSpec("Darius", "flame", "ATK 16", "DEF 10", 116f, 50f),
        };

        public static void ApplyActionButtonPresentation(ActionButtonView view, string iconToken = "swords")
        {
            ConfigureIcon(view.Icon, iconToken, 16, TextAnchor.MiddleCenter);
            CenterChild(view.Icon.rectTransform, view.Root);
        }

        public static void ApplyStatusIconPresentation(StatusIconView view, string iconToken = "flame")
        {
            ConfigureIcon(view.Icon, iconToken, 24, TextAnchor.MiddleCenter);
            CenterChild(view.Icon.rectTransform, view.Root);
        }

        public static ActionButtonView CreateGeneratedActionButton(Transform? parent, string iconToken = "swords")
        {
            var view = new ActionButtonView(parent);
            ApplyActionButtonPresentation(view, iconToken);
            return view;
        }

        public static ActionButtonView CreateActionButton(Transform? parent, string iconToken = "swords")
        {
            var view = TryInstantiatePrefab("ActionButton", parent, out var root)
                ? ActionButtonView.Bind(root)
                : CreateGeneratedActionButton(parent, iconToken);

            ApplyActionButtonPresentation(view, iconToken);
            return view;
        }

        public static StatusIconView CreateGeneratedStatusIcon(Transform? parent, string iconToken = "flame")
        {
            var view = new StatusIconView(parent);
            ApplyStatusIconPresentation(view, iconToken);
            return view;
        }

        public static StatusIconView CreateStatusIcon(Transform? parent, string iconToken = "flame")
        {
            var view = TryInstantiatePrefab("StatusIcon", parent, out var root)
                ? StatusIconView.Bind(root)
                : CreateGeneratedStatusIcon(parent, iconToken);

            ApplyStatusIconPresentation(view, iconToken);
            return view;
        }

        public static StatBarView CreateGeneratedStatBar(Transform? parent, float fillWidth = DefaultHpWidth, float height = 10f, Color? fillColor = null)
        {
            var view = new StatBarView(parent);
            ConfigureBarPresentation(view, fillWidth, height, fillColor ?? HpFillColor);
            return view;
        }

        public static StatBarView CreateStatBar(Transform? parent, float fillWidth = DefaultHpWidth, float height = 10f, Color? fillColor = null)
        {
            var view = TryInstantiatePrefab("StatBar", parent, out var root)
                ? StatBarView.Bind(root)
                : CreateGeneratedStatBar(parent, fillWidth, height, fillColor);

            ConfigureBarPresentation(view, fillWidth, height, fillColor ?? HpFillColor);
            return view;
        }

        public static MessageLogView CreateGeneratedMessageLog(Transform? parent)
        {
            var view = new MessageLogView(parent);
            ApplyMessageLogPresentation(view);
            return view;
        }

        public static MessageLogView CreateMessageLog(Transform? parent)
        {
            var view = TryInstantiatePrefab("MessageLog", parent, out var root)
                ? MessageLogView.Bind(root)
                : CreateGeneratedMessageLog(parent);

            ApplyMessageLogPresentation(view);
            return view;
        }

        public static MinimapView CreateGeneratedMinimap(Transform? parent)
        {
            return new MinimapView(parent);
        }

        public static MinimapView CreateMinimap(Transform? parent)
            => TryInstantiatePrefab("Minimap", parent, out var root)
                ? MinimapView.Bind(root)
                : CreateGeneratedMinimap(parent);

        public static UGuiComposedCharPortraitView CreateGeneratedComposedCharPortrait(Transform? parent)
        {
            var generated = new CharPortraitView(parent);
            return ComposeCharPortrait(generated);
        }

        public static UGuiComposedCharPortraitView CreateComposedCharPortrait(Transform? parent)
        {
            if (TryInstantiatePrefab("CharPortrait", parent, out var root))
            {
                return ComposeCharPortrait(CharPortraitView.Bind(root));
            }

            return CreateGeneratedComposedCharPortrait(parent);
        }

        public static void ApplyPartyMemberPresentation(UGuiComposedCharPortraitView view, PartyMemberSpec spec, float hpProgress = 1f, float mpProgress = 1f)
        {
            ApplyPreferredWidth(view.Generated.Root, PartyMemberWidth);
            view.Generated.Name.text = spec.Name;
            ConfigureIcon(view.Generated.ClassIcon, spec.IconToken, 32, TextAnchor.MiddleCenter);
            view.Generated.Stat1.text = spec.Stat1;
            view.Generated.Stat2.text = spec.Stat2;
            view.Generated.HpFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, spec.HpWidth * Mathf.Clamp01(hpProgress));
            view.Generated.MpFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, spec.MpWidth * Mathf.Clamp01(mpProgress));
            view.SyncStatusBarsFromGeneratedWidths();
        }

        public static void SyncComposedCharPortraitBars(CharPortraitView view)
        {
            SyncNamedStatusBar(view.Hp, view.HpFill, "HpBar");
            SyncNamedStatusBar(view.Mp, view.MpFill, "MpBar");
        }

        public static Text CreateCompassLabel(Transform parent)
        {
            var text = CreateText("Compass", parent, "N", 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -18f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 32f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 32f);
            return text;
        }

        public static RectTransform CreateRect(string name, Transform? parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            if (parent != null)
            {
                rectTransform.SetParent(parent, false);
            }
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            return rectTransform;
        }

        public static Image CreateImage(string name, Transform? parent, Color color)
        {
            var rectTransform = CreateRect(name, parent);
            rectTransform.gameObject.AddComponent<CanvasRenderer>();
            var image = rectTransform.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(string name, Transform? parent, string text, int fontSize, Color color, TextAnchor alignment = TextAnchor.UpperLeft, FontStyle fontStyle = FontStyle.Normal)
        {
            var rectTransform = CreateRect(name, parent);
            rectTransform.gameObject.AddComponent<CanvasRenderer>();
            var label = rectTransform.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = fontStyle;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static void StretchToParent(RectTransform rectTransform, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(-right, -top);
        }

        public static void PlaceTopLeft(RectTransform rectTransform, float left, float top)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(left, -top);
        }

        public static void PlaceTopRight(RectTransform rectTransform, float right, float top)
        {
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-right, -top);
        }

        public static void PlaceBottomLeft(RectTransform rectTransform, float left, float bottom)
        {
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(0f, 0f);
            rectTransform.pivot = new Vector2(0f, 0f);
            rectTransform.anchoredPosition = new Vector2(left, bottom);
        }

        private static void ConfigureIcon(Text label, string iconToken, int fontSize, TextAnchor alignment)
        {
            label.text = ResolveLucideGlyph(iconToken);
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static void CenterChild(RectTransform child, RectTransform parent)
        {
            child.SetParent(parent, false);
            child.anchorMin = new Vector2(0.5f, 0.5f);
            child.anchorMax = new Vector2(0.5f, 0.5f);
            child.pivot = new Vector2(0.5f, 0.5f);
            child.anchoredPosition = Vector2.zero;
        }

        private static void ConfigurePortraitLayout(CharPortraitView generated)
        {
            ApplyPreferredWidth(generated.Root, PartyMemberWidth);
            ApplyPreferredHeight(generated.Root, PartyMemberHeight);
            generated.Root.localScale = Vector3.one;

            if (generated.Root.GetComponent<VerticalLayoutGroup>() is { } rootLayout)
            {
                rootLayout.enabled = false;
            }

            if (generated.Root.GetComponent<ContentSizeFitter>() is { } rootFitter)
            {
                rootFitter.enabled = false;
            }

            ConfigureTextLine(generated.Name, PartyMemberWidth, TextAnchor.MiddleCenter, Color.white);
            ConfigureTextLine(generated.Stat1, 52f, TextAnchor.MiddleCenter, new Color(0.67f, 0.67f, 0.67f, 1f));
            ConfigureTextLine(generated.Stat2, 52f, TextAnchor.MiddleCenter, new Color(0.67f, 0.67f, 0.67f, 1f));

            ApplyPreferredWidth(generated.Hp, PartyMemberWidth);
            ApplyPreferredWidth(generated.Mp, PartyMemberWidth);
            ApplyPreferredWidth(generated.Stats, PartyMemberWidth);
            ApplyPreferredWidth(generated.ActionGrid, PartyMemberWidth);

            if (generated.Stats.GetComponent<HorizontalLayoutGroup>() is { } statsLayout)
            {
                statsLayout.padding = new RectOffset(0, 0, 0, 0);
                statsLayout.spacing = 4f;
                statsLayout.childAlignment = TextAnchor.MiddleCenter;
                statsLayout.childControlWidth = false;
                statsLayout.childControlHeight = true;
                statsLayout.childForceExpandWidth = false;
                statsLayout.childForceExpandHeight = false;
            }

            if (generated.ActionGrid.GetComponent<HorizontalLayoutGroup>() is { } actionLayout)
            {
                actionLayout.padding = new RectOffset(0, 0, 0, 0);
                actionLayout.spacing = 4f;
                actionLayout.childAlignment = TextAnchor.MiddleCenter;
                actionLayout.childControlWidth = false;
                actionLayout.childControlHeight = false;
                actionLayout.childForceExpandWidth = false;
                actionLayout.childForceExpandHeight = false;
            }

            CenterAbsoluteChild(generated.ClassIcon.rectTransform, 32f, 32f);
            ApplyExplicitBorder(generated.Face.gameObject, Color.white, 5f);
            ConfigureAbsoluteRect(generated.Face, 37f, 0f, 56f, 56f);
            ConfigureAbsoluteRect(generated.Name.rectTransform, 0f, 62f, PartyMemberWidth, 12f);
            ConfigureAbsoluteRect(generated.Hp, 0f, 84f, PartyMemberWidth, 10f);
            ConfigureAbsoluteRect(generated.Mp, 0f, 98f, PartyMemberWidth, 8f);
            ConfigureAbsoluteRect(generated.Stats, 11f, 112f, 108f, 10f);
            ConfigureAbsoluteRect(generated.ActionGrid, 5f, 132f, 120f, 27f);

            ApplyIgnoreLayout(generated.Face);
            ApplyIgnoreLayout(generated.Name.rectTransform);
            ApplyIgnoreLayout(generated.Hp);
            ApplyIgnoreLayout(generated.Mp);
            ApplyIgnoreLayout(generated.Stats);
            ApplyIgnoreLayout(generated.ActionGrid);
        }

        private static void ConfigurePortraitActionSlot(RectTransform slot, Text icon, string iconToken)
        {
            slot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 27f);
            slot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 27f);
            ApplyPreferredWidth(slot, 27f);
            ApplyPreferredHeight(slot, 27f);

            if (slot.GetComponent<VerticalLayoutGroup>() is { } layout)
            {
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.padding = new RectOffset(0, 0, 0, 0);
                layout.spacing = 0f;
            }

            ConfigureIcon(icon, iconToken, 16, TextAnchor.MiddleCenter);
            CenterChild(icon.rectTransform, slot);
            icon.rectTransform.anchoredPosition = new Vector2(0f, -0.5f);
            ApplyExplicitBorder(slot.gameObject, Color.white, 2f);
        }

        private static void ConfigureBarPresentation(StatBarView view, float fillWidth, float height, Color fillColor)
        {
            ApplyPreferredHeight(view.Root, height);
            ApplyPreferredWidth(view.Root, 108f);
            view.Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            view.Fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fillWidth);
            view.Fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            StretchVerticalLeft(view.Fill);

            if (view.Root.GetComponent<Image>() is { } rootImage)
            {
                rootImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                rootImage.raycastTarget = false;
            }

            if (view.Fill.GetComponent<Image>() is { } fillImage)
            {
                fillImage.color = fillColor;
                fillImage.raycastTarget = false;
            }
        }

        private static StatBarView AttachStatusBar(RectTransform container, RectTransform animatedFill, float fillWidth, float height, Color fillColor)
        {
            var animatedFillImage = animatedFill.GetComponent<Image>();
            if (animatedFillImage != null)
            {
                var color = animatedFillImage.color;
                color.a = 0f;
                animatedFillImage.color = color;
                animatedFillImage.raycastTarget = false;
            }

            var bar = new StatBarView(container);
            bar.Root.name = container.name + "Bar";
            StretchToParent(bar.Root);
            ApplyIgnoreLayout(bar.Root);
            ConfigureBarPresentation(bar, fillWidth, height, fillColor);

            return bar;
        }

        private static UGuiComposedCharPortraitView ComposeCharPortrait(CharPortraitView generated)
        {
            ConfigurePortraitLayout(generated);
            ConfigurePortraitActionSlot(generated.Atk, generated.QEpO3, "swords");
            ConfigurePortraitActionSlot(generated.Mag, generated.AIphN, "wand-sparkles");
            ConfigurePortraitActionSlot(generated.Def, generated.E4QKZ, "shield");
            ConfigurePortraitActionSlot(generated.Item, generated.DVzX7, "flask-conical");

            var hpBar = EnsureStatusBar(generated.Hp, generated.HpFill, "HpBar", DefaultHpWidth, 10f, HpFillColor);
            var mpBar = EnsureStatusBar(generated.Mp, generated.MpFill, "MpBar", DefaultMpWidth, 8f, MpFillColor);
            var view = new UGuiComposedCharPortraitView(generated, hpBar, mpBar);
            view.SyncStatusBarsFromGeneratedWidths();
            return view;
        }

        private static StatBarView EnsureStatusBar(RectTransform container, RectTransform animatedFill, string barName, float fillWidth, float height, Color fillColor)
        {
            if (container.Find(barName) is RectTransform existing)
            {
                var view = StatBarView.Bind(existing);
                ConfigureBarPresentation(view, fillWidth, height, fillColor);
                var animatedFillImage = animatedFill.GetComponent<Image>();
                if (animatedFillImage != null)
                {
                    var color = animatedFillImage.color;
                    color.a = 0f;
                    animatedFillImage.color = color;
                    animatedFillImage.raycastTarget = false;
                }

                return view;
            }

            return AttachStatusBar(container, animatedFill, fillWidth, height, fillColor);
        }

        private static void SyncNamedStatusBar(RectTransform container, RectTransform animatedFill, string barName)
        {
            if (container.Find(barName) is not RectTransform barRoot)
            {
                return;
            }

            var view = StatBarView.Bind(barRoot);
            view.Fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, animatedFill.rect.width);
            view.Fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, animatedFill.rect.height);
        }

        private static void ApplyMessageLogPresentation(MessageLogView view)
        {
            view.Line1.text = "You hear distant footsteps.";
            view.Line2.text = "Aelric blocks the attack.";
            view.Line3.text = "Lyra channels Firebolt.";
            view.Line4.text = "Critical hit for 28 damage.";
            view.Line5.text = "Treasure chest spotted.";
        }

        private static void StretchVerticalLeft(RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void ApplyPreferredWidth(RectTransform rectTransform, float width)
        {
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            var element = rectTransform.GetComponent<LayoutElement>() ?? rectTransform.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
        }

        private static void ApplyPreferredHeight(RectTransform rectTransform, float height)
        {
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            var element = rectTransform.GetComponent<LayoutElement>() ?? rectTransform.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
        }

        private static void ApplyIgnoreLayout(RectTransform rectTransform)
        {
            var element = rectTransform.GetComponent<LayoutElement>() ?? rectTransform.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
        }

        private static void ConfigureTextLine(Text text, float preferredWidth, TextAnchor alignment, Color color)
        {
            ApplyPreferredWidth(text.rectTransform, preferredWidth);
            text.alignment = alignment;
            text.color = color;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static void CenterAbsoluteChild(RectTransform rectTransform, float width, float height)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private static void ConfigureAbsoluteRect(RectTransform rectTransform, float left, float top, float width, float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(left, -top);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        public static void ApplyExplicitBorder(GameObject gameObject, Color color, float width)
        {
            if (width <= 0f)
            {
                return;
            }

            var outline = gameObject.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }

            if (!gameObject.TryGetComponent<RectTransform>(out var rectTransform))
            {
                return;
            }

            var borderRoot = gameObject.transform.Find("__Border") as RectTransform ?? CreateRect("__Border", rectTransform);
            borderRoot.SetParent(rectTransform, false);
            StretchToParent(borderRoot);
            ApplyIgnoreLayout(borderRoot);
            borderRoot.SetAsLastSibling();

            ConfigureBorderSegment(EnsureBorderSegment(borderRoot, "Top", color), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, width));
            ConfigureBorderSegment(EnsureBorderSegment(borderRoot, "Bottom", color), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, width));
            ConfigureBorderSegment(EnsureBorderSegment(borderRoot, "Left", color), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(width, 0f));
            ConfigureBorderSegment(EnsureBorderSegment(borderRoot, "Right", color), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(width, 0f));
        }

        private static RectTransform EnsureBorderSegment(RectTransform parent, string name, Color color)
        {
            var existing = parent.Find(name);
            if (existing != null && existing.TryGetComponent<Image>(out var image))
            {
                image.color = color;
                image.raycastTarget = false;
                return image.rectTransform;
            }

            var created = CreateImage(name, parent, color);
            created.raycastTarget = false;
            return created.rectTransform;
        }

        private static void ConfigureBorderSegment(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private static string ResolveLucideGlyph(string iconToken)
        {
            return iconToken switch
            {
                "cross" => "\uE1E5",
                "flame" => "\uE0D2",
                "flask-conical" => "\uE0D5",
                "moon" => "\uE11E",
                "shield" => "\uE158",
                "sparkles" => "\uE412",
                "sword" => "\uE2B3",
                "swords" => "\uE2B4",
                "wand" => "\uE246",
                "wand-2" => "\uE357",
                "wand-sparkles" => "\uE357",
                _ => iconToken,
            };
        }

        private static bool TryInstantiatePrefab(string prefabName, Transform? parent, out RectTransform root)
        {
            root = null!;
            var prefab = Resources.Load<GameObject>(PrefabResourcePrefix + prefabName);
            if (prefab == null)
            {
                return false;
            }

            var instance = Object.Instantiate(prefab, parent, false);
            if (!instance.TryGetComponent<RectTransform>(out root))
            {
                Object.DestroyImmediate(instance);
                return false;
            }

            return true;
        }

        public readonly struct PartyMemberSpec
        {
            public PartyMemberSpec(string name, string iconToken, string stat1, string stat2, float hpWidth, float mpWidth)
            {
                Name = name;
                IconToken = iconToken;
                Stat1 = stat1;
                Stat2 = stat2;
                HpWidth = hpWidth;
                MpWidth = mpWidth;
            }

            public string Name { get; }

            public string IconToken { get; }

            public string Stat1 { get; }

            public string Stat2 { get; }

            public float HpWidth { get; }

            public float MpWidth { get; }
        }

        public sealed class UGuiComposedCharPortraitView
        {
            private readonly StatBarView _hpBar;
            private readonly StatBarView _mpBar;

            public UGuiComposedCharPortraitView(CharPortraitView generated, StatBarView hpBar, StatBarView mpBar)
            {
                Generated = generated;
                _hpBar = hpBar;
                _mpBar = mpBar;
            }

            public CharPortraitView Generated { get; }

            public void SyncStatusBarsFromGeneratedWidths()
            {
                _hpBar.Fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Generated.HpFill.rect.width);
                _mpBar.Fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Generated.MpFill.rect.width);
                _hpBar.Fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Generated.Hp.rect.height);
                _mpBar.Fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Generated.Mp.rect.height);
            }
        }
    }
}
