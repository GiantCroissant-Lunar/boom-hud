using UnityEditor;
using UnityEngine;

namespace BoomHud.Unity.Editor
{
    internal static class BoomHudUnityPackageMenu
    {
        [MenuItem("Tools/BoomHud/Log Unity Package Info", priority = 2000)]
        private static void LogPackageInfo()
        {
            Debug.Log("BoomHud Unity package is installed. See Packages/com.boomhud.unity/Documentation~/README.md for setup details.");
        }
    }
}