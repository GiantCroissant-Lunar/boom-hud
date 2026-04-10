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
        /// <summary>
        /// Maps Figma's leadingTrim (encoded via Style.LineHeightUnit) to UniText
        /// OverEdge / UnderEdge trimming properties.
        ///
        /// Figma REST API encodes "Vertical Trim ON" as LineHeightUnit == "INTRINSIC_%".
        /// Any other unit value means Trim OFF.
        /// </summary>
        private static void PopulateVerticalTrim(UniText text, FObject fobject)
        {
            bool trimEnabled = fobject.Style.LineHeightUnit == "INTRINSIC_%";

            text.OverEdge  = trimEnabled ? TextOverEdge.CapHeight : TextOverEdge.Ascent;
            text.UnderEdge = trimEnabled ? TextUnderEdge.Baseline : TextUnderEdge.Descent;
        }
#endif
    }
}
