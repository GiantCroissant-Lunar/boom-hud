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
        private static void PopulateBold(UniText text, FObject fobject)
        {
            Style defaultStyle = fobject.Style;
            var overrides = fobject.CharacterStyleOverrides;
            var overrideTable = fobject.StyleOverrideTable;
            bool hasOverrides = overrides != null && overrides.Count > 0
                                && overrideTable != null && overrideTable.Count > 0;

            // No per-character overrides: apply bold globally if the default weight >= 500.
            if (!hasOverrides)
            {
                int weight = defaultStyle.FontWeight;
                if (weight >= 500)
                    RegisterRangeRule(text, new BoldModifier())
                        .data.Add(new RangeRule.Data { range = string.Empty, parameter = weight.ToString() });
                return;
            }

            // Per-character overrides: pass exact CSS weight as parameter.
            var data = BuildRunRanges<int>(
                fobject,
                selector: s => s.FontWeight,
                toParameter: w => w.ToString(),
                include: w => w >= 500);

            RegisterIfHasData(text, new BoldModifier(), data);
        }
#endif
    }
}
