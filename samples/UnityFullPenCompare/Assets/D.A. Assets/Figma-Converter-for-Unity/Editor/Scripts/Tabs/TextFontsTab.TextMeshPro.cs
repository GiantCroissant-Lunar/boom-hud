using DA_Assets.DAI;
using DA_Assets.FCU.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DA_Assets.FCU
{
    internal partial class TextFontsTab : MonoBehaviourLinkerEditor<FcuSettingsWindow, FigmaConverterUnity>
    {
        private void DrawTextMeshSettingsSection(VisualElement parent)
        {
#if TextMeshPro
            VisualElement panel = uitk.CreateSectionPanel();
            parent.Add(panel);

            Label header = new Label(FcuLocKey.label_textmeshpro_settings.Localize());
            header.tooltip = FcuLocKey.tooltip_textmeshpro_settings.Localize();
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.AddSectionResetMenu(() =>
            {
                var d = FcuDefaults.TextMeshSettings;
                var s = monoBeh.Settings.TextMeshSettings;
                s.AutoSize = d.AutoSize;
                s.OverrideTags = d.OverrideTags;
                s.Wrapping = d.Wrapping;
                s.RichText = d.RichText;
                s.RaycastTarget = d.RaycastTarget;
                s.ParseEscapeCharacters = d.ParseEscapeCharacters;
                s.VisibleDescender = d.VisibleDescender;
                s.Kerning = d.Kerning;
                s.ExtraPadding = d.ExtraPadding;
                s.Overflow = d.Overflow;
                s.HorizontalMapping = d.HorizontalMapping;
                s.VerticalMapping = d.VerticalMapping;
                s.GeometrySorting = d.GeometrySorting;
                scriptableObject.RefreshTabs();
            });
            panel.Add(header);
            panel.Add(uitk.ItemSeparator());

            var settings = monoBeh.Settings.TextMeshSettings;

            var autoSizeToggle = uitk.Toggle(FcuLocKey.label_auto_size.Localize());
            autoSizeToggle.tooltip = FcuLocKey.tooltip_auto_size.Localize();
            autoSizeToggle.value = settings.AutoSize;
            autoSizeToggle.RegisterValueChangedCallback(evt => settings.AutoSize = evt.newValue);
            autoSizeToggle.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.AutoSize, (s, v) => s.AutoSize = v);
            panel.Add(autoSizeToggle);
            panel.Add(uitk.ItemSeparator());

            var overrideTagsToggle = uitk.Toggle(FcuLocKey.label_override_tags.Localize());
            overrideTagsToggle.tooltip = FcuLocKey.tooltip_override_tags.Localize();
            overrideTagsToggle.value = settings.OverrideTags;
            overrideTagsToggle.RegisterValueChangedCallback(evt => settings.OverrideTags = evt.newValue);
            overrideTagsToggle.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.OverrideTags, (s, v) => s.OverrideTags = v);
            panel.Add(overrideTagsToggle);
            panel.Add(uitk.ItemSeparator());

            var wrappingToggle = uitk.Toggle(FcuLocKey.label_wrapping.Localize());
            wrappingToggle.tooltip = FcuLocKey.tooltip_wrapping.Localize();
            wrappingToggle.value = settings.Wrapping;
            wrappingToggle.RegisterValueChangedCallback(evt => settings.Wrapping = evt.newValue);
            wrappingToggle.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.Wrapping, (s, v) => s.Wrapping = v);
            panel.Add(wrappingToggle);
            panel.Add(uitk.ItemSeparator());

            if (monoBeh.IsNova() || monoBeh.IsDebug())
            {
                var orthoModeToggle = uitk.Toggle(FcuLocKey.label_orthographic_mode.Localize());
                orthoModeToggle.tooltip = FcuLocKey.tooltip_orthographic_mode.Localize();
                orthoModeToggle.value = settings.OrthographicMode;
                orthoModeToggle.RegisterValueChangedCallback(evt => settings.OrthographicMode = evt.newValue);
                orthoModeToggle.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.OrthographicMode, (s, v) => s.OrthographicMode = v);
                panel.Add(orthoModeToggle);
                panel.Add(uitk.ItemSeparator());
            }

            var richTextToggle = uitk.Toggle(FcuLocKey.label_rich_text.Localize());
            richTextToggle.tooltip = FcuLocKey.tooltip_rich_text.Localize();
            richTextToggle.value = settings.RichText;
            richTextToggle.RegisterValueChangedCallback(evt => settings.RichText = evt.newValue);
            richTextToggle.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.RichText, (s, v) => s.RichText = v);
            panel.Add(richTextToggle);
            panel.Add(uitk.ItemSeparator());

            var raycastTargetToggle = uitk.Toggle(FcuLocKey.label_raycast_target.Localize());
            raycastTargetToggle.tooltip = FcuLocKey.tooltip_raycast_target.Localize();
            raycastTargetToggle.value = settings.RaycastTarget;
            raycastTargetToggle.RegisterValueChangedCallback(evt => settings.RaycastTarget = evt.newValue);
            raycastTargetToggle.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.RaycastTarget, (s, v) => s.RaycastTarget = v);
            panel.Add(raycastTargetToggle);
            panel.Add(uitk.ItemSeparator());

            var parseEscCharsToggle = uitk.Toggle(FcuLocKey.label_parse_escape_characters.Localize());
            parseEscCharsToggle.tooltip = FcuLocKey.tooltip_parse_escape_characters.Localize();
            parseEscCharsToggle.value = settings.ParseEscapeCharacters;
            parseEscCharsToggle.RegisterValueChangedCallback(evt => settings.ParseEscapeCharacters = evt.newValue);
            parseEscCharsToggle.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.ParseEscapeCharacters, (s, v) => s.ParseEscapeCharacters = v);
            panel.Add(parseEscCharsToggle);
            panel.Add(uitk.ItemSeparator());

            var visibleDescenderToggle = uitk.Toggle(FcuLocKey.label_visible_descender.Localize());
            visibleDescenderToggle.tooltip = FcuLocKey.tooltip_visible_descender.Localize();
            visibleDescenderToggle.value = settings.VisibleDescender;
            visibleDescenderToggle.RegisterValueChangedCallback(evt => settings.VisibleDescender = evt.newValue);
            visibleDescenderToggle.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.VisibleDescender, (s, v) => s.VisibleDescender = v);
            panel.Add(visibleDescenderToggle);
            panel.Add(uitk.ItemSeparator());

            var kerningToggle = uitk.Toggle(FcuLocKey.label_kerning.Localize());
            kerningToggle.tooltip = FcuLocKey.tooltip_kerning.Localize();
            kerningToggle.value = settings.Kerning;
            kerningToggle.RegisterValueChangedCallback(evt => settings.Kerning = evt.newValue);
            kerningToggle.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.Kerning, (s, v) => s.Kerning = v);
            panel.Add(kerningToggle);
            panel.Add(uitk.ItemSeparator());

            var extraPaddingToggle = uitk.Toggle(FcuLocKey.label_extra_padding.Localize());
            extraPaddingToggle.tooltip = FcuLocKey.tooltip_extra_padding.Localize();
            extraPaddingToggle.value = settings.ExtraPadding;
            extraPaddingToggle.RegisterValueChangedCallback(evt => settings.ExtraPadding = evt.newValue);
            extraPaddingToggle.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.ExtraPadding, (s, v) => s.ExtraPadding = v);
            panel.Add(extraPaddingToggle);
            panel.Add(uitk.ItemSeparator());

            var overflowField = uitk.EnumField(FcuLocKey.label_overflow.Localize(), settings.Overflow);
            overflowField.tooltip = FcuLocKey.tooltip_overflow.Localize();
            overflowField.value = settings.Overflow;
            overflowField.RegisterValueChangedCallback(evt => settings.Overflow = (TMPro.TextOverflowModes)evt.newValue);
            overflowField.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.Overflow, (s, v) => s.Overflow = v);
            panel.Add(overflowField);
            panel.Add(uitk.ItemSeparator());

            var hMappingField = uitk.EnumField(FcuLocKey.label_horizontal_mapping.Localize(), settings.HorizontalMapping);
            hMappingField.tooltip = FcuLocKey.tooltip_horizontal_mapping.Localize();
            hMappingField.value = settings.HorizontalMapping;
            hMappingField.RegisterValueChangedCallback(evt => settings.HorizontalMapping = (TMPro.TextureMappingOptions)evt.newValue);
            hMappingField.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.HorizontalMapping, (s, v) => s.HorizontalMapping = v);
            panel.Add(hMappingField);
            panel.Add(uitk.ItemSeparator());

            var vMappingField = uitk.EnumField(FcuLocKey.label_vertical_mapping.Localize(), settings.VerticalMapping);
            vMappingField.tooltip = FcuLocKey.tooltip_vertical_mapping.Localize();
            vMappingField.value = settings.VerticalMapping;
            vMappingField.RegisterValueChangedCallback(evt => settings.VerticalMapping = (TMPro.TextureMappingOptions)evt.newValue);
            vMappingField.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.VerticalMapping, (s, v) => s.VerticalMapping = v);
            panel.Add(vMappingField);
            panel.Add(uitk.ItemSeparator());

            var geoSortingField = uitk.EnumField(FcuLocKey.label_geometry_sorting.Localize(), settings.GeometrySorting);
            geoSortingField.tooltip = FcuLocKey.tooltip_geometry_sorting.Localize();
            geoSortingField.value = settings.GeometrySorting;
            geoSortingField.RegisterValueChangedCallback(evt => settings.GeometrySorting = (TMPro.VertexSortingOrder)evt.newValue);
            geoSortingField.AddResetMenu(settings, FcuDefaults.TextMeshSettings, s => s.GeometrySorting, (s, v) => s.GeometrySorting = v);
            panel.Add(geoSortingField);
            panel.Add(uitk.ItemSeparator());

            List<string> shaderNames = ShaderUtil.GetAllShaderInfo().Select(info => info.name).ToList();

            string currentShaderName = settings.Shader != null
                ? settings.Shader.name
                : FcuConfig.DefaultTmpShaderName;

            int initialIndex = shaderNames.IndexOf(currentShaderName);
            if (initialIndex < 0) initialIndex = shaderNames.IndexOf(FcuConfig.DefaultTmpShaderName);
            if (initialIndex < 0) initialIndex = 0;

            var shaderDropdown = new PopupField<string>(FcuLocKey.label_shader.Localize(), shaderNames, initialIndex);
            shaderDropdown.tooltip = FcuLocKey.tooltip_shader.Localize();
            shaderDropdown.RegisterValueChangedCallback(evt => settings.Shader = Shader.Find(evt.newValue));
            shaderDropdown.AddPopupResetMenu(
                () => settings.Shader != null ? settings.Shader.name : FcuConfig.DefaultTmpShaderName,
                FcuConfig.DefaultTmpShaderName,
                v => { settings.Shader = Shader.Find(v); shaderDropdown.SetValueWithoutNotify(v); });
            panel.Add(shaderDropdown);

