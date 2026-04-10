#if FCU_EXISTS
using System.Text;
using UnityEngine;
using DA_Assets.Extensions;

namespace DA_Assets.FCU
{
    public static class StyleBuilderExtensions
    {
        public static void AddStyle(this StringBuilder sb, string key, string value)
        {
            sb.AppendLine($"    {key}: {value};");
        }

        public static void AddLocalStyle(this StringBuilder sb, string key, string value)
        {
            sb.Append($" {key}: {value};");
        }

        public static string ToCssColor(this Color c, float? alpha = null)
        {
            Color32 c32 = c;

            float aF = alpha ?? c.a;

            string a = 255.ToString();

            if (aF == 0 || aF == 1)
            {
                a = ((int)aF.Remap(0, 1, 0, 255)).ToString();
            }
            else
            {
                a = aF.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            }


            if (a == "255")
            {
                return $"rgb({c32.r}, {c32.g}, {c32.b})";
            }
            else
            {
                return $"rgba({c32.r}, {c32.g}, {c32.b}, {a})";
            }
        }
    }
}
#endif
