using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DA_Assets.DM
{
    public partial class DependencyManager
    {
        public static void ApplyDefines(List<DependencyItem> items)
        {
            var currentDefines = GetDefines()
                .Select(d => d?.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();

            var desiredDefines = new HashSet<string>(currentDefines, System.StringComparer.Ordinal);

            var allManagedSymbols = items
                .Select(i => i.ScriptingDefineSymbol?.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(System.StringComparer.Ordinal);

            foreach (string symbol in allManagedSymbols)
            {
                bool shouldBeEnabled = items.Any(i =>
                    string.Equals(i.ScriptingDefineSymbol?.Trim(), symbol, System.StringComparison.Ordinal) && i.IsEnabled);

                if (shouldBeEnabled)
                    desiredDefines.Add(symbol);
                else
                    desiredDefines.Remove(symbol);
            }

            // Sort to produce stable output and avoid unnecessary recompilations.
            var sortedDefines = desiredDefines.OrderBy(d => d, System.StringComparer.Ordinal).ToList();

            string desiredList = sortedDefines.Count > 0 ? string.Join(", ", sortedDefines) : "None";
            if (_debug)
                Debug.Log(DependencyManagerLocKey.log_final_desired_defines.Localize(desiredList));
            SetDefines(sortedDefines);
        }

        public static List<string> GetDefines()
        {
            string rawDefs;

#if UNITY_2023_1_OR_NEWER
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            UnityEditor.Build.NamedBuildTarget namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            rawDefs = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
#else
            rawDefs = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
#endif

            if (string.IsNullOrWhiteSpace(rawDefs))
            {
                return new List<string>();
            }

            return rawDefs.Split(';')
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();
        }

        public static void SetDefines(List<string> defines)
        {
            var cleaned = defines
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(System.StringComparer.Ordinal)
                .ToList();

            string joinedDefs = string.Join(";", cleaned);

#if UNITY_2023_1_OR_NEWER
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            UnityEditor.Build.NamedBuildTarget namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            string currentDefs = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);

            if (currentDefs != joinedDefs)
            {
                if (_debug)
                    Debug.Log(DependencyManagerLocKey.log_final_defines_named_target.Localize(namedBuildTarget, joinedDefs));
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, joinedDefs);
            }
#else
            BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
            string currentDefs = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);

            if (currentDefs != joinedDefs)
            {
                if (_debug)
                    Debug.Log(DependencyManagerLocKey.log_final_defines_group.Localize(group, joinedDefs));
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, joinedDefs);
            }
#endif
        }
    }
}