#if RTLTMP_EXISTS
            if (monoBeh.UsingRTLTextMeshPro() || monoBeh.IsDebug())
            {
                panel.Add(uitk.ItemSeparator());

                var farsiToggle = uitk.Toggle(FcuLocKey.label_farsi.Localize());
                farsiToggle.tooltip = FcuLocKey.tooltip_farsi.Localize();
                farsiToggle.value = settings.Farsi;
                farsiToggle.RegisterValueChangedCallback(evt => settings.Farsi = evt.newValue);
                panel.Add(farsiToggle);
                panel.Add(uitk.ItemSeparator());

                var forceFixToggle = uitk.Toggle(FcuLocKey.label_force_fix.Localize());
                forceFixToggle.tooltip = FcuLocKey.tooltip_force_fix.Localize();
                forceFixToggle.value = settings.ForceFix;
                forceFixToggle.RegisterValueChangedCallback(evt => settings.ForceFix = evt.newValue);
                panel.Add(forceFixToggle);
                panel.Add(uitk.ItemSeparator());

                var preserveNumsToggle = uitk.Toggle(FcuLocKey.label_preserve_numbers.Localize());
                preserveNumsToggle.tooltip = FcuLocKey.tooltip_preserve_numbers.Localize();
                preserveNumsToggle.value = settings.PreserveNumbers;
                preserveNumsToggle.RegisterValueChangedCallback(evt => settings.PreserveNumbers = evt.newValue);
                panel.Add(preserveNumsToggle);
                panel.Add(uitk.ItemSeparator());

                var fixTagsToggle = uitk.Toggle(FcuLocKey.label_fix_tags.Localize());
                fixTagsToggle.tooltip = FcuLocKey.tooltip_fix_tags.Localize();
                fixTagsToggle.value = settings.FixTags;
                fixTagsToggle.RegisterValueChangedCallback(evt => settings.FixTags = evt.newValue);
                panel.Add(fixTagsToggle);
            }
