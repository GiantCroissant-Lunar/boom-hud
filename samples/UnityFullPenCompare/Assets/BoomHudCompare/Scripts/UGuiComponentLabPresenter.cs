using Generated.Hud.UGui;
using UnityEngine;
using UnityEngine.UI;

namespace BoomHud.Compare
{
    [ExecuteAlways]
    public sealed class UGuiComponentLabPresenter : BoomHudUGuiHost
    {
        protected override string RootObjectName => "BoomHudUGuiComponentLabRoot";

        protected override void BindView(RectTransform root)
        {
            RemoveLayoutComponents(root.gameObject);

            var background = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            background.color = new Color(0.07f, 0.07f, 0.07f, 1f);

            var header = CreateText("Header", root, "BoomHud uGUI Component Lab", 24, new Color(0.96f, 0.96f, 0.96f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            ConfigureAbsoluteRect(header.rectTransform, 24f, 24f, 420f, 32f);

            var subtitle = CreateText("Subtitle", root, "Generated uGUI components mounted individually for parity and capture work.", 14, new Color(0.70f, 0.70f, 0.70f, 1f));
            ConfigureAbsoluteRect(subtitle.rectTransform, 24f, 58f, 620f, 20f);

            AddActionButtonPreview(root, 24f, 136f);
            AddStatusIconPreview(root, 260f, 136f);
            AddStatBarPreview(root, 24f, 272f);
            AddMessageLogPreview(root, 320f, 272f);
            AddCharPortraitPreview(root, 24f, 448f);
            AddMinimapPreview(root, 320f, 560f);
            AddPartyMemberLayoutPreview(root, 704f, 136f);
        }

        private static void AddActionButtonPreview(Transform parent, float left, float top)
        {
            var surface = CreateCard(parent, "ActionButton", 220f, 120f, left, top);
            var capture = CreateCaptureSurface(surface, "ActionButtonRoot", 28f, 28f);
            var view = UGuiHudPreviewComposer.CreateActionButton(capture);
            view.Root.name = "ActionButtonContent";
        }

        private static void AddStatusIconPreview(Transform parent, float left, float top)
        {
            var surface = CreateCard(parent, "StatusIcon", 220f, 120f, left, top);
            var capture = CreateCaptureSurface(surface, "StatusIconRoot", 24f, 24f);
            var view = UGuiHudPreviewComposer.CreateStatusIcon(capture);
            view.Root.name = "StatusIconContent";
        }

        private static void AddStatBarPreview(Transform parent, float left, float top)
        {
            var surface = CreateCard(parent, "StatBar", 280f, 120f, left, top);
            var capture = CreateCaptureSurface(surface, "StatBarRoot", 108f, 12f);
            var view = UGuiHudPreviewComposer.CreateStatBar(capture, 80f, 12f);
            view.Root.name = "StatBarContent";
        }

        private static void AddMessageLogPreview(Transform parent, float left, float top)
        {
            var surface = CreateCard(parent, "MessageLog", 360f, 230f, left, top);
            var capture = CreateCaptureSurface(surface, "MessageLogRoot", 280f, 144f);
            var view = UGuiHudPreviewComposer.CreateMessageLog(capture);
            view.Root.name = "MessageLogContent";
        }

        private static void AddCharPortraitPreview(Transform parent, float left, float top)
        {
            var surface = CreateCard(parent, "CharPortrait", 280f, 220f, left, top);
            var capture = CreateCaptureSurface(surface, "CharPortraitRoot", UGuiHudPreviewComposer.PartyMemberWidth, 160f);
            var view = UGuiHudPreviewComposer.CreateComposedCharPortrait(capture);
            view.Generated.Root.name = "CharPortraitContent";
            UGuiHudPreviewComposer.ApplyPartyMemberPresentation(view, UGuiHudPreviewComposer.ReferenceCharPortrait);
        }

        private static void AddMinimapPreview(Transform parent, float left, float top)
        {
            var surface = CreateCard(parent, "Minimap", 360f, 380f, left, top);
            var capture = CreateCaptureSurface(surface, "MinimapRoot", 280f, 268f);
            var view = UGuiHudPreviewComposer.CreateMinimap(capture);
            view.Root.name = "MinimapContent";
        }

        private static void AddPartyMemberLayoutPreview(Transform parent, float left, float top)
        {
            var surface = CreateCard(parent, "Party Member Layout", 360f, 560f, left, top);
            var content = CreateRect("PartyLayoutContent", surface);
            ConfigureAbsoluteRect(content, 18f, 12f, 324f, 500f);

            var specIndex = 0;
            for (var rowIndex = 0; rowIndex < 3; rowIndex++)
            {
                for (var columnIndex = 0; columnIndex < 2; columnIndex++)
                {
                    var slot = CreateRect($"PartySlot{specIndex + 1}", content);
                    ConfigureAbsoluteRect(
                        slot,
                        columnIndex * (UGuiHudPreviewComposer.PartyMemberWidth + 12f),
                        rowIndex * 164f,
                        UGuiHudPreviewComposer.PartyMemberWidth,
                        160f);

                    var view = UGuiHudPreviewComposer.CreateComposedCharPortrait(slot);
                    view.Generated.Root.name = $"PartyCharPortrait{specIndex + 1}";
                    UGuiHudPreviewComposer.ApplyPartyMemberPresentation(view, UGuiHudPreviewComposer.PartyMembers[specIndex]);
                    specIndex++;
                }
            }
        }

        private static RectTransform CreateCard(Transform parent, string title, float width, float height, float left, float top)
        {
            var card = CreateImage("Card_" + title, parent, new Color(0.11f, 0.11f, 0.11f, 1f)).rectTransform;
            ConfigureAbsoluteRect(card, left, top, width, height);

            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);

            var titleText = CreateText("Title", card, title, 14, new Color(0.94f, 0.94f, 0.94f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            ConfigureAbsoluteRect(titleText.rectTransform, 14f, 14f, width - 28f, 20f);

            var surface = CreateImage("Surface", card, Color.black).rectTransform;
            ConfigureAbsoluteRect(surface, 14f, 44f, width - 28f, height - 58f);
            return surface;
        }

        private static RectTransform CreateCaptureSurface(Transform parent, string targetName, float width, float height)
        {
            var capture = CreateRect(targetName, parent);
            capture.anchorMin = new Vector2(0.5f, 0.5f);
            capture.anchorMax = new Vector2(0.5f, 0.5f);
            capture.pivot = new Vector2(0.5f, 0.5f);
            capture.anchoredPosition = Vector2.zero;
            capture.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            capture.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return capture;
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

        private static void RemoveLayoutComponents(GameObject gameObject)
        {
            RemoveComponent<VerticalLayoutGroup>(gameObject);
            RemoveComponent<HorizontalLayoutGroup>(gameObject);
            RemoveComponent<GridLayoutGroup>(gameObject);
            RemoveComponent<ContentSizeFitter>(gameObject);
            RemoveComponent<LayoutElement>(gameObject);
        }

        private static void RemoveComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(component);
            }
            else
            {
                Object.DestroyImmediate(component);
            }
        }
    }
}
