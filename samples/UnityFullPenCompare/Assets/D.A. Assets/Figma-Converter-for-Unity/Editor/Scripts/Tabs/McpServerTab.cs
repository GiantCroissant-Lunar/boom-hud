using DA_Assets.DAI;
using DA_Assets.Shared.MCP;
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DA_Assets.FCU
{
    internal class McpServerTab : MonoBehaviourLinkerEditor<FcuSettingsWindow, FigmaConverterUnity>, IDisposable
    {
        private McpServerConfig _config;
        private Label _warningLabel;
        private Button _toggleButton;
        private bool _lastRunningState;

        public VisualElement Draw()
        {
            var root = new VisualElement();

            _config = FcuConfig.McpServerConfig as McpServerConfig;

            var inspector = new InspectorElement(_config);
            inspector.style.paddingLeft = DAI_UitkConstants.MarginPadding;
            inspector.style.paddingRight = DAI_UitkConstants.MarginPadding;
            inspector.style.paddingTop = 0;
            inspector.style.paddingBottom = 0;
            root.Add(inspector);
            root.Add(uitk.Space10());

            _warningLabel = new Label
            {
                style = { color = uitk.ColorScheme.RED, whiteSpace = WhiteSpace.Normal, marginBottom = 6 }
            };
            root.Add(_warningLabel);

            EditorApplication.update += OnUpdate;
            RefreshUi();

            return root;
        }

        public VisualElement DrawFooter()
        {
            var footer = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row }
            };
            UIHelpers.SetDefaultPadding(footer);
            footer.style.paddingTop = DAI_UitkConstants.SpacingXXS * 2;

            _toggleButton = uitk.Button("", OnToggleServer);
            _toggleButton.style.flexGrow = 1;

            footer.Add(_toggleButton);

            RefreshUi();

            return footer;
        }

        private void OnToggleServer()
        {
            bool isRunning = McpServerManager.IsRunning(_config.name);
            if (isRunning)
            {
                StopServer();
            }
            else
            {
                StartServer();
            }
        }

        private void StartServer()
        {
            McpServerManager.Start(_config, monoBeh);
            RefreshUi();
        }

        private void StopServer()
        {
            McpServerManager.Stop(_config.name);
            RefreshUi();
        }

        private void OnUpdate()
        {
            bool isRunning = McpServerManager.IsRunning(_config.name);
            if (isRunning != _lastRunningState)
            {
                RefreshUi();
            }
        }

        private void RefreshUi()
        {
            if (monoBeh == null)
                return;

            bool isRunning = McpServerManager.IsRunning(_config.name);
            var owner = McpServerManager.GetOwner(_config.name);

            FigmaConverterUnity ownerFcu = null;
            if (owner is GameObject go)
                ownerFcu = go.GetComponent<FigmaConverterUnity>();
            else if (owner is Component c)
                ownerFcu = c.GetComponent<FigmaConverterUnity>();

            bool runningForThis = isRunning && ReferenceEquals(ownerFcu, monoBeh);

            if (isRunning && !runningForThis)
            {
                string ownerName = owner is GameObject g ? g.name :
                                   owner is Component co ? co.gameObject.name : "Unknown";
                if (_warningLabel != null)
                {
                    _warningLabel.text = $"⚠ Server is running for another instance: {ownerName}";
                    _warningLabel.style.display = DisplayStyle.Flex;
                }
            }
            else
            {
                if (_warningLabel != null)
                {
                    _warningLabel.style.display = DisplayStyle.None;
                }
            }

            if (_toggleButton != null)
            {
                if (isRunning)
                {
                    _toggleButton.text = $"■ {FcuLocKey.label_mcp_stop.Localize()}";
                }
                else
                {
                    _toggleButton.text = $"▶ {FcuLocKey.label_mcp_start.Localize()}";
                }
            }

            _lastRunningState = isRunning;
        }

        public void Dispose()
        {
            EditorApplication.update -= OnUpdate;
        }
    }
}