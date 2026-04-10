using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System.Collections.Generic;
using System.Globalization;

#if UNITEXT
using LightSide;
using Style = DA_Assets.FCU.Model.Style;
#endif

namespace DA_Assets.FCU.Drawers.CanvasDrawers
{
    public partial class UniTextDrawer
    {
#if UNITEXT
        private static void PopulateTextCase(UniText text, FObject fobject)
        {
            Style defaultStyle = fobject.Style;
            var overrides = fobject.CharacterStyleOverrides;
            var overrideTable = fobject.StyleOverrideTable;
            string chars = fobject.GetText();
            int len = chars.Length;

            bool hasOverrides = overrides != null && overrides.Count > 0
                                && overrideTable != null && overrideTable.Count > 0;

            if (!hasOverrides)
            {
                ApplyGlobalTextCase(text, defaultStyle.TextCase);
                return;
            }

            var upperData = new List<RangeRule.Data>();
            var lowerData = new List<RangeRule.Data>();
            var smallCapsData = new List<RangeRule.Data>();
            // TITLE does not use a modifier; it is handled by pre-transforming the text.

            int i = 0;
            while (i < len)
            {
                Style eff = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable);
                TextCase tc = eff.TextCase;
                int runStart = i++;

                while (i < len)
                {
                    Style next = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable);
                    if (next.TextCase != tc) break;
                    i++;
                }

                switch (tc)
                {
                    case TextCase.UPPER:
                        upperData.Add(new RangeRule.Data { range = $"{runStart}..{i}", parameter = string.Empty });
                        break;
                    case TextCase.LOWER:
                        lowerData.Add(new RangeRule.Data { range = $"{runStart}..{i}", parameter = string.Empty });
                        break;
                    case TextCase.SMALL_CAPS:
                    case TextCase.SMALL_CAPS_FORCED:
                        smallCapsData.Add(new RangeRule.Data { range = $"{runStart}..{i}", parameter = string.Empty });
                        break;
                    // TITLE: handled during text pre-transformation (no modifier needed).
                }
            }

            RegisterIfHasData(text, new UppercaseModifier(), upperData);
            RegisterIfHasData(text, new LowercaseModifier(), lowerData);
            RegisterIfHasData(text, new SmallCapsModifier(), smallCapsData);
        }

        /// <summary>
        /// Applies a single TextCase to the entire text (no per-character overrides).
        /// </summary>
        private static void ApplyGlobalTextCase(UniText text, TextCase textCase)
        {
            switch (textCase)
            {
                case TextCase.UPPER:
                    RegisterRangeRule(text, new UppercaseModifier())
                        .data.Add(new RangeRule.Data { range = string.Empty, parameter = string.Empty });
                    break;
                case TextCase.LOWER:
                    RegisterRangeRule(text, new LowercaseModifier())
                        .data.Add(new RangeRule.Data { range = string.Empty, parameter = string.Empty });
                    break;
                case TextCase.SMALL_CAPS:
                case TextCase.SMALL_CAPS_FORCED:
                    RegisterRangeRule(text, new SmallCapsModifier())
                        .data.Add(new RangeRule.Data { range = string.Empty, parameter = string.Empty });
                    break;
                // TITLE: handled during text pre-transformation (no modifier needed).
            }
        }

        /// <summary>
        /// Converts text to title case using the invariant culture rules.
        /// Called before text.Text assignment when the default style uses TITLE case.
        /// </summary>
        internal static string ApplyTitleCase(string input, TextCase textCase)
        {
            if (textCase != TextCase.TITLE || string.IsNullOrEmpty(input))
                return input;

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());
        }
#endif
    }
}
