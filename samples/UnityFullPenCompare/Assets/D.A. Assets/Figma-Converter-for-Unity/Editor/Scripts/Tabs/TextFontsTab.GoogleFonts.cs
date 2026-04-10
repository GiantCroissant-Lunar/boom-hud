using DA_Assets.DAI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DA_Assets.FCU
{
    internal partial class TextFontsTab : MonoBehaviourLinkerEditor<FcuSettingsWindow, FigmaConverterUnity>
    {
        private void DrawGoogleFontsSettings(VisualElement parent)
        {
            VisualElement panel = uitk.CreateSectionPanel();
            parent.Add(panel);

            Label header = new Label(FcuLocKey.label_google_fonts_settings.Localize());
            header.tooltip = FcuLocKey.tooltip_google_fonts_settings.Localize();
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(header);
            panel.Add(uitk.ItemSeparator());

            {
                var apiKeyContainer = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center
                    }
                };

                var apiKeyField = uitk.TextField(FcuLocKey.label_google_fonts_api_key.Localize());
                apiKeyField.tooltip = FcuLocKey.tooltip_google_fonts_api_key.Localize(FcuLocKey.label_google_fonts_api_key.Localize());
                apiKeyField.value = FcuConfig.GoogleFontsApiKey;
                apiKeyField.style.flexGrow = 1;

                apiKeyField.RegisterValueChangedCallback(evt => FcuConfig.GoogleFontsApiKey = evt.newValue);
                apiKeyContainer.Add(apiKeyField);

                var getApiKeyButton = uitk.Button(FcuLocKey.label_get_google_api_key.Localize(), () =>
                {
                    Application.OpenURL("https://developers.google.com/fonts/docs/developer_api#identifying_your_application_to_google");
                });
                getApiKeyButton.tooltip = FcuLocKey.tooltip_get_google_api_key.Localize();

                getApiKeyButton.style.maxWidth = 100;
                getApiKeyButton.style.maxHeight = 18;
                getApiKeyButton.style.marginTop = 1;

                UIHelpers.SetRadius(getApiKeyButton, 3);
                apiKeyContainer.Add(getApiKeyButton);
                panel.Add(apiKeyContainer);
            }

        }
    }
}
