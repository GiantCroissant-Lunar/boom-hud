using System;
using BoomHud.Compare;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BoomHud.Compare.Editor
{
    public static class BoomHudFixtureUGuiCompareProjectSetup
    {
        private const string SceneDirectory = "Assets/BoomHudCompare/Scenes";
        private const string ScenePath = SceneDirectory + "/FixtureCompareUGui.unity";

        public static void SetupScene(string generatedViewTypeName, string targetObjectName)
        {
            if (string.IsNullOrWhiteSpace(generatedViewTypeName) || string.IsNullOrWhiteSpace(targetObjectName))
            {
                throw new ArgumentException("Fixture uGUI compare setup requires generated view type and target object name.");
            }

            BoomHudCompareProjectSetup.EnsureCompareFolders();
            BoomHudCompareProjectSetup.EnsureFolderPath(SceneDirectory);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BoomHudUGuiCompareProjectSetup.CreateSharedSceneObjects();

            var rootObject = new GameObject("BoomHud Fixture Compare UI");
            rootObject.AddComponent<Canvas>();
            rootObject.AddComponent<CanvasScaler>();
            rootObject.AddComponent<GraphicRaycaster>();
            var presenter = rootObject.AddComponent<FixtureUGuiPresenter>();
            presenter.Configure(generatedViewTypeName, targetObjectName);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            UnityEngine.Object.FindFirstObjectByType<FixtureUGuiPresenter>()
                ?.Configure(generatedViewTypeName, targetObjectName);
        }
    }
}
