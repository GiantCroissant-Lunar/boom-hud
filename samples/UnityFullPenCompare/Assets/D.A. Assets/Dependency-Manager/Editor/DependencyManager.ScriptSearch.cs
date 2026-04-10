using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DA_Assets.DM
{
    public partial class DependencyManager
    {
        /// <summary>
        /// Unified type resolution. Tries script-based search first (Path A),
        /// then falls back to direct Type.GetType for DLL/package dependencies (Path B).
        /// </summary>
        public static (bool found, string scriptPath, string asmdefPath) ResolveType(DependencyItem item)
        {
            string fullTypeName = ExtractFullTypeName(item.TypeAndAssembly);

            // --- Path A: Find .cs script first, then determine assembly ---
            if (fullTypeName != null)
            {
                var scriptResult = FindScript(fullTypeName);

                if (scriptResult.scriptPath != null)
                {
                    string asmdefPath = FindNearestAsmdef(scriptResult.scriptPath);
                    string assemblyName = asmdefPath != null
                        ? ReadAsmdefName(asmdefPath)
                        : DefaultAssemblyName;

                    if (_debug)
                        Debug.Log(DependencyManagerLocKey.log_script_found_at.Localize(item.name, scriptResult.scriptPath, assemblyName));

                    Type type = Type.GetType($"{fullTypeName}, {assemblyName}", false, true);

                    if (type != null)
                    {
                        return (true, scriptResult.scriptPath, asmdefPath);
                    }
                }
            }

            // --- Path B (fallback): Direct Type.GetType for DLL/package dependencies ---
            if (_debug)
                Debug.Log(DependencyManagerLocKey.log_processing_check_type.Localize(item.name, item.TypeAndAssembly));
            Type directType = Type.GetType(item.TypeAndAssembly, false, true);

            if (directType != null)
            {
                bool pathValid = ValidateAssemblyPath(item, directType.Assembly.Location);

                if (pathValid)
                {
                    if (_debug)
                        Debug.Log(DependencyManagerLocKey.log_processing_type_found.Localize(item.name, directType.FullName, directType.Assembly.GetName().Name));

                    // Find source path via MonoScript for display purposes.
                    string sourcePath = FindScriptPath(directType);
                    return (true, sourcePath, null);
                }
                else
                {
                    if (_debug)
                        Debug.Log(DependencyManagerLocKey.log_script_found_but_ignored.Localize(item.name, directType.Assembly.Location));
                }
            }
            else
            {
                if (_debug)
                    Debug.Log(DependencyManagerLocKey.log_processing_type_not_found.Localize(item.name, item.TypeAndAssembly));
            }

            return (false, null, null);
        }

        public static string ExtractFullTypeName(string typeAndAssembly)
        {
            if (string.IsNullOrWhiteSpace(typeAndAssembly)) return null;
            int commaIndex = typeAndAssembly.IndexOf(',');
            return commaIndex > 0 ? typeAndAssembly.Substring(0, commaIndex).Trim() : typeAndAssembly.Trim();
        }

        /// <summary>
        /// Searches for a .cs script matching the given fully-qualified type name.
        /// Uses MonoScript.GetClass() for reliable type matching.
        /// </summary>
        public static (string scriptPath, string asmdefPath) FindScript(string fullTypeName)
        {
            int lastDot = fullTypeName.LastIndexOf('.');
            if (lastDot < 0) return (null, null);

            string typeName = fullTypeName.Substring(lastDot + 1);

            string[] guids = AssetDatabase.FindAssets($"{typeName} {MonoScriptFilter}");

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // FindAssets does partial matching, so verify exact file name.
                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                if (!string.Equals(fileName, typeName, StringComparison.Ordinal))
                    continue;

                MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                if (monoScript == null)
                    continue;

                Type scriptType = monoScript.GetClass();
                if (scriptType != null && scriptType.FullName == fullTypeName)
                {
                    return (assetPath, FindNearestAsmdef(assetPath));
                }
            }

            return (null, null);
        }

        public static bool ContainsNamespace(string fileContent, string expectedNamespace)
        {
            // Word boundary after the namespace name prevents partial matches
            // (e.g. "Foo.Bar" must not match "Foo.Bar.Extended").
            string pattern = $@"namespace\s+{Regex.Escape(expectedNamespace)}(?:\s|$|{{)";
            return Regex.IsMatch(fileContent, pattern);
        }

        /// <summary>
        /// Finds the source .cs path for a resolved Type via MonoScript search.
        /// Used only for DLL/package types (Path B) where we don't have scriptPath from FindScript.
        /// </summary>
        public static string FindScriptPath(Type type)
        {
            if (type == null) return PathNotFound;

            string[] guids = AssetDatabase.FindAssets($"{type.Name} {MonoScriptFilter}");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);

                if (monoScript != null && monoScript.GetClass() == type)
                    return assetPath;
            }

            try
            {
                string assemblyLocation = type.Assembly.Location;
                if (!string.IsNullOrEmpty(assemblyLocation))
                {
                    string projectPath = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/');
                    string locationNormalized = assemblyLocation.Replace('\\', '/');

                    if (locationNormalized.StartsWith(projectPath + "/", StringComparison.OrdinalIgnoreCase))
                        return locationNormalized.Substring(projectPath.Length + 1);
                    else
                        return assemblyLocation;
                }
            }
            catch (Exception ex)
            {
                if (_debug)
                    Debug.LogWarning(DependencyManagerLocKey.log_assembly_location_failed.Localize(type.FullName, ex.Message));
            }

            return SourceNotFoundLabel;
        }

        public static bool ValidateAssemblyPath(DependencyItem item, string assemblyLocation)
        {
            // Normalize all separators to forward slash for reliable cross-platform comparison.
            string normalizedLocation = assemblyLocation.Replace('\\', '/');
            bool pathValid = true;

            if (item.ExpectedPaths != null && item.ExpectedPaths.Length > 0)
            {
                pathValid = false;
                foreach (string ep in item.ExpectedPaths)
                {
                    if (string.IsNullOrWhiteSpace(ep)) continue;
                    string normalizedExpected = ep.Replace('\\', '/');
                    if (normalizedLocation.EndsWith(normalizedExpected, StringComparison.OrdinalIgnoreCase))
                    {
                        pathValid = true;
                        break;
                    }
                }
            }

            if (pathValid && item.UnexpectedPaths != null && item.UnexpectedPaths.Length > 0)
            {
                foreach (string up in item.UnexpectedPaths)
                {
                    if (string.IsNullOrWhiteSpace(up)) continue;
                    string normalizedUnexpected = up.Replace('\\', '/');
                    if (normalizedLocation.EndsWith(normalizedUnexpected, StringComparison.OrdinalIgnoreCase))
                    {
                        pathValid = false;
                        break;
                    }
                }
            }

            return pathValid;
        }
    }
}
