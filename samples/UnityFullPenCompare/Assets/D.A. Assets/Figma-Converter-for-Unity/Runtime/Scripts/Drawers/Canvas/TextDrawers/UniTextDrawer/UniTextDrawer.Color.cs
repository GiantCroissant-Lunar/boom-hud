using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using UnityEngine;

#if UNITEXT
using LightSide;
using Style = DA_Assets.FCU.Model.Style;
#endif

namespace DA_Assets.FCU.Drawers.CanvasDrawers
{
    public partial class UniTextDrawer
    {
#if UNITEXT
        /// <summary>
        /// Registers per-character color overrides.
        /// <paramref name="baseColor"/> must match the value already assigned to <c>text.color</c>
        /// (computed in SetStyle) to avoid adding redundant modifiers for default-colored ranges.
        /// </summary>
        private static void PopulateColor(UniText text, FObject fobject, Color32 baseColor)
        {
            Style defaultStyle = fobject.Style;
            var overrides = fobject.CharacterStyleOverrides;
            var overrideTable = fobject.StyleOverrideTable;
            string chars = fobject.GetText();
            int len = chars.Length;

            bool hasOverrides = overrides != null && overrides.Count > 0
                                && overrideTable != null && overrideTable.Count > 0;

            // ColorModifier is only meaningful for per-character overrides.
            // The base color is already set via text.color in SetStyle.
            if (!hasOverrides)
                return;

            var data = new System.Collections.Generic.List<RangeRule.Data>();

            int i = 0;
            while (i < len)
            {
                Style eff = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable);
                Color32 col = GetSolidFillColor32(eff);

                // (0,0,0,0) means no explicit fill in this style — use baseColor.
                if (col.r == 0 && col.g == 0 && col.b == 0 && col.a == 0)
                    col = baseColor;

                int runStart = i++;

                while (i < len)
                {
                    Style next = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable);
                    Color32 nextCol = GetSolidFillColor32(next);
                    if (nextCol.r == 0 && nextCol.g == 0 && nextCol.b == 0 && nextCol.a == 0)
                        nextCol = baseColor;
                    if (!Color32Equals(nextCol, col)) break;
                    i++;
                }

                if (!Color32Equals(col, baseColor))
                    data.Add(new RangeRule.Data { range = $"{runStart}..{i}", parameter = Color32ToHex(col) });
            }

            RegisterIfHasData(text, new ColorModifier(), data);
        }
#endif
    }
}
