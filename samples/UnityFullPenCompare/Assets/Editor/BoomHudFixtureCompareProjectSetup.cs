using System;
using BoomHud.Compare;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace BoomHud.Compare.Editor
{
    public static class BoomHudFixtureCompareProjectSetup
    {
        private const string SceneDirectory = "Assets/BoomHudCompare/Scenes";
        private const string ScenePath = SceneDirectory + "/FixtureCompare.unity";

        public static void SetupScene(string resourceBasePath, string generatedRootName, string generatedViewTypeName)
        {
            if (string.IsNullOrWhiteSpace(resourceBasePath) ||
                string.IsNullOrWhiteSpace(generatedRootName) ||
                string.IsNullOrWhiteSpace(generatedViewTypeName))
            {
                throw new ArgumentException("Fixture UIToolkit compare setup requires resource path, root name, and generated view type.");
            }

            BoomHudCompareProjectSetup.EnsureCompareFolders();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var panelSettings = BoomHudCompareProjectSetup.EnsurePanelSettingsAsset();

            var rootObject = new GameObject("BoomHud Fixture Compare UI");
            var document = rootObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var presenter = rootObject.AddComponent<FixtureHudPresenter>();

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            camera.tag = "MainCamera";

            presenter.Configure(resourceBasePath, generatedRootName, generatedViewTypeName);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            UnityEngine.Object.FindFirstObjectByType<FixtureHudPresenter>()
                ?.Configure(resourceBasePath, generatedRootName, generatedViewTypeName);
        }
    }
}
