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
        private static void PopulateSize(UniText text, FObject fobject)
        {
            Style defaultStyle = fobject.Style;
            var overrides = fobject.CharacterStyleOverrides;
            var overrideTable = fobject.StyleOverrideTable;
            string chars = fobject.GetText();
            int len = chars.Length;

            bool hasOverrides = overrides != null && overrides.Count > 0
                                && overrideTable != null && overrideTable.Count > 0;

            // SizeModifier is only meaningful for per-character overrides.
            // The base font size is set via text.FontSize directly.
            if (!hasOverrides)
                return;

            float defaultFontSize = defaultStyle.FontSize;
            var data = new System.Collections.Generic.List<RangeRule.Data>();

            int i = 0;
            while (i < len)
            {
                Style eff = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable);
                float fs = eff.FontSize;
                int runStart = i++;

                while (i < len)
                {
                    Style next = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable);
                    if (next.FontSize != fs) break;
                    i++;
                }

                if (fs > 0 && fs != defaultFontSize)
                    data.Add(new RangeRule.Data { range = $"{runStart}..{i}", parameter = fs.ToString("G") });
            }

            RegisterIfHasData(text, new SizeModifier(), data);
        }
#endif
    }
}
