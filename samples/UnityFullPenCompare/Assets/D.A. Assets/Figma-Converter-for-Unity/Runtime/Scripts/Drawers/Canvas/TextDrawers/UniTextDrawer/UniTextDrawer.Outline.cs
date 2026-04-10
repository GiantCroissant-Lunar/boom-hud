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
        /// Registers OutlineModifier from the first solid Stroke.
        /// </summary>
        private static void PopulateOutline(UniText text, FObject fobject)
        {
            if (fobject.Strokes == null || fobject.Strokes.Count == 0)
                return;

            Paint stroke = fobject.Strokes[0];
            if (stroke.Type != PaintType.SOLID)
                return;

            float strokeWeight = fobject.StrokeWeight;
            if (strokeWeight <= 0f)
                return;

            string hex = Color32ToHex(stroke.Color);
            string parameter = FormattableString.Invariant($"{strokeWeight},{hex}");

            RegisterRangeRule(text, new OutlineModifier { fixedPixelSize = true })
                .data.Add(new RangeRule.Data { range = string.Empty, parameter = parameter });
        }
#endif
    }
}
