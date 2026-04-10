using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITEXT
using LightSide;
using Style = DA_Assets.FCU.Model.Style;
using UniTextStyle = LightSide.Style;
#endif

namespace DA_Assets.FCU.Drawers.CanvasDrawers
{
    [Serializable]
    public partial class UniTextDrawer : FcuBase
    {
#if UNITEXT
        // All modifier types managed by this drawer.
        private static readonly Type[] managedModifierTypes = new Type[]
        {
            typeof(LineHeightModifier),
            typeof(LetterSpacingModifier),
            typeof(SizeModifier),
            typeof(BoldModifier),
            typeof(ItalicModifier),
            typeof(UppercaseModifier),
            typeof(LowercaseModifier),
            typeof(SmallCapsModifier),
            typeof(ColorModifier),
            typeof(UnderlineModifier),
            typeof(StrikethroughModifier),
            typeof(LinkModifier),
            typeof(EllipsisModifier),
            typeof(ShadowModifier),
            typeof(OutlineModifier),
            typeof(GradientModifier),
        };

        public UniText Draw(FObject fobject)
        {
            fobject.Data.GameObject.TryAddGraphic(out UniText text);

            text.raycastTarget = monoBeh.Settings.UniTextSettings.RaycastTarget;

            UnregisterManagedModifiers(text);
            SetStyle(text, fobject);
            SetFont(text, fobject);

            text.HorizontalAlignment = fobject.GetUniTextHAlign();
            text.VerticalAlignment = fobject.GetUniTextVAlign();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(text);
#endif
            return text;
        }

        private void SetFont(UniText text, FObject fobject)
        {
            UniTextFontStack fontStack = monoBeh.FontLoader
                .GetFontFromArray(fobject, monoBeh.FontLoader.UniTextFontStacks);

            if (fontStack != null)
            {
                text.FontStack = fontStack;
            }
        }

        private void SetStyle(UniText text, FObject fobject)
        {
            text.AutoSize = monoBeh.Settings.UniTextSettings.AutoSize;
            text.MinFontSize = 1;
            text.MaxFontSize = fobject.Style.FontSize;
            text.WordWrap = monoBeh.Settings.UniTextSettings.WordWrap;

            // Figma uses the LeadingAbove model (all extra leading placed above the line).
            text.LeadingDistribution = LeadingDistribution.LeadingAbove;

            // Set RenderMode based on stroke join type.
            // MITER/BEVEL need sharp corners (MSDF), ROUND needs rounded corners (SDF).
            // Default to MSDF for best general quality.
            text.RenderMode = fobject.StrokeJoin == "ROUND"
                ? UniTextBase.RenderModee.SDF
                : UniTextBase.RenderModee.MSDF;

            // Compute base color first so it can be propagated to both text.color and PopulateColor.
            FGraphic graphic = fobject.Data.Graphic;
            Color baseColor = default;

            if (graphic.Fill.HasSolid)
            {
                baseColor = graphic.Fill.SolidPaint.Color;
                text.color = baseColor;
            }
            else if (graphic.Fill.HasGradient)
            {
                List<GradientColorKey> gradientColorKeys = graphic.Fill.GradientPaint.ToGradientColorKeys();

                if (!gradientColorKeys.IsEmpty())
                {
                    baseColor = gradientColorKeys.First().color;
                    text.color = baseColor;
                }
            }

            PopulateVerticalTrim(text, fobject);
            PopulateLineHeightAndLetterSpacing(text, fobject);
            PopulateSize(text, fobject);
            PopulateBold(text, fobject);
            PopulateItalic(text, fobject);
            PopulateTextCase(text, fobject);
            // Pass baseColor so PopulateColor compares overrides against the value already set in text.color.
            PopulateColor(text, fobject, (Color32)baseColor);
            PopulateUnderline(text, fobject);
            PopulateStrikethrough(text, fobject);
            PopulateLink(text, fobject, (Color32)baseColor);
            PopulateEllipsis(text, fobject);
            PopulateShadow(text, fobject);
            PopulateOutline(text, fobject);
            PopulateGradient(text, fobject);

            // Apply TITLE case pre-transformation before setting the text.
            // This must happen after PopulateTextCase (which handles UPPER/LOWER/SMALL_CAPS via modifiers)
            // because TITLE case has no dedicated modifier — text content is transformed directly.
            string finalText = ApplyTitleCase(fobject.GetText(), fobject.Style.TextCase);
            text.Text = finalText;
            text.FontSize = fobject.Style.FontSize;
        }

        /// <summary>
        /// Removes all previously managed modifier registrations from the UniText component.
        /// </summary>
        private static void UnregisterManagedModifiers(UniText text)
        {
            var toRemove = new List<UniTextStyle>();

            foreach (var style in text.Styles)
            {
                if (style.Modifier != null && Array.IndexOf(managedModifierTypes, style.Modifier.GetType()) >= 0)
                    toRemove.Add(style);
            }

            foreach (var style in toRemove)
                text.RemoveStyle(style);
        }

        /// <summary>
        /// Creates a RangeRule paired with the given modifier and registers it.
        /// </summary>
        private static RangeRule RegisterRangeRule(UniText text, BaseModifier modifier)
        {
            var rule = new RangeRule();
            var style = new UniTextStyle { Modifier = modifier, Rule = rule };
            text.AddStyle(style);
            return rule;
        }

