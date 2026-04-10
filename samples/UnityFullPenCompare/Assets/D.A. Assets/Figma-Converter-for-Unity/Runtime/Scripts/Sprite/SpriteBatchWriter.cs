using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DA_Assets.FCU.Model;
using Debug = UnityEngine.Debug;
using DA_Assets.DAI;
using System.Collections.Concurrent;
using DA_Assets.Logging;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DA_Assets.FCU
{
    public static class SpriteBatchWriter
    {
        private struct PendingSprite
        {
            public FObject FObject;
            public byte[] Data;
        }

        private static readonly ConcurrentQueue<PendingSprite> _pending = new ConcurrentQueue<PendingSprite>();

        // Set by ProjectImporterBase before calling Flush so WriteGuidMetaAsync
        // can read the cached render-key instead of recomputing it.
        private static SpriteIdentityCache _identityCache;

        public static void SetCache(SpriteIdentityCache cache) => _identityCache = cache;

        public static void Add(FObject fobject, byte[] data)
        {
            _pending.Enqueue(new PendingSprite
            {
                FObject = fobject,
                Data = data
            });
        }

        public static async Task Flush(UnityEngine.Object key, CancellationToken token)
        {
            if (_pending.IsEmpty)
                return;

            List<PendingSprite> pendingSnapshot = new List<PendingSprite>();
            while (_pending.TryDequeue(out PendingSprite pendingSprite))
            {
                pendingSnapshot.Add(pendingSprite);
            }

            if (pendingSnapshot.Count == 0)
                return;

            int totalCount = pendingSnapshot.Count;
            FigmaConverterUnity fcu = key as FigmaConverterUnity;

            if (fcu != null)
            {
                fcu.EditorDelegateHolder.StartProgress?.Invoke(key, ProgressBarCategory.WritingSprites, totalCount, false);
            }

            try
            {
                await WriteSpritesAsync(pendingSnapshot, fcu, key, token);
#if UNITY_EDITOR
                AssetDatabase.Refresh();
#endif
                await WaitForMetaFilesAsync(pendingSnapshot, token);
                await WriteGuidMetaAsync(pendingSnapshot, token);
#if UNITY_EDITOR
                AssetDatabase.Refresh();
#endif
            }
            finally
            {
                if (fcu != null)
                {
                    fcu.EditorDelegateHolder.CompleteProgress?.Invoke(key, ProgressBarCategory.WritingSprites);
                }
            }
        }

        private static async Task WriteSpritesAsync(List<PendingSprite> sprites, FigmaConverterUnity fcu, UnityEngine.Object key, CancellationToken token)
        {
            for (int i = 0; i < sprites.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                PendingSprite sprite = sprites[i];
                string dir = Path.GetDirectoryName(sprite.FObject.Data.SpritePath);

                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                try
                {
                    using (var stream = new FileStream(sprite.FObject.Data.SpritePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                    {
                        await stream.WriteAsync(sprite.Data, 0, sprite.Data.Length, token);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }


                fcu?.EditorDelegateHolder.UpdateProgress?.Invoke(key, ProgressBarCategory.WritingSprites, i + 1);
                await Task.Yield();
            }
        }

        private static async Task WaitForMetaFilesAsync(List<PendingSprite> sprites, CancellationToken token)
        {
            var remaining = new List<PendingSprite>(sprites);
            Stopwatch sw = Stopwatch.StartNew();
            const double timeoutSeconds = 60;

            while (remaining.Count > 0)
            {
                token.ThrowIfCancellationRequested();

                remaining.RemoveAll(ps => File.Exists(ps.FObject.Data.SpritePath + ".meta"));

                if (remaining.Count == 0)
                    return;

                if (sw.Elapsed.TotalSeconds > timeoutSeconds)
                {
                    Debug.LogError(FcuLocKey.log_sprite_batch_writer_timeout.Localize());
                    return;
                }

                await Task.Delay(200, token);
            }
        }

        private static async Task WriteGuidMetaAsync(List<PendingSprite> sprites, CancellationToken token)
        {
            foreach (PendingSprite ps in sprites)
            {
                token.ThrowIfCancellationRequested();

                // Use the pre-built cache when available; fall back to recomputing.
                int renderKey = _identityCache != null
                    ? _identityCache.GetRenderKey(ps.FObject)
                    : SpriteRenderKeyUtility.GetSpriteRenderKey(ps.FObject);

                GuidMetaUtility.WriteGuid(
                    ps.FObject.Data.SpritePath + ".meta",
                    renderKey);

                await Task.Yield();
            }
        }
    }
}
