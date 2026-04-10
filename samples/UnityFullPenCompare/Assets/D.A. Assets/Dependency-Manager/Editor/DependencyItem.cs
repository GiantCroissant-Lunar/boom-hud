using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DA_Assets.DM
{
    [CreateAssetMenu(fileName = "New Dependency", menuName = "D.A. Assets/Dependency Item")]
    public class DependencyItem : ScriptableObject
    {
        [Tooltip("Full type name, comma, assembly name.\nExample: TMPro.TextMeshPro, Unity.TextMeshPro")]
        public string TypeAndAssembly;

        [Tooltip("The symbol that will be added or removed.\nExample: ASSET_TMPRO_PRESENT")]
        public string ScriptingDefineSymbol;

        [Tooltip("If specified, the assembly is valid ONLY when its location ends with one of these paths.\nFormat: Editor/Data/Managed/Newtonsoft.Json.dll\nLeave empty to accept any location.")]
        public string[] ExpectedPaths;

        [Tooltip("If specified, the assembly is IGNORED when its location ends with one of these paths.\nFormat: Editor/Data/Managed/Newtonsoft.Json.dll\nLeave empty to not exclude any locations.")]
        public string[] UnexpectedPaths;

        [Tooltip("Asmdef files that need a reference to this dependency's assembly.")]
        public AssemblyDefinitionAsset[] DependentAsmdefAssets;

        [Tooltip("Name for auto-generated asmdef if the dependency lacks one.\nLeave empty to skip asmdef management.\nExample: DA_Generated.ProceduralUIImage")]
        public string GeneratedAsmdefName;

        [Tooltip("Asmdef assets that will be written as references inside the auto-generated asmdef.\nUse this when the dependency itself relies on external assemblies (e.g. TextMeshPro).")]
        public List<AssemblyDefinitionAsset> GeneratedAsmdefReferences = new List<AssemblyDefinitionAsset>();

        [Tooltip("Name for auto-generated Editor asmdef.\nLeave empty to skip Editor asmdef generation.\nExample: DA_Generated.I2L.Editor")]
        public string GeneratedEditorAsmdefName;

        [Tooltip("Additional asmdef assets for the Editor asmdef references (beyond the auto-included Runtime asmdef).\nFor example: Unity.TextMeshPro")]
        public List<AssemblyDefinitionAsset> GeneratedEditorAsmdefReferences = new List<AssemblyDefinitionAsset>();

        [Tooltip("All asmdefs that must exist in the project for the define to be added.\nIf any asmdef is missing, the define will NOT be added.\nLeave empty to skip this check (for dependencies without asmdefs).")]
        public AssemblyDefinitionAsset[] RequiredAsmdefs;

#if UNITY_EDITOR
        /// <summary>
        /// Returns true if all RequiredAsmdefs are assigned and resolvable.
        /// Empty array = no check needed = true.
        /// </summary>
        public bool AreAllRequiredAsmdefsPresent()
        {
            if (RequiredAsmdefs == null || RequiredAsmdefs.Length == 0)
                return true;

            foreach (var asmdef in RequiredAsmdefs)
            {
                if (asmdef == null)
                    return false;

                string path = AssetDatabase.GetAssetPath(asmdef);
                if (string.IsNullOrEmpty(path))
                    return false;
            }

            return true;
        }
#endif

        [SerializeField, HideInInspector]
        private bool isEnabled;

        // GUID of the found script asset. Never contains absolute paths.
        [SerializeField, HideInInspector]
        private string scriptGuid;

        // Non-path status labels: "Not found", "Enabled manually", "Disabled manually", etc.
        [SerializeField, HideInInspector]
        private string statusLabel;

        [SerializeField, HideInInspector]
        private bool disabledManually;

        public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

        public bool DisabledManually { get => disabledManually; set => disabledManually = value; }

        /// <summary>Stored GUID of the found script asset (never contains absolute paths).</summary>
        public string ScriptGuid => scriptGuid;

        /// <summary>Status label when no real path is available (e.g. "Not found").</summary>
        public string StatusLabel => statusLabel;

        /// <summary>
        /// Returns the relative asset path resolved from the stored GUID,
        /// or the status label if no real path is available.
        /// Setting this property converts a real path to GUID automatically.
        /// </summary>
        public string ScriptPath
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(scriptGuid))
                {
                    string path = AssetDatabase.GUIDToAssetPath(scriptGuid);
                    if (!string.IsNullOrEmpty(path))
                        return path;
                }
#endif
                return statusLabel;
            }
            set
            {
#if UNITY_EDITOR
                // If the value looks like a real asset path, convert to GUID.
                if (!string.IsNullOrEmpty(value) &&
                    (value.StartsWith("Assets/", System.StringComparison.Ordinal) ||
                     value.StartsWith("Packages/", System.StringComparison.Ordinal) ||
                     value.StartsWith("Library/", System.StringComparison.Ordinal)))
                {
                    string guid = AssetDatabase.AssetPathToGUID(value);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        scriptGuid  = guid;
                        statusLabel = null;
                        return;
                    }
                }
#endif
                // Otherwise treat as a status label (e.g. "Not found", "Enabled manually").
                statusLabel = value;
                scriptGuid  = null;
            }
        }
    }
}