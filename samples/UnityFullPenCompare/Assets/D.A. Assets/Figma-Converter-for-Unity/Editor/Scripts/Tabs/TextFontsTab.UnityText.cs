using DA_Assets.DAI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DA_Assets.FCU
{
    internal partial class TextFontsTab : MonoBehaviourLinkerEditor<FcuSettingsWindow, FigmaConverterUnity>
    {
        public void DrawDefaultTextSettings(VisualElement parent)
        {
            VisualElement panel = uitk.CreateSectionPanel();
            parent.Add(panel);

            Label header = new Label(FcuLocKey.label_unity_text_settings.Localize());
            header.tooltip = FcuLocKey.tooltip_unity_text_settings.Localize();
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.AddSectionResetMenu(() =>
            {
                var d = FcuDefaults.UnityTextSettings;
                var s = monoBeh.Settings.UnityTextSettings;
                s.BestFit = d.BestFit;
                s.FontLineSpacing = d.FontLineSpacing;
                s.HorizontalWrapMode = d.HorizontalWrapMode;
                s.VerticalWrapMode = d.VerticalWrapMode;
                scriptableObject.RefreshTabs();
            });
            panel.Add(header);
            panel.Add(uitk.ItemSeparator());

            var settings = monoBeh.Settings.UnityTextSettings;

            var bestFitToggle = uitk.Toggle(FcuLocKey.label_best_fit.Localize());
            bestFitToggle.tooltip = FcuLocKey.tooltip_best_fit.Localize();
            bestFitToggle.value = settings.BestFit;
            bestFitToggle.RegisterValueChangedCallback(evt =>
            {
                settings.BestFit = evt.newValue;
                if (settings.VerticalWrapMode == VerticalWrapMode.Overflow)
                {
                    settings.BestFit = false;
                    bestFitToggle.SetValueWithoutNotify(false);
                }
            });
            bestFitToggle.AddResetMenu(settings, FcuDefaults.UnityTextSettings, s => s.BestFit, (s, v) => s.BestFit = v);
            panel.Add(bestFitToggle);
            panel.Add(uitk.ItemSeparator());

            var lineSpacingField = uitk.FloatField(FcuLocKey.label_line_spacing.Localize());
            lineSpacingField.tooltip = FcuLocKey.tooltip_line_spacing.Localize();
            lineSpacingField.value = settings.FontLineSpacing;
            lineSpacingField.RegisterValueChangedCallback(evt => settings.FontLineSpacing = evt.newValue);
            lineSpacingField.AddResetMenu(settings, FcuDefaults.UnityTextSettings, s => s.FontLineSpacing, (s, v) => s.FontLineSpacing = v);
            panel.Add(lineSpacingField);
            panel.Add(uitk.ItemSeparator());

            var hOverflowField = uitk.EnumField(FcuLocKey.label_horizontal_overflow.Localize(), settings.HorizontalWrapMode);
            hOverflowField.tooltip = FcuLocKey.tooltip_horizontal_overflow.Localize();
            hOverflowField.RegisterValueChangedCallback(evt => settings.HorizontalWrapMode = (HorizontalWrapMode)evt.newValue);
            hOverflowField.AddResetMenu(settings, FcuDefaults.UnityTextSettings, s => s.HorizontalWrapMode, (s, v) => s.HorizontalWrapMode = v);
            panel.Add(hOverflowField);
            panel.Add(uitk.ItemSeparator());

            var vOverflowField = uitk.EnumField(FcuLocKey.label_vertical_overflow.Localize(), settings.VerticalWrapMode);
            vOverflowField.tooltip = FcuLocKey.tooltip_vertical_overflow.Localize();
            vOverflowField.RegisterValueChangedCallback(evt =>
            {
                settings.VerticalWrapMode = (VerticalWrapMode)evt.newValue;
                if (settings.VerticalWrapMode == VerticalWrapMode.Overflow)
                {
                    settings.BestFit = false;
                    bestFitToggle.SetValueWithoutNotify(false);
                }
            });
            vOverflowField.AddResetMenu(settings, FcuDefaults.UnityTextSettings, s => s.VerticalWrapMode, (s, v) => s.VerticalWrapMode = v);
            panel.Add(vOverflowField);
        }
    }
}