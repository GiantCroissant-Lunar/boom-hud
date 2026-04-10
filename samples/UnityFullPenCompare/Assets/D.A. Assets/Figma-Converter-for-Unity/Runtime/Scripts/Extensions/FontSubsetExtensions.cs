using System.Collections.Generic;

namespace DA_Assets.FCU
{
    /// <summary>
    /// Provides the ToApiName() extension for converting FontSubset enum values
    /// to Google Fonts API subset name strings (e.g. LatinExt → "latin-ext").
    /// </summary>
    public static class FontSubsetExtensions
    {
        private static readonly Dictionary<FontSubset, string> _apiNames = new Dictionary<FontSubset, string>
        {
            { FontSubset.Latin,              "latin" },
            { FontSubset.LatinExt,           "latin-ext" },
            { FontSubset.Cyrillic,           "cyrillic" },
            { FontSubset.CyrillicExt,        "cyrillic-ext" },
            { FontSubset.Greek,              "greek" },
            { FontSubset.GreekExt,           "greek-ext" },
            { FontSubset.Arabic,             "arabic" },
            { FontSubset.Hebrew,             "hebrew" },
            { FontSubset.Vietnamese,         "vietnamese" },
            { FontSubset.Devanagari,         "devanagari" },
            { FontSubset.Bengali,            "bengali" },
            { FontSubset.Gujarati,           "gujarati" },
            { FontSubset.Gurmukhi,           "gurmukhi" },
            { FontSubset.Kannada,            "kannada" },
            { FontSubset.Khmer,              "khmer" },
            { FontSubset.Malayalam,           "malayalam" },
            { FontSubset.Myanmar,            "myanmar" },
            { FontSubset.Odia,               "odia" },
            { FontSubset.Sinhala,            "sinhala" },
            { FontSubset.Tamil,              "tamil" },
            { FontSubset.Telugu,             "telugu" },
            { FontSubset.Thai,               "thai" },
            { FontSubset.Georgian,           "georgian" },
            { FontSubset.Ethiopic,           "ethiopic" },
            { FontSubset.Lao,                "lao" },
            { FontSubset.Tibetan,            "tibetan" },
            { FontSubset.Japanese,           "japanese" },
            { FontSubset.ChineseSimplified,  "chinese-simplified" },
            { FontSubset.ChineseTraditional, "chinese-traditional" },
            { FontSubset.Korean,             "korean" },
        };

        /// <summary>
        /// Converts a FontSubset enum value to the Google Fonts API subset name string.
        /// </summary>
        public static string ToApiName(this FontSubset subset)
        {
            return _apiNames.TryGetValue(subset, out string name) ? name : subset.ToString().ToLower();
        }
    }
}
