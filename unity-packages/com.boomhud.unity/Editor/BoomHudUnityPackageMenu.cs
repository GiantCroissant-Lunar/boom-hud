using UnityEditor;
using UnityEngine;

namespace BoomHud.Unity.Editor
{
    internal static class BoomHudUnityPackageMenu
    {
        [MenuItem("Tools/BoomHud/Open Project Settings", priority = 1)]
        private static void OpenProjectSettings()
        {
            SettingsService.OpenProjectSettings(BoomHudProjectSettings.SettingsPath);
        }

        [MenuItem("Tools/BoomHud/Rules/Export Default Rule Set JSON", priority = 50)]
        private static void ExportDefaultRuleSetJson()
        {
            BoomHudGenerationRuleSetUtility.ExportDefaultRuleSetJson();
        }

        [MenuItem("Tools/BoomHud/Rules/Import Default Rule Set JSON", priority = 51)]
        private static void ImportDefaultRuleSetJson()
        {
            BoomHudGenerationRuleSetUtility.ImportDefaultRuleSetJson();
        }

        [MenuItem("Tools/BoomHud/Log Unity Package Info", priority = 2000)]
        private static void LogPackageInfo()
        {
            Debug.Log("BoomHud Unity package is installed. See Packages/com.boomhud.unity/Documentation~/README.md for setup details.");
        }
    }
}
