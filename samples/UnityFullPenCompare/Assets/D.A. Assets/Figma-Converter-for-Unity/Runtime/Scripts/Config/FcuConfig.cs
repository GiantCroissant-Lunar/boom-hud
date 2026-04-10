using DA_Assets.Constants;
using DA_Assets.Extensions;
using DA_Assets.FCU.Model;
using DA_Assets.Singleton;
using DA_Assets.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

#pragma warning disable CS0649

namespace DA_Assets.FCU
{
    [CreateAssetMenu(menuName = DAConstants.Publisher + "/FcuConfig")]
    public class FcuConfig : AssetConfig<FcuConfig>
    {
        [SerializeField] List<TagConfig> tags;
        public static List<TagConfig> TagConfigs => Instance.tags;

        [Header("Docs Getter")]
        [SerializeField] string docsBaseUrl = "https://da-assets.gitbook.io/docs";
        public static string DocsBaseUrl => Instance.docsBaseUrl;

        [Header("File names")]
        [SerializeField] string webLogFileName;
        public static string WebLogFileName => Instance.webLogFileName;

        [SerializeField] string offlineManifestFileName = "manifest.json";
        public static string OfflineManifestFileName => Instance.offlineManifestFileName;

        [SerializeField] string offlineExtractFolderName = "FCU_Offline_Import";
        public static string OfflineExtractFolderName => Instance.offlineExtractFolderName;

        [SerializeField] string offlineNodeFileExtension = ".json";
        public static string OfflineNodeFileExtension => Instance.offlineNodeFileExtension;

        [SerializeField] string defaultTmpShaderName = "TextMeshPro/Distance Field";
        public static string DefaultTmpShaderName => Instance.defaultTmpShaderName;

        [Header("Formats")]
        [SerializeField] string dateTimeFormat1;
        public static string DateTimeFormat1 => Instance.dateTimeFormat1;

        [Header("GameObject names")]
        [SerializeField] string canvasGameObjectName;
        public static string CanvasGameObjectName => Instance.canvasGameObjectName;

        [SerializeField] string i2LocGameObjectName;
        public static string I2LocGameObjectName => Instance.i2LocGameObjectName;

        [Header("Values")]

        [SerializeField] RoundingConfig rounding;
        public static RoundingConfig Rounding => Instance.rounding;

        [SerializeField] int recentProjectsLimit = 20;
        public static int RecentProjectsLimit => Instance.recentProjectsLimit;

        [SerializeField] int figmaSessionsLimit = 10;
        public static int FigmaSessionsLimit => Instance.figmaSessionsLimit;

        [SerializeField] int logFilesLimit = 50;
        public static int LogFilesLimit => Instance.logFilesLimit;

        [SerializeField] int maxRenderSize = 4096;
        public static int MaxRenderSize => Instance.maxRenderSize;

        [SerializeField] int renderUpscaleFactor = 2;
        public static int RenderUpscaleFactor => Instance.renderUpscaleFactor;

        [SerializeField] int spriteDownloadTimeoutSeconds = 180;
        public static int SpriteDownloadTimeoutSeconds => Instance.spriteDownloadTimeoutSeconds;

        [SerializeField] string blurredObjectTag = "UIBlur";
        public static string BlurredObjectTag => Instance.blurredObjectTag;

        [SerializeField] string blurCameraTag = "BackgroundBlur";
        public static string BlurCameraTag => Instance.blurCameraTag;

        [SerializeField] char realTagSeparator = '-';
        public static char RealTagSeparator => Instance.realTagSeparator;

        [Tooltip("If an object has more than **N** children, **SmartTags** and **Hashes** will not be assigned to them.")]
        [SerializeField] int childParsingLimit = 512;
        public static int ChildParsingLimit => Instance.childParsingLimit;

        [Header("Api")]

        [SerializeField] int chunkSizeGetNodes;
        public static int ChunkSizeGetNodes => Instance.chunkSizeGetNodes;

        [SerializeField] int frameListDepth = 2;
        public static int FrameListDepth => Instance.frameListDepth;

