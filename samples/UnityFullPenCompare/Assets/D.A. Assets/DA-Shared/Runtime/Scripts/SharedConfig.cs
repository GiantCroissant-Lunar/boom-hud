using DA_Assets.Constants;
using DA_Assets.Singleton;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DA_Assets.Shared
{
    [CreateAssetMenu(menuName = DAConstants.Publisher + "/Shared Config")]
    public class SharedConfig : AssetConfig<SharedConfig>
    {
        [Tooltip("McpProxy template file")]
        [SerializeField] TextAsset _pythonProxyTemplate;
        public static TextAsset PythonProxyTemplate => Instance._pythonProxyTemplate;

        [Header("MCP Proxy")]
        [SerializeField] string _pythonCommand = "python";
        public static string PythonCommand => Instance._pythonCommand;

        [Header("Docs Getter")]
        [SerializeField] DocsGetterSettings docsGetterSettings = DocsGetterSettings.Default;
        public static DocsGetterSettings DocsGetterSettings => Instance.docsGetterSettings;
    }

    [Serializable]
    public struct DocsGetterSettings
    {
        public Object PythonDocsGetter;
        public int CacheExpiryHours;
        public string UserAgent;
        public int TimeoutSeconds;
        public bool CacheEnabled;

        public static DocsGetterSettings Default => new DocsGetterSettings
        {
            PythonDocsGetter = null,
            CacheExpiryHours = 720,
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            TimeoutSeconds = 30,
            CacheEnabled = true
        };
    }
}
