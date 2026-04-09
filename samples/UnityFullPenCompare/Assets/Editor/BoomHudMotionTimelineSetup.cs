using System.Linq;
using Generated.Hud;
using BoomHud.Unity.Timeline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace BoomHud.Compare.Editor
{
    public static class BoomHudMotionTimelineSetup
    {
        private const string SceneDirectory = "Assets/BoomHudCompare/Scenes";
        private const string ScenePath = SceneDirectory + "/DebugOverlayMotionTimeline.unity";
        private const string TimelineDirectory = "Assets/BoomHudCompare/Timelines";
        private const string TimelinePath = TimelineDirectory + "/DebugOverlayMotionTimeline.playable";
        private const string DebugOverlayUxmlPath = "Assets/Resources/BoomHudGenerated/DebugOverlayView.uxml";

        [MenuItem("Tools/BoomHud/Setup Debug Overlay Motion Timeline Scene", priority = 103)]
        public static void SetupSceneFromMenu()
        {
            SetupScene();
        }

        public static void SetupScene()
        {
            BoomHudCompareProjectSetup.EnsureCompareFolders();
            BoomHudCompareProjectSetup.EnsureFolderPath(SceneDirectory);
            BoomHudCompareProjectSetup.EnsureFolderPath(TimelineDirectory);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var panelSettings = BoomHudCompareProjectSetup.EnsurePanelSettingsAsset();
            var timeline = EnsureTimelineAsset();

            var rootObject = new GameObject("BoomHud Debug Overlay Motion Timeline");
            var document = rootObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DebugOverlayUxmlPath);

            var motionHost = rootObject.AddComponent<DebugOverlayMotionHost>();
            var director = rootObject.AddComponent<PlayableDirector>();
            director.playOnAwake = true;
            director.playableAsset = timeline;
            director.extrapolationMode = DirectorWrapMode.None;

            BindTimelineTrack(timeline, director, motionHost);

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

            Debug.Log($"BoomHud debug overlay motion Timeline scene ready at {ScenePath}");
        }

        private static TimelineAsset EnsureTimelineAsset()
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, TimelinePath);
            }

            foreach (var track in timeline.GetOutputTracks().ToArray())
            {
                timeline.DeleteTrack(track);
            }

            var motionTrack = timeline.CreateTrack<BoomHudMotionTrack>(null, "HUD Motion");
            var clip = motionTrack.CreateDefaultClip();
            clip.displayName = "intro";
            clip.duration = DebugOverlayMotion.GetClipDurationSeconds("intro");

            if (clip.asset is BoomHudMotionPlayableAsset motionClip)
            {
                motionClip.ClipId = "intro";
                motionClip.TimeOffsetSeconds = 0d;
                EditorUtility.SetDirty(motionClip);
            }

            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static void BindTimelineTrack(TimelineAsset timeline, PlayableDirector director, DebugOverlayMotionHost motionHost)
        {
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is BoomHudMotionTrack)
                {
                    director.SetGenericBinding(track, motionHost);
                }
            }

            EditorUtility.SetDirty(director);
        }
    }
}
