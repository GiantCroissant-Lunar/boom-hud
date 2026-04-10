using BoomHud.Compare;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BoomHud.Compare.Editor
{
    public static class BoomHudUGuiComponentLabSetup
    {
        private const string SceneDirectory = "Assets/BoomHudCompare/Scenes";
        private const string ScenePath = SceneDirectory + "/ComponentLabUGui.unity";

        [MenuItem("Tools/BoomHud/Setup uGUI Component Lab Scene", priority = 111)]
        public static void SetupSceneFromMenu()
        {
            SetupScene();
        }

        public static void SetupScene()
        {
            BoomHudCompareProjectSetup.EnsureCompareFolders();
            BoomHudCompareProjectSetup.EnsureFolderPath(SceneDirectory);
            BoomHudUGuiPrefabBuilder.BuildPrefabs();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BoomHudUGuiCompareProjectSetup.CreateSharedSceneObjects();

            var rootObject = new GameObject("BoomHud uGUI Component Lab");
            rootObject.AddComponent<Canvas>();
            rootObject.AddComponent<CanvasScaler>();
            rootObject.AddComponent<GraphicRaycaster>();
            var presenter = rootObject.AddComponent<UGuiComponentLabPresenter>();
            presenter.Rebind();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Debug.Log($"BoomHud uGUI component lab scene ready at {ScenePath}");
        }
    }
}
