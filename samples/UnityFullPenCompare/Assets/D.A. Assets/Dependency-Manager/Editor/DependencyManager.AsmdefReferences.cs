using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DA_Assets.DM
{
    public partial class DependencyManager
    {
        public static void EnsureDependentReferences(AssemblyDefinitionAsset[] dependentAsmdefAssets, string targetAsmdefGuid)
        {
            if (dependentAsmdefAssets == null || dependentAsmdefAssets.Length == 0)
                return;

            foreach (AssemblyDefinitionAsset asmdefAsset in dependentAsmdefAssets)
            {
                if (asmdefAsset == null)
                    continue;

                string asmdefPath = AssetDatabase.GetAssetPath(asmdefAsset);

                if (string.IsNullOrWhiteSpace(asmdefPath))
                {
                    if (_debug)
                        Debug.LogWarning(DependencyManagerLocKey.log_asmdef_path_not_found.Localize(asmdefAsset.name));
                    continue;
                }

                if (AddReferenceToAsmdef(asmdefPath, targetAsmdefGuid))
                {
                    if (_debug)
                        Debug.Log(DependencyManagerLocKey.log_asmdef_guid_added.Localize(targetAsmdefGuid, asmdefPath));
                }
            }
        }

        public static void RemoveDependentReferences(AssemblyDefinitionAsset[] dependentAsmdefAssets, string targetAsmdefGuid)
        {
            if (dependentAsmdefAssets == null || dependentAsmdefAssets.Length == 0)
                return;

            foreach (AssemblyDefinitionAsset asmdefAsset in dependentAsmdefAssets)
            {
                if (asmdefAsset == null)
                    continue;

                string asmdefPath = AssetDatabase.GetAssetPath(asmdefAsset);
                if (string.IsNullOrWhiteSpace(asmdefPath))
                    continue;

                if (RemoveReferenceFromAsmdef(asmdefPath, targetAsmdefGuid))
                {
                    if (_debug)
                        Debug.Log(DependencyManagerLocKey.log_asmdef_guid_removed.Localize(targetAsmdefGuid, asmdefPath));
                }
            }
        }

        public static bool AddReferenceToAsmdef(string asmdefAssetPath, string referenceGuid)
        {
            lock (_fileLock)
            {
                string fullPath = Path.GetFullPath(asmdefAssetPath);
                string content = File.ReadAllText(fullPath);

                AsmdefData data = JsonUtility.FromJson<AsmdefData>(content);
                if (string.IsNullOrEmpty(data.name)) return false;

                string guidRef = $"{GuidPrefix}{referenceGuid}";
                var refs = new List<string>(data.references ?? new string[0]);
                bool modified = false;

                // Remove any plain-name duplicates of this assembly.
                string targetPath = AssetDatabase.GUIDToAssetPath(referenceGuid);
                if (!string.IsNullOrWhiteSpace(targetPath))
                {
                    string assemblyName = ReadAsmdefName(targetPath);
                    if (!string.IsNullOrWhiteSpace(assemblyName))
                    {
                        int removed = refs.RemoveAll(r => string.Equals(r, assemblyName, StringComparison.Ordinal));
                        if (removed > 0)
                        {
                            modified = true;
                            if (_debug)
                                Debug.Log(DependencyManagerLocKey.log_asmdef_plain_name_removed.Localize(assemblyName, asmdefAssetPath));
                        }
                    }
                }

                if (refs.Contains(guidRef))
                {
                    if (modified)
                    {
                        data.references = refs.ToArray();
                        File.WriteAllText(fullPath, JsonUtility.ToJson(data, true));
                    }
                    return modified;
                }

                refs.Add(guidRef);
                data.references = refs.ToArray();
                File.WriteAllText(fullPath, JsonUtility.ToJson(data, true));
                return true;
            }
        }

        public static bool RemoveReferenceFromAsmdef(string asmdefAssetPath, string referenceGuid)
        {
            lock (_fileLock)
            {
                string fullPath = Path.GetFullPath(asmdefAssetPath);
                string content = File.ReadAllText(fullPath);

                string guidRef = $"{GuidPrefix}{referenceGuid}";

                if (!content.Contains(guidRef))
                    return false;

                AsmdefData data = JsonUtility.FromJson<AsmdefData>(content);
                if (string.IsNullOrEmpty(data.name)) return false;

                var refs = new List<string>(data.references ?? new string[0]);
                int removed = refs.RemoveAll(r => r == guidRef);

                if (removed == 0)
                    return false;

                data.references = refs.ToArray();
                File.WriteAllText(fullPath, JsonUtility.ToJson(data, true));
                return true;
            }
        }
    }
}
