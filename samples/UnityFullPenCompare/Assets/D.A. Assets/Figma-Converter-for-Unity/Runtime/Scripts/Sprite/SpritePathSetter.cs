using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#pragma warning disable CS1998

namespace DA_Assets.FCU
{
    [Serializable]
    public class SpritePathSetter : FcuBase
    {
        public async Task SetSpritePaths(List<FObject> fobjects, SpriteIdentityCache cache, CancellationToken token)
        {
#if UNITY_EDITOR
            await Task.Yield();

            string[] assetSpritePaths;

            if (monoBeh.IsPlaying())
            {
                string root = Path.Combine(
                   Application.persistentDataPath,
                   monoBeh.Settings.ImageSpritesSettings.SpritesPath);

                assetSpritePaths = Directory.Exists(root)
                    ? Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                               .ToArray()
                    : Array.Empty<string>();
            }
            else
            {
                string filter = $"t:{typeof(Sprite).Name}";

                string[] searchInFolder = new string[]
                {
                    monoBeh.Settings.ImageSpritesSettings.SpritesPath
                };

                assetSpritePaths = UnityEditor.AssetDatabase
                     .FindAssets(filter, searchInFolder)
                     .Select(x => UnityEditor.AssetDatabase.GUIDToAssetPath(x))
                     .ToArray();
            }

            // Build a single O(1) lookup: renderKey -> existing asset path on disk.
            // This replaces the per-item linear scan of assetSpritePaths.
            cache.BuildExistingSpritePathLookup(assetSpritePaths);

            IReadOnlyList<FObject> uniqueRepresentatives = cache.UniqueRepresentatives;

            for (int i = 0; i < uniqueRepresentatives.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                FObject item = uniqueRepresentatives[i];
                int renderKey = cache.GetRenderKey(item);

                bool imageFileExists;
                string spritePath;

                if (cache.TryGetExistingPath(renderKey, out string existingPath)
                    && IsTargetExtension(item, existingPath))
                {
                    // Found a valid file on disk for this render-key.
                    imageFileExists = true;
                    spritePath = existingPath;
                }
                else
                {
                    // No matching file on disk — generate a target path.
                    imageFileExists = false;
                    spritePath = GetSpritePath(item);
                }

                SetNeedDownloadFileFlag(item, imageFileExists);
                SetNeedGenerateFlag(item, imageFileExists);

                // Propagate to all objects that share the same render-key (O(1) group lookup).
                IReadOnlyList<FObject> group = cache.GetGroup(renderKey);
                foreach (FObject fo in group)
                {
                    fo.Data.SpritePath = spritePath;
                }

                // Re-apply download/generate flags to all group members (representative already done above).
                for (int j = 1; j < group.Count; j++)
                {
                    SetNeedDownloadFileFlag(group[j], imageFileExists);
                    SetNeedGenerateFlag(group[j], imageFileExists);
                }

                if (i % 500 == 0)
                {
                    await Task.Yield();
                }
            }
#endif
        }

        private string GetSpritePath(FObject fobject)
        {
            string spriteDir = fobject.Data.IsMutual
                ? "Mutual"
                : fobject.Data.RootFrame.Names.FolderName;

            string root = monoBeh.IsPlaying()
                ? Path.Combine(Application.persistentDataPath, monoBeh.Settings.ImageSpritesSettings.SpritesPath)
                : monoBeh.Settings.ImageSpritesSettings.SpritesPath.GetFullAssetPath();

            string absoluteFramePath = Path.Combine(root, spriteDir);
            absoluteFramePath.CreateFolderIfNotExists();

            string fileName = SpriteRenderKeyUtility.GetSpriteFileName(fobject);

            return monoBeh.IsPlaying()
                ? Path.Combine(absoluteFramePath, fileName)
                : Path.Combine(monoBeh.Settings.ImageSpritesSettings.SpritesPath, spriteDir, fileName).ToUnityPath();
        }

        private bool IsTargetExtension(FObject fobject, string spritePath)
        {
            string spriteExt = Path.GetExtension(spritePath);

            if (spriteExt.StartsWith(".") && spriteExt.Length > 1)
                spriteExt = spriteExt.Remove(0, 1);

            ImageFormat? targetExt = null;

            if (monoBeh.UsingSvgImage())
            {
                if (fobject.CanUseUnityImage(monoBeh))
                {
                    targetExt = ImageFormat.PNG;
                }
            }

            if (targetExt == null)
            {
                targetExt = monoBeh.Settings.ImageSpritesSettings.ImageFormat;
            }

            return spriteExt.ToLower() == targetExt.ToLower();
        }

        // Kept for internal use by GetSpritePath; public overload with string[] removed
        // since all callers now use SpriteIdentityCache.TryGetExistingPath.
        public bool GetSpritePath(FObject fobject, string[] spritePathes, out string path)
        {
            int renderKey = SpriteRenderKeyUtility.GetSpriteRenderKey(fobject);

            foreach (string spritePath in spritePathes)
            {
                if (!IsTargetExtension(fobject, spritePath))
                {
                    continue;
                }

                if (!GuidMetaUtility.TryExtractData(
                     spritePath + ".meta",
                     out int hash))
                {
                    continue;
                }

                if (SpriteRenderKeyUtility.MatchesPackedGuid(renderKey, hash))
                {
                    path = spritePath;
                    return true;
                }
            }

            path = null;
            return false;
        }

        private void SetNeedDownloadFileFlag(FObject fobject, bool imageFileExists)
        {
            if (fobject.IsDownloadableType()/* || fobject.IsGenerativeType()*/)
            {
                if (monoBeh.Settings.ImageSpritesSettings.RedownloadSprites)
                {
                    fobject.Data.NeedDownload = true;
                }
                else if (imageFileExists)
                {
                    fobject.Data.NeedDownload = false;
                }
                else
                {
                    fobject.Data.NeedDownload = true;
                }
            }
            else
            {
                fobject.Data.NeedDownload = false;
            }
        }

        private void SetNeedGenerateFlag(FObject fobject, bool imageFileExists)
        {
            if (fobject.IsGenerativeType())
            {
                if (monoBeh.Settings.ImageSpritesSettings.RedownloadSprites)
                {
                    fobject.Data.NeedGenerate = true;
                }
                else if (imageFileExists)
                {
                    fobject.Data.NeedGenerate = false;
                }
                else
                {
                    fobject.Data.NeedGenerate = true;
                }
            }
            else
            {
                fobject.Data.NeedGenerate = false;
            }
        }
    }
}
