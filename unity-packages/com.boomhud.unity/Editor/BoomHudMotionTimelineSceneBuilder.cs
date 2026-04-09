using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BoomHud.Unity.Timeline;
using BoomHud.Unity.UIToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace BoomHud.Unity.Editor
{
    public sealed class BoomHudMotionTimelineSceneOptions
    {
        public string OutputRootDirectory { get; set; } = "Assets/BoomHudGenerated/TimelineScenes";

        public string? SceneDirectory { get; set; }

        public string? TimelineDirectory { get; set; }

        public string? PanelSettingsAssetPath { get; set; }

        public string? SceneName { get; set; }

        public string? TimelineName { get; set; }

        public string? RootObjectName { get; set; }

        public string? DefaultClipId { get; set; }

        public Color CameraBackgroundColor { get; set; } = new Color(0.04f, 0.04f, 0.05f, 1f);

        public bool OpenSceneAfterCreation { get; set; } = true;
    }

    public sealed class BoomHudMotionTimelineSceneResult
    {
        public string ScenePath { get; set; } = string.Empty;

        public string TimelinePath { get; set; } = string.Empty;

        public string DefaultClipId { get; set; } = string.Empty;

        public IReadOnlyList<string> ClipIds { get; set; } = Array.Empty<string>();

        public Type HostType { get; set; } = typeof(BoomHudUiToolkitMotionHost);

        public Type MotionType { get; set; } = typeof(object);
    }

    public static class BoomHudMotionTimelineSceneBuilder
    {
        public static bool TryResolveSelectedMotionHostType(out Type? hostType, out string? error)
        {
            hostType = null;
            error = null;

            if (Selection.activeGameObject != null)
            {
                var hostComponents = Selection.activeGameObject
                    .GetComponents<MonoBehaviour>()
                    .OfType<BoomHudUiToolkitMotionHost>()
                    .ToArray();

                if (hostComponents.Length == 1)
                {
                    hostType = hostComponents[0].GetType();
                    return true;
                }

                if (hostComponents.Length > 1)
                {
                    error = "The selected GameObject contains multiple BoomHud motion hosts. Select a specific component script instead.";
                    return false;
                }
            }

            if (Selection.activeObject is MonoScript monoScript)
            {
                var scriptType = monoScript.GetClass();
                if (IsMotionHostType(scriptType))
                {
                    hostType = scriptType;
                    return true;
                }
            }

            if (Selection.activeObject is GameObject selectedObject)
            {
                var hostComponent = selectedObject.GetComponents<MonoBehaviour>().OfType<BoomHudUiToolkitMotionHost>().FirstOrDefault();
                if (hostComponent != null)
                {
                    hostType = hostComponent.GetType();
                    return true;
                }
            }

            error = "Select a generated BoomHud motion host script or a GameObject with a BoomHud motion host component.";
            return false;
        }

        public static BoomHudMotionTimelineSceneResult CreateForSelectedHost(BoomHudMotionTimelineSceneOptions? options = null)
        {
            if (!TryResolveSelectedMotionHostType(out var hostType, out var error) || hostType == null)
            {
                throw new InvalidOperationException(error ?? "Could not resolve a BoomHud motion host from the current selection.");
            }

            return Create(hostType, options);
        }

        public static BoomHudMotionTimelineSceneResult Create(Type hostType, BoomHudMotionTimelineSceneOptions? options = null)
        {
            if (!IsMotionHostType(hostType))
            {
                throw new ArgumentException($"Type '{hostType.FullName}' is not a BoomHud UI Toolkit motion host.", nameof(hostType));
            }

            options ??= new BoomHudMotionTimelineSceneOptions();
            var descriptor = ResolveDescriptor(hostType, options.DefaultClipId);

            var sceneDirectory = NormalizeAssetPath(options.SceneDirectory ?? $"{options.OutputRootDirectory.TrimEnd('/')}/{descriptor.BaseName}");
            var timelineDirectory = NormalizeAssetPath(options.TimelineDirectory ?? sceneDirectory);
            var sceneName = string.IsNullOrWhiteSpace(options.SceneName) ? $"{descriptor.BaseName}MotionTimeline" : options.SceneName!.Trim();
            var timelineName = string.IsNullOrWhiteSpace(options.TimelineName) ? $"{descriptor.BaseName}MotionTimeline" : options.TimelineName!.Trim();
            var rootObjectName = string.IsNullOrWhiteSpace(options.RootObjectName) ? $"{descriptor.BaseName} Motion Timeline" : options.RootObjectName!.Trim();
            var panelSettingsPath = NormalizeAssetPath(options.PanelSettingsAssetPath ?? $"{sceneDirectory}/BoomHudPanelSettings.asset");
            var scenePath = NormalizeAssetPath($"{sceneDirectory}/{sceneName}.unity");
            var timelinePath = NormalizeAssetPath($"{timelineDirectory}/{timelineName}.playable");

            EnsureFolderPath(sceneDirectory);
            EnsureFolderPath(timelineDirectory);
            EnsureFolderPath(Path.GetDirectoryName(panelSettingsPath)?.Replace('\\', '/') ?? sceneDirectory);

            var panelSettings = EnsurePanelSettings(panelSettingsPath, options.CameraBackgroundColor);
            var timeline = EnsureTimelineAsset(timelinePath, descriptor);
            var visualTreeAssetPath = AssetDatabase.GetAssetPath(descriptor.VisualTreeAsset);
            if (string.IsNullOrWhiteSpace(visualTreeAssetPath))
            {
                throw new InvalidOperationException(
                    $"Could not resolve the asset path for VisualTreeAsset '{descriptor.VisualTreeAsset.name}'.");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rootObject = new GameObject(rootObjectName);
            var document = rootObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = descriptor.VisualTreeAsset;

            var motionHost = (BoomHudUiToolkitMotionHost)rootObject.AddComponent(hostType);
            var previewBootstrap = rootObject.AddComponent<BoomHudMotionPreviewBootstrap>();
            var director = rootObject.AddComponent<PlayableDirector>();
            director.playOnAwake = true;
            director.playableAsset = timeline;
            director.extrapolationMode = DirectorWrapMode.None;

            BindTimelineTrack(timeline, director, motionHost);
            CreateCamera(options.CameraBackgroundColor);

            EditorUtility.SetDirty(document);
            EditorUtility.SetDirty(motionHost);
            ConfigurePreviewBootstrap(previewBootstrap, director, motionHost, descriptor.DefaultClipId);
            EditorUtility.SetDirty(previewBootstrap);
            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(scene);

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var reopenedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ReapplyDocumentBindings(
                panelSettingsPath,
                visualTreeAssetPath);
            EditorSceneManager.MarkSceneDirty(reopenedScene);
            EditorSceneManager.SaveScene(reopenedScene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (options.OpenSceneAfterCreation)
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset != null)
            {
                EditorGUIUtility.PingObject(sceneAsset);
            }

            return new BoomHudMotionTimelineSceneResult
            {
                ScenePath = scenePath,
                TimelinePath = timelinePath,
                DefaultClipId = descriptor.DefaultClipId,
                ClipIds = descriptor.ClipIds,
                HostType = hostType,
                MotionType = descriptor.MotionType
            };
        }

        private static bool IsMotionHostType(Type? type)
            => type != null && typeof(BoomHudUiToolkitMotionHost).IsAssignableFrom(type) && !type.IsAbstract;

        private static MotionHostDescriptor ResolveDescriptor(Type hostType, string? preferredClipId)
        {
            var baseName = hostType.Name.EndsWith("MotionHost", StringComparison.Ordinal)
                ? hostType.Name[..^"MotionHost".Length]
                : hostType.Name;

            var motionType = ResolveMotionType(hostType, baseName)
                ?? throw new InvalidOperationException($"Could not resolve generated motion type for host '{hostType.FullName}'.");

            var visualTreeAsset = ResolveVisualTreeAsset(hostType, baseName)
                ?? throw new InvalidOperationException($"Could not resolve generated VisualTreeAsset for host '{hostType.FullName}'.");

            var clipIds = ResolveClipIds(motionType);
            var defaultClipId = ResolveDefaultClipId(motionType, clipIds, preferredClipId);

            return new MotionHostDescriptor
            {
                BaseName = baseName,
                MotionType = motionType,
                VisualTreeAsset = visualTreeAsset,
                ClipIds = clipIds,
                DefaultClipId = defaultClipId
            };
        }

        private static Type? ResolveMotionType(Type hostType, string baseName)
        {
            var candidateNames = new[]
            {
                $"{hostType.Namespace}.{baseName}Motion",
                hostType.FullName?.Replace("MotionHost", "Motion", StringComparison.Ordinal)
            };

            foreach (var candidateName in candidateNames.Where(name => !string.IsNullOrWhiteSpace(name)))
            {
                var resolved = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(candidateName!, throwOnError: false))
                    .FirstOrDefault(type => type != null);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            return null;
        }

        private static IReadOnlyList<string> ResolveClipIds(Type motionType)
        {
            var clipIdsField = motionType.GetField("ClipIds", BindingFlags.Public | BindingFlags.Static);
            if (clipIdsField?.GetValue(null) is IEnumerable<string> clipIds)
            {
                var resolved = clipIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
                if (resolved.Length > 0)
                {
                    return resolved;
                }
            }

            var defaultClipId = motionType.GetField("DefaultClipId", BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() as string;
            return string.IsNullOrWhiteSpace(defaultClipId)
                ? Array.Empty<string>()
                : new[] { defaultClipId! };
        }

        private static string ResolveDefaultClipId(Type motionType, IReadOnlyList<string> clipIds, string? preferredClipId)
        {
            if (!string.IsNullOrWhiteSpace(preferredClipId) && clipIds.Contains(preferredClipId, StringComparer.Ordinal))
            {
                return preferredClipId;
            }

            var defaultClipId = motionType.GetField("DefaultClipId", BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() as string;
            if (!string.IsNullOrWhiteSpace(defaultClipId))
            {
                return defaultClipId!;
            }

            return clipIds.FirstOrDefault() ?? "intro";
        }

        private static VisualTreeAsset? ResolveVisualTreeAsset(Type hostType, string baseName)
        {
            var resourcePath = hostType.GetField("VisualTreeResourcePath", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetRawConstantValue() as string;
            var expectedFileName = string.IsNullOrWhiteSpace(resourcePath)
                ? $"{baseName}View"
                : Path.GetFileName(resourcePath);

            var candidatePaths = new List<string>();
            if (!string.IsNullOrWhiteSpace(resourcePath))
            {
                candidatePaths.Add(NormalizeAssetPath($"Assets/Resources/{resourcePath}.uxml"));
            }

            candidatePaths.AddRange(
                AssetDatabase.FindAssets($"{expectedFileName} t:VisualTreeAsset")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), expectedFileName, StringComparison.Ordinal))
                    .OrderBy(path => path.Contains("/Resources/", StringComparison.OrdinalIgnoreCase) ? 0 : 1));

            foreach (var candidatePath in candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(candidatePath);
                if (visualTree != null)
                {
                    return visualTree;
                }
            }

            return null;
        }

        private static PanelSettings EnsurePanelSettings(string panelSettingsPath, Color clearColor)
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath);
            var created = false;
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, panelSettingsPath);
                created = true;
            }

            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.referenceDpi = 96f;
            panelSettings.fallbackDpi = 96f;
            panelSettings.clearColor = true;
            panelSettings.colorClearValue = clearColor;
            EditorUtility.SetDirty(panelSettings);
            AssetDatabase.SaveAssets();

            if (created)
            {
                AssetDatabase.ImportAsset(panelSettingsPath, ImportAssetOptions.ForceUpdate);
                panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath)
                    ?? throw new InvalidOperationException($"Could not reload PanelSettings asset at '{panelSettingsPath}'.");
            }

            return panelSettings;
        }

        private static void ConfigurePreviewBootstrap(
            BoomHudMotionPreviewBootstrap previewBootstrap,
            PlayableDirector director,
            BoomHudUiToolkitMotionHost motionHost,
            string defaultClipId)
        {
            previewBootstrap.Configure(director, motionHost, defaultClipId);
        }

        private static TimelineAsset EnsureTimelineAsset(string timelinePath, MotionHostDescriptor descriptor)
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, timelinePath);
            }

            foreach (var track in timeline.GetOutputTracks().ToArray())
            {
                timeline.DeleteTrack(track);
            }

            var motionTrack = timeline.CreateTrack<BoomHudMotionTrack>(null, "HUD Motion");
            var currentStart = 0d;

            foreach (var clipId in descriptor.ClipIds.DefaultIfEmpty(descriptor.DefaultClipId))
            {
                var clipDuration = Math.Max(0.1d, GetClipDurationSeconds(descriptor.MotionType, clipId));
                var clip = motionTrack.CreateClip<BoomHudMotionPlayableAsset>();
                clip.displayName = clipId;
                clip.start = currentStart;
                clip.duration = clipDuration;

                if (clip.asset is BoomHudMotionPlayableAsset motionClip)
                {
                    motionClip.ClipId = clipId;
                    motionClip.TimeOffsetSeconds = 0d;
                    EditorUtility.SetDirty(motionClip);
                }

                currentStart += clipDuration;
            }

            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static double GetClipDurationSeconds(Type motionType, string clipId)
        {
            var method = motionType.GetMethod("GetClipDurationSeconds", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Motion type '{motionType.FullName}' does not expose GetClipDurationSeconds(string).");

            var result = method.Invoke(null, new object[] { clipId });
            return Convert.ToDouble(result ?? 0d, CultureInfo.InvariantCulture);
        }

        private static void BindTimelineTrack(TimelineAsset timeline, PlayableDirector director, BoomHudUiToolkitMotionHost motionHost)
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

        private static void CreateCamera(Color backgroundColor)
        {
            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            camera.tag = "MainCamera";
        }

        private static void ReapplyDocumentBindings(string panelSettingsPath, string visualTreeAssetPath)
        {
            var document = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
            if (document == null)
            {
                throw new InvalidOperationException("The generated Timeline scene is missing its UIDocument after reopen.");
            }

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath)
                ?? throw new InvalidOperationException($"Could not reload PanelSettings asset at '{panelSettingsPath}'.");
            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(visualTreeAssetPath)
                ?? throw new InvalidOperationException($"Could not reload VisualTreeAsset at '{visualTreeAssetPath}'.");

            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTreeAsset;
            EditorUtility.SetDirty(document);
        }

        private static void EnsureFolderPath(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var normalizedPath = NormalizeAssetPath(folderPath);
            var parent = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
            var name = Path.GetFileName(normalizedPath);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            EnsureFolderPath(parent);
            if (!AssetDatabase.IsValidFolder(normalizedPath))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static string NormalizeAssetPath(string path)
            => path.Replace('\\', '/').TrimEnd('/');

        private sealed class MotionHostDescriptor
        {
            public string BaseName { get; set; } = string.Empty;

            public Type MotionType { get; set; } = typeof(object);

            public VisualTreeAsset VisualTreeAsset { get; set; } = null!;

            public IReadOnlyList<string> ClipIds { get; set; } = Array.Empty<string>();

            public string DefaultClipId { get; set; } = string.Empty;
        }
    }

    internal static class BoomHudMotionTimelineSceneMenu
    {
        [MenuItem("Tools/BoomHud/Create Timeline Scene From Selected Motion Host", priority = 140)]
        private static void CreateFromSelection()
        {
            try
            {
                var result = BoomHudMotionTimelineSceneBuilder.CreateForSelectedHost();
                Debug.Log(
                    $"BoomHud Timeline scene ready for {result.HostType.FullName} at {result.ScenePath} using clips [{string.Join(", ", result.ClipIds)}].");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("BoomHud Timeline Scene", exception.Message, "OK");
            }
        }

        [MenuItem("Tools/BoomHud/Create Timeline Scene From Selected Motion Host", true)]
        private static bool ValidateCreateFromSelection()
            => BoomHudMotionTimelineSceneBuilder.TryResolveSelectedMotionHostType(out _, out _);
    }
}
