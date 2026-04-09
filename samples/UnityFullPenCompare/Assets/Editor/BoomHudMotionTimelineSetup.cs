using Generated.Hud;
using BoomHud.Compare;
using BoomHud.Unity.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoomHud.Compare.Editor
{
    public static class BoomHudMotionTimelineSetup
    {
        private const string SceneDirectory = "Assets/BoomHudCompare/Scenes";
        private const string TimelineDirectory = "Assets/BoomHudCompare/Timelines";

        [MenuItem("Tools/BoomHud/Setup Char Portrait Motion Timeline Scene", priority = 103)]
        public static void SetupSceneFromMenu()
        {
            SetupScene();
        }

        public static void SetupScene()
        {
            BoomHudCompareProjectSetup.EnsureCompareFolders();
            BoomHudCompareProjectSetup.EnsureFolderPath(SceneDirectory);
            BoomHudCompareProjectSetup.EnsureFolderPath(TimelineDirectory);
            var result = BoomHudMotionTimelineSceneBuilder.Create(
                typeof(CharPortraitMotionHost),
                new BoomHudMotionTimelineSceneOptions
                {
                    SceneDirectory = SceneDirectory,
                    TimelineDirectory = TimelineDirectory,
                    SceneName = "CharPortraitMotionTimeline",
                    TimelineName = "CharPortraitMotionTimeline",
                    DefaultClipId = CharPortraitMotion.DefaultClipId,
                    PanelSettingsAssetPath = BoomHudCompareProjectSetup.PanelSettingsPath,
                    RootObjectName = "BoomHud Char Portrait Motion Timeline",
                    CameraBackgroundColor = new Color(0.04f, 0.04f, 0.05f, 1f)
                });

            var scene = EditorSceneManager.OpenScene(result.ScenePath, OpenSceneMode.Single);
            var rootObject = GameObject.Find("BoomHud Char Portrait Motion Timeline");
            if (rootObject == null)
            {
                return;
            }

            var motionHost = rootObject.GetComponent<CharPortraitMotionHost>();
            if (motionHost != null)
            {
                ConfigureMotionHostDefaults(motionHost);
            }

            if (rootObject.GetComponent<MotionStagePresenter>() == null)
            {
                rootObject.AddComponent<MotionStagePresenter>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, result.ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureMotionHostDefaults(CharPortraitMotionHost host)
        {
            var serializedHost = new SerializedObject(host);

            serializedHost.FindProperty("_initialClip").stringValue = CharPortraitMotion.DefaultClipId;
            serializedHost.FindProperty("_playOnEnable").boolValue = true;
            serializedHost.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
