using DA_Assets.DAI;
using DA_Assets.Shared.Extensions;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DA_Assets.Shared.MCP.UI
{
    /// <summary>
    /// Central window for managing all MCP servers.
    /// </summary>
    public class McpServerManagerWindow : EditorWindow
    {
        [SerializeField] DAInspectorUITK uitk;

        [MenuItem("Tools/D.A. Assets/MCP Server Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<McpServerManagerWindow>();
            window.titleContent = new GUIContent(SharedLocKey.label_mcp_server_manager_title.Localize());
            window.minSize = new Vector2(400, 300);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            UIHelpers.SetDefaultPadding(root);
            root.style.backgroundColor = uitk.ColorScheme.BG;

            var scrollView = uitk.ScrollView();
            scrollView.style.flexGrow = 1;

            var configs = McpServerManager.GetAllConfigs().ToList();

            if (!configs.Any())
            {
                var emptyPanel = CreatePanel();
                var label = new Label(SharedLocKey.label_mcp_no_servers_found.Localize())
                {
                    style = { color = uitk.ColorScheme.TEXT_SECOND, unityTextAlign = TextAnchor.MiddleCenter }
                };
                emptyPanel.Add(label);
                scrollView.Add(emptyPanel);
            }
            else
            {
                foreach (var config in configs)
                    scrollView.Add(CreateServerFoldout(config));
            }

            root.Add(scrollView);
            root.Add(CreateFooter());

            EditorApplication.update += Repaint;
        }

        private void OnDestroy()
        {
            EditorApplication.update -= Repaint;
        }

        private VisualElement CreateServerFoldout(McpServerConfig config)
        {
            var header = CreateServerHeader(config);
            var body = CreateServerBody(config);

            var foldout = new AnimatedFoldout(
                $"mcp_server_{config.name}",
                header,
                body,
                false,
                uitk.FoldoutCurve,
                uitk.FoldoutDuration,
                uitk.ColorScheme.GROUP
            );

            foldout.style.marginBottom = 8;
            UIHelpers.SetDefaultRadius(foldout);
            UIHelpers.SetBorderWidth(foldout, 1);
            UIHelpers.SetBorderColor(foldout, uitk.ColorScheme.OUTLINE);

            return foldout;
        }

        private VisualElement CreateServerHeader(McpServerConfig config)
        {
            var header = new VisualElement
            {
                style = 
                { 
                    flexDirection = FlexDirection.Row, 
                    alignItems = Align.Center,
                    backgroundColor = uitk.ColorScheme.GROUP
                }
            };
            UIHelpers.SetDefaultPadding(header);

            bool isRunning = McpServerManager.IsRunning(config.name);

            var statusBadge = new Label(isRunning ? "●" : "○")
            {
                style = 
                { 
                    fontSize = 16, 
                    color = isRunning ? uitk.ColorScheme.GREEN : uitk.ColorScheme.TEXT_SECOND,
                    marginRight = 8
                }
            };

            var nameLabel = new Label(config.ServerName)
            {
                style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 }
            };

            var toggleBtn = uitk.Button(isRunning ? "■" : "▶", () => ToggleServer(config));
            toggleBtn.style.width = 30;
            toggleBtn.style.height = 30;
            toggleBtn.style.minWidth = 30;
            toggleBtn.style.minHeight = 30;
            toggleBtn.style.maxWidth = 30;
            toggleBtn.style.maxHeight = 30;
            toggleBtn.style.flexGrow = 0;
            toggleBtn.style.flexShrink = 0;
            toggleBtn.style.paddingLeft = 0;
            toggleBtn.style.paddingRight = 0;
            toggleBtn.tooltip = isRunning ? SharedLocKey.label_mcp_stop.Localize() : SharedLocKey.label_mcp_start.Localize();

            header.Add(statusBadge);
            header.Add(nameLabel);
            header.Add(toggleBtn);

            return header;
        }

        private VisualElement CreateServerBody(McpServerConfig config)
        {
            var body = new VisualElement();
            UIHelpers.SetDefaultPadding(body);
            body.style.paddingTop = 0;
            body.style.paddingLeft = 0;
            var inspector = new InspectorElement(config)
            {
                style = { marginTop = 0 }
            };
            body.Add(inspector);

            return body;
        }

        private void ToggleServer(McpServerConfig config)
        {
            if (McpServerManager.IsRunning(config.name))
            {
                McpServerManager.Stop(config.name);
            }
            else
            {
                var owner = McpServerManager.GetOwner(config.name);
                if (owner != null)
                {
                    McpServerManager.Start(config, owner);
                }
                else
                {
                    Debug.LogError(SharedLocKey.log_mcp_cannot_start_no_owner.Localize("MCP", config.name));
                }
            }
            
            Refresh();
        }

        private VisualElement CreatePanel()
        {
            var panel = new VisualElement();
            panel.style.backgroundColor = uitk.ColorScheme.GROUP;
            panel.style.marginBottom = 8;
            UIHelpers.SetDefaultPadding(panel);
            UIHelpers.SetDefaultRadius(panel);
            UIHelpers.SetBorderWidth(panel, 1);
            UIHelpers.SetBorderColor(panel, uitk.ColorScheme.OUTLINE);
            return panel;
        }

        private VisualElement CreateFooter()
        {
            var footer = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, paddingTop = 8 }
            };
            var refreshBtn = uitk.Button(SharedLocKey.label_mcp_refresh.Localize(), Refresh);
            refreshBtn.style.flexGrow = 1;
            refreshBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            var stopAllBtn = uitk.Button(SharedLocKey.label_mcp_stop_all.Localize(), () =>
            {
                McpServerManager.StopAll();
                Refresh();
            });
            stopAllBtn.style.flexGrow = 1;
            stopAllBtn.style.marginLeft = 8;
            stopAllBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            stopAllBtn.color = uitk.ColorScheme.RED;

            footer.Add(refreshBtn);
            footer.Add(stopAllBtn);
            return footer;
        }

        private void Refresh()
        {
            rootVisualElement.Clear();
            CreateGUI();
        }
    }
}

