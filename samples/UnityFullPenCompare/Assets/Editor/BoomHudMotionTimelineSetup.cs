using Generated.Hud;
using BoomHud.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace BoomHud.Compare.Editor
{
    public static class BoomHudMotionTimelineSetup
    {
        private const string SceneDirectory = "Assets/BoomHudCompare/Scenes";
        private const string TimelineDirectory = "Assets/BoomHudCompare/Timelines";

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
            BoomHudMotionTimelineSceneBuilder.Create(
                typeof(DebugOverlayMotionHost),
                new BoomHudMotionTimelineSceneOptions
                {
                    SceneDirectory = SceneDirectory,
                    TimelineDirectory = TimelineDirectory,
                    SceneName = "DebugOverlayMotionTimeline",
                    TimelineName = "DebugOverlayMotionTimeline",
                    PanelSettingsAssetPath = BoomHudCompareProjectSetup.PanelSettingsPath,
                    RootObjectName = "BoomHud Debug Overlay Motion Timeline",
                    CameraBackgroundColor = new Color(0.04f, 0.04f, 0.05f, 1f)
                });
        }
    }
}
