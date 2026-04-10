using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using BoomHud.Compare;
using BoomHud.Unity.Runtime;
using BoomHud.Unity.UIToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace BoomHud.Compare.Editor
{
    public static class BoomHudFidelityCapture
    {
        private const string MenuPath = "Tools/BoomHud/Capture Fidelity Artifacts";
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        [MenuItem(MenuPath, priority = 104)]
        public static void CaptureFromMenu()
        {
            var defaultManifestPath = ResolveRepoRelativePath("fidelity/pen-remotion-unity.fullpen.json");
            CaptureManifest(defaultManifestPath);
        }

        public static void CaptureFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var manifestPath = GetArgumentValue(args, "--manifest");
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                throw new InvalidOperationException("Missing required '--manifest' command-line argument.");
            }

            CaptureManifest(manifestPath);
        }

        public static void CaptureManifestAtPath(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                throw new ArgumentException("Manifest path is required.", nameof(manifestPath));
            }

            CaptureManifest(manifestPath);
        }

        private static void CaptureManifest(string manifestPath)
        {
            var manifestAbsolutePath = Path.GetFullPath(manifestPath);
            if (!File.Exists(manifestAbsolutePath))
            {
                throw new FileNotFoundException("Could not find fidelity manifest.", manifestAbsolutePath);
            }

            var manifestJson = File.ReadAllText(manifestAbsolutePath);
            var manifest = JsonUtility.FromJson<FidelityManifest>(manifestJson);
            if (manifest == null)
            {
                throw new InvalidOperationException($"Could not parse fidelity manifest '{manifestAbsolutePath}'.");
            }

            var artifactsRoot = ResolveManifestPath(manifest.artifactsRoot);
            Directory.CreateDirectory(artifactsRoot);

            foreach (var surface in manifest.surfaces ?? Array.Empty<FidelitySurface>())
            {
                CaptureStaticSurface(surface, artifactsRoot);
            }

            foreach (var timeline in manifest.timelines ?? Array.Empty<FidelityTimeline>())
            {
                CaptureTimelineFrames(timeline, artifactsRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"BoomHud fidelity capture complete. Artifacts written to {artifactsRoot}");
        }

        private static void CaptureStaticSurface(FidelitySurface surface, string artifactsRoot)
        {
            if (surface?.unity == null)
            {
                return;
            }

            var context = PrepareScene(surface.unity.scene);
            try
            {
                var outputPath = ResolveManifestPath(Path.Combine(artifactsRoot, surface.unity.output ?? string.Empty));
                if (!string.IsNullOrWhiteSpace(surface.unity.targetObjectName))
                {
                    CaptureTargetObject(surface.unity, outputPath);
                }
                else
                {
                    CaptureTargetElement(context.Document ?? throw new InvalidOperationException($"Scene '{context.SceneName}' does not have a UIDocument for UI Toolkit capture."), surface.unity, outputPath, freezeAnimatedPreviews: context.SceneName == "ComponentLab");
                }
            }
            finally
            {
                context.Dispose();
            }
        }

        private static void CaptureTimelineFrames(FidelityTimeline timeline, string artifactsRoot)
        {
            if (timeline?.unity == null)
            {
                return;
            }

            var context = PrepareScene(timeline.unity.scene);
            try
            {
                var director = UnityEngine.Object.FindFirstObjectByType<PlayableDirector>();
                if (director == null)
                {
                    throw new InvalidOperationException($"Could not find a PlayableDirector in scene '{timeline.unity.scene}'.");
                }

                var framesPerSecond = ResolveTimelineFramesPerSecond();
                var outputDirectory = ResolveManifestPath(Path.Combine(artifactsRoot, timeline.unity.outputDir ?? string.Empty));
                Directory.CreateDirectory(outputDirectory);

                foreach (var frame in timeline.sampleFrames ?? Array.Empty<int>())
                {
                    EvaluateTimelineAtFrame(director, frame, framesPerSecond);
                    var fileName = $"frame-{frame:0000}.png";
                    var outputPath = Path.Combine(outputDirectory, fileName);
                    CaptureTargetElement(context.Document, timeline.unity, outputPath, freezeAnimatedPreviews: false);
                }
            }
            finally
            {
                context.Dispose();
            }
        }

        private static SceneCaptureContext PrepareScene(string sceneName)
        {
            switch (sceneName)
            {
                case "ExploreHudCompare":
                    BoomHudCompareProjectSetup.SetupScene();
                    break;

                case "ComponentLab":
                    BoomHudComponentLabSetup.SetupScene();
                    break;

                case "CharPortraitMotionTimeline":
                    BoomHudMotionTimelineSetup.SetupScene();
                    break;

                case "ExploreHudCompareUGui":
                    BoomHudUGuiCompareProjectSetup.SetupScene();
                    break;

                case "ComponentLabUGui":
                    BoomHudUGuiComponentLabSetup.SetupScene();
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported Unity fidelity scene '{sceneName}'.");
            }

            UIDocument? document = null;
            if (sceneName is "ExploreHudCompare" or "ComponentLab" or "CharPortraitMotionTimeline")
            {
                document = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
                if (document == null)
                {
                    throw new InvalidOperationException($"Could not find a UIDocument after preparing scene '{sceneName}'.");
                }

                if (document.panelSettings == null)
                {
                    throw new InvalidOperationException($"Scene '{sceneName}' does not have PanelSettings assigned.");
                }
            }

            switch (sceneName)
            {
                case "ExploreHudCompare":
                    UnityEngine.Object.FindFirstObjectByType<ExploreHudPresenter>()?.Rebind();
                    break;

                case "ComponentLab":
                    UnityEngine.Object.FindFirstObjectByType<ComponentLabPresenter>()?.Rebind();
                    break;

                case "CharPortraitMotionTimeline":
                    var motionHost = UnityEngine.Object.FindFirstObjectByType<BoomHudUiToolkitMotionHost>();
                    motionHost?.Rebind();
                    var director = UnityEngine.Object.FindFirstObjectByType<PlayableDirector>();
                    if (director != null)
                    {
                        director.RebuildGraph();
                        director.time = 0d;
                        director.Evaluate();
                    }
                    break;

                case "ExploreHudCompareUGui":
                    UnityEngine.Object.FindFirstObjectByType<UGuiExploreHudPresenter>()?.Rebind();
                    break;

                case "ComponentLabUGui":
                    UnityEngine.Object.FindFirstObjectByType<UGuiComponentLabPresenter>()?.Rebind();
                    break;
            }

            return new SceneCaptureContext(sceneName, document);
        }

        private static void CaptureTargetObject(FidelityUnityCapture capture, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException($"Could not resolve output directory for '{outputPath}'."));

            Texture2D? fullTexture = null;
            Texture2D? croppedTexture = null;
            try
            {
                var target = WaitForTargetRectTransform(capture.targetObjectName);
                WaitForRectTransformToSettle(target);
                fullTexture = CaptureGameViewTexture(CaptureWidth, CaptureHeight);
                target = WaitForTargetRectTransform(capture.targetObjectName);
                croppedTexture = CropToRectTransform(fullTexture, target);
                File.WriteAllBytes(outputPath, croppedTexture.EncodeToPNG());
            }
            finally
            {
                if (croppedTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(croppedTexture);
                }

                if (fullTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(fullTexture);
                }
            }
        }

        private static void CaptureTargetElement(UIDocument document, FidelityUnityCapture capture, string outputPath, bool freezeAnimatedPreviews)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException($"Could not resolve output directory for '{outputPath}'."));
            document = ResolveActiveDocument(document);

            if (freezeAnimatedPreviews)
            {
                var componentLabPresenter = UnityEngine.Object.FindFirstObjectByType<ComponentLabPresenter>();
                if (componentLabPresenter != null)
                {
                    componentLabPresenter.enabled = false;
                }
            }

            Texture2D? fullTexture = null;
            Texture2D? croppedTexture = null;
            try
            {
                fullTexture = CaptureDocumentTexture(document, CaptureWidth, CaptureHeight);
                document = ResolveActiveDocument(document);
                var targetElement = WaitForTargetElement(document, capture.targetElementName);
                ApplyCaptureTweaks(targetElement, capture);
                EnsureTargetVisible(targetElement);

                UnityEngine.Object.DestroyImmediate(fullTexture);
                fullTexture = CaptureDocumentTexture(document, CaptureWidth, CaptureHeight);
                document = ResolveActiveDocument(document);
                targetElement = WaitForTargetElement(document, capture.targetElementName);
                ApplyCaptureTweaks(targetElement, capture);
                croppedTexture = CropToElement(fullTexture, document.rootVisualElement, targetElement);
                File.WriteAllBytes(outputPath, croppedTexture.EncodeToPNG());
            }
            finally
            {
                if (croppedTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(croppedTexture);
                }

                if (fullTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(fullTexture);
                }
            }
        }

        private static Texture2D CaptureDocumentTexture(UIDocument document, int width, int height)
        {
            var panelSettings = document.panelSettings ?? throw new InvalidOperationException("UIDocument does not have PanelSettings.");
            var mainCamera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            var originalPanelTargetTexture = panelSettings.targetTexture;
            var originalCameraTargetTexture = mainCamera != null ? mainCamera.targetTexture : null;

            try
            {
                var offscreenTexture = TryCaptureOffscreen(document, panelSettings, mainCamera, width, height);
                if (offscreenTexture != null && HasMeaningfulContent(offscreenTexture))
                {
                    return offscreenTexture;
                }

                if (offscreenTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(offscreenTexture);
                }

                var gameViewTexture = CaptureGameViewTexture(width, height);
                if (HasMeaningfulContent(gameViewTexture))
                {
                    return gameViewTexture;
                }

                UnityEngine.Object.DestroyImmediate(gameViewTexture);
                throw new InvalidOperationException("Unity UI capture did not produce meaningful content.");
            }
            finally
            {
                panelSettings.targetTexture = originalPanelTargetTexture;
                if (mainCamera != null)
                {
                    mainCamera.targetTexture = originalCameraTargetTexture;
                }
            }
        }

        private static Texture2D? TryCaptureOffscreen(UIDocument document, PanelSettings panelSettings, Camera? mainCamera, int width, int height)
        {
            RenderTexture? renderTexture = null;
            var previousActive = RenderTexture.active;
            var originalPanelTargetTexture = panelSettings.targetTexture;
            var originalCameraTargetTexture = mainCamera != null ? mainCamera.targetTexture : null;

            try
            {
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "BoomHudFidelityCapture",
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                renderTexture.Create();

                panelSettings.targetTexture = renderTexture;
                if (mainCamera != null)
                {
                    mainCamera.targetTexture = renderTexture;
                }

                WaitForDocumentToSettle(document);

                for (var attempt = 0; attempt < 4; attempt++)
                {
                    document.rootVisualElement.MarkDirtyRepaint();
                    if (mainCamera != null)
                    {
                        mainCamera.Render();
                    }

                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    EditorApplication.QueuePlayerLoopUpdate();
                    Thread.Sleep(100);
                }

                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                FlipTextureVertically(texture);
                return texture;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previousActive;
                panelSettings.targetTexture = originalPanelTargetTexture;
                if (mainCamera != null)
                {
                    mainCamera.targetTexture = originalCameraTargetTexture;
                }

                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
            }
        }

        private static Texture2D CaptureGameViewTexture(int fallbackWidth, int fallbackHeight)
        {
            var gameView = GetGameViewWindow();
            if (gameView == null)
            {
                throw new InvalidOperationException("Could not open the Unity Game View for capture.");
            }

            FocusAndRepaintWindow(gameView);

            var viewportRectPixels = GetWindowPixelRect(gameView);
            var width = Mathf.Max(fallbackWidth, Mathf.RoundToInt(viewportRectPixels.width));
            var height = Mathf.Max(fallbackHeight, Mathf.RoundToInt(viewportRectPixels.height));
            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException("The Unity Game View reported an empty viewport.");
            }

            var hostView = GetHostView(gameView);
            if (hostView == null)
            {
                throw new InvalidOperationException("Could not resolve the Unity Game View host.");
            }

            var grabPixels = hostView.GetType().GetMethod(
                "GrabPixels",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(RenderTexture), typeof(Rect) },
                null);
            if (grabPixels == null)
            {
                throw new MissingMethodException(hostView.GetType().FullName, "GrabPixels");
            }

            return CaptureTextureFromHostView(hostView, grabPixels, viewportRectPixels, width, height);
        }

        private static Texture2D CaptureTextureFromHostView(object hostView, MethodInfo grabPixels, Rect viewportRectPixels, int width, int height)
        {
            RenderTexture? renderTexture = null;
            var previousActive = RenderTexture.active;

            try
            {
                renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "BoomHudFidelityGameViewCapture",
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                renderTexture.Create();

                grabPixels.Invoke(hostView, new object[] { renderTexture, viewportRectPixels });

                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                FlipTextureVertically(texture);
                return texture;
            }
            finally
            {
                RenderTexture.active = previousActive;

                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
            }
        }

        private static Texture2D CropToElement(Texture2D fullTexture, VisualElement root, VisualElement targetElement)
        {
            var rootBounds = ResolveElementRect(root, fullTexture.width, fullTexture.height);
            var targetBounds = ResolveElementRect(targetElement, fullTexture.width, fullTexture.height);

            var scaleX = rootBounds.width > 0f ? fullTexture.width / rootBounds.width : 1f;
            var scaleY = rootBounds.height > 0f ? fullTexture.height / rootBounds.height : 1f;

            var x = Mathf.Clamp(Mathf.RoundToInt((targetBounds.xMin - rootBounds.xMin) * scaleX), 0, fullTexture.width - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt((targetBounds.yMin - rootBounds.yMin) * scaleY), 0, fullTexture.height - 1);
            var width = Mathf.Clamp(Mathf.RoundToInt(targetBounds.width * scaleX), 1, fullTexture.width - x);
            var height = Mathf.Clamp(Mathf.RoundToInt(targetBounds.height * scaleY), 1, fullTexture.height - y);

            var pixels = fullTexture.GetPixels(x, y, width, height);
            var croppedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            croppedTexture.SetPixels(pixels);
            croppedTexture.Apply();
            return croppedTexture;
        }

        private static Texture2D CropToRectTransform(Texture2D fullTexture, RectTransform target)
        {
            var worldCorners = new Vector3[4];
            target.GetWorldCorners(worldCorners);

            var canvas = target.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera ?? Camera.main
                : null;

            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;

            foreach (var corner in worldCorners)
            {
                var viewportPoint = camera != null
                    ? camera.WorldToViewportPoint(corner)
                    : ScreenPointToViewport(RectTransformUtility.WorldToScreenPoint(null, corner));

                minX = Mathf.Min(minX, viewportPoint.x);
                minY = Mathf.Min(minY, viewportPoint.y);
                maxX = Mathf.Max(maxX, viewportPoint.x);
                maxY = Mathf.Max(maxY, viewportPoint.y);
            }

            if (!float.IsFinite(minX) || !float.IsFinite(minY) || !float.IsFinite(maxX) || !float.IsFinite(maxY) || maxX <= minX || maxY <= minY)
            {
                throw new InvalidOperationException($"Could not resolve crop bounds for RectTransform '{target.name}'.");
            }

            var x = Mathf.Clamp(Mathf.RoundToInt(minX * fullTexture.width), 0, fullTexture.width - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(minY * fullTexture.height), 0, fullTexture.height - 1);
            var width = Mathf.Clamp(Mathf.RoundToInt((maxX - minX) * fullTexture.width), 1, fullTexture.width - x);
            var height = Mathf.Clamp(Mathf.RoundToInt((maxY - minY) * fullTexture.height), 1, fullTexture.height - y);

            var pixels = fullTexture.GetPixels(x, y, width, height);
            var croppedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            croppedTexture.SetPixels(pixels);
            croppedTexture.Apply();
            return croppedTexture;
        }

        private static VisualElement WaitForTargetElement(UIDocument document, string targetElementName)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                document = ResolveActiveDocument(document);
                RebindAllHosts();
                WaitForDocumentToSettle(document);
                var targetElement = document.rootVisualElement?.Q<VisualElement>(targetElementName);
                if (targetElement != null)
                {
                    return targetElement;
                }

                Thread.Sleep(100);
            }

            throw new InvalidOperationException($"Could not resolve target element '{targetElementName}'.");
        }

        private static RectTransform WaitForTargetRectTransform(string targetObjectName)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                RebindAllUGuiHosts();
                Canvas.ForceUpdateCanvases();
                EditorApplication.QueuePlayerLoopUpdate();
                Thread.Sleep(100);

                var target = GameObject.Find(targetObjectName);
                if (target != null && target.TryGetComponent<RectTransform>(out var rectTransform) && HasMeaningfulRect(rectTransform))
                {
                    return rectTransform;
                }
            }

            throw new InvalidOperationException($"Could not resolve target object '{targetObjectName}'.");
        }

        private static Rect ResolveElementRect(VisualElement element, int fallbackWidth, int fallbackHeight)
        {
            var worldBounds = element.worldBound;
            if (worldBounds.width > 0f && worldBounds.height > 0f)
            {
                return worldBounds;
            }

            var layout = element.layout;
            if (layout.width > 0f && layout.height > 0f)
            {
                return layout;
            }

            var resolvedStyle = element.resolvedStyle;
            var width = resolvedStyle.width > 0f ? resolvedStyle.width : fallbackWidth;
            var height = resolvedStyle.height > 0f ? resolvedStyle.height : fallbackHeight;
            var left = float.IsNaN(resolvedStyle.left) ? 0f : resolvedStyle.left;
            var top = float.IsNaN(resolvedStyle.top) ? 0f : resolvedStyle.top;
            return new Rect(left, top, width, height);
        }

        private static UIDocument ResolveActiveDocument(UIDocument? preferredDocument)
        {
            if (preferredDocument != null &&
                preferredDocument.gameObject != null &&
                preferredDocument.rootVisualElement != null)
            {
                return preferredDocument;
            }

            var activeSceneName = EditorSceneManager.GetActiveScene().name;
            var documentRootObjectName = GetDocumentRootObjectName(activeSceneName);
            if (!string.IsNullOrWhiteSpace(documentRootObjectName))
            {
                var rootObject = GameObject.Find(documentRootObjectName);
                if (rootObject != null && rootObject.TryGetComponent<UIDocument>(out var namedDocument))
                {
                    return namedDocument;
                }
            }

            var resolvedDocument = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
            if (resolvedDocument == null)
            {
                throw new InvalidOperationException("Could not find an active UIDocument in the prepared scene.");
            }

            return resolvedDocument;
        }

        private static string? GetDocumentRootObjectName(string sceneName)
        {
            return sceneName switch
            {
                "ExploreHudCompare" => "BoomHud Compare UI",
                "ComponentLab" => "BoomHud Component Lab",
                "CharPortraitMotionTimeline" => "BoomHud Char Portrait Motion Timeline",
                _ => null
            };
        }

        private static void RebindAllHosts()
        {
            var hosts = UnityEngine.Object.FindObjectsByType<BoomHudViewHost>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var host in hosts)
            {
                if (host != null && host.isActiveAndEnabled)
                {
                    host.Rebind();
                }
            }
        }

        private static void RebindAllUGuiHosts()
        {
            var hosts = UnityEngine.Object.FindObjectsByType<BoomHudUGuiHost>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var host in hosts)
            {
                if (host != null && host.isActiveAndEnabled)
                {
                    host.Rebind();
                }
            }
        }

        private static void EnsureTargetVisible(VisualElement targetElement)
        {
            var scrollView = FindAncestor<ScrollView>(targetElement);
            if (scrollView == null)
            {
                return;
            }

            var offsetY = Mathf.Max(0f, targetElement.worldBound.yMin - 32f);
            scrollView.scrollOffset = new Vector2(scrollView.scrollOffset.x, offsetY);
            targetElement.MarkDirtyRepaint();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            EditorApplication.QueuePlayerLoopUpdate();
            Thread.Sleep(100);
        }

        private static void ApplyCaptureTweaks(VisualElement targetElement, FidelityUnityCapture capture)
        {
            if (capture == null)
            {
                return;
            }

            if (!float.IsNaN(capture.allIconMarginTop))
            {
                ApplyMarginTopToIcons(targetElement, capture.allIconMarginTop);
            }

            if (!float.IsNaN(capture.classIconMarginTop))
            {
                var classIcon = targetElement.Q<Label>("ClassIcon");
                if (classIcon != null)
                {
                    classIcon.style.marginTop = capture.classIconMarginTop;
                }
            }

            if (!float.IsNaN(capture.actionIconMarginTop))
            {
                ApplyMarginTopToActionIcons(targetElement, capture.actionIconMarginTop);
            }
        }

        private static void ApplyMarginTopToIcons(VisualElement targetElement, float marginTop)
        {
            var classIcon = targetElement.Q<Label>("ClassIcon");
            if (classIcon != null)
            {
                classIcon.style.marginTop = marginTop;
            }

            ApplyMarginTopToActionIcons(targetElement, marginTop);
        }

        private static void ApplyMarginTopToActionIcons(VisualElement targetElement, float marginTop)
        {
            ApplyMarginTopToFirstLabel(targetElement.Q<VisualElement>("Atk"), marginTop);
            ApplyMarginTopToFirstLabel(targetElement.Q<VisualElement>("Mag"), marginTop);
            ApplyMarginTopToFirstLabel(targetElement.Q<VisualElement>("Def"), marginTop);
            ApplyMarginTopToFirstLabel(targetElement.Q<VisualElement>("Item"), marginTop);
        }

        private static void ApplyMarginTopToFirstLabel(VisualElement? container, float marginTop)
        {
            if (container == null)
            {
                return;
            }

            var iconLabel = container.Children().OfType<Label>().FirstOrDefault();
            if (iconLabel != null)
            {
                iconLabel.style.marginTop = marginTop;
            }
        }

        private static T? FindAncestor<T>(VisualElement element) where T : VisualElement
        {
            var current = element.parent;
            while (current != null)
            {
                if (current is T typed)
                {
                    return typed;
                }

                current = current.parent;
            }

            return null;
        }

        private static void EvaluateTimelineAtFrame(PlayableDirector director, int frame, int framesPerSecond)
        {
            var host = director.GetComponent<BoomHudUiToolkitMotionHost>();
            host?.Pause();

            director.RebuildGraph();
            director.time = frame / (double)framesPerSecond;
            director.Evaluate();

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            EditorApplication.QueuePlayerLoopUpdate();
            Thread.Sleep(100);
        }

        private static int ResolveTimelineFramesPerSecond()
        {
            var motionHost = UnityEngine.Object.FindFirstObjectByType<BoomHudUiToolkitMotionHost>();
            if (motionHost == null)
            {
                return 30;
            }

            var hostType = motionHost.GetType();
            var baseName = hostType.Name.EndsWith("MotionHost", StringComparison.Ordinal)
                ? hostType.Name[..^"MotionHost".Length]
                : hostType.Name;
            var motionTypeName = $"{hostType.Namespace}.{baseName}Motion";
            var motionType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(motionTypeName, throwOnError: false))
                .FirstOrDefault(type => type != null);
            var framesPerSecondField = motionType?.GetField("FramesPerSecond", BindingFlags.Public | BindingFlags.Static);

            return framesPerSecondField?.GetValue(null) as int? ?? 30;
        }

        private static void WaitForDocumentToSettle(UIDocument document)
        {
            var root = document.rootVisualElement;
            if (root == null)
            {
                return;
            }

            for (var attempt = 0; attempt < 8; attempt++)
            {
                root.MarkDirtyRepaint();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();
                Thread.Sleep(100);

                if (HasResolvedLayout(root) && HasMeaningfulBounds(root))
                {
                    return;
                }
            }
        }

        private static void WaitForRectTransformToSettle(RectTransform rectTransform)
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();
                Thread.Sleep(100);

                if (HasMeaningfulRect(rectTransform))
                {
                    return;
                }
            }
        }

        private static bool HasResolvedLayout(VisualElement? element)
        {
            if (element == null)
            {
                return false;
            }

            var layout = element.layout;
            return !float.IsNaN(layout.width) && !float.IsNaN(layout.height) && layout.width > 0f && layout.height > 0f;
        }

        private static bool HasMeaningfulBounds(VisualElement element)
        {
            var bounds = element.worldBound;
            return !float.IsNaN(bounds.width) && !float.IsNaN(bounds.height) && bounds.width > 0f && bounds.height > 0f;
        }

        private static bool HasMeaningfulRect(RectTransform rectTransform)
        {
            var rect = rectTransform.rect;
            return !float.IsNaN(rect.width) && !float.IsNaN(rect.height) && rect.width > 0f && rect.height > 0f;
        }

        private static Vector3 ScreenPointToViewport(Vector2 screenPoint)
        {
            var width = Mathf.Max(1f, Screen.width);
            var height = Mathf.Max(1f, Screen.height);
            return new Vector3(screenPoint.x / width, screenPoint.y / height, 0f);
        }

        private static bool HasMeaningfulContent(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            if (pixels.Length == 0)
            {
                return false;
            }

            var baseline = pixels[0];
            var differingPixels = 0;
            var brightPixels = 0;

            for (var index = 0; index < pixels.Length; index++)
            {
                var pixel = pixels[index];
                var delta = Mathf.Abs(pixel.r - baseline.r) + Mathf.Abs(pixel.g - baseline.g) + Mathf.Abs(pixel.b - baseline.b);
                if (delta >= 12)
                {
                    differingPixels++;
                }

                if (pixel.r >= 80 || pixel.g >= 80 || pixel.b >= 80)
                {
                    brightPixels++;
                }

                if (differingPixels >= 256 && brightPixels >= 256)
                {
                    return true;
                }
            }

            return false;
        }

        private static EditorWindow? GetGameViewWindow()
        {
            try
            {
                if (!EditorApplication.ExecuteMenuItem("Window/General/Game"))
                {
                    EditorApplication.ExecuteMenuItem("Window/General/Game %2");
                }
            }
            catch
            {
            }

            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            return gameViewType != null ? EditorWindow.GetWindow(gameViewType) : null;
        }

        private static void FocusAndRepaintWindow(EditorWindow window)
        {
            try
            {
                window.Focus();
            }
            catch
            {
            }

            window.Repaint();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
            Thread.Sleep(150);
        }

        private static Rect GetWindowPixelRect(EditorWindow window)
        {
            var contentRectPoints = window.rootVisualElement.contentRect;
            var pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            return new Rect(
                Mathf.Round(contentRectPoints.x * pixelsPerPoint),
                Mathf.Round(contentRectPoints.y * pixelsPerPoint),
                Mathf.Round(contentRectPoints.width * pixelsPerPoint),
                Mathf.Round(contentRectPoints.height * pixelsPerPoint));
        }

        private static object? GetHostView(EditorWindow window)
        {
            var parentField = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
            if (parentField != null)
            {
                var parent = parentField.GetValue(window);
                if (parent != null)
                {
                    return parent;
                }
            }

            var hostViewProperty = typeof(EditorWindow).GetProperty("hostView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return hostViewProperty?.GetValue(window, null);
        }

        private static void FlipTextureVertically(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            var width = texture.width;
            var height = texture.height;
            var row = new Color32[width];

            for (var y = 0; y < height / 2; y++)
            {
                var topIndex = y * width;
                var bottomIndex = (height - 1 - y) * width;

                Array.Copy(pixels, topIndex, row, 0, width);
                Array.Copy(pixels, bottomIndex, pixels, topIndex, width);
                Array.Copy(row, 0, pixels, bottomIndex, width);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
        }

        private static string ResolveRepoRelativePath(string relativePath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Could not resolve the Unity project root.");
            var repoRoot = Directory.GetParent(projectRoot)?.Parent?.FullName
                ?? throw new InvalidOperationException("Could not resolve the repository root from the Unity project path.");
            return Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ResolveManifestPath(string pathValue)
        {
            if (Path.IsPathRooted(pathValue))
            {
                return Path.GetFullPath(pathValue);
            }

            return Path.GetFullPath(ResolveRepoRelativePath(pathValue));
        }

        private static string? GetArgumentValue(string[] args, string name)
        {
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.Ordinal))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private sealed class SceneCaptureContext : IDisposable
        {
            public SceneCaptureContext(string sceneName, UIDocument document)
            {
                SceneName = sceneName;
                Document = document;
            }

            public string SceneName { get; }

            public UIDocument? Document { get; }

            public void Dispose()
            {
                var componentLabPresenter = UnityEngine.Object.FindFirstObjectByType<ComponentLabPresenter>();
                if (componentLabPresenter != null && !componentLabPresenter.enabled)
                {
                    componentLabPresenter.enabled = true;
                }
            }
        }

        [Serializable]
        private sealed class FidelityManifest
        {
            public string artifactsRoot = string.Empty;
            public FidelitySurface[]? surfaces;
            public FidelityTimeline[]? timelines;
        }

        [Serializable]
        private sealed class FidelitySurface
        {
            public string id = string.Empty;
            public FidelityUnityCapture? unity;
        }

        [Serializable]
        private sealed class FidelityTimeline
        {
            public string id = string.Empty;
            public int[]? sampleFrames;
            public FidelityUnityCapture? unity;
        }

        [Serializable]
        private sealed class FidelityUnityCapture
        {
            public string captureId = string.Empty;
            public string scene = string.Empty;
            public string targetElementName = string.Empty;
            public string targetObjectName = string.Empty;
            public string output = string.Empty;
            public string outputDir = string.Empty;
            public float allIconMarginTop = float.NaN;
            public float classIconMarginTop = float.NaN;
            public float actionIconMarginTop = float.NaN;
        }
    }
}
