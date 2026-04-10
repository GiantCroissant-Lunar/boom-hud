using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;

#if UNITEXT
using LightSide;
using Style = DA_Assets.FCU.Model.Style;
#endif

namespace DA_Assets.FCU.Drawers.CanvasDrawers
{
    public partial class UniTextDrawer
    {
#if UNITEXT
        private static void PopulateLineHeightAndLetterSpacing(UniText text, FObject fobject)
        {
            Style defaultStyle = fobject.Style;
            System.Collections.Generic.List<int> overrides = fobject.CharacterStyleOverrides;
            System.Collections.Generic.Dictionary<string, Style> overrideTable = fobject.StyleOverrideTable;
            string chars = fobject.GetText();
            int len = chars.Length;

            bool hasOverrides = overrides != null && overrides.Count > 0
                                && overrideTable != null && overrideTable.Count > 0;

            if (!hasOverrides)
            {
                if (defaultStyle.LineHeightPx > 0 && defaultStyle.FontSize > 0)
                    RegisterRangeRule(text, new LineHeightModifier())
                        .data.Add(new RangeRule.Data { range = string.Empty, parameter = defaultStyle.LineHeightPx.ToString("G") });

                if (defaultStyle.LetterSpacing != 0)
                    RegisterRangeRule(text, new LetterSpacingModifier())
                        .data.Add(new RangeRule.Data { range = string.Empty, parameter = defaultStyle.LetterSpacing.ToString("G") });

                return;
            }

            var lhData = new System.Collections.Generic.List<RangeRule.Data>();
            var lsData = new System.Collections.Generic.List<RangeRule.Data>();

            int i = 0;
            while (i < len)
            {
                Style eff = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable);
                float lh = eff.LineHeightPx;
                float ls = eff.LetterSpacing;
                int runStart = i++;

                while (i < len)
                {
                    Style next = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable);
                    if (next.LineHeightPx != lh || next.LetterSpacing != ls) break;
                    i++;
                }

                string rangeStr = $"{runStart}..{i}";

                if (lh > 0 && defaultStyle.FontSize > 0)
                    lhData.Add(new RangeRule.Data { range = rangeStr, parameter = lh.ToString("G") });

                if (ls != 0)
                    lsData.Add(new RangeRule.Data { range = rangeStr, parameter = ls.ToString("G") });
            }

            RegisterIfHasData(text, new LineHeightModifier(), lhData);
            RegisterIfHasData(text, new LetterSpacingModifier(), lsData);
        }
#endif
    }
}
