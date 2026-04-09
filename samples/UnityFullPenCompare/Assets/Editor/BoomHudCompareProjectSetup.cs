using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BoomHud.Compare;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace BoomHud.Compare.Editor
{
    public static class BoomHudCompareProjectSetup
    {
        private const string SceneDirectory = "Assets/BoomHudCompare/Scenes";
        private const string ScenePath = SceneDirectory + "/ExploreHudCompare.unity";
        internal const string PanelSettingsDirectory = "Assets/BoomHudCompare/Settings";
        internal const string PanelSettingsPath = PanelSettingsDirectory + "/BoomHudPanelSettings.asset";
        internal const string PanelTextSettingsPath = PanelSettingsDirectory + "/BoomHudPanelTextSettings.asset";
        private const string FontResourcesDirectory = "Assets/Resources/BoomHudFonts";
        private const string PressStartFontPath = FontResourcesDirectory + "/PressStart2P-Regular.ttf";
        private const string LucideFontPath = FontResourcesDirectory + "/lucide.ttf";
        private const string PressStartFontAssetPath = FontResourcesDirectory + "/PressStart2P-Regular.asset";
        private const string LucideFontAssetPath = FontResourcesDirectory + "/lucide.asset";

        [MenuItem("Tools/BoomHud/Setup Full Pen Compare Scene", priority = 100)]
        public static void SetupSceneFromMenu()
        {
            SetupScene();
        }

        [InitializeOnLoadMethod]
        private static void EnsureRuntimeAssetsOnEditorLoad()
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    Debug.Log("BoomHud compare runtime asset bootstrap starting.");
                    EnsureRuntimeAssets();
                    Debug.Log("BoomHud compare runtime asset bootstrap finished.");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
        }

        public static void SetupScene()
        {
            EnsureCompareFolders();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var panelSettings = EnsurePanelSettingsAsset();

            var rootObject = new GameObject("BoomHud Compare UI");
            var document = rootObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            rootObject.AddComponent<ExploreHudPresenter>();

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            camera.tag = "MainCamera";

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Debug.Log($"BoomHud compare scene ready at {ScenePath}");
        }

        public static void CaptureCompareScreenshot()
        {
            SetupScene();

            var document = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
            if (document == null)
            {
                throw new InvalidOperationException("Could not find a UIDocument in the compare scene.");
            }

            if (document.panelSettings == null)
            {
                throw new InvalidOperationException("The compare UIDocument does not have PanelSettings assigned.");
            }

            var outputPath = GetCompareScreenshotOutputPath();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var panelSettings = document.panelSettings;
            var renderTexture = new RenderTexture(1682, 906, 32, RenderTextureFormat.ARGB32)
            {
                name = "BoomHudCompareCapture"
            };

            try
            {
                renderTexture.Create();
                panelSettings.targetTexture = renderTexture;

                for (var iteration = 0; iteration < 3; iteration++)
                {
                    document.rootVisualElement?.MarkDirtyRepaint();
                    EditorUtility.SetDirty(panelSettings);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    Canvas.ForceUpdateCanvases();
                }

                var previousActive = RenderTexture.active;
                RenderTexture.active = renderTexture;

                try
                {
                    var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
                    try
                    {
                        texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                        texture.Apply();

                        var png = texture.EncodeToPNG();
                        File.WriteAllBytes(outputPath, png);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                }
                finally
                {
                    RenderTexture.active = previousActive;
                }

                Debug.Log($"BoomHud compare screenshot saved to {outputPath}");
            }
            finally
            {
                panelSettings.targetTexture = null;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static void EnsureRuntimeAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<Font>(PressStartFontPath) == null ||
                AssetDatabase.LoadAssetAtPath<Font>(LucideFontPath) == null)
            {
                Debug.LogWarning("BoomHud compare runtime asset bootstrap skipped because bundled fonts are missing.");
                return;
            }

            EnsurePanelSettingsAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static string GetCompareScreenshotOutputPath()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Could not resolve the Unity project root.");
            var repoRoot = Directory.GetParent(projectRoot)?.FullName
                ?? throw new InvalidOperationException("Could not resolve the repository root from the Unity project path.");

            return Path.Combine(repoRoot, "build", "_artifacts", "latest", "screenshots", "unity-fullpen-compare.png");
        }

        internal static void EnsureCompareFolders()
        {
            EnsureFolderPath("Assets/BoomHudCompare");
            EnsureFolderPath(SceneDirectory);
            EnsureFolderPath(PanelSettingsDirectory);
            EnsureFolderPath("Assets/Resources");
            EnsureFolderPath(FontResourcesDirectory);
        }

        internal static PanelSettings EnsurePanelSettingsAsset()
        {
            EnsureCompareFolders();

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
                panelSettings.referenceDpi = 96f;
                panelSettings.fallbackDpi = 96f;
                panelSettings.clearColor = true;
                panelSettings.colorClearValue = new Color(0.06f, 0.06f, 0.06f, 1f);

                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }

            panelSettings.textSettings = EnsurePanelTextSettingsAsset();
            EditorUtility.SetDirty(panelSettings);
            return panelSettings;
        }

        internal static PanelTextSettings EnsurePanelTextSettingsAsset()
        {
            EnsureCompareFolders();

            var textSettings = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(PanelTextSettingsPath);
            if (textSettings == null)
            {
                textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
                AssetDatabase.CreateAsset(textSettings, PanelTextSettingsPath);
            }

            var pressStartFontAsset = EnsureSdfFontAsset(
                PressStartFontPath,
                PressStartFontAssetPath,
                preferredRenderModeName: "SDFAA",
                samplingPointSize: 128,
                atlasPadding: 8,
                atlasSize: 1024);

            var lucideFontAsset = EnsureSdfFontAsset(
                LucideFontPath,
                LucideFontAssetPath,
                preferredRenderModeName: "SDFAA",
                samplingPointSize: 128,
                atlasPadding: 8,
                atlasSize: 1024);

            ConfigurePanelTextSettings(textSettings, pressStartFontAsset, lucideFontAsset);
            EditorUtility.SetDirty(textSettings);
            return textSettings;
        }

        internal static void EnsureFolderPath(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var name = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                return;
            }

            EnsureFolderPath(parent);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static UnityEngine.Object EnsureSdfFontAsset(
            string sourceFontPath,
            string fontAssetPath,
            string preferredRenderModeName,
            int samplingPointSize,
            int atlasPadding,
            int atlasSize)
        {
            var existingFontAsset = AssetDatabase.LoadAssetAtPath<FontAsset>(fontAssetPath);
            if (IsUsableFontAsset(existingFontAsset, fontAssetPath))
            {
                return existingFontAsset;
            }

            DeleteAssetIfPresent(fontAssetPath);

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"BoomHud compare setup could not load font at {sourceFontPath}");
                return null;
            }

            var createdFontAsset = CreateSdfFontAsset(sourceFont, preferredRenderModeName, samplingPointSize, atlasPadding, atlasSize) as FontAsset;
            if (createdFontAsset == null)
            {
                Debug.LogWarning($"BoomHud compare setup could not create a TextCore font asset for {sourceFontPath}");
                return null;
            }

            createdFontAsset.name = Path.GetFileNameWithoutExtension(fontAssetPath);
            AssetDatabase.CreateAsset(createdFontAsset, fontAssetPath);
            PersistFontAssetSubAssets(createdFontAsset);
            EditorUtility.SetDirty(createdFontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(fontAssetPath, ImportAssetOptions.ForceUpdate);

            var persistedFontAsset = AssetDatabase.LoadAssetAtPath<FontAsset>(fontAssetPath);
            if (!IsUsableFontAsset(persistedFontAsset, fontAssetPath))
            {
                DeleteAssetIfPresent(fontAssetPath);
                return null;
            }

            return persistedFontAsset;
        }

        private static UnityEngine.Object CreateSdfFontAsset(
            Font sourceFont,
            string preferredRenderModeName,
            int samplingPointSize,
            int atlasPadding,
            int atlasSize)
        {
            var fontAssetType = ResolveUnityType("UnityEngine.TextCore.Text.FontAsset");
            if (fontAssetType == null)
            {
                return null;
            }

            var createMethod = fontAssetType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => string.Equals(method.Name, "CreateFontAsset", System.StringComparison.Ordinal))
                .Select(method => new { Method = method, Parameters = method.GetParameters() })
                .Where(candidate => candidate.Parameters.Length > 0 && candidate.Parameters[0].ParameterType == typeof(Font))
                .OrderByDescending(candidate => candidate.Parameters.Length)
                .Select(candidate => candidate.Method)
                .FirstOrDefault();

            if (createMethod == null)
            {
                return null;
            }

            var arguments = BuildCreateFontAssetArguments(
                createMethod.GetParameters(),
                sourceFont,
                preferredRenderModeName,
                samplingPointSize,
                atlasPadding,
                atlasSize);

            return createMethod.Invoke(null, arguments) as UnityEngine.Object;
        }

        private static object[] BuildCreateFontAssetArguments(
            ParameterInfo[] parameters,
            Font sourceFont,
            string preferredRenderModeName,
            int samplingPointSize,
            int atlasPadding,
            int atlasSize)
        {
            var arguments = new object[parameters.Length];

            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                var parameterName = parameter.Name ?? string.Empty;

                if (parameter.ParameterType == typeof(Font))
                {
                    arguments[index] = sourceFont;
                    continue;
                }

                if (parameter.ParameterType == typeof(int))
                {
                    arguments[index] = parameterName switch
                    {
                        var name when name.Contains("face", System.StringComparison.OrdinalIgnoreCase) => 0,
                        var name when name.Contains("sampling", System.StringComparison.OrdinalIgnoreCase) => samplingPointSize,
                        var name when name.Contains("pointSize", System.StringComparison.OrdinalIgnoreCase) => samplingPointSize,
                        var name when name.Contains("padding", System.StringComparison.OrdinalIgnoreCase) => atlasPadding,
                        var name when name.Contains("width", System.StringComparison.OrdinalIgnoreCase) => atlasSize,
                        var name when name.Contains("height", System.StringComparison.OrdinalIgnoreCase) => atlasSize,
                        _ => parameter.HasDefaultValue ? parameter.DefaultValue : 0
                    };
                    continue;
                }

                if (parameter.ParameterType == typeof(bool))
                {
                    arguments[index] = true;
                    continue;
                }

                if (parameter.ParameterType.IsEnum)
                {
                    arguments[index] = parameterName.Contains("render", System.StringComparison.OrdinalIgnoreCase)
                        ? ParseEnumValue(parameter.ParameterType, preferredRenderModeName, "SDFAA")
                        : ParseEnumValue(parameter.ParameterType, "Dynamic", null);
                    continue;
                }

                arguments[index] = parameter.HasDefaultValue ? parameter.DefaultValue : null;
            }

            return arguments;
        }

        private static object ParseEnumValue(System.Type enumType, string preferredValue, string fallbackValue)
        {
            if (System.Enum.TryParse(enumType, preferredValue, ignoreCase: true, out var preferred))
            {
                return preferred;
            }

            if (!string.IsNullOrWhiteSpace(fallbackValue) && System.Enum.TryParse(enumType, fallbackValue, ignoreCase: true, out var fallback))
            {
                return fallback;
            }

            return System.Enum.GetValues(enumType).GetValue(0);
        }

        private static void ConfigurePanelTextSettings(
            PanelTextSettings textSettings,
            UnityEngine.Object defaultFontAsset,
            UnityEngine.Object fallbackFontAsset)
        {
            var textSettingsType = textSettings.GetType();

            textSettingsType.GetProperty("defaultFontAssetPath")?.SetValue(textSettings, "BoomHudFonts/");
            textSettingsType.GetProperty("defaultFontAsset")?.SetValue(textSettings, defaultFontAsset);

            if (textSettingsType.GetProperty("fallbackFontAssets")?.GetValue(textSettings) is IList fallbackFontAssets)
            {
                fallbackFontAssets.Clear();
                if (fallbackFontAsset != null)
                {
                    fallbackFontAssets.Add(fallbackFontAsset);
                }
            }
        }

        private static bool IsUsableFontAsset(FontAsset fontAsset, string fontAssetPath)
        {
            if (fontAsset == null)
            {
                return false;
            }

            try
            {
                var atlasTextures = fontAsset.atlasTextures;
                if (atlasTextures != null && atlasTextures.Any(texture => texture != null))
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"BoomHud compare setup rejected font asset at {fontAssetPath}: {exception.Message}");
                return false;
            }

            var hasPersistedAtlasTexture = AssetDatabase
                .LoadAllAssetsAtPath(fontAssetPath)
                .OfType<Texture2D>()
                .Any(texture => texture != null);

            if (hasPersistedAtlasTexture)
            {
                return true;
            }

            Debug.LogWarning($"BoomHud compare setup found a broken font asset at {fontAssetPath}; falling back to raw font resources.");
            return false;
        }

        private static void PersistFontAssetSubAssets(FontAsset fontAsset)
        {
            if (fontAsset.material != null && AssetDatabase.GetAssetPath(fontAsset.material) != AssetDatabase.GetAssetPath(fontAsset))
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                EditorUtility.SetDirty(fontAsset.material);
            }

            var atlasTextures = fontAsset.atlasTextures;
            if (atlasTextures == null)
            {
                return;
            }

            foreach (var atlasTexture in atlasTextures)
            {
                if (atlasTexture == null || AssetDatabase.GetAssetPath(atlasTexture) == AssetDatabase.GetAssetPath(fontAsset))
                {
                    continue;
                }

                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
                EditorUtility.SetDirty(atlasTexture);
            }
        }

        private static void DeleteAssetIfPresent(string assetPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
            {
                return;
            }

            AssetDatabase.DeleteAsset(assetPath);
        }

        private static System.Type ResolveUnityType(string fullName)
        {
            return System.AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, throwOnError: false))
                .FirstOrDefault(type => type != null);
        }
    }
}