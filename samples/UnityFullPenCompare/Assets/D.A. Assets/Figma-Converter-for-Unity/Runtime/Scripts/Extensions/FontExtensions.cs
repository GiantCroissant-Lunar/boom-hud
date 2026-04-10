using DA_Assets.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DA_Assets.FCU.Extensions
{
    public static class FontExtensions
    {
        public static string FormatFontName(this string value)
        {
            if (value.IsEmpty())
            {
                return "null";
            }

            // Ordered longest-first to avoid partial-match problems (e.g. "extralight" before "light").
            Dictionary<string, string> weightSynonyms = new Dictionary<string, string>
            {
                { "ultrablack", "extrablack" },
                { "ultralight", "extralight" },
                { "ultrabold", "extrabold" },
                { "demibold", "semibold" },
                { "hairline", "thin" },
                { "normal", "regular" },
                { "medium", "medium" },
                { "light", "light" },
                { "heavy", "black" },
                { "bold", "bold" },
            };

            string formatted = value
                .Replace("SDF", "")
                .Replace("FontStack", "")
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("_", "")
                .ToLower();

            // Split camelCase/PascalCase into tokens for exact matching.
            string[] tokens = Regex.Split(formatted, @"(?<=\p{Ll})(?=\p{Lu})");
            string joined = string.Join(" ", tokens).ToLower();

            bool hasWeight = weightSynonyms.Keys.Any(x => joined.Contains(x)) ||
                             weightSynonyms.Values.Any(x => joined.Contains(x));

            bool hasItalic = formatted.Contains("italic");

            if (hasWeight)
            {
                foreach (var pair in weightSynonyms)
                {
                    formatted = formatted.Replace(pair.Key, pair.Value);
                }
            }
            else if (hasItalic)
            {
                formatted = formatted.Replace("italic", "regularitalic");
            }

            return formatted;
        }
    }
}
