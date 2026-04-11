using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BoomHud.Unity.Editor
{
    [FilePath("ProjectSettings/BoomHudSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class BoomHudProjectSettings : ScriptableSingleton<BoomHudProjectSettings>
    {
        internal const string SettingsPath = "Project/GiantCroissant/BoomHud";
        internal const string DefaultPenSourceRoot = "BoomHud";
        internal const string DefaultMotionSourceRoot = "BoomHud";
        internal const string DefaultUiToolkitGeneratedOutputPath = "Assets/BoomHudGenerated";
        internal const string DefaultUGuiGeneratedOutputPath = "Assets/BoomHudGeneratedUGui";
        internal const string DefaultGenerationRuleAssetPath = "Assets/BoomHudGenerated/Rules/BoomHudGenerationRules.asset";
        internal const string DefaultGenerationRuleJsonPath = "BoomHud/generation-rules.json";
        internal const string DefaultTimelineSceneOutputRoot = "Assets/BoomHudGenerated/TimelineScenes";
        internal const string DefaultTimelineAssetOutputRoot = "Assets/BoomHudGenerated/Timelines";
        internal const string DefaultTimelinePanelSettingsAssetPath = "Assets/BoomHudGenerated/Settings/BoomHudPanelSettings.asset";

        [SerializeField] private string _penSourceRoot = DefaultPenSourceRoot;
        [SerializeField] private string _motionSourceRoot = DefaultMotionSourceRoot;
        [SerializeField] private string _uiToolkitGeneratedOutputPath = DefaultUiToolkitGeneratedOutputPath;
        [SerializeField] private string _uGuiGeneratedOutputPath = DefaultUGuiGeneratedOutputPath;
        [SerializeField] private string _generationRuleAssetPath = DefaultGenerationRuleAssetPath;
        [SerializeField] private string _generationRuleJsonPath = DefaultGenerationRuleJsonPath;
        [SerializeField] private string _timelineSceneOutputRoot = DefaultTimelineSceneOutputRoot;
        [SerializeField] private string _timelineAssetOutputRoot = DefaultTimelineAssetOutputRoot;
        [SerializeField] private string _timelinePanelSettingsAssetPath = DefaultTimelinePanelSettingsAssetPath;

        internal static BoomHudProjectSettings Current => instance;

        internal string PenSourceRoot => NormalizeProjectRelativePath(_penSourceRoot, DefaultPenSourceRoot);

        internal string MotionSourceRoot => NormalizeProjectRelativePath(_motionSourceRoot, DefaultMotionSourceRoot);

        internal string UiToolkitGeneratedOutputPath => NormalizeAssetPath(_uiToolkitGeneratedOutputPath, DefaultUiToolkitGeneratedOutputPath);

        internal string UGuiGeneratedOutputPath => NormalizeAssetPath(_uGuiGeneratedOutputPath, DefaultUGuiGeneratedOutputPath);

        internal string GenerationRuleAssetPath => NormalizeAssetPath(_generationRuleAssetPath, DefaultGenerationRuleAssetPath);

        internal string GenerationRuleJsonPath => NormalizeProjectRelativePath(_generationRuleJsonPath, DefaultGenerationRuleJsonPath);

        internal string TimelineSceneOutputRoot => NormalizeAssetPath(_timelineSceneOutputRoot, DefaultTimelineSceneOutputRoot);

        internal string TimelineAssetOutputRoot => NormalizeAssetPath(_timelineAssetOutputRoot, DefaultTimelineAssetOutputRoot);

        internal string TimelinePanelSettingsAssetPath => NormalizeAssetPath(_timelinePanelSettingsAssetPath, DefaultTimelinePanelSettingsAssetPath);

        internal void SaveSettings()
        {
            _penSourceRoot = PenSourceRoot;
            _motionSourceRoot = MotionSourceRoot;
            _uiToolkitGeneratedOutputPath = UiToolkitGeneratedOutputPath;
            _uGuiGeneratedOutputPath = UGuiGeneratedOutputPath;
            _generationRuleAssetPath = GenerationRuleAssetPath;
            _generationRuleJsonPath = GenerationRuleJsonPath;
            _timelineSceneOutputRoot = TimelineSceneOutputRoot;
            _timelineAssetOutputRoot = TimelineAssetOutputRoot;
            _timelinePanelSettingsAssetPath = TimelinePanelSettingsAssetPath;
            Save(true);
        }

        internal void ResetToDefaults()
        {
            _penSourceRoot = DefaultPenSourceRoot;
            _motionSourceRoot = DefaultMotionSourceRoot;
            _uiToolkitGeneratedOutputPath = DefaultUiToolkitGeneratedOutputPath;
            _uGuiGeneratedOutputPath = DefaultUGuiGeneratedOutputPath;
            _generationRuleAssetPath = DefaultGenerationRuleAssetPath;
            _generationRuleJsonPath = DefaultGenerationRuleJsonPath;
            _timelineSceneOutputRoot = DefaultTimelineSceneOutputRoot;
            _timelineAssetOutputRoot = DefaultTimelineAssetOutputRoot;
            _timelinePanelSettingsAssetPath = DefaultTimelinePanelSettingsAssetPath;
            Save(true);
        }

        internal static bool IsAssetsPath(string path)
            => string.Equals(path, "Assets", System.StringComparison.Ordinal)
                || path.StartsWith("Assets/", System.StringComparison.Ordinal);

        private static string NormalizeProjectRelativePath(string value, string fallback)
        {
            var normalized = (string.IsNullOrWhiteSpace(value) ? fallback : value).Replace('\\', '/').Trim();
            return normalized.TrimEnd('/');
        }

        private static string NormalizeAssetPath(string value, string fallback)
        {
            var normalized = NormalizeProjectRelativePath(value, fallback);
            return IsAssetsPath(normalized) ? normalized : fallback;
        }
    }

    internal static class BoomHudProjectSettingsProvider
    {
        private static SerializedObject? _settingsObject;

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(BoomHudProjectSettings.SettingsPath, SettingsScope.Project)
            {
                label = "BoomHud",
                guiHandler = DrawGui,
                keywords = new HashSet<string>
                {
                    "BoomHud",
                    "pen",
                    "motion",
                    "UIToolkit",
                    "uGUI",
                    "rules",
                    "timeline",
                    "PanelSettings"
                }
            };
        }

        private static void DrawGui(string searchContext)
        {
            var settings = BoomHudProjectSettings.Current;
            _settingsObject ??= new SerializedObject(settings);
            _settingsObject.Update();

            EditorGUILayout.LabelField("Source Inputs", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _settingsObject.FindProperty("_penSourceRoot"),
                new GUIContent("Pen Source Root", "Project-relative or absolute folder that contains .pen files."));
            EditorGUILayout.PropertyField(
                _settingsObject.FindProperty("_motionSourceRoot"),
                new GUIContent("Motion Source Root", "Project-relative or absolute folder that contains motion JSON files."));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated UI", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _settingsObject.FindProperty("_uiToolkitGeneratedOutputPath"),
                new GUIContent("UIToolkit Output", "Asset path for generated UI Toolkit assets."));
            EditorGUILayout.PropertyField(
                _settingsObject.FindProperty("_uGuiGeneratedOutputPath"),
                new GUIContent("uGUI Output", "Asset path for generated uGUI code and related assets."));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generation Rules", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _settingsObject.FindProperty("_generationRuleAssetPath"),
                new GUIContent("Rule Asset", "Asset path for the editor-authored ScriptableObject rule set."));
            EditorGUILayout.PropertyField(
                _settingsObject.FindProperty("_generationRuleJsonPath"),
                new GUIContent("Rule JSON Path", "Project-relative or absolute JSON path exported for CLI generation."));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export Rule JSON"))
            {
                _settingsObject.ApplyModifiedPropertiesWithoutUndo();
                settings.SaveSettings();
                BoomHudGenerationRuleSetUtility.ExportDefaultRuleSetJson();
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Import Rule JSON"))
            {
                _settingsObject.ApplyModifiedPropertiesWithoutUndo();
                settings.SaveSettings();
                BoomHudGenerationRuleSetUtility.ImportDefaultRuleSetJson();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _settingsObject.FindProperty("_timelineSceneOutputRoot"),
                new GUIContent("Scene Output Root", "Asset path root for generated Timeline scenes."));
            EditorGUILayout.PropertyField(
                _settingsObject.FindProperty("_timelineAssetOutputRoot"),
                new GUIContent("Playable Output Root", "Asset path root for generated Timeline playable assets."));
            EditorGUILayout.PropertyField(
                _settingsObject.FindProperty("_timelinePanelSettingsAssetPath"),
                new GUIContent("PanelSettings Asset", "Asset path for the shared PanelSettings used by Timeline scene generation."));

            EditorGUILayout.Space();
            DrawValidationHelp(_settingsObject);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Defaults"))
            {
                settings.ResetToDefaults();
                _settingsObject = new SerializedObject(settings);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Save"))
            {
                _settingsObject.ApplyModifiedPropertiesWithoutUndo();
                settings.SaveSettings();
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();

            if (_settingsObject.ApplyModifiedProperties())
            {
                settings.SaveSettings();
            }
        }

        private static void DrawValidationHelp(SerializedObject settingsObject)
        {
            var uiToolkitPath = settingsObject.FindProperty("_uiToolkitGeneratedOutputPath").stringValue;
            if (!BoomHudProjectSettings.IsAssetsPath(uiToolkitPath))
            {
                EditorGUILayout.HelpBox("UIToolkit Output must be an asset path under Assets/.", MessageType.Error);
            }

            var uGuiPath = settingsObject.FindProperty("_uGuiGeneratedOutputPath").stringValue;
            if (!BoomHudProjectSettings.IsAssetsPath(uGuiPath))
            {
                EditorGUILayout.HelpBox("uGUI Output must be an asset path under Assets/.", MessageType.Error);
            }

            var ruleAssetPath = settingsObject.FindProperty("_generationRuleAssetPath").stringValue;
            if (!BoomHudProjectSettings.IsAssetsPath(ruleAssetPath))
            {
                EditorGUILayout.HelpBox("Rule Asset must be an asset path under Assets/.", MessageType.Error);
            }

            var scenePath = settingsObject.FindProperty("_timelineSceneOutputRoot").stringValue;
            if (!BoomHudProjectSettings.IsAssetsPath(scenePath))
            {
                EditorGUILayout.HelpBox("Scene Output Root must be an asset path under Assets/.", MessageType.Error);
            }

            var timelinePath = settingsObject.FindProperty("_timelineAssetOutputRoot").stringValue;
            if (!BoomHudProjectSettings.IsAssetsPath(timelinePath))
            {
                EditorGUILayout.HelpBox("Playable Output Root must be an asset path under Assets/.", MessageType.Error);
            }

            var panelSettingsPath = settingsObject.FindProperty("_timelinePanelSettingsAssetPath").stringValue;
            if (!BoomHudProjectSettings.IsAssetsPath(panelSettingsPath))
            {
                EditorGUILayout.HelpBox("PanelSettings Asset must be an asset path under Assets/.", MessageType.Error);
            }
        }
    }
}
