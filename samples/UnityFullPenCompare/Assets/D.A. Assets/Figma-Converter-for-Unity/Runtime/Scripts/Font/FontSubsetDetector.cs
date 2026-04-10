using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace DA_Assets.FCU
{
    /// <summary>
    /// Analyses FObject.Characters from Figma text nodes and determines
    /// which FontSubsets are actually used in the project.
    /// Uses FontSubsetUnicodeRanges as a reverse lookup: codepoint → subset.
    /// FontSubset.Latin is always included.
    /// </summary>
    internal static class FontSubsetDetector
    {
        // Initialized once by the CLR before first use — thread-safe without any locks.
        // Data is read-only after construction.
        private static readonly Dictionary<FontSubset, HashSet<uint>> _ranges = BuildCache();

        private static Dictionary<FontSubset, HashSet<uint>> BuildCache()
        {
            var dict = new Dictionary<FontSubset, HashSet<uint>>();

            foreach (FontSubset subset in Enum.GetValues(typeof(FontSubset)))
            {
                if (FontSubsetUnicodeRanges.TryGet(subset, out uint[] range))
                    dict[subset] = new HashSet<uint>(range);
            }

            return dict;
        }

        /// <summary>
        /// Scans all characters in the supplied FObjects and returns the set
        /// of FontSubsets that are needed to render them.
        /// Always includes <see cref="FontSubset.Latin"/>.
        /// </summary>
        public static Task<HashSet<FontSubset>> DetectFromFObjects(List<FObject> fobjects)
        {
            Debug.Log(FcuLocKey.log_detecting_font_subsets.Localize());

            return Task.Run(() =>
            {
                var result = new HashSet<FontSubset> { FontSubset.Latin };
                var resultLock = new object();

                Parallel.ForEach(fobjects, fobject =>
                {
                    if (!fobject.ContainsTag(FcuTag.Text))
                        return;

                    string characters = fobject.Characters;

                    if (string.IsNullOrEmpty(characters))
                        return;

                    foreach (char c in characters)
                    {
                        uint codePoint = c;

                        foreach (var pair in _ranges)
                        {
                            lock (resultLock)
                            {
                                if (result.Contains(pair.Key))
                                    continue;

                                if (pair.Value.Contains(codePoint))
                                    result.Add(pair.Key);
                            }
                        }
                    }
                });

                return result;
            });
        }
    }
}