#endif
#endif
        }

        private void DrawFontGenerationSettings(VisualElement parent)
        {
#if TextMeshPro
            VisualElement panel = uitk.CreateSectionPanel();
            parent.Add(panel);

            Label header = new Label(FcuLocKey.label_asset_creator_settings.Localize());
            header.tooltip = FcuLocKey.tooltip_asset_creator_settings.Localize();
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(header);
            panel.Add(uitk.ItemSeparator());

            var tmpDownloader = monoBeh.FontDownloader.TmpDownloader;

            var multiAtlasToggle = uitk.Toggle(FcuLocKey.label_enable_multi_atlas_support.Localize());
            multiAtlasToggle.tooltip = FcuLocKey.tooltip_enable_multi_atlas_support.Localize();
            multiAtlasToggle.value = tmpDownloader.EnableMultiAtlasSupport;
            multiAtlasToggle.RegisterValueChangedCallback(evt => tmpDownloader.EnableMultiAtlasSupport = evt.newValue);
            panel.Add(multiAtlasToggle);
            panel.Add(uitk.ItemSeparator());

            var atlasConfigProp = scriptableObject.SerializedObject
                .FindProperty(nameof(FigmaConverterUnity.FontDownloader))
                ?.FindPropertyRelative(nameof(FontDownloader.TmpDownloader))
                ?.FindPropertyRelative("atlasConfig");

            var atlasConfigField = new PropertyField(atlasConfigProp, FcuLocKey.label_atlas_config.Localize());
            atlasConfigField.Bind(scriptableObject.SerializedObject);

            panel.Add(atlasConfigField);
            panel.Add(uitk.ItemSeparator());
            panel.Add(uitk.Space5());

            var downloadButton = uitk.Button(FcuLocKey.label_download_fonts_from_project.Localize(monoBeh.Settings.TextFontsSettings.TextComponent), () =>
            {
                monoBeh.FontDownloader.DownloadFontsCts?.Cancel();
                monoBeh.FontDownloader.DownloadFontsCts?.Dispose();
                monoBeh.FontDownloader.DownloadFontsCts = new CancellationTokenSource();
                _ = monoBeh.FontDownloader.DownloadAllProjectFonts(monoBeh.FontDownloader.DownloadFontsCts.Token);
            });
            downloadButton.tooltip = FcuLocKey.tooltip_download_fonts_from_project.Localize(monoBeh.Settings.TextFontsSettings.TextComponent);
            panel.Add(downloadButton);
#endif
        }

    }
}