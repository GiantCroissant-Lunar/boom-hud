using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BoomHud.Unity.Runtime;
using BoomHud.Unity.Timeline;
using BoomHud.Unity.UGUI;
using BoomHud.Unity.UIToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace BoomHud.Unity.Editor
{
    public enum BoomHudMotionTimelineFillMode
    {
        None,
        HoldStart,
        HoldEnd,
        HoldBoth
    }

    public sealed class BoomHudMotionTimelineClipSchedule
    {
        public string ClipId { get; set; } = string.Empty;

        public double StartSeconds { get; set; }

        public double? DurationSeconds { get; set; }

        public BoomHudMotionTimelineFillMode FillMode { get; set; } = BoomHudMotionTimelineFillMode.None;

        public string? DisplayName { get; set; }
    }

    public sealed class BoomHudMotionTimelineSceneOptions
    {
        public string? OutputRootDirectory { get; set; }

        public string? SceneDirectory { get; set; }

        public string? TimelineDirectory { get; set; }

        public string? PanelSettingsAssetPath { get; set; }

        public string? SceneName { get; set; }

        public string? TimelineName { get; set; }

        public string? RootObjectName { get; set; }

        public string? DefaultClipId { get; set; }

        public string? SequenceId { get; set; }

        public Color CameraBackgroundColor { get; set; } = new Color(0.04f, 0.04f, 0.05f, 1f);

        public bool OpenSceneAfterCreation { get; set; } = true;

        public IReadOnlyList<BoomHudMotionTimelineClipSchedule>? ClipSchedule { get; set; }
    }

    public sealed class BoomHudMotionTimelineSceneResult
    {
        public string ScenePath { get; set; } = string.Empty;

        public string TimelinePath { get; set; } = string.Empty;

        public string DefaultClipId { get; set; } = string.Empty;

        public IReadOnlyList<string> ClipIds { get; set; } = Array.Empty<string>();

        public Type HostType { get; set; } = typeof(BoomHudViewHost);

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
                    .OfType<BoomHudViewHost>()
                    .Where(static component => component is IBoomHudMotionHost)
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
                var hostComponent = selectedObject.GetComponents<MonoBehaviour>()
                    .OfType<BoomHudViewHost>()
                    .FirstOrDefault(static component => component is IBoomHudMotionHost);
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
                throw new ArgumentException($"Type '{hostType.FullName}' is not a BoomHud motion host.", nameof(hostType));
            }

            options ??= new BoomHudMotionTimelineSceneOptions();
            var settings = BoomHudProjectSettings.Current;
            var descriptor = ResolveDescriptor(hostType, options.DefaultClipId, options.SequenceId);

            var outputRootDirectory = NormalizeAssetPath(
                string.IsNullOrWhiteSpace(options.OutputRootDirectory)
                    ? settings.TimelineSceneOutputRoot
                    : options.OutputRootDirectory!);
            var sceneDirectory = NormalizeAssetPath(options.SceneDirectory ?? $"{outputRootDirectory.TrimEnd('/')}/{descriptor.BaseName}");
            var timelineDirectory = NormalizeAssetPath(options.TimelineDirectory ?? $"{settings.TimelineAssetOutputRoot.TrimEnd('/')}/{descriptor.BaseName}");
            var sceneName = string.IsNullOrWhiteSpace(options.SceneName) ? $"{descriptor.BaseName}MotionTimeline" : options.SceneName!.Trim();
            var timelineName = string.IsNullOrWhiteSpace(options.TimelineName) ? $"{descriptor.BaseName}MotionTimeline" : options.TimelineName!.Trim();
            var rootObjectName = string.IsNullOrWhiteSpace(options.RootObjectName) ? $"{descriptor.BaseName} Motion Timeline" : options.RootObjectName!.Trim();
            var panelSettingsPath = NormalizeAssetPath(options.PanelSettingsAssetPath ?? settings.TimelinePanelSettingsAssetPath);
            var scenePath = NormalizeAssetPath($"{sceneDirectory}/{sceneName}.unity");
            var timelinePath = NormalizeAssetPath($"{timelineDirectory}/{timelineName}.playable");

            EnsureFolderPath(sceneDirectory);
            EnsureFolderPath(timelineDirectory);
            EnsureFolderPath(Path.GetDirectoryName(panelSettingsPath)?.Replace('\\', '/') ?? sceneDirectory);

            var timeline = EnsureTimelineAsset(timelinePath, descriptor, options.ClipSchedule ?? descriptor.SequenceClipSchedule);
            if (descriptor.HostKind == MotionHostKind.UiToolkit)
            {
                var panelSettings = EnsurePanelSettings(panelSettingsPath, options.CameraBackgroundColor);
                var visualTreeAssetPath = AssetDatabase.GetAssetPath(descriptor.VisualTreeAsset!);
                if (string.IsNullOrWhiteSpace(visualTreeAssetPath))
                {
                    throw new InvalidOperationException(
                        $"Could not resolve the asset path for VisualTreeAsset '{descriptor.VisualTreeAsset!.name}'.");
                }

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var rootObject = new GameObject(rootObjectName);
                var document = rootObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = descriptor.VisualTreeAsset;

                var motionHost = (BoomHudViewHost)rootObject.AddComponent(hostType);
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
            }
            else
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var rootObject = new GameObject(rootObjectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var rectTransform = rootObject.GetComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;

                var canvas = rootObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.pixelPerfect = false;
                canvas.sortingOrder = 0;

                var scaler = rootObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                var motionHost = (BoomHudViewHost)rootObject.AddComponent(hostType);
                var previewBootstrap = rootObject.AddComponent<BoomHudMotionPreviewBootstrap>();
                var director = rootObject.AddComponent<PlayableDirector>();
                director.playOnAwake = true;
                director.playableAsset = timeline;
                director.extrapolationMode = DirectorWrapMode.None;

                BindTimelineTrack(timeline, director, motionHost);
                CreateCamera(options.CameraBackgroundColor);

                EditorUtility.SetDirty(rectTransform);
                EditorUtility.SetDirty(canvas);
                EditorUtility.SetDirty(scaler);
                EditorUtility.SetDirty(motionHost);
                ConfigurePreviewBootstrap(previewBootstrap, director, motionHost, descriptor.DefaultClipId);
                EditorUtility.SetDirty(previewBootstrap);
                EditorUtility.SetDirty(director);
                EditorSceneManager.MarkSceneDirty(scene);

                EditorSceneManager.SaveScene(scene, scenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

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
            => type != null
                && typeof(BoomHudViewHost).IsAssignableFrom(type)
                && typeof(IBoomHudMotionHost).IsAssignableFrom(type)
                && !type.IsAbstract;

        private static MotionHostDescriptor ResolveDescriptor(Type hostType, string? preferredClipId, string? preferredSequenceId)
        {
            var baseName = hostType.Name.EndsWith("MotionHost", StringComparison.Ordinal)
                ? hostType.Name[..^"MotionHost".Length]
                : hostType.Name;

            var motionType = ResolveMotionType(hostType, baseName)
                ?? throw new InvalidOperationException($"Could not resolve generated motion type for host '{hostType.FullName}'.");

            var hostKind = ResolveHostKind(hostType);
            var visualTreeAsset = hostKind == MotionHostKind.UiToolkit
                ? ResolveVisualTreeAsset(hostType, baseName)
                    ?? throw new InvalidOperationException($"Could not resolve generated VisualTreeAsset for host '{hostType.FullName}'.")
                : null;

            var clipIds = ResolveClipIds(motionType);
            var defaultClipId = ResolveDefaultClipId(motionType, clipIds, preferredClipId);

            return new MotionHostDescriptor
            {
                BaseName = baseName,
                HostKind = hostKind,
                MotionType = motionType,
                VisualTreeAsset = visualTreeAsset,
                ClipIds = clipIds,
                DefaultClipId = defaultClipId,
                SequenceClipSchedule = ResolveSequenceClipSchedule(motionType, clipIds, preferredSequenceId)
            };
        }

        private static MotionHostKind ResolveHostKind(Type hostType)
            => typeof(BoomHudUiToolkitMotionHost).IsAssignableFrom(hostType)
                ? MotionHostKind.UiToolkit
                : typeof(BoomHudUguiMotionHost).IsAssignableFrom(hostType)
                    ? MotionHostKind.UGui
                    : throw new InvalidOperationException($"Type '{hostType.FullName}' is not a supported BoomHud motion host.");

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
            BoomHudViewHost motionHost,
            string defaultClipId)
        {
            previewBootstrap.Configure(director, motionHost, defaultClipId);
        }

        private static TimelineAsset EnsureTimelineAsset(
            string timelinePath,
            MotionHostDescriptor descriptor,
            IReadOnlyList<BoomHudMotionTimelineClipSchedule>? clipSchedule)
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

            if (clipSchedule != null && clipSchedule.Count > 0)
            {
                CreateScheduledTracks(timeline, descriptor, clipSchedule);
            }
            else
            {
                CreateSequentialTrack(timeline, descriptor);
            }

            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static void CreateSequentialTrack(TimelineAsset timeline, MotionHostDescriptor descriptor)
        {
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
        }

        private static void CreateScheduledTracks(
            TimelineAsset timeline,
            MotionHostDescriptor descriptor,
            IReadOnlyList<BoomHudMotionTimelineClipSchedule> clipSchedule)
        {
            var resolvedSchedule = clipSchedule
                .Select(item => ResolveScheduledClip(item, descriptor))
                .ToArray();

            var sequenceDurationSeconds = resolvedSchedule
                .Select(item => item.StartSeconds + item.ActiveDurationSeconds)
                .DefaultIfEmpty(0.1d)
                .Max();

            for (var index = 0; index < resolvedSchedule.Length; index++)
            {
                var scheduledClip = resolvedSchedule[index];
                var timing = ResolveScheduledClipTiming(scheduledClip, sequenceDurationSeconds);
                var trackName = string.IsNullOrWhiteSpace(scheduledClip.DisplayName)
                    ? $"{index + 1:00} {scheduledClip.ClipId}"
                    : scheduledClip.DisplayName!;
                var track = timeline.CreateTrack<BoomHudMotionTrack>(null, trackName);
                var clip = track.CreateClip<BoomHudMotionPlayableAsset>();
                clip.displayName = scheduledClip.ClipId;
                clip.start = timing.TimelineStartSeconds;
                clip.duration = timing.TimelineDurationSeconds;

                if (clip.asset is BoomHudMotionPlayableAsset motionClip)
                {
                    motionClip.ClipId = scheduledClip.ClipId;
                    motionClip.TimeOffsetSeconds = timing.TimeOffsetSeconds;
                    EditorUtility.SetDirty(motionClip);
                }
            }
        }

        private static ResolvedScheduledClip ResolveScheduledClip(
            BoomHudMotionTimelineClipSchedule clipSchedule,
            MotionHostDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(clipSchedule.ClipId))
            {
                throw new InvalidOperationException("Timeline clip schedule items must declare a clip id.");
            }

            if (!descriptor.ClipIds.Contains(clipSchedule.ClipId, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Motion clip '{clipSchedule.ClipId}' is not available on '{descriptor.MotionType.FullName}'.");
            }

            var clipDurationSeconds = Math.Max(
                0.1d,
                clipSchedule.DurationSeconds ?? GetClipDurationSeconds(descriptor.MotionType, clipSchedule.ClipId));

            return new ResolvedScheduledClip
            {
                ClipId = clipSchedule.ClipId,
                StartSeconds = Math.Max(0d, clipSchedule.StartSeconds),
                ActiveDurationSeconds = clipDurationSeconds,
                FillMode = clipSchedule.FillMode,
                DisplayName = clipSchedule.DisplayName
            };
        }

        private static ScheduledClipTiming ResolveScheduledClipTiming(
            ResolvedScheduledClip clipSchedule,
            double sequenceDurationSeconds)
        {
            return clipSchedule.FillMode switch
            {
                BoomHudMotionTimelineFillMode.HoldBoth => new ScheduledClipTiming
                {
                    TimelineStartSeconds = 0d,
                    TimelineDurationSeconds = Math.Max(0.1d, sequenceDurationSeconds),
                    TimeOffsetSeconds = -clipSchedule.StartSeconds
                },
                BoomHudMotionTimelineFillMode.HoldEnd => new ScheduledClipTiming
                {
                    TimelineStartSeconds = clipSchedule.StartSeconds,
                    TimelineDurationSeconds = Math.Max(0.1d, sequenceDurationSeconds - clipSchedule.StartSeconds),
                    TimeOffsetSeconds = 0d
                },
                BoomHudMotionTimelineFillMode.HoldStart => new ScheduledClipTiming
                {
                    TimelineStartSeconds = 0d,
                    TimelineDurationSeconds = Math.Max(0.1d, clipSchedule.StartSeconds + clipSchedule.ActiveDurationSeconds),
                    TimeOffsetSeconds = -clipSchedule.StartSeconds
                },
                _ => new ScheduledClipTiming
                {
                    TimelineStartSeconds = clipSchedule.StartSeconds,
                    TimelineDurationSeconds = clipSchedule.ActiveDurationSeconds,
                    TimeOffsetSeconds = 0d
                }
            };
        }

        private static IReadOnlyList<BoomHudMotionTimelineClipSchedule>? ResolveSequenceClipSchedule(
            Type motionType,
            IReadOnlyList<string> clipIds,
            string? preferredSequenceId)
        {
            var sequenceIds = ResolveSequenceIds(motionType);
            if (sequenceIds.Count == 0)
            {
                return null;
            }

            var sequenceId = ResolveSequenceId(motionType, sequenceIds, preferredSequenceId);
            if (string.IsNullOrWhiteSpace(sequenceId))
            {
                return null;
            }

            var sequenceItemsMethod = motionType.GetMethod("GetSequenceItems", BindingFlags.Public | BindingFlags.Static);
            if (sequenceItemsMethod == null)
            {
                return null;
            }

            if (sequenceItemsMethod.Invoke(null, new object[] { sequenceId! }) is not IEnumerable sequenceItems)
            {
                return null;
            }

            var framesPerSecond = ResolveFramesPerSecond(motionType);
            var clipSchedule = new List<BoomHudMotionTimelineClipSchedule>();

            foreach (var sequenceItem in sequenceItems)
            {
                if (sequenceItem == null)
                {
                    continue;
                }

                var itemType = sequenceItem.GetType();
                var clipId = itemType.GetProperty("ClipId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(sequenceItem) as string;
                if (string.IsNullOrWhiteSpace(clipId) || !clipIds.Contains(clipId, StringComparer.Ordinal))
                {
                    continue;
                }

                var startFrame = Convert.ToInt32(
                    itemType.GetProperty("StartFrame", BindingFlags.Public | BindingFlags.Instance)?.GetValue(sequenceItem) ?? 0,
                    CultureInfo.InvariantCulture);
                var durationFrames = Convert.ToInt32(
                    itemType.GetProperty("DurationFrames", BindingFlags.Public | BindingFlags.Instance)?.GetValue(sequenceItem) ?? 0,
                    CultureInfo.InvariantCulture);
                var fillModeName = itemType.GetProperty("FillMode", BindingFlags.Public | BindingFlags.Instance)?.GetValue(sequenceItem)?.ToString();

                clipSchedule.Add(new BoomHudMotionTimelineClipSchedule
                {
                    ClipId = clipId,
                    StartSeconds = startFrame / (double)framesPerSecond,
                    DurationSeconds = durationFrames > 0 ? durationFrames / (double)framesPerSecond : null,
                    FillMode = ParseTimelineFillMode(fillModeName),
                    DisplayName = $"{startFrame:000}f {clipId}"
                });
            }

            return clipSchedule.Count == 0 ? null : clipSchedule;
        }

        private static IReadOnlyList<string> ResolveSequenceIds(Type motionType)
        {
            var sequenceIdsField = motionType.GetField("SequenceIds", BindingFlags.Public | BindingFlags.Static);
            if (sequenceIdsField?.GetValue(null) is IEnumerable<string> sequenceIds)
            {
                var resolved = sequenceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
                if (resolved.Length > 0)
                {
                    return resolved;
                }
            }

            var defaultSequenceId = motionType.GetField("DefaultSequenceId", BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() as string;
            return string.IsNullOrWhiteSpace(defaultSequenceId)
                ? Array.Empty<string>()
                : new[] { defaultSequenceId! };
        }

        private static string? ResolveSequenceId(Type motionType, IReadOnlyList<string> sequenceIds, string? preferredSequenceId)
        {
            if (!string.IsNullOrWhiteSpace(preferredSequenceId) && sequenceIds.Contains(preferredSequenceId, StringComparer.Ordinal))
            {
                return preferredSequenceId;
            }

            var defaultSequenceId = motionType.GetField("DefaultSequenceId", BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() as string;
            if (!string.IsNullOrWhiteSpace(defaultSequenceId))
            {
                return defaultSequenceId;
            }

            return sequenceIds.FirstOrDefault();
        }

        private static int ResolveFramesPerSecond(Type motionType)
        {
            var framesPerSecondField = motionType.GetField("FramesPerSecond", BindingFlags.Public | BindingFlags.Static);
            if (framesPerSecondField?.GetRawConstantValue() is int framesPerSecond)
            {
                return Math.Max(1, framesPerSecond);
            }

            return 30;
        }

        private static BoomHudMotionTimelineFillMode ParseTimelineFillMode(string? fillModeName)
            => fillModeName switch
            {
                nameof(BoomHudMotionTimelineFillMode.HoldStart) => BoomHudMotionTimelineFillMode.HoldStart,
                nameof(BoomHudMotionTimelineFillMode.HoldEnd) => BoomHudMotionTimelineFillMode.HoldEnd,
                nameof(BoomHudMotionTimelineFillMode.HoldBoth) => BoomHudMotionTimelineFillMode.HoldBoth,
                _ => BoomHudMotionTimelineFillMode.None
            };

        private static double GetClipDurationSeconds(Type motionType, string clipId)
        {
            var method = motionType.GetMethod("GetClipDurationSeconds", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Motion type '{motionType.FullName}' does not expose GetClipDurationSeconds(string).");

            var result = method.Invoke(null, new object[] { clipId });
            return Convert.ToDouble(result ?? 0d, CultureInfo.InvariantCulture);
        }

        private static void BindTimelineTrack(TimelineAsset timeline, PlayableDirector director, BoomHudViewHost motionHost)
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

        private enum MotionHostKind
        {
            UiToolkit,
            UGui
        }

        private sealed class MotionHostDescriptor
        {
            public string BaseName { get; set; } = string.Empty;

            public MotionHostKind HostKind { get; set; }

            public Type MotionType { get; set; } = typeof(object);

            public VisualTreeAsset? VisualTreeAsset { get; set; }

            public IReadOnlyList<string> ClipIds { get; set; } = Array.Empty<string>();

            public string DefaultClipId { get; set; } = string.Empty;

            public IReadOnlyList<BoomHudMotionTimelineClipSchedule>? SequenceClipSchedule { get; set; }
        }

        private sealed class ResolvedScheduledClip
        {
            public string ClipId { get; set; } = string.Empty;

            public double StartSeconds { get; set; }

            public double ActiveDurationSeconds { get; set; }

            public BoomHudMotionTimelineFillMode FillMode { get; set; }

            public string? DisplayName { get; set; }
        }

        private sealed class ScheduledClipTiming
        {
            public double TimelineStartSeconds { get; set; }

            public double TimelineDurationSeconds { get; set; }

            public double TimeOffsetSeconds { get; set; }
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
