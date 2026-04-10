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
        private static void PopulateEllipsis(UniText text, FObject fobject)
        {
            if (fobject.Style.TextTruncation == TextTruncation.ENDING)
                RegisterRangeRule(text, new EllipsisModifier())
                    .data.Add(new RangeRule.Data { range = string.Empty, parameter = "1" });
        }
#endif
    }
}
