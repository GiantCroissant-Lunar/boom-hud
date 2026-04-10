#if FCU_EXISTS
using DA_Assets.Constants;
using DA_Assets.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DA_Assets.FCU
{
    /// <summary>
    /// Collects unique design tokens (colors, font sizes, spacing) during UXML generation
    /// and generates a CSS :root block with custom properties (USS variables).
    /// </summary>
    public static class UssVariableCollector
    {
        // color CSS string -> variable name, e.g. "rgba(18,171,63,255)" -> "--color-12ab3fff"
        private static Dictionary<string, string> _colorVars = new Dictionary<string, string>();

        // font size px -> variable name, e.g. 16 -> "--font-size-16"
        private static Dictionary<int, string> _fontSizeVars = new Dictionary<int, string>();

        // spacing px -> variable name, e.g. 8 -> "--spacing-8"
        private static Dictionary<int, string> _spacingVars = new Dictionary<int, string>();

        // border width px -> variable name, e.g. 1 -> "--border-width-1"
        private static Dictionary<int, string> _borderWidthVars = new Dictionary<int, string>();

        // border radius px -> variable name, e.g. 4 -> "--radius-4"
        private static Dictionary<int, string> _radiusVars = new Dictionary<int, string>();

        public static void Clear()
        {
            _colorVars.Clear();
            _fontSizeVars.Clear();
            _spacingVars.Clear();
            _borderWidthVars.Clear();
            _radiusVars.Clear();
        }

        /// <summary>
        /// Registers a color and returns the CSS var() reference.
        /// Naming strategy:
        ///   - exact RGB match, alpha 255  → --color-Black
        ///   - exact RGB match, alpha lt 255 → --color-Black-80  (2-char hex alpha suffix)
        ///   - nearest RGB (inexact)        → --color-Gray-000000ff  (name + full RRGGBBAA hex)
        /// </summary>
        public static string CollectColor(Color c)
        {
            string cssColor = c.ToCssColor(c.a);

            if (!_colorVars.TryGetValue(cssColor, out string varName))
            {
                varName = BuildColorVarName(c);
                _colorVars[cssColor] = varName;
            }

            return $"var({varName})";
        }

        private static string BuildColorVarName(Color c)
        {
            Color32 c32 = c;
            string hexFull = $"{c32.r:x2}{c32.g:x2}{c32.b:x2}{c32.a:x2}";

            string nearestName = null;
            float minDistance  = float.MaxValue;
            bool  exactRgb     = false;

            foreach (var kvp in NetKnownColors.KnownColors)
            {
                Color32 known = kvp.Value;
                float rd = known.r - c32.r;
                float gd = known.g - c32.g;
                float bd = known.b - c32.b;
                float dist = Mathf.Sqrt(rd * rd + gd * gd + bd * bd);

                if (dist < minDistance)
                {
                    minDistance  = dist;
                    nearestName  = kvp.Key.ToString();
                    exactRgb     = dist == 0f;

                    if (exactRgb)
                        break; // perfect match — no need to continue
                }
            }

            if (nearestName == null)
            {
                // Fallback: full hex (should never happen as long as KnownColors is non-empty).
                return $"--color-{hexFull}";
            }

            if (exactRgb)
            {
                // Exact RGB match.
                if (c32.a == 255)
                    return $"--color-{nearestName}";
                else
                    return $"--color-{nearestName}-{c32.a:x2}";
            }
            else
            {
                // Nearest match — append full hex so the name stays unique.
                return $"--color-{nearestName}-{hexFull}";
            }
        }


        /// <summary>
        /// Registers a font size and returns the CSS var() reference, e.g. "var(--font-size-16)".
        /// </summary>
        public static string CollectFontSize(float px)
        {
            int roundedPx = Mathf.RoundToInt(px);

            if (!_fontSizeVars.TryGetValue(roundedPx, out string varName))
            {
                varName = $"--font-size-{roundedPx}";
                _fontSizeVars[roundedPx] = varName;
            }

            return $"var({varName})";
        }

        /// <summary>
        /// Registers a spacing value and returns the CSS var() reference, e.g. "var(--spacing-8)".
        /// </summary>
        public static string CollectSpacing(float px)
        {
            int roundedPx = Mathf.RoundToInt(px);

            if (!_spacingVars.TryGetValue(roundedPx, out string varName))
            {
                varName = $"--spacing-{roundedPx}";
                _spacingVars[roundedPx] = varName;
            }

            return $"var({varName})";
        }

        /// <summary>
        /// Registers a border width and returns the CSS var() reference, e.g. "var(--border-width-1)".
        /// </summary>
        public static string CollectBorderWidth(float px)
        {
            int roundedPx = Mathf.RoundToInt(px);

            if (!_borderWidthVars.TryGetValue(roundedPx, out string varName))
            {
                varName = $"--border-width-{roundedPx}";
                _borderWidthVars[roundedPx] = varName;
            }

            return $"var({varName})";
        }

        /// <summary>
        /// Registers a border radius and returns the CSS var() reference, e.g. "var(--radius-4)".
        /// Call only when all four corners are equal.
        /// </summary>
        public static string CollectRadius(float px)
        {
            int roundedPx = Mathf.RoundToInt(px);

            if (!_radiusVars.TryGetValue(roundedPx, out string varName))
            {
                varName = $"--radius-{roundedPx}";
                _radiusVars[roundedPx] = varName;
            }

            return $"var({varName})";
        }

        /// <summary>
        /// Generates the full :root { } block with all collected variables.
        /// Returns an empty string if no variables were collected.
        /// To be prepended to the .uss file content.
        /// </summary>
        public static string GenerateRootBlock()
        {
            bool hasAny = _colorVars.Count > 0 || _fontSizeVars.Count > 0 || _spacingVars.Count > 0
                       || _borderWidthVars.Count > 0 || _radiusVars.Count > 0;

            if (!hasAny)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(":root {");

            foreach (var kvp in _colorVars)
                sb.AppendLine($"    {kvp.Value}: {kvp.Key};");

            foreach (var kvp in _fontSizeVars)
                sb.AppendLine($"    {kvp.Value}: {kvp.Key}px;");

            foreach (var kvp in _spacingVars)
                sb.AppendLine($"    {kvp.Value}: {kvp.Key}px;");

            foreach (var kvp in _borderWidthVars)
                sb.AppendLine($"    {kvp.Value}: {kvp.Key}px;");

            foreach (var kvp in _radiusVars)
                sb.AppendLine($"    {kvp.Value}: {kvp.Key}px;");

            sb.AppendLine("}");
            sb.AppendLine();

            return sb.ToString();
        }
    }
}
#endif
