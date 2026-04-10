using DA_Assets.Tools;
using UnityEngine;

namespace DA_Assets.Shared.MCP
{
    public abstract class McpToolSO : ScriptableObject
    {
        [Header("Tool Identity")]
        public string ToolName;

        [TextArea(2, 4)]
        public string ToolDescription;

        [Header("Input Schema")]
        public InputSchema Schema;

        [Header("Response Templates")]
        public SerializedDictionary<string, string> ResponseTemplates = new();
        public abstract IMcpTool CreateInstance(object context);

        public string GetTemplate(string key)
        {
            return ResponseTemplates != null && ResponseTemplates.TryGetValue(key, out var template)
                ? template
                : string.Empty;
        }

        public string FormatTemplate(string key, params object[] args)
        {
            string template = GetTemplate(key);
            return string.IsNullOrEmpty(template) ? string.Empty : string.Format(template, args);
        }
    }
}