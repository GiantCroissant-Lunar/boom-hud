using DA_Assets.Extensions;
using DA_Assets.FCU.Model;
using DA_Assets.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DA_Assets.FCU
{
    public class SpriteDownloadOnline : ISpriteDownloadStrategy
    {
        private readonly FigmaConverterUnity _monoBeh;
        private readonly SpriteIdentityCache _cache;
        
        private int _maxConcurrentDownloads = 100;
        private int _maxDownloadAttempts = 3;
        private float _maxChunkSize = 24_000_000;
        private int _maxSpritesCount = 100;
        private int _errorLogSplitLimit = 50;
        
        public ImportMode Mode => ImportMode.Online;
        
        public SpriteDownloadOnline(FigmaConverterUnity monoBeh, SpriteIdentityCache cache)
        {
            _monoBeh = monoBeh;
            _cache = cache;
        }
        
        public async Task DownloadSprites(List<FObject> fobjects, CancellationToken token)
        {
            // Use pre-built unique representatives from the cache instead of GroupBy(GetSpriteRenderKey).
            List<FObject> uniqueFObjectsToDownload;

            if (_cache != null)
            {
                IReadOnlyList<FObject> reps = _cache.UniqueRepresentatives;
                uniqueFObjectsToDownload = new List<FObject>(reps.Count);
                foreach (FObject rep in reps)
                {
                    if (rep.Data.NeedDownload)
                        uniqueFObjectsToDownload.Add(rep);
                }
            }
            else
            {
                // Fallback if no cache is available (should not happen in normal flow).
                uniqueFObjectsToDownload = fobjects
                    .Where(x => x.Data.NeedDownload)
                    .GroupBy(SpriteRenderKeyUtility.GetSpriteRenderKey)
                    .Select(g => g.First())
                    .ToList();
            }

            if (uniqueFObjectsToDownload.IsEmpty())
            {
                Debug.Log(FcuLocKey.log_sprite_downloader_no_sprites.Localize());
                return;
            }

            await SpriteDataCalculator.CalculateAndSetSpriteData(uniqueFObjectsToDownload, _monoBeh, token);

            Dictionary<ImageFormatScaleKey, List<List<SpriteData>>> chunks = SpriteChunker.CreateChunks(
                uniqueFObjectsToDownload,
                _maxChunkSize,
                _maxSpritesCount);

            List<SpriteData> spritesWithLinks = await FigmaLinkFetcher.FetchLinksAsync(
                chunks,
                _monoBeh,
                token);

            ConcurrentBag<FObject> failedObjects = await ConcurrentSpriteDownloader.DownloadAllAsync(
                spritesWithLinks,
                _maxConcurrentDownloads,
                _maxDownloadAttempts,
                _monoBeh,
                token);

            LogFailedDownloads(failedObjects);
        }
        
        private void LogFailedDownloads(ConcurrentBag<FObject> failedObjects)
        {
            if (failedObjects.IsEmpty())
            {
                return;
            }

            List<List<string>> components = failedObjects.Select(x => x.Data.NameHierarchy).Split(_errorLogSplitLimit);

            foreach (List<string> component in components)
            {
                string hierarchies = string.Join("\n", component);
                Debug.LogError(FcuLocKey.log_malformed_url.Localize(component.Count, hierarchies));
            }
        }
    }
}
