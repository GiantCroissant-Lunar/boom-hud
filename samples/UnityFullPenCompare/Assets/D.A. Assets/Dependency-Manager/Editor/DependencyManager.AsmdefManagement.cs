using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DA_Assets.DM
{
    public partial class DependencyManager
    {
        public static string FindNearestAsmdef(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath);
            string assetsRoot = Path.GetFullPath("Assets").Replace('\\', '/').TrimEnd('/');

            while (!string.IsNullOrEmpty(directory))
            {
                string normalizedDir = Path.GetFullPath(directory).Replace('\\', '/').TrimEnd('/');

                // Stop at or above the project's Assets folder.
                if (string.Equals(normalizedDir, assetsRoot, StringComparison.OrdinalIgnoreCase) ||
                    !normalizedDir.StartsWith(assetsRoot + "/", StringComparison.OrdinalIgnoreCase))
                    break;

                string[] asmdefFiles = Directory.GetFiles(directory, $"*{ExtAsmdef}");
                if (asmdefFiles.Length > 0)
                {
                    string fullPath = Path.GetFullPath(asmdefFiles[0]).Replace('\\', '/');
                    string projectPath = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/');

                    if (fullPath.StartsWith(projectPath + "/", StringComparison.OrdinalIgnoreCase))
                        return fullPath.Substring(projectPath.Length + 1);

                    return fullPath;
                }

                directory = Path.GetDirectoryName(directory);
            }

            return null;
        }

        public static string ReadAsmdefName(string asmdefAssetPath)
        {
            string fullPath = Path.GetFullPath(asmdefAssetPath);
            string content = File.ReadAllText(fullPath);
            AsmdefData data = JsonUtility.FromJson<AsmdefData>(content);
            return !string.IsNullOrEmpty(data.name) ? data.name : null;
        }

        public static string CreateGeneratedAsmdef(string directory, string asmdefName, string[] referenceGuids = null, string[] includePlatforms = null)
        {
            string filePath = Path.Combine(directory, $"{asmdefName}{ExtAsmdef}");
            string assetPath = filePath.Replace('\\', '/').Replace(
                Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/", "");

            // Build the references array in "GUID:xxxx" format.
            string[] desiredRefs = referenceGuids != null && referenceGuids.Length > 0
                ? System.Array.ConvertAll(referenceGuids, g => $"{GuidPrefix}{g}")
                : new string[0];

            if (File.Exists(filePath))
            {
                string existingContent = File.ReadAllText(filePath);
                AsmdefData existingData = JsonUtility.FromJson<AsmdefData>(existingContent);

                if (existingData.name == asmdefName)
                {
                    string[] currentRefs = existingData.references ?? new string[0];
                    bool refsChanged = !RefsEqual(currentRefs, desiredRefs);

                    if (refsChanged)
                    {
                        existingData.references = desiredRefs;
                        File.WriteAllText(filePath, JsonUtility.ToJson(existingData, true));
                        AssetDatabase.ImportAsset(assetPath);

                        if (_debug)
                            Debug.Log(DependencyManagerLocKey.log_asmdef_references_updated.Localize(asmdefName));
                    }

                    return assetPath;
                }
            }

            var data = new AsmdefData
            {
                name = asmdefName,
                autoReferenced = true,
                references = desiredRefs,
                includePlatforms = includePlatforms ?? new string[0],
                excludePlatforms = new string[0],
                precompiledReferences = new string[0],
                defineConstraints = new string[0],
                versionDefines = new VersionDefineData[0]
            };

            File.WriteAllText(filePath, JsonUtility.ToJson(data, true));

            // Convert to asset-relative path and import so .meta is created.
            AssetDatabase.ImportAsset(assetPath);
            return assetPath;
        }

        private static bool RefsEqual(string[] a, string[] b)
        {
            if (a.Length != b.Length) return false;
            var setA = new System.Collections.Generic.HashSet<string>(a);
            foreach (string r in b)
                if (!setA.Contains(r)) return false;
            return true;
        }

        public static string FindAsmdefByAssemblyName(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName)) return null;

            string[] guids = AssetDatabase.FindAssets(AsmdefAssetFilter);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = ReadAsmdefName(path);

                if (string.Equals(name, assemblyName, StringComparison.Ordinal))
                    return path;
            }

            return null;
        }
    }
}
