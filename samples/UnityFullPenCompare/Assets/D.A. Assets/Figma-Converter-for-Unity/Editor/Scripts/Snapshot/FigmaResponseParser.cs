using System;
using System.Collections.Generic;
using UnityEngine;

#if JSONNET_PLASTIC_EXISTS
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;
#elif JSONNET_EXISTS
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#endif

namespace DA_Assets.FCU.Snapshot
{
    /// <summary>
    /// Parses Figma API response log files and extracts FObject JSON
    /// for each node by its Figma ID.
    /// </summary>
    public static class FigmaResponseParser
    {
        private const string FigmaResponseEntry = "_figma_response.json";

        public static string FigmaResponseEntryName => FigmaResponseEntry;

        /// <summary>
        /// Extracts the JSON body from a log file.
        /// Log format: line 1 = request URL, line 2 = error, lines 3+ = JSON body.
        /// </summary>
        public static string ExtractJsonBody(string logContent)
        {
            if (string.IsNullOrEmpty(logContent))
                return null;

            int firstNewline = logContent.IndexOf('\n');
            if (firstNewline < 0)
                return null;

            int secondNewline = logContent.IndexOf('\n', firstNewline + 1);
            if (secondNewline < 0)
                return null;

            return logContent.Substring(secondNewline + 1).Trim();
        }

        /// <summary>
        /// Parses FObjects from a Figma API response JSON body.
        /// Returns a dictionary mapping Figma node IDs to their prettified JSON representation.
        /// Skips the "children" array in each node to keep output flat.
        /// </summary>
        public static Dictionary<string, string> ParseFObjects(string jsonBody)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(jsonBody))
                return result;

#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
            try
            {
                JObject root = JObject.Parse(jsonBody);

                // Figma /v1/files/:id/nodes response has "nodes" dictionary.
                JToken nodesToken = root["nodes"];

                if (nodesToken != null && nodesToken.Type == JTokenType.Object)
                {
                    foreach (var kvp in (JObject)nodesToken)
                    {
                        JToken documentToken = kvp.Value?["document"];

                        if (documentToken != null && documentToken.Type == JTokenType.Object)
                        {
                            CollectFObjectsRecursive((JObject)documentToken, result);
                        }
                    }
                }
                else
                {
                    // Fallback: try to parse as a single FigmaProject with "document" at root.
                    JToken documentToken = root["document"];

                    if (documentToken != null && documentToken.Type == JTokenType.Object)
                    {
                        CollectFObjectsRecursive((JObject)documentToken, result);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"FigmaResponseParser: Failed to parse JSON. {ex.Message}");
            }
#endif

            return result;
        }

        /// <summary>
        /// Parses FObjects from a log file content (URL + error + JSON body).
        /// </summary>
        public static Dictionary<string, string> ParseFObjectsFromLog(string logContent)
        {
            string jsonBody = ExtractJsonBody(logContent);
            return ParseFObjects(jsonBody);
        }

#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        /// <summary>
        /// Recursively collects FObject nodes from a JObject tree.
        /// Each node is stored by its "id" field as a prettified JSON without "children".
        /// </summary>
        private static void CollectFObjectsRecursive(JObject node, Dictionary<string, string> result)
        {
            if (node == null)
                return;

            string id = node["id"]?.ToString();

            if (!string.IsNullOrEmpty(id))
            {
                // Create a copy without "children" for clean output.
                JObject cleaned = new JObject();

                foreach (var prop in node.Properties())
                {
                    if (string.Equals(prop.Name, "children", StringComparison.OrdinalIgnoreCase))
                        continue;

                    cleaned.Add(prop.Name, prop.Value.DeepClone());
                }

                result[id] = cleaned.ToString(Formatting.Indented);
            }

            // Recurse into children.
            JToken childrenToken = node["children"];

            if (childrenToken != null && childrenToken.Type == JTokenType.Array)
            {
                foreach (JToken child in childrenToken)
                {
                    if (child.Type == JTokenType.Object)
                    {
                        CollectFObjectsRecursive((JObject)child, result);
                    }
                }
            }
        }
#endif
    }
}
