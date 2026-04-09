using Generated.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace BoomHud.Compare.Editor
{
    public static class BoomHudMotionPreviewSetup
    {
        private const string SceneDirectory = "Assets/BoomHudCompare/Scenes";
        private const string ScenePath = SceneDirectory + "/DebugOverlayMotionPreview.unity";
        private const string DebugOverlayUxmlPath = "Assets/Resources/BoomHudGenerated/DebugOverlayView.uxml";

        [MenuItem("Tools/BoomHud/Setup Debug Overlay Motion Scene", priority = 102)]
        public static void SetupSceneFromMenu()
        {
            SetupScene();
        }

        public static void SetupScene()
        {
            BoomHudCompareProjectSetup.EnsureCompareFolders();
            BoomHudCompareProjectSetup.EnsureFolderPath(SceneDirectory);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var panelSettings = BoomHudCompareProjectSetup.EnsurePanelSettingsAsset();

            var rootObject = new GameObject("BoomHud Debug Overlay Motion");
            var document = rootObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DebugOverlayUxmlPath);
            rootObject.AddComponent<DebugOverlayMotionHost>();

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.05f, 1f);
            camera.tag = "MainCamera";

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Debug.Log($"BoomHud debug overlay motion scene ready at {ScenePath}");
        }
    }
}
