using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DA_Assets.FCU
{
    /// <summary>
    /// Stores the result of a single top-down sprite-identity pass over the node list.
    /// Built once per import stage via <see cref="SpriteIdentityCacheBuilder.Build"/>,
    /// then shared across SetSpritePaths, downloaders, and SpriteBatchWriter.
    /// </summary>
    public sealed class SpriteIdentityCache
    {
        // Cached render-key per FObject (avoids re-calling GetSpriteRenderKey anywhere).
        private readonly Dictionary<FObject, int> _renderKeys;

        // Pre-built groups: render-key -> list of FObjects with the same key.
        private readonly Dictionary<int, List<FObject>> _byRenderKey;

        // One representative per unique render-key (the "noDuplicates" list).
        private readonly List<FObject> _uniqueRepresentatives;

        // Filled by BuildExistingSpritePathLookup: render-key -> existing asset path on disk.
        private Dictionary<int, string> _existingPathByKey;

        internal SpriteIdentityCache(
            Dictionary<FObject, int> renderKeys,
            Dictionary<int, List<FObject>> byRenderKey,
            List<FObject> uniqueRepresentatives)
        {
            _renderKeys = renderKeys;
            _byRenderKey = byRenderKey;
            _uniqueRepresentatives = uniqueRepresentatives;
        }

        /// <summary>Returns the cached render-key for a given FObject (O(1)).</summary>
        internal int GetRenderKey(FObject fobject) =>
            _renderKeys.TryGetValue(fobject, out int k) ? k : 0;

        /// <summary>One representative per unique render-key.</summary>
        internal IReadOnlyList<FObject> UniqueRepresentatives => _uniqueRepresentatives;

        /// <summary>All FObjects that share the given render-key.</summary>
        internal IReadOnlyList<FObject> GetGroup(int renderKey) =>
            _byRenderKey.TryGetValue(renderKey, out var list) ? list : Array.Empty<FObject>() as IReadOnlyList<FObject>;

        /// <summary>
        /// Scans <paramref name="assetSpritePaths"/> once and builds an O(1) lookup.
        /// Must be called before <see cref="TryGetExistingPath"/>.
        /// </summary>
        internal void BuildExistingSpritePathLookup(string[] assetSpritePaths)
        {
            _existingPathByKey = new Dictionary<int, string>();

            foreach (string spritePath in assetSpritePaths)
            {
                if (!GuidMetaUtility.TryExtractData(spritePath + ".meta", out int hash))
                    continue;

                if (!_existingPathByKey.ContainsKey(hash))
                    _existingPathByKey[hash] = spritePath;
            }
        }

        /// <summary>
        /// Returns the pre-indexed existing sprite path for the given render-key, or null.
        /// </summary>
        internal bool TryGetExistingPath(int renderKey, out string spritePath)
        {
            if (_existingPathByKey != null && _existingPathByKey.TryGetValue(renderKey, out spritePath))
                return true;

            spritePath = null;
            return false;
        }
    }
}