        /// <summary>
        /// Registers a modifier with pre-collected data only if the data list is not empty.
        /// </summary>
        private static void RegisterIfHasData(UniText text, BaseModifier modifier, List<RangeRule.Data> data)
        {
            if (data.Count == 0)
                return;

            RegisterRangeRule(text, modifier).data.AddRange(data);
        }

        /// <summary>
        /// Returns the effective Style for a given character index by merging
        /// the default style with the per-character override from styleOverrideTable.
        /// Key 0 means no override (use default).
        /// </summary>
        private static Style GetEffectiveStyle(
            int charIndex,
            Style defaultStyle,
            List<int> overrides,
            Dictionary<string, Style> overrideTable)
        {
            if (charIndex >= overrides.Count)
                return defaultStyle;

            int key = overrides[charIndex];
            if (key == 0)
                return defaultStyle;

            if (overrideTable.TryGetValue(key.ToString(), out Style ov))
            {
                Style result = defaultStyle;

                // LetterSpacing: always apply override (0 is a valid explicit value).
                result.LetterSpacing = ov.LetterSpacing;

                // LineHeightPx: only apply if > 0 (0 is not a valid line height in Figma).
                if (ov.LineHeightPx > 0)
                    result.LineHeightPx = ov.LineHeightPx;

                // FontSize: only apply if > 0.
                if (ov.FontSize > 0)
                    result.FontSize = ov.FontSize;

                // FontWeight: only apply if > 0 (0 means not specified in the override).
                if (ov.FontWeight > 0)
                    result.FontWeight = ov.FontWeight;

                // Italic: apply if explicitly specified.
                if (ov.Italic.HasValue)
                    result.Italic = ov.Italic;

                // TextDecoration: always apply (NONE is a valid explicit value).
                result.TextDecoration = ov.TextDecoration;

                // TextCase: always apply.
                result.TextCase = ov.TextCase;

                // Fills: apply if specified.
                if (ov.Fills != null && ov.Fills.Count > 0)
                    result.Fills = ov.Fills;

                // Hyperlink: apply if URL is specified.
                if (!string.IsNullOrEmpty(ov.Hyperlink.Url))
                    result.Hyperlink = ov.Hyperlink;

                return result;
            }

            return defaultStyle;
        }

        /// <summary>
        /// Extracts the first SOLID fill color from Style.Fills as Color32.
        /// Returns transparent black if no solid fill exists.
        /// </summary>
        private static Color32 GetSolidFillColor32(Style style)
        {
            if (style.Fills != null)
            {
                foreach (Paint fill in style.Fills)
                {
                    if (fill.Type == PaintType.SOLID)
                        return fill.Color;
                }
            }

            return new Color32(0, 0, 0, 0);
        }

        /// <summary>
        /// Returns the hyperlink URL from a Style, or null if none.
        /// </summary>
        private static string GetHyperlinkUrl(Style style)
        {
            return string.IsNullOrEmpty(style.Hyperlink.Url) ? null : style.Hyperlink.Url;
        }

        /// <summary>
        /// Converts a Color32 to hex string in #RRGGBBAA format.
        /// </summary>
        private static string Color32ToHex(Color32 c)
        {
            return $"#{c.r:X2}{c.g:X2}{c.b:X2}{c.a:X2}";
        }

        /// <summary>
        /// Compares two Color32 values for exact byte-level equality.
        /// </summary>
        private static bool Color32Equals(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        /// <summary>
        /// Iterates character overrides and groups consecutive runs with equal <typeparamref name="T"/> values.
        /// For each run where <paramref name="include"/> returns true, adds an entry to the returned list.
        /// Eliminates the duplicated while-loop pattern across all Populate* partial classes.
        /// </summary>
        /// <param name="fobject">Source Figma object with style override data.</param>
        /// <param name="selector">Extracts the property value from an effective Style.</param>
        /// <param name="toParameter">Converts the property value to the RangeRule parameter string.</param>
        /// <param name="include">Determines whether to emit a range entry for the given value.</param>
        private static System.Collections.Generic.List<RangeRule.Data> BuildRunRanges<T>(
            FObject fobject,
            System.Func<Style, T> selector,
            System.Func<T, string> toParameter,
            System.Func<T, bool> include,
            System.Collections.Generic.IEqualityComparer<T> comparer = null)
        {
            comparer ??= System.Collections.Generic.EqualityComparer<T>.Default;

            var result = new System.Collections.Generic.List<RangeRule.Data>();

            Style defaultStyle = fobject.Style;
            var overrides = fobject.CharacterStyleOverrides;
            var overrideTable = fobject.StyleOverrideTable;
            string chars = fobject.GetText();
            int len = chars.Length;

            bool hasOverrides = overrides != null && overrides.Count > 0
                                && overrideTable != null && overrideTable.Count > 0;

            if (!hasOverrides)
                return result;

            int i = 0;
            while (i < len)
            {
                Style eff = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable);
                T value = selector(eff);
                int runStart = i;
                i++;

                while (i < len)
                {
                    T next = selector(GetEffectiveStyle(i, defaultStyle, overrides, overrideTable));
                    if (!comparer.Equals(next, value)) break;
                    i++;
                }

                if (include(value))
                    result.Add(new RangeRule.Data { range = $"{runStart}..{i}", parameter = toParameter(value) });
            }

            return result;
        }
#endif
    }
}
