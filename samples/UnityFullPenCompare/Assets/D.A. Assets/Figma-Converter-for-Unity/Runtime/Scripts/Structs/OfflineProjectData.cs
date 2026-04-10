using System;
using System.IO;

namespace DA_Assets.FCU.Model
{
    [Serializable]
    public struct OfflineProjectData
    {
        public string ExtractedFolderPath;

        /// <summary>
        /// MD5 hash of the ZIP archive at the time of extraction.
        /// Used to detect archive changes without relying on filesystem timestamps.
        /// </summary>
        public string ArchiveHash;

        public string JsonFilePath => Path.Combine(ExtractedFolderPath, "project.json");
        public string NodesFolder => Path.Combine(ExtractedFolderPath, "nodes");
        public string ImagesFolder => Path.Combine(ExtractedFolderPath, "images");
        public string ManifestFilePath => Path.Combine(ExtractedFolderPath, FcuConfig.OfflineManifestFileName);

        public bool IsValid => !string.IsNullOrEmpty(ExtractedFolderPath)
                            && File.Exists(JsonFilePath);

        public OfflineProjectData(string extractedFolderPath)
        {
            ExtractedFolderPath = extractedFolderPath;
            ArchiveHash = null;
        }

        public static string SanitizeNodeId(string id) => id.Replace(":", "_").Replace("/", "_");
    }
}
