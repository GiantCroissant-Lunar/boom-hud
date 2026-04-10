using Generated.Hud.UGui;
using BoomHud.Unity.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoomHud.Compare.Editor
{
    public static class BoomHudUGuiMotionTimelineSetup
    {
        private const string SceneDirectory = "Assets/BoomHudCompare/Scenes";
        private const string TimelineDirectory = "Assets/BoomHudCompare/Timelines";
        private const string PrefabPath = "Assets/Resources/BoomHudUGuiPrefabs/CharPortrait.prefab";
        private const string RootObjectName = "BoomHud Char Portrait Motion Timeline UGUI";

        [MenuItem("Tools/BoomHud/Setup Char Portrait Motion Timeline Scene (uGUI)", priority = 104)]
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
                    SceneName = "CharPortraitMotionTimelineUGui",
                    TimelineName = "CharPortraitMotionTimelineUGui",
                    DefaultClipId = CharPortraitMotion.DefaultClipId,
                    RootObjectName = RootObjectName,
                    CameraBackgroundColor = new Color(0.04f, 0.04f, 0.05f, 1f)
                });

            var scene = EditorSceneManager.OpenScene(result.ScenePath, OpenSceneMode.Single);
            var rootObject = GameObject.Find(RootObjectName);
            if (rootObject == null)
            {
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null && rootObject.transform.Find("CharPortrait") == null)
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance != null)
                {
                    instance.name = "CharPortrait";
                    instance.transform.SetParent(rootObject.transform, false);
                    if (instance.TryGetComponent<RectTransform>(out var rectTransform))
                    {
                        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        rectTransform.anchoredPosition = Vector2.zero;
                    }
                }
            }

            var motionHost = rootObject.GetComponent<CharPortraitMotionHost>();
            if (motionHost != null)
            {
                if (rootObject.GetComponent<CharPortraitUguiMotionSync>() == null)
                {
                    rootObject.AddComponent<CharPortraitUguiMotionSync>();
                }

                ConfigureMotionHostDefaults(motionHost);
                motionHost.Rebind();
                UGuiHudPreviewComposer.SyncComposedCharPortraitBars(motionHost.View);
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
