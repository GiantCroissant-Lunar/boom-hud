using BoomHud.Compare;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BoomHud.Compare.Editor
{
    public static class BoomHudUGuiCompareProjectSetup
    {
        private const string SceneDirectory = "Assets/BoomHudCompare/Scenes";
        private const string ScenePath = SceneDirectory + "/ExploreHudCompareUGui.unity";

        [MenuItem("Tools/BoomHud/Setup Full Pen uGUI Compare Scene", priority = 110)]
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

            CreateSharedSceneObjects();

            var rootObject = new GameObject("BoomHud uGUI Compare UI");
            rootObject.AddComponent<Canvas>();
            rootObject.AddComponent<CanvasScaler>();
            rootObject.AddComponent<GraphicRaycaster>();
            var presenter = rootObject.AddComponent<UGuiExploreHudPresenter>();
            presenter.Rebind();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Debug.Log($"BoomHud uGUI compare scene ready at {ScenePath}");
        }

        internal static void CreateSharedSceneObjects()
        {
            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            camera.tag = "MainCamera";

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }
}
