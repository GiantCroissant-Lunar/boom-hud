using System.IO;
using UnityEditor;
using UnityEngine;
using static BoomHud.Unity.Editor.BoomHudGenerationRuleValueConverter;

namespace BoomHud.Unity.Editor
{
    internal static class BoomHudGenerationRuleSetUtility
    {
        internal static bool ExportDefaultRuleSetJson()
        {
            var settings = BoomHudProjectSettings.Current;
            var asset = LoadOrCreateAsset(settings.GenerationRuleAssetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("BoomHud", "Could not load or create the generation rule asset.", "OK");
                return false;
            }

            var absolutePath = ToAbsoluteProjectPath(settings.GenerationRuleJsonPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllText(absolutePath, asset.ToCanonicalJson());
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("BoomHud", $"Exported generation rules to:\n{absolutePath}", "OK");
            return true;
        }

        internal static bool ImportDefaultRuleSetJson()
        {
            var settings = BoomHudProjectSettings.Current;
            var absolutePath = ToAbsoluteProjectPath(settings.GenerationRuleJsonPath);
            if (!File.Exists(absolutePath))
            {
                EditorUtility.DisplayDialog("BoomHud", $"Rule JSON was not found:\n{absolutePath}", "OK");
                return false;
            }

            var asset = LoadOrCreateAsset(settings.GenerationRuleAssetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("BoomHud", "Could not load or create the generation rule asset.", "OK");
                return false;
            }

            asset.LoadCanonicalJson(File.ReadAllText(absolutePath));
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("BoomHud", $"Imported generation rules from:\n{absolutePath}", "OK");
            return true;
        }

        internal static BoomHudGenerationRuleSetAsset? LoadOrCreateAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var existing = AssetDatabase.LoadAssetAtPath<BoomHudGenerationRuleSetAsset>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureAssetParentFolder(assetPath);
            var asset = ScriptableObject.CreateInstance<BoomHudGenerationRuleSetAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        internal static string ToAbsoluteProjectPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            return Path.GetFullPath(Path.Combine(GetProjectRoot(), NullIfWhiteSpace(path) ?? string.Empty));
        }

        private static string GetProjectRoot()
            => Directory.GetParent(Application.dataPath)!.FullName;

        private static void EnsureAssetParentFolder(string assetPath)
        {
            var folderPath = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            var segments = folderPath.Split('/');
            if (segments.Length == 0 || segments[0] != "Assets")
            {
                return;
            }

            var current = "Assets";
            for (var index = 1; index < segments.Length; index++)
            {
                var segment = segments[index];
                var next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segment);
                }

                current = next;
            }
        }
    }
}
