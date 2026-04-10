using DA_Assets.DAI;
using DA_Assets.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DA_Assets.FCU
{
    public class SpriteDownloadOffline : ISpriteDownloadStrategy
    {
        private readonly FigmaConverterUnity _monoBeh;
        private readonly SpriteIdentityCache _cache;

        public ImportMode Mode => ImportMode.Offline;

        public SpriteDownloadOffline(FigmaConverterUnity monoBeh, SpriteIdentityCache cache)
        {
            _monoBeh = monoBeh;
            _cache = cache;
        }

        public async Task DownloadSprites(List<FObject> fobjects, CancellationToken token)
        {
            // Use pre-built unique representatives from the cache instead of GroupBy(GetSpriteRenderKey).
            List<FObject> toDownload;

            if (_cache != null)
            {
                IReadOnlyList<FObject> reps = _cache.UniqueRepresentatives;
                toDownload = new List<FObject>(reps.Count);
                foreach (FObject rep in reps)
                {
                    if (rep.Data.NeedDownload)
                        toDownload.Add(rep);
                }
            }
            else
            {
                // Fallback if no cache is available (should not happen in normal flow).
                toDownload = fobjects
                    .Where(x => x.Data.NeedDownload)
                    .GroupBy(SpriteRenderKeyUtility.GetSpriteRenderKey)
                    .Select(g => g.First())
                    .ToList();
            }

            if (toDownload.Count == 0)
            {
                Debug.Log(FcuLocKey.log_offline_no_sprites_to_load.Localize());
                return;
            }

            await SpriteDataCalculator.CalculateAndSetSpriteData(toDownload, _monoBeh, token);

            var offlineData = _monoBeh.CurrentProject.OfflineData;
            var format = _monoBeh.Settings.ImageSpritesSettings.ImageFormat;

            Debug.Log(FcuLocKey.log_offline_loading_sprites.Localize(toDownload.Count, offlineData.ImagesFolder));
            _monoBeh.EditorDelegateHolder.StartProgress?.Invoke(_monoBeh, ProgressBarCategory.DownloadingSprites, toDownload.Count, false);

            int loadedCount = 0;
            int failedCount = 0;

            foreach (var fobj in toDownload)
            {
                if (token.IsCancellationRequested)
                    break;

                string sanitizedId = OfflineProjectData.SanitizeNodeId(fobj.Id);
                string filePath = GetPath(offlineData.ImagesFolder, sanitizedId, format);

                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    try
                    {
                        byte[] bytes = await ReadFileAsync(filePath, token);
                        Debug.Log(FcuLocKey.log_offline_image_added.Localize(filePath));
                        SpriteBatchWriter.Add(fobj, bytes);
                        loadedCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(FcuLocKey.log_offline_image_load_failed.Localize(fobj.Id, ex.Message));
                        failedCount++;
                    }
                }
                else
                {
                    Debug.LogError(FcuLocKey.log_offline_image_not_found.Localize(fobj.Id, $"{sanitizedId}.{format}"));
                    failedCount++;
                }

                _monoBeh.EditorDelegateHolder.UpdateProgress?.Invoke(_monoBeh, ProgressBarCategory.DownloadingSprites, loadedCount + failedCount);
            }

            _monoBeh.EditorDelegateHolder.CompleteProgress?.Invoke(_monoBeh, ProgressBarCategory.DownloadingSprites);
            Debug.Log(FcuLocKey.log_offline_sprites_loaded.Localize(loadedCount, toDownload.Count, failedCount));
        }

        private string GetPath(string imagesFolder, string sanitizedId, ImageFormat format)
        {
            string ext = format.ToString().ToLower();
            string path = Path.Combine(imagesFolder, $"{sanitizedId}.{ext}");

            if (File.Exists(path))
                return path;

            return null;
        }

        private async Task<byte[]> ReadFileAsync(string filePath, CancellationToken token)
        {
#if UNITY_2021_3_OR_NEWER
            return await File.ReadAllBytesAsync(filePath, token);
#else
            return await Task.Run(() => File.ReadAllBytes(filePath), token);
#endif
        }
    }
}
