using DA_Assets.DAI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DA_Assets.FCU
{
    internal partial class TextFontsTab : MonoBehaviourLinkerEditor<FcuSettingsWindow, FigmaConverterUnity>
    {
        private void DrawUitkTextSettings(VisualElement parent)
        {
            VisualElement panel = uitk.CreateSectionPanel();
            parent.Add(panel);

            Label header = new Label(FcuLocKey.label_uitk_text_settings.Localize());
            header.tooltip = FcuLocKey.tooltip_uitk_text_settings.Localize();
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.AddSectionResetMenu(() =>
            {
                var d = FcuDefaults.UitkTextSettings;
                var s = monoBeh.Settings.UitkTextSettings;
                s.WhiteSpace = d.WhiteSpace;
                s.TextOverflow = d.TextOverflow;
                s.AutoSize = d.AutoSize;
                s.Focusable = d.Focusable;
                s.EnableRichText = d.EnableRichText;
                s.EmojiFallbackSupport = d.EmojiFallbackSupport;
                s.ParseEscapeSequences = d.ParseEscapeSequences;
                s.Selectable = d.Selectable;
                s.DoubleClickSelectsWord = d.DoubleClickSelectsWord;
                s.TripleClickSelectsLine = d.TripleClickSelectsLine;
                s.DisplayTooltipWhenElided = d.DisplayTooltipWhenElided;
                scriptableObject.RefreshTabs();
            });
            panel.Add(header);
            panel.Add(uitk.ItemSeparator());

            var settings = monoBeh.Settings.UitkTextSettings;

            var whiteSpaceField = uitk.EnumField(FcuLocKey.label_white_space.Localize(), settings.WhiteSpace);
            whiteSpaceField.tooltip = FcuLocKey.tooltip_white_space.Localize();
            whiteSpaceField.RegisterValueChangedCallback(evt => settings.WhiteSpace = (WhiteSpace)evt.newValue);
            whiteSpaceField.AddResetMenu(settings, FcuDefaults.UitkTextSettings, s => s.WhiteSpace, (s, v) => s.WhiteSpace = v);
            panel.Add(whiteSpaceField);
            panel.Add(uitk.ItemSeparator());

            var textOverflowField = uitk.EnumField(FcuLocKey.label_text_overflow.Localize(), settings.TextOverflow);
            textOverflowField.tooltip = FcuLocKey.tooltip_text_overflow.Localize();
            textOverflowField.RegisterValueChangedCallback(evt => settings.TextOverflow = (TextOverflow)evt.newValue);
            textOverflowField.AddResetMenu(settings, FcuDefaults.UitkTextSettings, s => s.TextOverflow, (s, v) => s.TextOverflow = v);
            panel.Add(textOverflowField);
            panel.Add(uitk.ItemSeparator());

#if UNITY_2022_3_OR_NEWER
            var languageDirectionField = uitk.EnumField(FcuLocKey.label_language_direction.Localize(), settings.LanguageDirection);
            languageDirectionField.tooltip = FcuLocKey.tooltip_language_direction.Localize();
            languageDirectionField.RegisterValueChangedCallback(evt => settings.LanguageDirection = (LanguageDirection)evt.newValue);
            languageDirectionField.AddResetMenu(settings, FcuDefaults.UitkTextSettings, s => s.LanguageDirection, (s, v) => s.LanguageDirection = v);
            panel.Add(languageDirectionField);
            panel.Add(uitk.ItemSeparator());
#endif
            var autoSizeToggle = uitk.Toggle(FcuLocKey.label_auto_size.Localize());
            autoSizeToggle.tooltip = FcuLocKey.tooltip_auto_size.Localize();
            autoSizeToggle.value = settings.AutoSize;
            autoSizeToggle.RegisterValueChangedCallback(evt => settings.AutoSize = evt.newValue);
            autoSizeToggle.AddResetMenu(settings, FcuDefaults.UitkTextSettings, s => s.AutoSize, (s, v) => s.AutoSize = v);
            panel.Add(autoSizeToggle);
            panel.Add(uitk.ItemSeparator());

            var focusableToggle = uitk.Toggle(FcuLocKey.label_focusable.Localize());
            focusableToggle.tooltip = FcuLocKey.tooltip_focusable.Localize();
            focusableToggle.value = settings.Focusable;
            focusableToggle.RegisterValueChangedCallback(evt => settings.Focusable = evt.newValue);
            focusableToggle.AddResetMenu(settings, FcuDefaults.UitkTextSettings, s => s.Focusable, (s, v) => s.Focusable = v);
            panel.Add(focusableToggle);
            panel.Add(uitk.ItemSeparator());

            var richTextToggle = uitk.Toggle(FcuLocKey.label_rich_text.Localize());
            richTextToggle.tooltip = FcuLocKey.tooltip_rich_text.Localize();
            richTextToggle.value = settings.EnableRichText;
            richTextToggle.RegisterValueChangedCallback(evt => settings.EnableRichText = evt.newValue);
            richTextToggle.AddResetMenu(settings, FcuDefaults.UitkTextSettings, s => s.EnableRichText, (s, v) => s.EnableRichText = v);
            panel.Add(richTextToggle);
            panel.Add(uitk.ItemSeparator());

            var emojiFallbackToggle = uitk.Toggle(FcuLocKey.label_emoji_fallback_support.Localize());
            emojiFallbackToggle.tooltip = FcuLocKey.tooltip_emoji_fallback_support.Localize();
            emojiFallbackToggle.value = settings.EmojiFallbackSupport;
            emojiFallbackToggle.RegisterValueChangedCallback(evt => settings.EmojiFallbackSupport = evt.newValue);
            emojiFallbackToggle.AddResetMenu(settings, FcuDefaults.UitkTextSettings, s => s.EmojiFallbackSupport, (s, v) => s.EmojiFallbackSupport = v);
            panel.Add(emojiFallbackToggle);
            panel.Add(uitk.ItemSeparator());

            var parseEscapeToggle = uitk.Toggle(FcuLocKey.label_parse_escape_characters.Localize());
            parseEscapeToggle.tooltip = FcuLocKey.tooltip_parse_escape_characters.Localize();
            parseEscapeToggle.value = settings.ParseEscapeSequences;
            parseEscapeToggle.RegisterValueChangedCallback(evt => settings.ParseEscapeSequences = evt.newValue);
            parseEscapeToggle.AddResetMenu(settings, FcuDefaults.UitkTextSettings, s => s.ParseEscapeSequences, (s, v) => s.ParseEscapeSequences = v);
            panel.Add(parseEscapeToggle);
            panel.Add(uitk.ItemSeparator());

            var selectableToggle = uitk.Toggle(FcuLocKey.label_selectable.Localize());
            selectableToggle.tooltip = FcuLocKey.tooltip_selectable.Localize();
            selectableToggle.value = settings.Selectable;
            selectableToggle.RegisterValueChangedCallback(evt => settings.Selectable = evt.newValue);
            selectableToggle.AddResetMenu(settings, FcuDefaults.UitkTextSettings, s => s.Selectable, (s, v) => s.Selectable = v);
            panel.Add(selectableToggle);
            panel.Add(uitk.ItemSeparator());

            var doubleClickToggle = uitk.Toggle(FcuLocKey.label_double_click_selects_word.Localize());
            doubleClickToggle.tooltip = FcuLocKey.tooltip_double_click_selects_word.Localize();
            doubleClickToggle.value = settings.DoubleClickSelectsWord;
            doubleClickToggle.RegisterValueChangedCallback(evt => settings.DoubleClickSelectsWord = evt.newValue);
            doubleClickToggle.AddResetMenu(settings, FcuDefaults.UitkTextSettings, s => s.DoubleClickSelectsWord, (s, v) => s.DoubleClickSelectsWord = v);
            panel.Add(doubleClickToggle);
            panel.Add(uitk.ItemSeparator());

            var tripleClickToggle = uitk.Toggle(FcuLocKey.label_triple_click_selects_line.Localize());
            tripleClickToggle.tooltip = FcuLocKey.tooltip_triple_click_selects_line.Localize();
            tripleClickToggle.value = settings.TripleClickSelectsLine;
            tripleClickToggle.RegisterValueChangedCallback(evt => settings.TripleClickSelectsLine = evt.newValue);
            tripleClickToggle.AddResetMenu(settings, FcuDefaults.UitkTextSettings, s => s.TripleClickSelectsLine, (s, v) => s.TripleClickSelectsLine = v);
            panel.Add(tripleClickToggle);
            panel.Add(uitk.ItemSeparator());

            var tooltipWhenElidedToggle = uitk.Toggle(FcuLocKey.label_display_tooltip_when_elided.Localize());
            tooltipWhenElidedToggle.tooltip = FcuLocKey.tooltip_display_tooltip_when_elided.Localize();
            tooltipWhenElidedToggle.value = settings.DisplayTooltipWhenElided;
            tooltipWhenElidedToggle.RegisterValueChangedCallback(evt => settings.DisplayTooltipWhenElided = evt.newValue);
            tooltipWhenElidedToggle.AddResetMenu(settings, FcuDefaults.UitkTextSettings, s => s.DisplayTooltipWhenElided, (s, v) => s.DisplayTooltipWhenElided = v);
            panel.Add(tooltipWhenElidedToggle);
        }
    }
}