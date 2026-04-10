#if FCU_EXISTS
using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;

namespace DA_Assets.FCU
{
    [Serializable]
    public class TextStyleBuilder : FcuBase
    {
        public string CreateGlobalTextStyle(FObject fobject)
        {
            StringBuilder styleBuilder = new StringBuilder();

            int px0 = 0;
            string zeroSpacing = UssVariableCollector.CollectSpacing(px0);

            styleBuilder.AddStyle("margin-left",   zeroSpacing);
            styleBuilder.AddStyle("margin-right",  zeroSpacing);
            styleBuilder.AddStyle("margin-top",    zeroSpacing);
            styleBuilder.AddStyle("margin-bottom", zeroSpacing);

            styleBuilder.AddStyle("padding-left",   zeroSpacing);
            styleBuilder.AddStyle("padding-right",  zeroSpacing);
            styleBuilder.AddStyle("padding-top",    zeroSpacing);
            styleBuilder.AddStyle("padding-bottom", zeroSpacing);

            FGraphic graphic = fobject.Data.Graphic;

            string rgba = null;

            if (graphic.HasFill)
            {
                rgba = graphic.Fill.SingleColor.ToCssColor(graphic.Fill.SingleColor.a);
            }
            else if (graphic.HasStroke)
            {
                rgba = graphic.Stroke.SingleColor.ToCssColor(graphic.Stroke.SingleColor.a);
            }

            if (!rgba.IsEmpty())
            {
                // Register color as a USS variable and use var(--...) reference.
                string colorVar = UssVariableCollector.CollectColor(graphic.HasFill
                    ? graphic.Fill.SingleColor
                    : graphic.Stroke.SingleColor);
                styleBuilder.AddStyle("color", colorVar);
            }

            //////////////////////////////////////////////////////

            var uitkSettings = monoBeh.Settings?.UitkTextSettings;
            bool isAutoWidthText = fobject.Style.TextAutoResize == TextAutoResize.WIDTH_AND_HEIGHT;

            if (isAutoWidthText)
            {
                styleBuilder.AddStyle("white-space", "nowrap");
                styleBuilder.AddStyle("text-overflow", "clip");
                styleBuilder.AddStyle("overflow", "hidden");
            }
            else if (uitkSettings != null)
            {
                string whiteSpaceCss = GetWhiteSpaceCss(uitkSettings.WhiteSpace);

                if (!whiteSpaceCss.IsEmpty())
                {
                    styleBuilder.AddStyle("white-space", whiteSpaceCss);
                }
                else
                {
                    styleBuilder.AddStyle("white-space", "nowrap");
                }

                string textOverflowCss = GetTextOverflowCss(uitkSettings.TextOverflow);

                if (!textOverflowCss.IsEmpty())
                {
                    styleBuilder.AddStyle("text-overflow", textOverflowCss);

                    switch (uitkSettings.TextOverflow)
                    {
                        case TextOverflow.Ellipsis:
                            styleBuilder.AddStyle("-unity-text-overflow-position", "end");
                            styleBuilder.AddStyle("overflow", "hidden");
                            break;
                        case TextOverflow.Clip:
                            styleBuilder.AddStyle("overflow", "hidden");
                            break;
                    }
                }
            }
            else
            {
                styleBuilder.AddStyle("white-space", "nowrap");
            }

            string acnhor = fobject.GetTextAnchor().ToUITKAnchor();
            styleBuilder.AddStyle("-unity-text-align", acnhor);

            KeyValuePair<string, string> kvp = GetFontDefinition(fobject);
            if (kvp.Key != null)
            {
                styleBuilder.AddStyle(kvp.Key, kvp.Value);
            }
            else
            {
                //no font or font == NotInter
            }

            int fontSize = System.Convert.ToInt32(fobject.Style.FontSize);
            // Register font size as a USS variable and use var(--...) reference.
            string fontSizeVar = UssVariableCollector.CollectFontSize(fontSize);
            styleBuilder.AddStyle("font-size", fontSizeVar);

            //////////////////////////////////////////////////////

            return styleBuilder.ToString();
        }

        private static string GetWhiteSpaceCss(WhiteSpace whiteSpace)
        {
            switch (whiteSpace)
            {
                case WhiteSpace.Normal:
                    return "normal";
                case WhiteSpace.NoWrap:
                    return "nowrap";
#if UNITY_6000_3_OR_NEWER
                case WhiteSpace.Pre:
                    return "pre";
                case WhiteSpace.PreWrap:
                    return "pre-wrap";
#endif
                default:
                    return "normal";
            }
        }

        private static string GetTextOverflowCss(TextOverflow textOverflow)
        {
            switch (textOverflow)
            {
                case TextOverflow.Clip:
                    return "clip";
                case TextOverflow.Ellipsis:
                    return "ellipsis";
                default:
                    return string.Empty;
            }
        }

        public KeyValuePair<string, string> GetFontDefinition(FObject fobject)
        {
#if FCU_UITK_EXT_EXISTS
            string fontPath = fobject.Data.Names.UITK_FontPath;

            if (!fontPath.IsEmpty() && !fontPath.StartsWith("project://database/Library"))
            {
                return new KeyValuePair<string, string>("-unity-font-definition", $"url({fobject.Data.Names.UITK_FontPath})");
            }
            else
#endif
            {
                return new KeyValuePair<string, string>(null, null);
            }
        }
    }
}
#endif
