using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;

namespace DA_Assets.FCU
{
    /// <summary>
    /// Builds a <see cref="SpriteIdentityCache"/> in a single top-down pass over a flat node list.
    ///
    /// Key design decisions:
    /// - Absolute matrix angle is propagated top-down (parentAbsoluteAngle + localAngle)
    ///   so each ancestor is visited exactly once instead of re-walking the parent chain
    ///   for every descendant (eliminates the while(parent) loop for 325 -> ~N calls).
    /// - GetSpriteRenderKey is called once per FObject and stored; all downstream
    ///   consumers read from the cache.
    /// - objectsByRenderKey groups are built in the same pass for O(1) lookup later.
    /// </summary>
    internal static class SpriteIdentityCacheBuilder
    {
        /// <summary>
        /// Builds the sprite identity cache from a flat list of nodes.
        /// The list must already be in top-down order (as produced by ConvertTreeToList),
        /// so that a parent is always processed before its children.
        /// </summary>
        internal static SpriteIdentityCache Build(List<FObject> fobjects)
        {
            // Pass 1: compute absolute angle top-down and cache it per FObject.
            // Because the list is in pre-order, parent is always visited before children.
            Dictionary<FObject, float> absoluteAngleCache = new Dictionary<FObject, float>(fobjects.Count);

            foreach (FObject fo in fobjects)
            {
                float parentAngle = 0f;

                if (fo.Data?.Parent != null && absoluteAngleCache.TryGetValue(fo.Data.Parent, out float p))
                    parentAngle = p;

                float localAngle = fo.Data != null && fo.Data.HasTransformComputationCache
                    ? fo.Data.CachedMatrixAngle
                    : fo.GetAngleFromMatrix();
                absoluteAngleCache[fo] = parentAngle + localAngle;
            }

            // Pass 2: compute render-key and build groups.
            // Render-key depends on the cached absolute angle, so this cannot be merged with pass 1.
            Dictionary<FObject, int> renderKeys = new Dictionary<FObject, int>(fobjects.Count);
            Dictionary<int, List<FObject>> byRenderKey = new Dictionary<int, List<FObject>>();
            List<FObject> uniqueRepresentatives = new List<FObject>();

            foreach (FObject fo in fobjects)
            {
                if (!(fo.IsDownloadableType() || fo.IsGenerativeType()))
                    continue;

                int key = ComputeRenderKey(fo, absoluteAngleCache);
                renderKeys[fo] = key;

                if (!byRenderKey.TryGetValue(key, out List<FObject> group))
                {
                    group = new List<FObject>();
                    byRenderKey[key] = group;
                    uniqueRepresentatives.Add(fo); // first encounter => unique representative
                }

                group.Add(fo);
            }

            return new SpriteIdentityCache(renderKeys, byRenderKey, uniqueRepresentatives);
        }

        /// <summary>
        /// Replicates the logic of <see cref="SpriteRenderKeyUtility.GetSpriteRenderKey"/>
        /// but reads the absolute angle from the pre-built cache instead of walking parents.
        /// </summary>
        private static int ComputeRenderKey(FObject fobject, Dictionary<FObject, float> absoluteAngleCache)
        {
            if (ReferenceEquals(fobject, null) || fobject.Data == null)
                return 0;

            if (!fobject.IsDownloadableType())
                return fobject.Data.Hash;

            absoluteAngleCache.TryGetValue(fobject, out float absoluteMatrixAngle);
            float roundedAngle = (float)Math.Round(absoluteMatrixAngle, FcuConfig.Rounding.Rotation);

            string key = $"{fobject.Data.Hash}|abs-matrix-angle:{roundedAngle}";
            return key.GetDeterministicHashCode();
        }
    }
}
