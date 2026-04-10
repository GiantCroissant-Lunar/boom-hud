using DA_Assets.Extensions;
using DA_Assets.FCU.Components;
using DA_Assets.FCU.Model;
using System;
using UnityEngine;

namespace DA_Assets.FCU.Drawers.CanvasDrawers
{
    [Serializable]
    public class LayoutGridDrawer : FcuBase
    {
        public void Draw(FObject fobject)
        {
            if (fobject.LayoutGrids.IsEmpty())
                return;

            // Remove old components if re-importing
            FigmaLayoutGrid[] oldComponents = fobject.Data.GameObject.GetComponents<FigmaLayoutGrid>();
            foreach (FigmaLayoutGrid old in oldComponents)
            {
                old.Destroy();
            }

            // Add a separate component for each grid
            foreach (var layoutGrid in fobject.LayoutGrids)
            {
                fobject.Data.GameObject.TryAddComponent(out FigmaLayoutGrid component, supportMultiInstance: true);

                // Convert string pattern to enum
                LayoutGridPattern pattern = LayoutGridPattern.GRID;
                if (layoutGrid.Pattern == "ROWS")
                    pattern = LayoutGridPattern.ROWS;
                else if (layoutGrid.Pattern == "COLUMNS")
                    pattern = LayoutGridPattern.COLUMNS;

                // Convert string alignment to enum
                LayoutGridAlignment alignment = LayoutGridAlignment.MIN;
                if (!string.IsNullOrEmpty(layoutGrid.Alignment))
                {
                    if (layoutGrid.Alignment == "MAX")
                        alignment = LayoutGridAlignment.MAX;
                    else if (layoutGrid.Alignment == "STRETCH")
                        alignment = LayoutGridAlignment.STRETCH;
                    else if (layoutGrid.Alignment == "CENTER")
                        alignment = LayoutGridAlignment.CENTER;
                }

                component.Pattern = pattern;
                component.SectionSize = layoutGrid.SectionSize;
                component.Visible = layoutGrid.Visible;
                component.Color = layoutGrid.Color;
                component.Alignment = alignment;
                component.GutterSize = layoutGrid.GutterSize;
                component.Offset = layoutGrid.Offset;
                component.Count = layoutGrid.Count;
            }
        }
    }
}
