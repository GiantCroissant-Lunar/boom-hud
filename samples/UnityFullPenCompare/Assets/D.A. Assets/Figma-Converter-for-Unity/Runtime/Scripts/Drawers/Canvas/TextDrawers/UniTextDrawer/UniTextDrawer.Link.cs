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
        private static void PopulateLink(UniText text, FObject fobject, Color32 baseColor)
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
                if (!string.IsNullOrEmpty(defaultStyle.Hyperlink.Url))
                {
                    var modifier = CreateLinkModifier(defaultStyle, baseColor);
                    RegisterRangeRule(text, modifier)
                        .data.Add(new RangeRule.Data { range = string.Empty, parameter = defaultStyle.Hyperlink.Url });
                }
                return;
            }

            var data = new System.Collections.Generic.List<RangeRule.Data>();

            // Find the first linked range to extract link styling from.
            Style firstLinkStyle = defaultStyle;
            bool foundLinkStyle = !string.IsNullOrEmpty(defaultStyle.Hyperlink.Url);

            int i = 0;
            while (i < len)
            {
                Style eff = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable);
                string url = GetHyperlinkUrl(eff);
                int runStart = i++;

                while (i < len)
                {
                    Style next = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable);
                    if (GetHyperlinkUrl(next) != url) break;
                    i++;
                }

                if (!string.IsNullOrEmpty(url))
                {
                    if (!foundLinkStyle)
                    {
                        firstLinkStyle = eff;
                        foundLinkStyle = true;
                    }
                    data.Add(new RangeRule.Data { range = $"{runStart}..{i}", parameter = url });
                }
            }

            if (data.Count > 0)
            {
                var modifier = CreateLinkModifier(firstLinkStyle, baseColor);
                RegisterIfHasData(text, modifier, data);
            }
        }

        /// <summary>
        /// Creates a LinkModifier with color and underline settings derived from the Figma style.
        /// Falls back to baseColor when the style has no explicit fills.
        /// </summary>
        private static LinkModifier CreateLinkModifier(Style linkStyle, Color32 baseColor)
        {
            var modifier = new LinkModifier();

            // Set link color from the style's fills, falling back to baseColor.
            Color32 fillColor = GetSolidFillColor32(linkStyle);
            if (fillColor.r == 0 && fillColor.g == 0 && fillColor.b == 0 && fillColor.a == 0)
                fillColor = baseColor;

            modifier.LinkColor = fillColor;

            // Enable underline only if the Figma style has UNDERLINE decoration.
            modifier.EnableUnderline = linkStyle.TextDecoration == TextDecoration.UNDERLINE;

            return modifier;
        }
#endif
    }
}
