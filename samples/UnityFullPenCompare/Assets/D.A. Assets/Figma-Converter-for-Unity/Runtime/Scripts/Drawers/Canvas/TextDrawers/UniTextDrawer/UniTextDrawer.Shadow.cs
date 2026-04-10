using DA_Assets.FCU.Model;
using System;

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
        /// Registers ShadowModifier from visible DROP_SHADOW and INNER_SHADOW effects.
        /// INNER_SHADOW is rendered using the same ShadowModifier as a best-effort approximation.
        /// </summary>
        private static void PopulateShadow(UniText text, FObject fobject)
        {
            if (fobject.Effects == null)
                return;

            foreach (Effect effect in fobject.Effects)
            {
                if (effect.Type != EffectType.DROP_SHADOW && effect.Type != EffectType.INNER_SHADOW)
                    continue;

                if (effect.Visible.HasValue && !effect.Visible.Value)
                    continue;

                float dilate = effect.Spread ?? 0f;
                UnityEngine.Color32 color = effect.Color;
                float offsetX = effect.Offset.x;
                // Negate Y: Figma Y-down → UniText Y-up.
                float offsetY = -effect.Offset.y;
                float softness = effect.Radius;
                string hex = Color32ToHex(color);
                string parameter = FormattableString.Invariant(
                    $"{dilate},{hex},{offsetX},{offsetY},{softness}");

                RegisterRangeRule(text, new ShadowModifier())
                    .data.Add(new RangeRule.Data { range = string.Empty, parameter = parameter });

                // Only the first visible shadow effect is used.
                break;
            }
        }
#endif
    }
}
