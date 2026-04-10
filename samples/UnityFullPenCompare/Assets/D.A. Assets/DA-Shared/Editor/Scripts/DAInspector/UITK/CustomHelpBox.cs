using System;
using System.Collections.Generic;
using DA_Assets.Shared.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DA_Assets.DAI
{
    public struct HelpBoxData
    {
        public string Message;
        public MessageType MessageType;
        public Action OnClick;
        public int FontSize;
    }

    public class CustomHelpBox : VisualElement
    {
        public CustomHelpBox(DAInspectorUITK uitk, HelpBoxData data)
        {
            var colorScheme = uitk.ColorScheme;
            var originalColor = colorScheme.GROUP;

            var hoverColor = EditorGUIUtility.isProSkin
                ? new StyleColor(UIHelpers.Lighten(originalColor, DAI_UitkConstants.HelpBoxHoverPct))
                : new StyleColor(UIHelpers.Darken(originalColor, DAI_UitkConstants.HelpBoxHoverPct));

            var activeColor = EditorGUIUtility.isProSkin
                ? new StyleColor(UIHelpers.Lighten(originalColor, DAI_UitkConstants.HelpBoxActivePct))
                : new StyleColor(UIHelpers.Darken(originalColor, DAI_UitkConstants.HelpBoxActivePct));

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.backgroundColor = originalColor;
            style.position = Position.Relative;

            UIHelpers.SetDefaultRadius(this);
            UIHelpers.SetDefaultPadding(this);
            UIHelpers.SetBorderWidth(this, DAI_UitkConstants.BorderWidth);
            UIHelpers.SetBorderColor(this, uitk.ColorScheme.OUTLINE);

            style.transitionProperty = new List<StylePropertyName> { "background-color" };
            style.transitionDuration = new List<TimeValue> { new TimeValue(DAI_UitkConstants.TransitionDuration, TimeUnit.Second) };
            style.transitionTimingFunction = new List<EasingFunction> { EasingMode.Ease };

            RegisterCallback<PointerEnterEvent>(evt => style.backgroundColor = hoverColor);
            RegisterCallback<PointerLeaveEvent>(evt => style.backgroundColor = originalColor);
            RegisterCallback<PointerDownEvent>(evt => style.backgroundColor = activeColor);
            RegisterCallback<PointerUpEvent>(evt => style.backgroundColor = hoverColor);

            var icon = new Image
            {
                image = GetIconForMessageType(data.MessageType),
                scaleMode = ScaleMode.ScaleToFit
            };

            icon.style.width = DAI_UitkConstants.IconSizeLarge;
            icon.style.height = DAI_UitkConstants.IconSizeLarge;
            icon.style.marginRight = DAI_UitkConstants.SpacingM;
            icon.style.flexShrink = 0;

            var label = new Label(data.Message);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = data.FontSize > 0 ? data.FontSize : DAI_UitkConstants.FontSizeNormal;
            label.style.color = colorScheme.TEXT;
            label.style.flexShrink = 1;
            label.enableRichText = true;

            const float closeButtonSize = 16f; // DAI_UitkConstants.CloseButtonSize
            const float closeButtonInset = 4f; // DAI_UitkConstants.CloseButtonInset
            style.paddingRight = DAI_UitkConstants.MarginPadding + closeButtonSize + closeButtonInset;

            Add(icon);
            Add(label);

            var closeButton = new Button(() => style.display = DisplayStyle.None)
            {
                text = "x",
                tooltip = SharedLocKey.text_close.Localize(),
                style =
                {
                    position = Position.Absolute,
                    right = closeButtonInset,
                    top = closeButtonInset,
                    width = closeButtonSize,
                    height = closeButtonSize,
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 0,
                    paddingBottom = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    backgroundColor = new StyleColor(uitk.ColorScheme.BUTTON),
                    color = new StyleColor(uitk.ColorScheme.TEXT_SECOND)
                }
            };

            UIHelpers.SetRadius(closeButton, DAI_UitkConstants.CornerRadiusSmall);
            UIHelpers.SetBorderWidth(closeButton, DAI_UitkConstants.BorderWidth);
            UIHelpers.SetBorderColor(closeButton, uitk.ColorScheme.OUTLINE);
            closeButton.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            closeButton.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
            closeButton.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            Add(closeButton);

            if (data.OnClick != null)
            {
                this.AddManipulator(new Clickable(data.OnClick));
            }
        }

        private Texture2D GetIconForMessageType(MessageType type)
        {
            string iconName;
            switch (type)
            {
                case MessageType.Info:
                    iconName = "console.infoicon";
                    break;
                case MessageType.Warning:
                    iconName = "console.warnicon";
                    break;
                case MessageType.Error:
                    iconName = "console.erroricon";
                    break;
                default:
                    iconName = "console.infoicon";
                    break;
            }

            return EditorGUIUtility.IconContent(iconName).image as Texture2D;
        }
    }
}
