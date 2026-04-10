using BoomHud.Compare;
using Generated.Hud.UGui;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BoomHud.Compare.Editor
{
    public static class BoomHudUGuiPrefabBuilder
    {
        private const string ResourcesDirectory = "Assets/Resources";
        private const string PrefabDirectory = ResourcesDirectory + "/BoomHudUGuiPrefabs";

        [MenuItem("Tools/BoomHud/Build uGUI Component Prefabs", priority = 109)]
        public static void BuildPrefabsFromMenu()
        {
            BuildPrefabs();
        }

        public static void BuildPrefabs()
        {
            BoomHudCompareProjectSetup.EnsureFolderPath(ResourcesDirectory);
            BoomHudCompareProjectSetup.EnsureFolderPath(PrefabDirectory);

            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                SavePrefab(previewScene, "ActionButton", () => UGuiHudPreviewComposer.CreateGeneratedActionButton(null).Root.gameObject);
                SavePrefab(previewScene, "StatusIcon", () => UGuiHudPreviewComposer.CreateGeneratedStatusIcon(null).Root.gameObject);
                SavePrefab(previewScene, "StatBar", () => UGuiHudPreviewComposer.CreateGeneratedStatBar(null).Root.gameObject);
                SavePrefab(previewScene, "MessageLog", () => UGuiHudPreviewComposer.CreateGeneratedMessageLog(null).Root.gameObject);
                SavePrefab(previewScene, "Minimap", () => UGuiHudPreviewComposer.CreateGeneratedMinimap(null).Root.gameObject);
                SavePrefab(previewScene, "CharPortrait", CreateCharPortraitPrefabRoot);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }

            Debug.Log($"BoomHud uGUI prefabs ready at {PrefabDirectory}");
        }

        private static GameObject CreateCharPortraitPrefabRoot()
        {
            var view = UGuiHudPreviewComposer.CreateGeneratedComposedCharPortrait(null);
            UGuiHudPreviewComposer.ApplyPartyMemberPresentation(view, UGuiHudPreviewComposer.ReferenceCharPortrait);
            view.Generated.Root.name = "CharPortrait";
            return view.Generated.Root.gameObject;
        }

        private static void SavePrefab(Scene previewScene, string prefabName, System.Func<GameObject> create)
        {
            var root = create();
            root.name = prefabName;
            SceneManager.MoveGameObjectToScene(root, previewScene);

            var prefabPath = $"{PrefabDirectory}/{prefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }
    }
}
