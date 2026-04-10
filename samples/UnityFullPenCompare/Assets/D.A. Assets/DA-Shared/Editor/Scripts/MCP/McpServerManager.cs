using DA_Assets.Shared.Extensions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DA_Assets.Shared.MCP
{
    [InitializeOnLoad]
    public static class McpServerManager
    {
        private static readonly Dictionary<string, McpServer> _servers = new();

        private const string RunningPrefix = "MCP.Running.";
        private const string OwnerPrefix = "MCP.Owner.";

        static McpServerManager()
        {
            EditorApplication.delayCall += RestoreServers;
        }

        private static bool _restored;

        private static void RestoreServers()
        {
            if (_restored) return;
            _restored = true;

            foreach (McpServerConfig config in GetAllConfigs())
            {
                if (!EditorPrefs.GetBool(RunningPrefix + config.name, false))
                    continue;

                var owner = LoadOwner(config.name);
                if (owner == null)
                {
                    Debug.LogError(SharedLocKey.log_mcp_restore_owner_not_found.Localize("MCP", config.name));
                    EditorPrefs.SetBool(RunningPrefix + config.name, false);
                    continue;
                }

                Start(config, owner);
            }
        }

        public static bool IsRunning(string configName) => 
            !string.IsNullOrEmpty(configName) && _servers.ContainsKey(configName) && _servers[configName].IsRunning;

        public static McpServer Start(McpServerConfig config, UnityEngine.Object owner)
        {
            if (config == null || owner == null)
            {
                Debug.LogError(SharedLocKey.log_mcp_config_owner_null.Localize("MCP"));
                return null;
            }

            string name = config.name;

            if (_servers.ContainsKey(name))
                Stop(name);

            var server = new McpServer(config);

            server.Start();
            _servers[name] = server;
            
            SaveOwner(name, owner);
            EditorPrefs.SetBool(RunningPrefix + name, true);

            config.RegisterTools(server, owner);
            
            return server;
        }

        public static void Stop(string configName)
        {
            if (string.IsNullOrEmpty(configName)) return;

            if (_servers.TryGetValue(configName, out var server))
            {
                server.Stop();
                server.Dispose();
                _servers.Remove(configName);
            }
            
            EditorPrefs.SetBool(RunningPrefix + configName, false);
        }

        public static void StopAll()
        {
            foreach (var name in new List<string>(_servers.Keys))
                Stop(name);
        }

        public static UnityEngine.Object GetOwner(string configName)
        {
            if (string.IsNullOrEmpty(configName)) return null;
            return LoadOwner(configName);
        }

        public static IEnumerable<McpServerConfig> GetAllConfigs() => 
            Resources.LoadAll<McpServerConfig>("MCP");

        public static bool HasPortConflict(int port, string excludeConfig)
        {
            foreach (var config in GetAllConfigs())
            {
                if (config.name != excludeConfig && config.Port == port && IsRunning(config.name))
                    return true;
            }
            return false;
        }


        private static void SaveOwner(string configName, UnityEngine.Object owner)
        {
            var globalId = GlobalObjectId.GetGlobalObjectIdSlow(owner);
            EditorPrefs.SetString(OwnerPrefix + configName, globalId.ToString());
        }

        private static UnityEngine.Object LoadOwner(string configName)
        {
            var str = EditorPrefs.GetString(OwnerPrefix + configName, "");
            if (string.IsNullOrEmpty(str)) return null;
            
            if (GlobalObjectId.TryParse(str, out var globalId))
                return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
            
            return null;
        }
    }
}