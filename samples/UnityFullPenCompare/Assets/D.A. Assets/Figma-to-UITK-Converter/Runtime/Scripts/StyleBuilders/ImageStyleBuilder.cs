#if FCU_EXISTS
using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;



namespace DA_Assets.FCU
{
    [Serializable]
    public class ImageStyleBuilder : FcuBase
    {
        public KeyValuePair<string, string> GetSpriteLocalStyle(FObject fobject)
        {
#if FCU_UITK_EXT_EXISTS
            string assetPath = fobject.Data.Names.UITK_SpritePath;
            return new KeyValuePair<string, string>("background-image", $"url({assetPath})");
#else
            return default;
#endif
        }

        public void AppendSpriteLocalStyles(FObject fobject, StringBuilder styleBuilder)
        {
#if FCU_UITK_EXT_EXISTS
            var kvp = GetSpriteLocalStyle(fobject);
            styleBuilder.AddLocalStyle(kvp.Key, kvp.Value);
            // Prevent stretching when the exported sprite is smaller than the element bounds
            // (e.g. Figma API clips empty space from the node before exporting).
            // For sprites that exactly fill the element this is a no-op (scale factor = 1).
            styleBuilder.AddLocalStyle("background-size", "contain");
#endif
        }


        public string CreateImageLocalStyle(FObject fobject)
        {
            StringBuilder styleBuilder = new StringBuilder();

            if (fobject.IsSprite())
            {
                if (!fobject.Data.SpritePath.IsEmpty())
                {
                    AppendSpriteLocalStyles(fobject, styleBuilder);
                }
            }

            return styleBuilder.ToString();
        }


        public string CreateImageGlobalStyle(FObject fobject)
        {
            StringBuilder styleBuilder = new StringBuilder();

            if (fobject.IsSprite() && !fobject.Data.Graphic.SpriteSingleColor.IsDefault())
            {
                Color c = fobject.Data.Graphic.SpriteSingleColor;
                string tintVar = UssVariableCollector.CollectColor(c);
                styleBuilder.AddStyle("-unity-background-image-tint-color", tintVar);
            }
            else
            {
                FGraphic graphic = fobject.Data.Graphic;

                if (graphic.HasFill)
                {
                    Color color = graphic.Fill.SingleColor;

                    if (fobject.Data.FcuImageType == FcuImageType.Downloadable)
                    {
                        color.a = 0;
                    }
                    else if (fobject.IsMask.ToBoolNullFalse())
                    {
                        color.a = 0;
                    }

                    // Register color as a USS variable and use var(--...) reference.
                    string colorVar = UssVariableCollector.CollectColor(color);
                    styleBuilder.AddStyle("background-color", colorVar);
                }
            }

            return styleBuilder.ToString();
        }

    }
}
#endif