using UnityEditor;

namespace DA_Assets.Shared.MCP
{
    public static class McpIdePreferences
    {
        private const string PrefKeyPrefix = "DA_Assets.MCP.SupportedIDEs.Enabled.";

        public static bool GetEnabled(McpServerConfig config, IdeName toolType)
        {
            if (config == null) return false;
            return EditorPrefs.GetBool(GetKey(config, toolType), false);
        }

        public static void SetEnabled(McpServerConfig config, IdeName toolType, bool enabled)
        {
            if (config == null) return;
            EditorPrefs.SetBool(GetKey(config, toolType), enabled);
        }

        private static string GetKey(McpServerConfig config, IdeName toolType)
        {
            string configId = GetConfigId(config);
            return $"{PrefKeyPrefix}{configId}.{toolType}";
        }

        private static string GetConfigId(McpServerConfig config)
        {
            if (config == null) return "null";

            string path = AssetDatabase.GetAssetPath(config);
            if (!string.IsNullOrEmpty(path))
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid))
                    return guid;
            }

            return config.name;
        }
    }
}
