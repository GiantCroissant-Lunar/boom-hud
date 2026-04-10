using DA_Assets.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#if JSONNET_PLASTIC_EXISTS
using Unity.Plastic.Newtonsoft.Json;
#elif JSONNET_EXISTS
using Newtonsoft.Json;
#endif

namespace DA_Assets.FCU
{
    [Serializable]
    public class SpriteDownloader : FcuBase
    {
        private ISpriteDownloadStrategy _strategy;
        private ImportMode _cachedMode;

        // Set by ProjectImporterBase before calling DownloadSprites.
        // Resetting _strategy forces the strategy to be recreated with the new cache.
        private SpriteIdentityCache _identityCache;
        internal SpriteIdentityCache IdentityCache
        {
            get => _identityCache;
            set
            {
                _identityCache = value;
                _strategy = null; // force recreation with the new cache
            }
        }

        private ISpriteDownloadStrategy GetStrategy(SpriteIdentityCache cache)
        {
            var currentMode = monoBeh.Settings.MainSettings.ImportMode;
            if (_strategy == null || _cachedMode != currentMode)
            {
                _cachedMode = currentMode;
                _strategy = currentMode switch
                {
                    ImportMode.Online  => new SpriteDownloadOnline(monoBeh, cache),
                    ImportMode.Offline => new SpriteDownloadOffline(monoBeh, cache),
                    _ => throw new NotSupportedException($"ImportMode {currentMode} is not supported")
                };
            }
            return _strategy;
        }
        
        public Task DownloadSprites(List<FObject> fobjects, CancellationToken token)
        {
            return GetStrategy(IdentityCache).DownloadSprites(fobjects, token);
        }

        public static void LogFailedDownloads(ConcurrentBag<FObject> failedObjects, int splitLimit)
        {
            if (failedObjects.IsEmpty())
            {
                return;
            }

            List<List<string>> components = failedObjects.Select(x => x.Data.NameHierarchy).Split(splitLimit);

            foreach (List<string> component in components)
            {
                string hierarchies = string.Join("\n", component);
                Debug.LogError(FcuLocKey.log_malformed_url.Localize(component.Count, hierarchies));
            }
        }
    }

    public struct FigmaImageRequest
    {
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("err")]
#endif
        public string error;
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
        [JsonProperty("images")]
#endif
        // key = id, value = link
        public Dictionary<string, string> images;
    }

    public struct SpriteData
    {
        public FObject FObject { get; set; }
        public string Format { get; set; }
        public string Link { get; set; }
        public float Scale { get; set; }
    }

    public struct ImageFormatScaleKey
    {
        public ImageFormat ImageFormat { get; set; }
        public float Scale { get; set; }
    }
}