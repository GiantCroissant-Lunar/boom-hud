using System;
using System.Collections.Generic;
using DA_Assets.Shared.Extensions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DA_Assets.Shared.MCP
{
    [CreateAssetMenu(fileName = "McpServerConfig", menuName = "D.A. Assets/MCP/Server Config")]
    public class McpServerConfig : ScriptableObject
    {
        public string Host = "127.0.0.1";
        public int Port = 8673;

        public string RpcVersion = "2.0";
        public string ProtocolVersion = "2024-11-05";
        public string ServerVersion = "1.0.0";

        public List<McpToolSO> McpTools = new();

        [NonSerialized]
        public List<IdeName> SupportedAgents = new()
        {
            IdeName.Antigravity,
            IdeName.Cursor,
            IdeName.Codex,
            IdeName.Coplay
        };

        [SerializeField] bool _debug;

        public string ServerName => name;

        /// <summary>
        /// Registers all MCP tools with the server. Called on server start.
        /// </summary>
        public virtual void RegisterTools(McpServer server, Object owner)
        {
            if (server == null || owner == null)
            {
                Debug.LogError(SharedLocKey.log_mcp_server_owner_null.Localize("MCP"));
                return;
            }

            int registered = 0;
            foreach (var toolSO in this.McpTools)
            {
                if (toolSO == null)
                {
                    Debug.LogError(SharedLocKey.log_mcp_skip_null_tool.Localize("MCP", name));
                    continue;
                }

                try
                {
                    IMcpTool tool = toolSO.CreateInstance(owner);
                    server.RegisterTool(tool);
                    registered++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError(SharedLocKey.log_mcp_create_tool_failed.Localize("MCP", toolSO.name, ex.Message));
                }
            }

            if (_debug)
            {
                Debug.Log(SharedLocKey.log_mcp_registered_tools.Localize("MCP", registered, name));
            }
        }
    }
}