        [SerializeField] string gFontsApiKey;
        public static string GoogleFontsApiKey { get => Instance.gFontsApiKey; set => Instance.gFontsApiKey = value; }

        [Header("Other")]

        [SerializeField] ScriptableObject fuitkConfig;
        public static ScriptableObject FuitkConfig => Instance.fuitkConfig;

        [SerializeField] ScriptableObject mcpServerConfig;
        public static ScriptableObject McpServerConfig => Instance.mcpServerConfig;

#if UNITY_EDITOR
        [SerializeField] UnityEditor.MonoScript uitkConverterScript;
        public static UnityEditor.MonoScript UitkConverterScript => Instance.uitkConverterScript;
#endif

        [SerializeField] Sprite whiteSprite32px;
        public static Sprite WhiteSprite32px => Instance.whiteSprite32px;

        [SerializeField] Sprite missingImageTexture128px;
        public static Sprite MissingImageTexture128px => Instance.missingImageTexture128px;

        [SerializeField] TextAsset baseClass;
        public static TextAsset BaseClass => Instance.baseClass;

        [SerializeField] Material imageLinearMaterial;
        public static Material ImageLinearMaterial => Instance.imageLinearMaterial;

        [SerializeField] VectorMaterials vectorMaterials;
        public static VectorMaterials VectorMaterials => Instance.vectorMaterials;

        [Header("Space Between Prefabs")]
        [SerializeField] GameObject horizontalSpacePrefab;
        public static GameObject HorizontalSpacePrefab => Instance.horizontalSpacePrefab;

        [SerializeField] GameObject verticalSpacePrefab;
        public static GameObject VerticalSpacePrefab => Instance.verticalSpacePrefab;

        [Header("Reason Descriptions")]

        [SerializeField] SerializedDictionary<ReasonKey, string> reasonDescriptions = new();
        public static SerializedDictionary<ReasonKey, string> ReasonDescriptions => Instance.reasonDescriptions;

        public static float IMAGE_SCALE_MIN => 0.25f;
        public static float IMAGE_SCALE_MAX => 4f;

        [SerializeField] char hierarchyDelimiter = '/';
        public static char HierarchyDelimiter => Instance.hierarchyDelimiter;

        [SerializeField] string parentId = "603951929:602259738";
        public static string PARENT_ID => Instance.parentId;

        [SerializeField] char asterisksChar = '•';
        public static char AsterisksChar => Instance.asterisksChar;

        public static string DefaultLocalizationCulture => "en-US";

        [SerializeField] string rateMePrefsKey = "DONT_SHOW_RATEME";
        public static string RATEME_PREFS_KEY => Instance.rateMePrefsKey;

        [SerializeField] string recentProjectsPrefsKey = "recentProjectsPrefsKey";
        public static string RECENT_PROJECTS_PREFS_KEY => Instance.recentProjectsPrefsKey;

        [SerializeField] string figmaSessionsPrefsKey = "FigmaSessions";
        public static string FIGMA_SESSIONS_PREFS_KEY => Instance.figmaSessionsPrefsKey;



        public const string ProductName = "Figma Converter for Unity";
        public const string ProductNameShort = "FCU";
        public const string DestroyChilds = "Destroy childs";
        public const string SetFcuToSyncHelpers = "Set current FCU to SyncHelpers";
        public const string CompareTwoObjects = "Compare two selected objects";
        public const string DestroyLastImported = "Destroy last imported frames";
        public const string DestroySyncHelpers = "Destroy SyncHelpers";
        public const string CreatePrefabs = "Create Prefabs";
        public const string UpdatePrefabs = "Update Prefabs";
        public const string Create = "Create";
        public const string OptimizeSyncHelpers = "Optimize SyncHelpers";
        public const string GenerateScripts = "Generate scripts";

        public const string DefaultClientId = "LaB1ONuPoY7QCdfshDbQbT";

        [SerializeField]
        string clientId = DefaultClientId;
        public static string ClientId => Instance.clientId;

        public const string DefaultClientSecret = "E9PblceydtAyE7Onhg5FHLmnvingDp";

