using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using UnityEngine.UI;

namespace DA_Assets.FCU.Drawers.CanvasDrawers
{
    [Serializable]
    public class LayoutElementDrawer : FcuBase
    {
        public void Draw(FObject fobject, FObject parent)
        {
            fobject.Data.GameObject.TryAddComponent(out LayoutElement layoutElement);

            bool isSpaceBetween = parent.PrimaryAxisAlignItems == PrimaryAxisAlignItem.SPACE_BETWEEN;

            if (isSpaceBetween)
            {
                // Inside SPACE_BETWEEN, use minWidth/minHeight along primary axis
                // so spacer objects can occupy remaining free space.
                if (parent.LayoutMode == LayoutMode.HORIZONTAL)
                {
                    layoutElement.minWidth = fobject.Size.x;
                }
                else if (parent.LayoutMode == LayoutMode.VERTICAL)
                {
                    layoutElement.minHeight = fobject.Size.y;
                }
            }
            else if (fobject.ContainsTag(FcuTag.Text))
            {
                // For text in non-SPACE_BETWEEN layouts, set preferred sizes
                // to ensure parent auto-layout computes correct dimensions
                // regardless of actual font metric differences.
                layoutElement.preferredWidth = fobject.Size.x;
                layoutElement.preferredHeight = fobject.Size.y;
            }

            if (parent.LayoutWrap == LayoutWrap.WRAP)
            {
                layoutElement.minWidth = fobject.Size.x;
                layoutElement.minHeight = fobject.Size.y;
            }

            if (fobject.LayoutPositioning == LayoutPositioning.ABSOLUTE)
            {
                layoutElement.ignoreLayout = true;
            }
            else
            {
                layoutElement.ignoreLayout = false;
            }
        }
    }
}