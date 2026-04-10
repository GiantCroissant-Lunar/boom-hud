using DA_Assets.DAI;
using DA_Assets.Shared.Extensions;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;


namespace DA_Assets.Shared.MCP
{
    [CustomEditor(typeof(McpServerConfig))]
    public class McpServerConfigEditor : Editor
    {
        [SerializeField] DAInspectorUITK _uitk;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var config = (McpServerConfig)target;

            root.Add(CreateSupportedAgentsPanel(config));
            root.Add(CreateNetworkPanel());
            root.Add(CreateProtocolPanel());
            root.Add(CreateMcpToolsPanel());

            return root;
        }

        private VisualElement CreateStyledPanel()
        {
            var panel = new VisualElement();

            panel.style.backgroundColor = _uitk.ColorScheme.GROUP;
            UIHelpers.SetDefaultRadius(panel);
            UIHelpers.SetBorderWidth(panel, 1);
            UIHelpers.SetBorderColor(panel, _uitk.ColorScheme.OUTLINE);
            UIHelpers.SetDefaultPadding(panel);

            panel.style.marginTop = 10;

            return panel;
        }

        private Label CreatePanelHeader(string text)
        {
            return new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14,
                    marginBottom = 8
                }
            };
        }

        private PropertyField CreatePropertyField(string propertyName)
        {
            var prop = serializedObject.FindProperty(propertyName);
            return new PropertyField(prop);
        }

        private VisualElement CreateSupportedAgentsPanel(McpServerConfig config)
        {
            var panel = CreateStyledPanel();
            panel.Add(CreatePanelHeader(SharedLocKey.label_mcp_supported_agents.Localize()));

            var listContainer = new VisualElement();
            var ideNames = config.SupportedAgents;

            if (ideNames != null)
            {
                for (int i = 0; i < ideNames.Count; i++)
                {
                    var row = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            marginBottom = 4
                        }
                    };
                    if (i == ideNames.Count - 1)
                    {
                        row.style.marginBottom = 0;
                    }

                    var ideName = ideNames[i];
                    var label = new Label(ideName.ToString())
                    {
                        style = { flexGrow = 1, minWidth = 100 }
                    };

                    Button stateButton = null;
                    System.Action toggleAction = () =>
                    {
                        bool nextState = !McpIdePreferences.GetEnabled(config, ideName);
                        McpIdePreferences.SetEnabled(config, ideName, nextState);
                        McpProxyManager.UpdateProxyForIde(config, ideName);
                        if (stateButton != null)
                        {
                            stateButton.text = nextState
                                ? SharedLocKey.label_mcp_enabled.Localize()
                                : SharedLocKey.label_mcp_disabled.Localize();
                        }
                    };
                    bool isEnabled = McpIdePreferences.GetEnabled(config, ideName);
                    stateButton = CreateInlineButton(
                        isEnabled ? SharedLocKey.label_mcp_enabled.Localize() : SharedLocKey.label_mcp_disabled.Localize(),
                        SharedLocKey.tooltip_mcp_toggle_integration.Localize(ideName),
                        toggleAction,
                        0);

                    var openConfigButton = CreateInlineButton(
                        SharedLocKey.label_mcp_open_config.Localize(),
                        SharedLocKey.tooltip_mcp_open_config_file.Localize(ideName),
                        () =>
                    {
                        string configPath = McpProxyManager.GetIdeConfigPath(ideName);
                        if (File.Exists(configPath))
                        {
                            EditorUtility.OpenWithDefaultApp(configPath);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog(
                                SharedLocKey.label_mcp_config_not_found_title.Localize(),
                                SharedLocKey.message_mcp_config_file_not_found.Localize(configPath),
                                SharedLocKey.label_ok.Localize());
                        }
                    }, 6);

                    var openFolderButton = CreateInlineButton(
                        SharedLocKey.label_mcp_open_folder.Localize(),
                        SharedLocKey.tooltip_mcp_open_config_folder.Localize(ideName),
                        () =>
                    {
                        string configPath = McpProxyManager.GetIdeConfigPath(ideName);
                        string configDir = Path.GetDirectoryName(configPath);
                        if (!string.IsNullOrEmpty(configDir) && Directory.Exists(configDir))
                        {
                            EditorUtility.OpenWithDefaultApp(configDir);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog(
                                SharedLocKey.label_mcp_folder_not_found_title.Localize(),
                                SharedLocKey.message_mcp_config_folder_not_found.Localize(configDir),
                                SharedLocKey.label_ok.Localize());
                        }
                    }, 4);

                    row.Add(label);
                    row.Add(stateButton);
                    row.Add(openConfigButton);
                    row.Add(openFolderButton);
                    listContainer.Add(row);
                }
            }

            panel.Add(listContainer);

            var helpBox = new CustomHelpBox(_uitk, new HelpBoxData
            {
                Message = SharedLocKey.helpbox_mcp_agent_settings_warning.Localize(),
                MessageType = MessageType.Info
            });
            helpBox.style.marginTop = 10;
            panel.Add(helpBox);

            return panel;
        }

        private Button CreateInlineButton(string text, string tooltip, System.Action onClick, float marginLeft)
        {
            Button button = _uitk.Button(text, onClick);
            button.tooltip = tooltip ?? string.Empty;
            button.style.flexGrow = 0;
            button.style.flexShrink = 0;
            button.style.marginLeft = marginLeft;

            return button;
        }

        private VisualElement CreateNetworkPanel()
        {
            var panel = CreateStyledPanel();
            var config = (McpServerConfig)target;
            panel.Add(CreatePanelHeader(SharedLocKey.label_mcp_connection_details.Localize()));
            panel.Add(CreatePropertyField(nameof(config.Host)));
            panel.Add(CreatePropertyField(nameof(config.Port)));
            return panel;
        }

        private VisualElement CreateProtocolPanel()
        {
            var panel = CreateStyledPanel();
            var config = (McpServerConfig)target;
            panel.Add(CreatePanelHeader(SharedLocKey.label_mcp_protocol.Localize()));
            panel.Add(CreatePropertyField(nameof(config.RpcVersion)));
            panel.Add(CreatePropertyField(nameof(config.ProtocolVersion)));
            panel.Add(CreatePropertyField(nameof(config.ServerVersion)));
            return panel;
        }

        private VisualElement CreateMcpToolsPanel()
        {
            var panel = CreateStyledPanel();
            var config = (McpServerConfig)target;
            panel.Add(CreatePanelHeader(SharedLocKey.label_mcp_tools.Localize()));
            panel.Add(CreatePropertyField(nameof(config.McpTools)));
            return panel;
        }
    }
}