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
        private const int MaxGameViewCaptureDimension = 4096;

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

            var context = PrepareScene(surface.unity);
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

            var context = PrepareScene(timeline.unity);
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

        private static SceneCaptureContext PrepareScene(FidelityUnityCapture capture)
        {
            var sceneName = capture?.scene ?? throw new ArgumentNullException(nameof(capture));

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

                case "FixtureCompare":
                    BoomHudFixtureCompareProjectSetup.SetupScene(
                        capture.resourceBasePath,
                        capture.generatedRootName,
                        capture.generatedViewTypeName);
                    break;

                case "FixtureCompareUGui":
                    BoomHudFixtureUGuiCompareProjectSetup.SetupScene(
                        capture.generatedViewTypeName,
                        capture.targetObjectName);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported Unity fidelity scene '{sceneName}'.");
            }

            UIDocument? document = null;
            if (sceneName is "ExploreHudCompare" or "ComponentLab" or "CharPortraitMotionTimeline" or "FixtureCompare")
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

                case "FixtureCompare":
                    UnityEngine.Object.FindFirstObjectByType<FixtureHudPresenter>()
                        ?.Configure(capture.resourceBasePath, capture.generatedRootName, capture.generatedViewTypeName);
                    break;

                case "FixtureCompareUGui":
                    UnityEngine.Object.FindFirstObjectByType<FixtureUGuiPresenter>()
                        ?.Configure(capture.generatedViewTypeName, capture.targetObjectName);
                    break;
            }

            return new SceneCaptureContext(sceneName, document);
        }

        private static void CaptureTargetObject(FidelityUnityCapture capture, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException($"Could not resolve output directory for '{outputPath}'."));
            var requestedWidth = ResolveCaptureWidth(capture);
            var requestedHeight = ResolveCaptureHeight(capture);

            Texture2D? fullTexture = null;
            Texture2D? croppedTexture = null;
            try
            {
                var target = WaitForTargetRectTransform(capture.targetObjectName);
                WaitForRectTransformToSettle(target);
                fullTexture = CaptureCameraTexture(requestedWidth, requestedHeight);
                target = WaitForTargetRectTransform(capture.targetObjectName);
                croppedTexture = CropToRectTransform(fullTexture, target);
                croppedTexture = NormalizeCapturedTextureSize(croppedTexture, requestedWidth, requestedHeight);
                FlipTextureVertically(croppedTexture);
                File.WriteAllBytes(outputPath, croppedTexture.EncodeToPNG());
                WriteActualLayoutSnapshot(outputPath, CreateUGuiLayoutSnapshot(capture, target));
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
            var requestedWidth = ResolveCaptureWidth(capture);
            var requestedHeight = ResolveCaptureHeight(capture);

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
                document = ResolveActiveDocument(document);
                var targetElement = WaitForTargetElement(document, capture.targetElementName);
                ApplyCaptureTweaks(targetElement, capture);
                EnsureTargetVisible(targetElement);
                WaitForDocumentToSettle(document);

                fullTexture = CaptureDocumentTexture(document, requestedWidth, requestedHeight);
                document = ResolveActiveDocument(document);
                targetElement = WaitForTargetElement(document, capture.targetElementName);
                ApplyCaptureTweaks(targetElement, capture);
                croppedTexture = CropToElement(fullTexture, document.rootVisualElement, targetElement);
                croppedTexture = NormalizeCapturedTextureSize(croppedTexture, requestedWidth, requestedHeight);
                File.WriteAllBytes(outputPath, croppedTexture.EncodeToPNG());
                WriteActualLayoutSnapshot(outputPath, CreateUiToolkitLayoutSnapshot(capture, targetElement));
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

                Texture2D? gameViewTexture = null;
                Exception? gameViewException = null;
                try
                {
                    gameViewTexture = CaptureGameViewTexture(width, height);
                    if (HasMeaningfulContent(gameViewTexture))
                    {
                        if (offscreenTexture != null)
                        {
                            UnityEngine.Object.DestroyImmediate(offscreenTexture);
                        }

                        return gameViewTexture;
                    }
                }
                catch (Exception exception)
                {
                    gameViewException = exception;
                }

                if (offscreenTexture != null)
                {
                    if (gameViewTexture != null)
                    {
                        UnityEngine.Object.DestroyImmediate(gameViewTexture);
                    }

                    Debug.LogWarning(
                        $"Falling back to the offscreen UI Toolkit capture despite a low-content heuristic result."
                        + $"{FormatCaptureFailureSuffix(gameViewException)}");
                    return offscreenTexture;
                }

                if (gameViewTexture != null)
                {
                    Debug.LogWarning("Falling back to the Unity Game View capture despite a low-content heuristic result.");
                    return gameViewTexture;
                }

                if (gameViewException != null)
                {
                    throw new InvalidOperationException(
                        "Unity UI capture failed for both offscreen and Game View capture paths.",
                        gameViewException);
                }

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

        private static int ResolveCaptureWidth(FidelityUnityCapture capture)
            => capture.captureWidth > 0 ? capture.captureWidth : CaptureWidth;

        private static int ResolveCaptureHeight(FidelityUnityCapture capture)
            => capture.captureHeight > 0 ? capture.captureHeight : CaptureHeight;

        private static Texture2D? TryCaptureOffscreen(UIDocument document, PanelSettings panelSettings, Camera? mainCamera, int width, int height)
        {
            RenderTexture? renderTexture = null;
            var previousActive = RenderTexture.active;
            var originalPanelTargetTexture = panelSettings.targetTexture;

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

                WaitForDocumentToSettle(document);

                for (var attempt = 0; attempt < 10; attempt++)
                {
                    document.rootVisualElement.MarkDirtyRepaint();
                    PumpUiToolkitPanels();
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                    EditorApplication.QueuePlayerLoopUpdate();
                    Thread.Sleep(100);
                }

                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
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
            var width = ResolveRequestedCaptureDimension(fallbackWidth, viewportRectPixels.width);
            var height = ResolveRequestedCaptureDimension(fallbackHeight, viewportRectPixels.height);
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

            var captureRect = new Rect(viewportRectPixels.x, viewportRectPixels.y, width, height);
            return CaptureTextureFromHostView(hostView, grabPixels, captureRect, width, height);
        }

        private static Texture2D CaptureCameraTexture(int width, int height)
        {
            var mainCamera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (mainCamera == null)
            {
                return CaptureGameViewTexture(width, height);
            }

            RenderTexture? renderTexture = null;
            var previousActive = RenderTexture.active;
            var originalCameraTargetTexture = mainCamera.targetTexture;

            try
            {
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "BoomHudFidelityCameraCapture",
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                renderTexture.Create();

                mainCamera.targetTexture = renderTexture;
                mainCamera.Render();

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
                mainCamera.targetTexture = originalCameraTargetTexture;

                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
            }
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
            var width = Mathf.Clamp(Mathf.RoundToInt(targetBounds.width * scaleX), 1, fullTexture.width - x);
            var bottom = Mathf.RoundToInt((targetBounds.yMax - rootBounds.yMin) * scaleY);
            var y = Mathf.Clamp(fullTexture.height - bottom, 0, fullTexture.height - 1);
            var height = Mathf.Clamp(Mathf.RoundToInt(targetBounds.height * scaleY), 1, fullTexture.height - y);
            height = Mathf.Clamp(height, 1, fullTexture.height - y);

            var pixels = fullTexture.GetPixels(x, y, width, height);
            var croppedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            croppedTexture.SetPixels(pixels);
            croppedTexture.Apply();
            return croppedTexture;
        }

        private static Texture2D CropToRectTransform(Texture2D fullTexture, RectTransform target)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas != null && TryCropScreenSpaceCameraRect(fullTexture, target, canvas, out var pixelAdjustedCrop))
            {
                return pixelAdjustedCrop;
            }

            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera ?? Camera.main
                : null;

            if (!TryResolveViewportBounds(target, camera, out var minX, out var minY, out var maxX, out var maxY))
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

        private static bool TryCropScreenSpaceCameraRect(Texture2D fullTexture, RectTransform target, Canvas canvas, out Texture2D croppedTexture)
        {
            croppedTexture = null!;
            if (canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                return false;
            }

            var pixelRect = RectTransformUtility.PixelAdjustRect(target, canvas);
            if (pixelRect.width <= 0f || pixelRect.height <= 0f)
            {
                return false;
            }

            var x = Mathf.RoundToInt((fullTexture.width * 0.5f) + pixelRect.xMin);
            var y = Mathf.RoundToInt((fullTexture.height * 0.5f) + pixelRect.yMin);
            var width = Mathf.RoundToInt(pixelRect.width);
            var height = Mathf.RoundToInt(pixelRect.height);

            x = Mathf.Clamp(x, 0, fullTexture.width - 1);
            y = Mathf.Clamp(y, 0, fullTexture.height - 1);
            width = Mathf.Clamp(width, 1, fullTexture.width - x);
            height = Mathf.Clamp(height, 1, fullTexture.height - y);
            if (width <= 1 || height <= 1)
            {
                return false;
            }

            var pixels = fullTexture.GetPixels(x, y, width, height);
            croppedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            croppedTexture.SetPixels(pixels);
            croppedTexture.Apply();
            return true;
        }

        private static Texture2D NormalizeCapturedTextureSize(Texture2D texture, int requestedWidth, int requestedHeight)
        {
            if (requestedWidth <= 0 || requestedHeight <= 0)
            {
                return texture;
            }

            if (texture.width == requestedWidth && texture.height == requestedHeight)
            {
                return texture;
            }

            var resized = ResizeTextureNearestNeighbor(texture, requestedWidth, requestedHeight);
            UnityEngine.Object.DestroyImmediate(texture);
            return resized;
        }

        private static Texture2D ResizeTextureNearestNeighbor(Texture2D source, int width, int height)
        {
            var sourcePixels = source.GetPixels32();
            var resizedPixels = new Color32[width * height];
            var scaleX = source.width / (float)width;
            var scaleY = source.height / (float)height;

            for (var y = 0; y < height; y++)
            {
                var sourceY = Mathf.Clamp(Mathf.FloorToInt(y * scaleY), 0, source.height - 1);
                var sourceRow = sourceY * source.width;
                var targetRow = y * width;

                for (var x = 0; x < width; x++)
                {
                    var sourceX = Mathf.Clamp(Mathf.FloorToInt(x * scaleX), 0, source.width - 1);
                    resizedPixels[targetRow + x] = sourcePixels[sourceRow + sourceX];
                }
            }

            var resized = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            resized.SetPixels32(resizedPixels);
            resized.Apply();
            return resized;
        }

        private static void WriteActualLayoutSnapshot(string outputPath, ActualLayoutSnapshot snapshot)
        {
            var snapshotPath = ResolveActualLayoutSnapshotPath(outputPath);
            var snapshotDirectory = Path.GetDirectoryName(snapshotPath);
            if (!string.IsNullOrWhiteSpace(snapshotDirectory))
            {
                Directory.CreateDirectory(snapshotDirectory);
            }

            File.WriteAllText(snapshotPath, JsonUtility.ToJson(snapshot, true));
        }

        private static string ResolveActualLayoutSnapshotPath(string outputPath)
            => Path.Combine(
                Path.GetDirectoryName(outputPath) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(outputPath)}.layout.actual.json");

        private static ActualLayoutSnapshot CreateUiToolkitLayoutSnapshot(FidelityUnityCapture capture, VisualElement targetElement)
            => new ActualLayoutSnapshot
            {
                Version = "1.0",
                BackendFamily = "unity",
                CaptureId = capture.captureId,
                TargetName = string.IsNullOrWhiteSpace(capture.targetElementName) ? targetElement.name : capture.targetElementName,
                Root = BuildUiToolkitLayoutNode(targetElement, null, "root")
            };

        private static ActualLayoutSnapshot CreateUGuiLayoutSnapshot(FidelityUnityCapture capture, RectTransform target)
            => new ActualLayoutSnapshot
            {
                Version = "1.0",
                BackendFamily = "ugui",
                CaptureId = capture.captureId,
                TargetName = string.IsNullOrWhiteSpace(capture.targetObjectName) ? target.name : capture.targetObjectName,
                Root = BuildUGuiLayoutNode(target, null, "root")
            };

        private static ActualLayoutNode BuildUiToolkitLayoutNode(VisualElement element, VisualElement? parent, string localPath)
        {
            var rect = ResolveUiToolkitLocalRect(element, parent);
            var clipContent = false;
            var wrapText = element.resolvedStyle.whiteSpace == WhiteSpace.Normal;
            var fontSize = element is TextElement textElement && !float.IsNaN(textElement.resolvedStyle.fontSize)
                ? textElement.resolvedStyle.fontSize
                : -1f;
            var preferredSize = element is TextElement measuredTextElement
                ? measuredTextElement.MeasureTextSize(
                    measuredTextElement.text ?? string.Empty,
                    0,
                    VisualElement.MeasureMode.Undefined,
                    0,
                    VisualElement.MeasureMode.Undefined)
                : Vector2.zero;

            return new ActualLayoutNode
            {
                LocalPath = localPath,
                Name = string.IsNullOrWhiteSpace(element.name) ? element.GetType().Name : element.name,
                NodeType = element.GetType().Name,
                X = rect.x,
                Y = rect.y,
                Width = rect.width,
                Height = rect.height,
                ScaleX = element.transform.scale.x,
                ScaleY = element.transform.scale.y,
                PreferredWidth = preferredSize.x > 0f ? preferredSize.x : -1f,
                PreferredHeight = preferredSize.y > 0f ? preferredSize.y : -1f,
                Text = element is TextElement uiText ? uiText.text : string.Empty,
                FontSize = fontSize,
                WrapText = wrapText,
                ClipContent = clipContent,
                PaddingLeft = element.resolvedStyle.paddingLeft,
                PaddingTop = element.resolvedStyle.paddingTop,
                PaddingRight = element.resolvedStyle.paddingRight,
                PaddingBottom = element.resolvedStyle.paddingBottom,
                MarginLeft = element.resolvedStyle.marginLeft,
                MarginTop = element.resolvedStyle.marginTop,
                MarginRight = element.resolvedStyle.marginRight,
                MarginBottom = element.resolvedStyle.marginBottom,
                Children = element.Children()
                    .OfType<VisualElement>()
                    .Select((child, index) => BuildUiToolkitLayoutNode(child, element, $"{localPath}/{index}"))
                    .ToArray()
            };
        }

        private static ActualLayoutNode BuildUGuiLayoutNode(RectTransform target, RectTransform? parent, string localPath)
        {
            var rect = ResolveUGuiLocalRect(target, parent);
            var preferredWidth = ResolvePreferredWidth(target);
            var preferredHeight = ResolvePreferredHeight(target);
            var text = target.GetComponent<Text>();
            var layoutGroup = target.GetComponent<LayoutGroup>();

            return new ActualLayoutNode
            {
                LocalPath = localPath,
                Name = target.name,
                NodeType = ResolveUGuiNodeType(target),
                X = rect.x,
                Y = rect.y,
                Width = rect.width,
                Height = rect.height,
                ScaleX = target.localScale.x,
                ScaleY = target.localScale.y,
                PreferredWidth = preferredWidth > 0f ? preferredWidth : -1f,
                PreferredHeight = preferredHeight > 0f ? preferredHeight : -1f,
                Text = text != null ? text.text : string.Empty,
                FontSize = text != null ? text.fontSize : -1f,
                WrapText = text != null && text.horizontalOverflow == HorizontalWrapMode.Wrap,
                ClipContent = target.GetComponent<Mask>() != null || target.GetComponent<RectMask2D>() != null,
                PaddingLeft = layoutGroup?.padding.left ?? 0,
                PaddingTop = layoutGroup?.padding.top ?? 0,
                PaddingRight = layoutGroup?.padding.right ?? 0,
                PaddingBottom = layoutGroup?.padding.bottom ?? 0,
                MarginLeft = 0,
                MarginTop = 0,
                MarginRight = 0,
                MarginBottom = 0,
                Children = target.Cast<Transform>()
                    .OfType<RectTransform>()
                    .Select((child, index) => BuildUGuiLayoutNode(child, target, $"{localPath}/{index}"))
                    .ToArray()
            };
        }

        private static Rect ResolveUiToolkitLocalRect(VisualElement element, VisualElement? parent)
        {
            if (parent == null)
            {
                var rootRect = ResolveElementRect(element, CaptureWidth, CaptureHeight);
                return new Rect(0f, 0f, rootRect.width, rootRect.height);
            }

            var layout = element.layout;
            if (!float.IsNaN(layout.x) && !float.IsNaN(layout.y) && layout.width > 0f && layout.height > 0f)
            {
                return layout;
            }

            var parentBounds = parent.worldBound;
            var childBounds = element.worldBound;
            return new Rect(
                childBounds.xMin - parentBounds.xMin,
                childBounds.yMin - parentBounds.yMin,
                childBounds.width,
                childBounds.height);
        }

        private static Rect ResolveUGuiLocalRect(RectTransform target, RectTransform? parent)
        {
            if (parent == null)
            {
                var width = Mathf.Max(target.rect.width, ResolvePreferredWidth(target));
                var height = Mathf.Max(target.rect.height, ResolvePreferredHeight(target));
                return new Rect(0f, 0f, width, height);
            }

            if (TryResolveLocalBounds(target, parent, out var left, out var top, out var widthBounds, out var heightBounds))
            {
                return new Rect(left, top, widthBounds, heightBounds);
            }

            var fallbackWidth = Mathf.Max(target.rect.width, ResolvePreferredWidth(target));
            var fallbackHeight = Mathf.Max(target.rect.height, ResolvePreferredHeight(target));
            return new Rect(0f, 0f, fallbackWidth, fallbackHeight);
        }

        private static bool TryResolveLocalBounds(
            RectTransform target,
            RectTransform parent,
            out float left,
            out float top,
            out float width,
            out float height)
        {
            var childCorners = GetWorldCorners(target);
            var localCorners = childCorners
                .Select(parent.InverseTransformPoint)
                .ToArray();

            if (localCorners.Length != 4)
            {
                left = top = width = height = 0f;
                return false;
            }

            var minX = localCorners.Min(corner => corner.x);
            var maxX = localCorners.Max(corner => corner.x);
            var minY = localCorners.Min(corner => corner.y);
            var maxY = localCorners.Max(corner => corner.y);
            var parentRect = parent.rect;

            left = minX - parentRect.xMin;
            top = parentRect.yMax - maxY;
            width = maxX - minX;
            height = maxY - minY;

            return width > 0f && height > 0f;
        }

        private static string ResolveUGuiNodeType(RectTransform target)
        {
            if (target.TryGetComponent<Text>(out _))
            {
                return nameof(Text);
            }

            if (target.TryGetComponent<UnityEngine.UI.Image>(out _))
            {
                return nameof(UnityEngine.UI.Image);
            }

            if (target.TryGetComponent<HorizontalLayoutGroup>(out _))
            {
                return nameof(HorizontalLayoutGroup);
            }

            if (target.TryGetComponent<VerticalLayoutGroup>(out _))
            {
                return nameof(VerticalLayoutGroup);
            }

            if (target.TryGetComponent<GridLayoutGroup>(out _))
            {
                return nameof(GridLayoutGroup);
            }

            return nameof(RectTransform);
        }

        private static bool TryResolveViewportBounds(
            RectTransform target,
            Camera? camera,
            out float minX,
            out float minY,
            out float maxX,
            out float maxY)
        {
            if (TryAccumulateViewportBounds(GetWorldCorners(target), camera, out minX, out minY, out maxX, out maxY))
            {
                return true;
            }

            if (TryAccumulateViewportBounds(GetPreferredWorldCorners(target), camera, out minX, out minY, out maxX, out maxY))
            {
                return true;
            }

            var descendantBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(target);
            if (descendantBounds.size.sqrMagnitude > 0f)
            {
                var descendantCorners = new[]
                {
                    target.TransformPoint(new Vector3(descendantBounds.min.x, descendantBounds.min.y, descendantBounds.center.z)),
                    target.TransformPoint(new Vector3(descendantBounds.min.x, descendantBounds.max.y, descendantBounds.center.z)),
                    target.TransformPoint(new Vector3(descendantBounds.max.x, descendantBounds.max.y, descendantBounds.center.z)),
                    target.TransformPoint(new Vector3(descendantBounds.max.x, descendantBounds.min.y, descendantBounds.center.z))
                };

                if (TryAccumulateViewportBounds(descendantCorners, camera, out minX, out minY, out maxX, out maxY))
                {
                    return true;
                }
            }

            minX = minY = maxX = maxY = 0f;
            return false;
        }

        private static Vector3[] GetWorldCorners(RectTransform target)
        {
            var worldCorners = new Vector3[4];
            target.GetWorldCorners(worldCorners);
            return worldCorners;
        }

        private static Vector3[] GetPreferredWorldCorners(RectTransform target)
        {
            var width = ResolvePreferredWidth(target);
            var height = ResolvePreferredHeight(target);
            if (width <= 0f || height <= 0f)
            {
                return Array.Empty<Vector3>();
            }

            var pivot = target.pivot;
            var min = new Vector3(-pivot.x * width, -pivot.y * height, 0f);
            var max = new Vector3((1f - pivot.x) * width, (1f - pivot.y) * height, 0f);

            return new[]
            {
                target.TransformPoint(new Vector3(min.x, min.y, 0f)),
                target.TransformPoint(new Vector3(min.x, max.y, 0f)),
                target.TransformPoint(new Vector3(max.x, max.y, 0f)),
                target.TransformPoint(new Vector3(max.x, min.y, 0f))
            };
        }

        private static bool TryAccumulateViewportBounds(
            Vector3[] worldCorners,
            Camera? camera,
            out float minX,
            out float minY,
            out float maxX,
            out float maxY)
        {
            minX = float.PositiveInfinity;
            minY = float.PositiveInfinity;
            maxX = float.NegativeInfinity;
            maxY = float.NegativeInfinity;

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

            return float.IsFinite(minX) &&
                   float.IsFinite(minY) &&
                   float.IsFinite(maxX) &&
                   float.IsFinite(maxY) &&
                   maxX > minX &&
                   maxY > minY;
        }

        private static VisualElement WaitForTargetElement(UIDocument document, string targetElementName)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                document = ResolveActiveDocument(document);
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
            RectTransform? lastResolved = null;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                RebindAllUGuiHosts();
                Canvas.ForceUpdateCanvases();
                EditorApplication.QueuePlayerLoopUpdate();
                Thread.Sleep(100);

                var rectTransform = FindTargetRectTransformUnderActiveUGuiHosts(targetObjectName);
                if (rectTransform != null)
                {
                    lastResolved = rectTransform;
                    ForceRebuildLayoutChain(rectTransform);
                    if (HasMeaningfulRect(rectTransform) || HasPreferredRect(rectTransform))
                    {
                        return rectTransform;
                    }
                }
            }

            if (lastResolved != null)
            {
                return lastResolved;
            }

            throw new InvalidOperationException($"Could not resolve target object '{targetObjectName}'.");
        }

        private static RectTransform? FindTargetRectTransformUnderActiveUGuiHosts(string targetObjectName)
        {
            var hosts = UnityEngine.Object.FindObjectsByType<BoomHudUGuiHost>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var host in hosts)
            {
                if (host == null || !host.isActiveAndEnabled)
                {
                    continue;
                }

                var matches = host.GetComponentsInChildren<RectTransform>(true)
                    .Where(rect => rect != null && string.Equals(rect.name, targetObjectName, StringComparison.Ordinal))
                    .ToArray();

                if (matches.Length == 0)
                {
                    continue;
                }

                var exactRootChild = matches.FirstOrDefault(match =>
                    match.parent != null &&
                    string.Equals(match.parent.name, "BoomHudUGuiFixtureRoot", StringComparison.Ordinal));
                if (exactRootChild != null)
                {
                    return exactRootChild;
                }

                return matches[0];
            }

            var target = GameObject.Find(targetObjectName);
            return target != null && target.TryGetComponent<RectTransform>(out var rectTransform)
                ? rectTransform
                : null;
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
                "FixtureCompare" => "BoomHud Fixture Compare UI",
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
                PumpUiToolkitPanels();
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

        private static void PumpUiToolkitPanels()
        {
            var runtimeUtilityType = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
            if (runtimeUtilityType == null)
            {
                return;
            }

            runtimeUtilityType.GetMethod("UpdatePanels", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(null, null);
            runtimeUtilityType.GetMethod("RepaintPanels", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(null, new object[] { false });
            runtimeUtilityType.GetMethod("RenderOffscreenPanels", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(null, null);
        }

        private static void WaitForRectTransformToSettle(RectTransform rectTransform)
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                Canvas.ForceUpdateCanvases();
                ForceRebuildLayoutChain(rectTransform);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();
                Thread.Sleep(100);

                if (HasMeaningfulRect(rectTransform))
                {
                    return;
                }

                ApplyPreferredSizeFallback(rectTransform);
            }
        }

        private static void FitTargetToSurface(RectTransform target)
        {
            if (target.parent is not RectTransform surface)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(surface);
            LayoutRebuilder.ForceRebuildLayoutImmediate(target);

            var preferredWidth = Mathf.Max(target.rect.width, ResolvePreferredWidth(target));
            var preferredHeight = Mathf.Max(target.rect.height, ResolvePreferredHeight(target));
            var canvas = target.GetComponentInParent<Canvas>();
            var scaleFactor = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
            var safeWidth = Mathf.Max(surface.rect.width, Screen.width / scaleFactor);
            var safeHeight = Mathf.Max(surface.rect.height, Screen.height / scaleFactor);
            if (preferredWidth <= 0f || preferredHeight <= 0f || safeWidth <= 0f || safeHeight <= 0f)
            {
                return;
            }

            var scale = Mathf.Min(1f, safeWidth / preferredWidth, safeHeight / preferredHeight);
            target.anchorMin = new Vector2(0.5f, 0.5f);
            target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = Vector2.zero;
            target.localScale = new Vector3(scale, scale, 1f);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(surface);
            LayoutRebuilder.ForceRebuildLayoutImmediate(target);
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

        private static bool HasPreferredRect(RectTransform rectTransform)
        {
            var preferredWidth = ResolvePreferredWidth(rectTransform);
            var preferredHeight = ResolvePreferredHeight(rectTransform);
            return preferredWidth > 0f && preferredHeight > 0f;
        }

        private static float ResolvePreferredWidth(RectTransform rectTransform)
        {
            var preferredWidth = LayoutUtility.GetPreferredWidth(rectTransform);
            if (preferredWidth > 0f)
            {
                return preferredWidth;
            }

            if (rectTransform.TryGetComponent<LayoutElement>(out var layoutElement) && layoutElement.preferredWidth > 0f)
            {
                return layoutElement.preferredWidth;
            }

            return rectTransform.sizeDelta.x;
        }

        private static float ResolvePreferredHeight(RectTransform rectTransform)
        {
            var preferredHeight = LayoutUtility.GetPreferredHeight(rectTransform);
            if (preferredHeight > 0f)
            {
                return preferredHeight;
            }

            if (rectTransform.TryGetComponent<LayoutElement>(out var layoutElement) && layoutElement.preferredHeight > 0f)
            {
                return layoutElement.preferredHeight;
            }

            return rectTransform.sizeDelta.y;
        }

        private static void ApplyPreferredSizeFallback(RectTransform rectTransform)
        {
            if (HasMeaningfulRect(rectTransform))
            {
                return;
            }

            var preferredWidth = ResolvePreferredWidth(rectTransform);
            var preferredHeight = ResolvePreferredHeight(rectTransform);
            if (preferredWidth <= 0f || preferredHeight <= 0f)
            {
                return;
            }

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, preferredWidth);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);
        }

        private static void ForceRebuildLayoutChain(RectTransform rectTransform)
        {
            RectTransform? current = rectTransform;
            while (current != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(current);
                current = current.parent as RectTransform;
            }
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
            var visiblePixels = 0;
            var nonBlackPixels = 0;

            for (var index = 0; index < pixels.Length; index++)
            {
                var pixel = pixels[index];
                var delta =
                    Mathf.Abs(pixel.r - baseline.r)
                    + Mathf.Abs(pixel.g - baseline.g)
                    + Mathf.Abs(pixel.b - baseline.b)
                    + Mathf.Abs(pixel.a - baseline.a);
                if (delta >= 12)
                {
                    differingPixels++;
                }

                if (pixel.a >= 8)
                {
                    visiblePixels++;
                }

                if (pixel.r >= 16 || pixel.g >= 16 || pixel.b >= 16)
                {
                    nonBlackPixels++;
                }

                if ((differingPixels >= 256 && visiblePixels >= 64) || (visiblePixels >= 256 && nonBlackPixels >= 64))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveRequestedCaptureDimension(int requestedDimension, float viewportDimension)
        {
            var resolvedDimension = requestedDimension > 0
                ? requestedDimension
                : Mathf.RoundToInt(viewportDimension);
            return Mathf.Clamp(resolvedDimension, 1, MaxGameViewCaptureDimension);
        }

        private static string FormatCaptureFailureSuffix(Exception? exception)
        {
            if (exception == null)
            {
                return string.Empty;
            }

            return $" Game View fallback failed: {exception.GetType().Name}: {exception.Message}";
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
            public string resourceBasePath = string.Empty;
            public string generatedViewTypeName = string.Empty;
            public string generatedRootName = string.Empty;
            public string output = string.Empty;
            public string outputDir = string.Empty;
            public int captureWidth;
            public int captureHeight;
            public float allIconMarginTop = float.NaN;
            public float classIconMarginTop = float.NaN;
            public float actionIconMarginTop = float.NaN;
        }

        [Serializable]
        private sealed class ActualLayoutSnapshot
        {
            public string Version = string.Empty;
            public string BackendFamily = string.Empty;
            public string CaptureId = string.Empty;
            public string TargetName = string.Empty;
            public ActualLayoutNode Root = new();
        }

        [Serializable]
        private sealed class ActualLayoutNode
        {
            public string LocalPath = string.Empty;
            public string Name = string.Empty;
            public string NodeType = string.Empty;
            public float X;
            public float Y;
            public float Width;
            public float Height;
            public float ScaleX = 1f;
            public float ScaleY = 1f;
            public float PreferredWidth = -1f;
            public float PreferredHeight = -1f;
            public string Text = string.Empty;
            public float FontSize = -1f;
            public bool WrapText;
            public bool ClipContent;
            public float PaddingLeft;
            public float PaddingTop;
            public float PaddingRight;
            public float PaddingBottom;
            public float MarginLeft;
            public float MarginTop;
            public float MarginRight;
            public float MarginBottom;
            public ActualLayoutNode[] Children = Array.Empty<ActualLayoutNode>();
        }
    }
}
