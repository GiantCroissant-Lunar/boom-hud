using DA_Assets.DAI;
using System.Threading;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DA_Assets.FCU
{
    internal partial class TextFontsTab : MonoBehaviourLinkerEditor<FcuSettingsWindow, FigmaConverterUnity>
    {
        private void DrawPathSettings(VisualElement parent)
        {
            SerializedProperty fontLoaderProp = scriptableObject.SerializedObject.FindProperty(nameof(FigmaConverterUnity.FontLoader));

            if (fontLoaderProp == null)
            {
                VisualElement errorPanel = uitk.CreateSectionPanel();
                errorPanel.Add(uitk.HelpBox(new HelpBoxData
                {
                    Message = FcuLocKey.textfonts_error_fontloader_not_found.Localize(nameof(FigmaConverterUnity.FontLoader)),
                    MessageType = MessageType.Error
                }));
                parent.Add(errorPanel);
                return;
            }

            DrawTtfFontSection(parent, fontLoaderProp);
            parent.Add(uitk.Space10());

            DrawUitkFontSection(parent, fontLoaderProp);

#if TextMeshPro
            parent.Add(uitk.Space10());
            DrawTmpFontSection(parent, fontLoaderProp);
#endif

#if UNITEXT
            parent.Add(uitk.Space10());
            DrawUniTextFontSection(parent, fontLoaderProp);
#endif
        }

        private void DrawTtfFontSection(VisualElement parent, SerializedProperty fontLoaderProp)
        {
            VisualElement panel = CreateFontPanel(parent, "TTF FONTS", FcuLocKey.tooltip_ttf_path.Localize());

            var ttfPathContainer = uitk.CreateFolderInput(
                label: FcuLocKey.label_ttf_path.Localize(),
                tooltip: FcuLocKey.tooltip_ttf_path.Localize(),
                initialValue: monoBeh.FontLoader.TtfFontsPath,
                onPathChanged: newValue => monoBeh.FontLoader.TtfFontsPath = newValue,
                onButtonClick: () => EditorUtility.OpenFolderPanel(
                    FcuLocKey.label_select_fonts_folder.Localize(),
                    monoBeh.FontLoader.TtfFontsPath,
                    ""),
                buttonTooltip: FcuLocKey.tooltip_select_fonts_folder.Localize());
            ttfPathContainer.AddFolderResetMenu(
                () => monoBeh.FontLoader.TtfFontsPath,
                "",
                v => monoBeh.FontLoader.TtfFontsPath = v);

            panel.Add(ttfPathContainer);
            panel.Add(uitk.ItemSeparator());

            var addTtfButton = uitk.Button(FcuLocKey.label_add_ttf_fonts_from_folder.Localize(), () =>
            {
                monoBeh.FontDownloader.AddTtfFontsCts?.Cancel();
                monoBeh.FontDownloader.AddTtfFontsCts?.Dispose();
                monoBeh.FontDownloader.AddTtfFontsCts = new CancellationTokenSource();
                _ = monoBeh.FontLoader.AddToTtfFontsList(monoBeh.FontDownloader.AddTtfFontsCts.Token);
            });
            addTtfButton.tooltip = FcuLocKey.tooltip_add_ttf_fonts_from_folder.Localize();
            panel.Add(addTtfButton);
            panel.Add(uitk.ItemSeparator());

            panel.Add(CreateBoundPropertyField(fontLoaderProp, nameof(FontLoader.TtfFonts)));
        }

        private void DrawUitkFontSection(VisualElement parent, SerializedProperty fontLoaderProp)
        {
            VisualElement panel = CreateFontPanel(parent, "UITK FONTASSETS", "UI Toolkit TextCore FontAsset settings.");

            var uitkPathContainer = uitk.CreateFolderInput(
                label: "UITK FontAsset Path",
                tooltip: "Folder that stores UI Toolkit TextCore FontAsset assets.",
                initialValue: monoBeh.FontLoader.UitkFontAssetsPath,
                onPathChanged: newValue => monoBeh.FontLoader.UitkFontAssetsPath = newValue,
                onButtonClick: () => EditorUtility.OpenFolderPanel(
                    FcuLocKey.label_select_fonts_folder.Localize(),
                    monoBeh.FontLoader.UitkFontAssetsPath,
                    ""),
                buttonTooltip: FcuLocKey.tooltip_select_fonts_folder.Localize());
            uitkPathContainer.AddFolderResetMenu(
                () => monoBeh.FontLoader.UitkFontAssetsPath,
                "",
                v => monoBeh.FontLoader.UitkFontAssetsPath = v);

            panel.Add(uitkPathContainer);
            panel.Add(uitk.ItemSeparator());

            var buttonsRow = new VisualElement();
            buttonsRow.style.flexDirection = FlexDirection.Row;

            var addUitkButton = uitk.Button("Add UITK FontAssets from Folder", () =>
            {
                monoBeh.FontDownloader.AddUitkFontAssetsCts?.Cancel();
                monoBeh.FontDownloader.AddUitkFontAssetsCts?.Dispose();
                monoBeh.FontDownloader.AddUitkFontAssetsCts = new CancellationTokenSource();
                _ = monoBeh.FontLoader.AddToUitkFontAssetsList(monoBeh.FontDownloader.AddUitkFontAssetsCts.Token);
            });
            addUitkButton.tooltip = "Scan the configured folder and refresh the UITK FontAsset registry.";
            addUitkButton.style.flexGrow = 1;

            var createUitkButton = uitk.Button("Create UITK FontAssets from TTF", () =>
            {
                var cts = new CancellationTokenSource();
                _ = monoBeh.FontDownloader.UitkFontAssetCreator.CreateFromTtfFolder(cts.Token);
            });
            createUitkButton.tooltip = "Create TextCore FontAsset assets from the configured TTF folder.";
            createUitkButton.style.flexGrow = 1;

            buttonsRow.Add(addUitkButton);
            buttonsRow.Add(uitk.Space5());
            buttonsRow.Add(createUitkButton);
            panel.Add(buttonsRow);
            panel.Add(uitk.ItemSeparator());

            panel.Add(CreateBoundPropertyField(fontLoaderProp, nameof(FontLoader.UitkFontAssets)));
        }

#if TextMeshPro
        private void DrawTmpFontSection(VisualElement parent, SerializedProperty fontLoaderProp)
        {
            VisualElement panel = CreateFontPanel(parent, "TMP FONTS", FcuLocKey.tooltip_tmp_path.Localize());

            var tmpPathContainer = uitk.CreateFolderInput(
                label: FcuLocKey.label_tmp_path.Localize(),
                tooltip: FcuLocKey.tooltip_tmp_path.Localize(),
                initialValue: monoBeh.FontLoader.TmpFontsPath,
                onPathChanged: newValue => monoBeh.FontLoader.TmpFontsPath = newValue,
                onButtonClick: () => EditorUtility.OpenFolderPanel(
                    FcuLocKey.label_select_fonts_folder.Localize(),
                    monoBeh.FontLoader.TmpFontsPath,
                    ""),
                buttonTooltip: FcuLocKey.tooltip_select_fonts_folder.Localize());
            tmpPathContainer.AddFolderResetMenu(
                () => monoBeh.FontLoader.TmpFontsPath,
                "",
                v => monoBeh.FontLoader.TmpFontsPath = v);

            panel.Add(tmpPathContainer);
            panel.Add(uitk.ItemSeparator());

            var buttonsRow = new VisualElement();
            buttonsRow.style.flexDirection = FlexDirection.Row;

            var addTmpButton = uitk.Button(FcuLocKey.label_add_tmp_fonts_from_folder.Localize(), () =>
            {
                monoBeh.FontDownloader.AddTmpFontsCts?.Cancel();
                monoBeh.FontDownloader.AddTmpFontsCts?.Dispose();
                monoBeh.FontDownloader.AddTmpFontsCts = new CancellationTokenSource();
                _ = monoBeh.FontLoader.AddToTmpMeshFontsList(monoBeh.FontDownloader.AddTmpFontsCts.Token);
            });
            addTmpButton.tooltip = FcuLocKey.tooltip_add_fonts_from_folder.Localize();
            addTmpButton.style.flexGrow = 1;

            var createTmpButton = uitk.Button(FcuLocKey.label_create_tmp_from_ttf.Localize(), () =>
            {
                var cts = new CancellationTokenSource();
                _ = monoBeh.FontDownloader.TmpDownloader.CreateFromTtfFolder(cts.Token);
            });
            createTmpButton.tooltip = FcuLocKey.tooltip_create_tmp_from_ttf.Localize();
            createTmpButton.style.flexGrow = 1;

            buttonsRow.Add(addTmpButton);
            buttonsRow.Add(uitk.Space5());
            buttonsRow.Add(createTmpButton);
            panel.Add(buttonsRow);
            panel.Add(uitk.ItemSeparator());

            panel.Add(CreateBoundPropertyField(fontLoaderProp, nameof(FontLoader.TmpFonts)));
        }
#endif

#if UNITEXT
        private void DrawUniTextFontSection(VisualElement parent, SerializedProperty fontLoaderProp)
        {
            VisualElement panel = CreateFontPanel(parent, "UNITEXT", FcuLocKey.tooltip_unitext_fonts_path.Localize());

            var uniTextPathContainer = uitk.CreateFolderInput(
                label: FcuLocKey.label_unitext_fonts_path.Localize(),
                tooltip: FcuLocKey.tooltip_unitext_fonts_path.Localize(),
                initialValue: monoBeh.FontLoader.UniTextFontsPath,
                onPathChanged: newValue => monoBeh.FontLoader.UniTextFontsPath = newValue,
                onButtonClick: () => EditorUtility.OpenFolderPanel(
                    FcuLocKey.label_select_fonts_folder.Localize(),
                    monoBeh.FontLoader.UniTextFontsPath,
                    ""),
                buttonTooltip: FcuLocKey.tooltip_select_fonts_folder.Localize());
            uniTextPathContainer.AddFolderResetMenu(
                () => monoBeh.FontLoader.UniTextFontsPath,
                "",
                v => monoBeh.FontLoader.UniTextFontsPath = v);

            panel.Add(uniTextPathContainer);
            panel.Add(uitk.ItemSeparator());

            var buttonsRow = new VisualElement();
            buttonsRow.style.flexDirection = FlexDirection.Row;

            var addUniTextButton = uitk.Button(FcuLocKey.label_add_unitext_fonts_from_folder.Localize(), () =>
            {
                var cts = new CancellationTokenSource();
                _ = monoBeh.FontLoader.AddToUniTextFontsList(cts.Token);
            });
            addUniTextButton.tooltip = FcuLocKey.tooltip_add_unitext_fonts_from_folder.Localize();
            addUniTextButton.style.flexGrow = 1;

            var createUniTextButton = uitk.Button(FcuLocKey.label_create_unitext_from_ttf.Localize(), () =>
            {
                var cts = new CancellationTokenSource();
                _ = monoBeh.FontDownloader.UniTextFontCreator.CreateFromTtfFolder(cts.Token);
            });
            createUniTextButton.tooltip = FcuLocKey.tooltip_create_unitext_from_ttf.Localize();
            createUniTextButton.style.flexGrow = 1;

            buttonsRow.Add(addUniTextButton);
            buttonsRow.Add(uitk.Space5());
            buttonsRow.Add(createUniTextButton);
            panel.Add(buttonsRow);
            panel.Add(uitk.ItemSeparator());

            panel.Add(CreateBoundPropertyField(fontLoaderProp, nameof(FontLoader.UniTextFontStacks)));
        }
#endif

        private VisualElement CreateFontPanel(VisualElement parent, string title, string tooltip)
        {
            VisualElement panel = uitk.CreateSectionPanel();
            parent.Add(panel);

            Label header = new Label(title);
            header.tooltip = tooltip;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(header);
            panel.Add(uitk.ItemSeparator());

            return panel;
        }

        private PropertyField CreateBoundPropertyField(SerializedProperty parentProperty, string propertyName)
        {
            SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);
            var field = new PropertyField(property);
            field.Bind(scriptableObject.SerializedObject);
            return field;
        }
    }
}