        [SerializeField]
        string clientSecret = DefaultClientSecret;
        public static string ClientSecret => Instance.clientSecret;

        public const string DefaultRedirectUri = "http://localhost:1923/";

        [SerializeField]
        string redirectUri = DefaultRedirectUri;
        public static string RedirectUri => Instance.redirectUri;

        public const FigmaScope DefaultScopes =
            FigmaScope.CurrentUserRead |
            FigmaScope.FileContentRead |
            FigmaScope.LibraryContentRead;

        [SerializeField]
        FigmaScope scopes = DefaultScopes;
        public static FigmaScope Scopes => Instance.scopes;

        public static string GetOAuthUrl(FigmaScope scopes) =>
            $"https://www.figma.com/oauth?client_id={{0}}&redirect_uri={{1}}&scope={GetScopesAsString(scopes)}&state={{2}}&response_type=code";

        private static string logPath;
        public static string LogPath
        {
            get
            {
                if (logPath.IsEmpty())
                    logPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");

                logPath.CreateFolderIfNotExists();

                return logPath;
            }
        }

        private static string cachePath;
        public static string CachePath
        {
            get
            {
                if (cachePath.IsEmpty())
                {
                    string tempFolder = Path.GetTempPath();
                    cachePath = Path.Combine(tempFolder, "FcuCache");
                }

                cachePath.CreateFolderIfNotExists();

                return cachePath;
            }
        }

        public static string GetScopesAsString(FigmaScope scopes)
        {
            var selectedScopes = new List<string>();

            foreach (FigmaScope scope in Enum.GetValues(typeof(FigmaScope)))
            {
                if (scopes.HasFlag(scope))
                {
                    switch (scope)
                    {
                        case FigmaScope.CurrentUserRead:
                            selectedScopes.Add("current_user:read");
                            break;
                        case FigmaScope.FileContentRead:
                            selectedScopes.Add("file_content:read");
                            break;
                        case FigmaScope.LibraryContentRead:
                            selectedScopes.Add("library_content:read");
                            break;
                        case FigmaScope.LibraryAnalyticsRead:
                            selectedScopes.Add("library_analytics:read");
                            break;
                        case FigmaScope.LibraryAssetsRead:
                            selectedScopes.Add("library_assets:read");
                            break;
                        case FigmaScope.OrgActivityLogRead:
                            selectedScopes.Add("org:activity_log_read");
                            break;
                        case FigmaScope.OrgDiscoveryRead:
                            selectedScopes.Add("org:discovery_read");
                            break;
                        case FigmaScope.ProjectsRead:
                            selectedScopes.Add("projects:read");
                            break;
                        case FigmaScope.SelectionsRead:
                            selectedScopes.Add("selections:read");
                            break;
                        case FigmaScope.TeamLibraryContentRead:
                            selectedScopes.Add("team_library_content:read");
                            break;
                        case FigmaScope.WebhooksRead:
                            selectedScopes.Add("webhooks:read");
                            break;
                        case FigmaScope.WebhooksWrite:
                            selectedScopes.Add("webhooks:write");
                            break;
                        case FigmaScope.FileCommentsRead:
                            selectedScopes.Add("file_comments:read");
                            break;
                        case FigmaScope.FileCommentsWrite:
                            selectedScopes.Add("file_comments:write");
                            break;
                        case FigmaScope.FileDevResourcesRead:
                            selectedScopes.Add("file_dev_resources:read");
                            break;
                        case FigmaScope.FileDevResourcesWrite:
                            selectedScopes.Add("file_dev_resources:write");
                            break;
                        case FigmaScope.FileMetadataRead:
                            selectedScopes.Add("file_metadata:read");
                            break;
                        case FigmaScope.FileVariablesRead:
                            selectedScopes.Add("file_variables:read");
                            break;
                        case FigmaScope.FileVariablesWrite:
                            selectedScopes.Add("file_variables:write");
                            break;
                        case FigmaScope.FileVersionsRead:
                            selectedScopes.Add("file_versions:read");
                            break;
                    }
                }
            }

            return string.Join("%20", selectedScopes);
        }
    }
}
