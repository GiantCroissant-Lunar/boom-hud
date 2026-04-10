using DA_Assets.DAI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DA_Assets.FCU
{
    internal partial class TextFontsTab : MonoBehaviourLinkerEditor<FcuSettingsWindow, FigmaConverterUnity>
    {
        private void DrawUniTextSettings(VisualElement parent)
        {
            VisualElement panel = uitk.CreateSectionPanel();
            parent.Add(panel);

            Label header = new Label(FcuLocKey.label_unitext_settings.Localize());
            header.tooltip = FcuLocKey.tooltip_unitext_settings.Localize();
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(header);
            panel.Add(uitk.ItemSeparator());

            var settings = monoBeh.Settings.UniTextSettings;

            var autoSizeToggle = uitk.Toggle(FcuLocKey.label_auto_size.Localize());
            autoSizeToggle.tooltip = FcuLocKey.tooltip_auto_size.Localize();
            autoSizeToggle.value = settings.AutoSize;
            autoSizeToggle.RegisterValueChangedCallback(evt => settings.AutoSize = evt.newValue);
            panel.Add(autoSizeToggle);
            panel.Add(uitk.ItemSeparator());

            var wordWrapToggle = uitk.Toggle(FcuLocKey.label_wrapping.Localize());
            wordWrapToggle.tooltip = FcuLocKey.tooltip_unitext_word_wrap.Localize();
            wordWrapToggle.value = settings.WordWrap;
            wordWrapToggle.RegisterValueChangedCallback(evt => settings.WordWrap = evt.newValue);
            panel.Add(wordWrapToggle);
            panel.Add(uitk.ItemSeparator());

            var raycastTargetToggle = uitk.Toggle(FcuLocKey.label_raycast_target.Localize());
            raycastTargetToggle.tooltip = FcuLocKey.tooltip_raycast_target.Localize();
            raycastTargetToggle.value = settings.RaycastTarget;
            raycastTargetToggle.RegisterValueChangedCallback(evt => settings.RaycastTarget = evt.newValue);
            panel.Add(raycastTargetToggle);
        }
    }
}