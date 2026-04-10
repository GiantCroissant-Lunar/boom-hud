using Generated.Hud.UGui;
using UnityEngine;
using UnityEngine.UI;

namespace BoomHud.Compare
{
    [ExecuteAlways]
    public sealed class UGuiExploreHudPresenter : BoomHudUGuiHost
    {
        private const float PartyMemberScale = 0.78f;

        protected override string RootObjectName => "BoomHudUGuiExploreRoot";

        protected override void BindView(RectTransform root)
        {
            var background = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.08f, 0.08f, 1f);

            var hudRoot = UGuiHudPreviewComposer.CreateRect("ExploreHudRoot", root);
            UGuiHudPreviewComposer.StretchToParent(hudRoot, 24f, 24f, 18f, 24f);

            _ = UGuiHudPreviewComposer.CreateCompassLabel(hudRoot);

            var messageLog = UGuiHudPreviewComposer.CreateMessageLog(hudRoot);
            messageLog.Line1.text = "You see a locked door.";
            messageLog.Line2.text = "Aelric attacks Slime!";
            messageLog.Line3.text = "12 damage dealt.";
            messageLog.Line4.text = "Lyra casts Fireball!";
            messageLog.Line5.text = "28 damage dealt!";
            UGuiHudPreviewComposer.PlaceBottomLeft(messageLog.Root, 8f, 156f);

            var rightPanel = UGuiHudPreviewComposer.CreateRect("RightPanel", hudRoot);
            rightPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300f);
            rightPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 672f);
            UGuiHudPreviewComposer.PlaceTopRight(rightPanel, 8f, 8f);

            var minimap = UGuiHudPreviewComposer.CreateMinimap(rightPanel);
            UGuiHudPreviewComposer.PlaceTopLeft(minimap.Root, 0f, 0f);

            var divider = UGuiHudPreviewComposer.CreateImage("Divider", rightPanel, new Color(0.6f, 0.6f, 0.6f, 1f));
            divider.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 280f);
            divider.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 2f);
            UGuiHudPreviewComposer.PlaceTopLeft(divider.rectTransform, 0f, 292f);

            var partyLayout = UGuiHudPreviewComposer.CreateRect("PartyLayout", rightPanel);
            partyLayout.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 280f);
            partyLayout.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 344f);
            UGuiHudPreviewComposer.PlaceTopLeft(partyLayout, 0f, 326f);

            var rowStep = 126f;
            var columnStep = (UGuiHudPreviewComposer.PartyMemberWidth * PartyMemberScale) + 10f;
            var specIndex = 0;
            for (var rowIndex = 0; rowIndex < 3; rowIndex++)
            {
                for (var columnIndex = 0; columnIndex < 2; columnIndex++)
                {
                    var slot = UGuiHudPreviewComposer.CreateRect($"PartySlot{specIndex + 1}", partyLayout);
                    slot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UGuiHudPreviewComposer.PartyMemberWidth);
                    UGuiHudPreviewComposer.PlaceTopLeft(
                        slot,
                        columnIndex * columnStep,
                        rowIndex * rowStep);

                    var view = UGuiHudPreviewComposer.CreateComposedCharPortrait(slot);
                    UGuiHudPreviewComposer.ApplyPartyMemberPresentation(view, UGuiHudPreviewComposer.PartyMembers[specIndex]);
                    view.Generated.Root.localScale = new Vector3(PartyMemberScale, PartyMemberScale, 1f);
                    specIndex++;
                }
            }
        }
    }
}
