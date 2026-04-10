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
        private static void PopulateItalic(UniText text, FObject fobject)
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
                if (defaultStyle.Italic == true)
                    RegisterRangeRule(text, new ItalicModifier())
                        .data.Add(new RangeRule.Data { range = string.Empty, parameter = string.Empty });
                return;
            }

            var data = new System.Collections.Generic.List<RangeRule.Data>();

            int i = 0;
            while (i < len)
            {
                Style eff = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable);
                bool isItalic = eff.Italic == true;
                int runStart = i++;

                while (i < len)
                {
                    Style next = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable);
                    if ((next.Italic == true) != isItalic) break;
                    i++;
                }

                if (isItalic)
                    data.Add(new RangeRule.Data { range = $"{runStart}..{i}", parameter = string.Empty });
            }

            RegisterIfHasData(text, new ItalicModifier(), data);
        }
#endif
    }
}
