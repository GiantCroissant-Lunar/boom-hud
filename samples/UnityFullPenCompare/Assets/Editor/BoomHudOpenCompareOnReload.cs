using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BoomHud.Compare.Editor
{
    [InitializeOnLoad]
    internal static class BoomHudOpenCompareOnReload
    {
        private const string FlagFileName = "BoomHud.OpenCompareScene.flag";

        static BoomHudOpenCompareOnReload()
        {
            EditorApplication.delayCall += TryOpenCompareScene;
        }

        private static void TryOpenCompareScene()
        {
            try
            {
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrWhiteSpace(projectRoot))
                {
                    return;
                }

                var flagPath = Path.Combine(projectRoot, FlagFileName);
                if (!File.Exists(flagPath))
                {
                    return;
                }

                File.Delete(flagPath);
                BoomHudCompareProjectSetup.SetupScene();
                Debug.Log("BoomHud compare scene auto-open completed from reload flag.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}