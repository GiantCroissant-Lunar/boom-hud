using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using DA_Assets.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#pragma warning disable IDE0060

namespace DA_Assets.FCU
{
    [Serializable]
    public class GraphicHelpers : FcuBase
    {
        internal FGraphic GetGraphic(FObject fobject)
        {
            FGraphic graphic = new FGraphic();

            Paint solidFill = fobject.Fills.GetFirstSolidPaint();
            Paint gradientFill = GetFirstPaint(fobject.Fills);
            Color fillSingleColor = !solidFill.IsDefault() ? solidFill.Color : gradientFill.ToGradientColorKeys().FirstOrDefault().color;

            graphic.Fill = new FFill
            {
                HasSolid = !solidFill.IsDefault(),
                HasGradient = !gradientFill.IsDefault(),
                SolidPaint = solidFill,
                GradientPaint = gradientFill,
                SingleColor = fillSingleColor
            };

            bool nonZeroStroke = fobject.StrokeWeight > 0;

            if (nonZeroStroke)
            {
                Paint solidStroke = fobject.Strokes.GetFirstSolidPaint();
                Paint gradientStroke = GetFirstPaint(fobject.Strokes);

                Color strokeSingleColor = !solidStroke.IsDefault() ? solidStroke.Color : gradientStroke.ToGradientColorKeys().FirstOrDefault().color;

                graphic.Stroke = new FStroke
                {
                    Weight = fobject.StrokeWeight,

                    HasSolid = !solidStroke.IsDefault(),
                    HasGradient = !gradientStroke.IsDefault(),
                    SolidPaint = solidStroke,
                    GradientPaint = gradientStroke,
                    SingleColor = strokeSingleColor
                };
            }

            if (graphic.Fill.SingleColor.a == 1)
            {
                graphic.FillAlpha1 = true;
            }

            graphic.HasSingleColor = fobject.IsSingleColor(out Color singleColor);
            graphic.SpriteSingleColor = singleColor;

            graphic.HasSingleGradient = fobject.IsSingleLinearGradient(out Paint gradient);
            graphic.SpriteSingleLinearGradient = gradient;

            graphic.HasFill = !graphic.Fill.SolidPaint.IsDefault() || !graphic.Fill.GradientPaint.IsDefault();
            graphic.HasStroke = nonZeroStroke && (!graphic.Stroke.SolidPaint.IsDefault() || !graphic.Stroke.GradientPaint.IsDefault());

            FStroke updStroke = graphic.Stroke;
            updStroke.Align = GetStrokeAlign(fobject, graphic);
            graphic.Stroke = updStroke;

            return graphic;
        }

        private StrokeAlign GetStrokeAlign(FObject fobject, FGraphic graphic)
        {
            if (monoBeh.IsNova())
            {
                return fobject.StrokeAlign;
            }
            else if (monoBeh.IsUITK())
            {
                return StrokeAlign.INSIDE;
            }
            else if (monoBeh.UsingShapes2D())
            {
                if (graphic.FillAlpha1)
                {
                    if (graphic.Fill.HasSolid)
                    {
                        if (fobject.StrokeAlign == StrokeAlign.OUTSIDE)
                        {
                            return StrokeAlign.OUTSIDE;
                        }
                        else
                        {
                            return StrokeAlign.INSIDE;
                        }
                    }
                    else if (graphic.Fill.HasGradient)
                    {
                        return StrokeAlign.OUTSIDE;
                    }
                    else
                    {
                        return StrokeAlign.NONE;
                    }
                }
                else
                {
                    return StrokeAlign.INSIDE;
                }
            }
            else if (monoBeh.UsingJoshPui())
            {
                if (graphic.FillAlpha1)
                {
                    return StrokeAlign.OUTSIDE;
                }
                else
                {
                    return StrokeAlign.NONE;
                }
            }
            else if (monoBeh.UsingDttPui())
            {
                if (graphic.HasStroke && graphic.Fill.GradientPaint.IsDefault())
                {
                    return StrokeAlign.OUTSIDE;
                }
                else
                {
                    return StrokeAlign.NONE;
                }
            }
            else if (monoBeh.UsingMPUIKit())
            {
                if (graphic.FillAlpha1)
                {
                    if (graphic.Fill.HasSolid)
                    {
                        if (fobject.StrokeAlign == StrokeAlign.OUTSIDE)
                        {
                            return StrokeAlign.OUTSIDE;
                        }
                        else
                        {
                            return StrokeAlign.INSIDE;
                        }
                    }
                    else if (graphic.Fill.HasGradient)
                    {
                        return StrokeAlign.OUTSIDE;
                    }
                    else
                    {
                        return StrokeAlign.NONE;
                    }
                }
                else
                {
                    return StrokeAlign.INSIDE;
                }
            }
            else if (monoBeh.UsingFlexibleImage())
            {
                if (fobject.StrokeAlign == StrokeAlign.OUTSIDE)
                {
                    return StrokeAlign.OUTSIDE;
                }
                else
                {
                    return StrokeAlign.INSIDE;
                }
            }
            else if (monoBeh.UsingSpriteRenderer())
            {
                return StrokeAlign.OUTSIDE;
            }
            else
            {
                return StrokeAlign.OUTSIDE;
            }
        }

        public Color32 GetRectTransformColor(FObject fobject)
        {
            FGraphic graphic = fobject.Data.Graphic;

            Color32? color = null;

            if (graphic.HasFill)
            {
                color = graphic.Fill.SingleColor;
            }
            else if (graphic.HasStroke)
            {
                color = graphic.Stroke.SingleColor;
            }

            if (color == null)
            {
                color = new Color32(255, 255, 255, 0);
            }
            else
            {
                Color32 tempColor = color.Value;
                tempColor.a = 128;
                color = tempColor;
            }

            return color.Value;
        }

        public Vector4 GetCornerRadius(FObject fobject)
        {
            if (fobject.IsCircle())
            {
                return new Vector4
                {
                    x = 9999f,
                    y = 9999f,
                    z = 9999f,
                    w = 9999f,
                };
            }

            bool uniform = fobject.CornerRadiuses.IsEmpty();

            if (uniform)
            {
                return new Vector4
                {
                    x = fobject.CornerRadius.ToFloat(),
                    y = fobject.CornerRadius.ToFloat(),
                    z = fobject.CornerRadius.ToFloat(),
                    w = fobject.CornerRadius.ToFloat()
                };
            }

            if (monoBeh.IsUITK())
            {
                return new Vector4
                {
                    x = fobject.CornerRadiuses[0],
                    y = fobject.CornerRadiuses[3],
                    z = fobject.CornerRadiuses[2],
                    w = fobject.CornerRadiuses[1]
                };
            }

            if (monoBeh.IsNova())
            {
                return new Vector4
                {
                    x = fobject.CornerRadiuses[0],
                    y = fobject.CornerRadiuses[3],
                    z = fobject.CornerRadiuses[2],
                    w = fobject.CornerRadiuses[1]
                };
            }

            switch (monoBeh.Settings.ImageSpritesSettings.ImageComponent)
            {
                case ImageComponent.UnityImage:
                    {
                        return new Vector4
                        {
                            x = fobject.CornerRadiuses[0],
                            y = fobject.CornerRadiuses[1],
                            z = fobject.CornerRadiuses[2],
                            w = fobject.CornerRadiuses[3]
                        };
                    }
                case ImageComponent.ProceduralImage:
                    {
                        return new Vector4
                        {
                            x = fobject.CornerRadiuses[0],
                            y = fobject.CornerRadiuses[1],
                            z = fobject.CornerRadiuses[2],
                            w = fobject.CornerRadiuses[3]
                        };
                    }
                case ImageComponent.RoundedImage:
                    {
                        return new Vector4
                        {
                            x = fobject.CornerRadiuses[0],
                            y = fobject.CornerRadiuses[1],
                            z = fobject.CornerRadiuses[3],
                            w = fobject.CornerRadiuses[2],
                        };
                    }
                case ImageComponent.MPImage:
                    {
                        return new Vector4
                        {
                            x = fobject.CornerRadiuses[3],
                            y = fobject.CornerRadiuses[2],
                            z = fobject.CornerRadiuses[1],
                            w = fobject.CornerRadiuses[0]
                        };
                    }
                case ImageComponent.SubcShape:
                    {
                        return new Vector4
                        {
                            x = fobject.CornerRadiuses[3],
                            y = fobject.CornerRadiuses[2],
                            z = fobject.CornerRadiuses[1],
                            w = fobject.CornerRadiuses[0]
                        };
                    }
                case ImageComponent.FlexibleImage:
                    {
                        return new Vector4
                        {
                            x = fobject.CornerRadiuses[0],
                            y = fobject.CornerRadiuses[1],
                            z = fobject.CornerRadiuses[3],
                            w = fobject.CornerRadiuses[2]
                        };
                    }
                case ImageComponent.SpriteRenderer:
                    {
                        return new Vector4
                        {
                            x = fobject.CornerRadiuses[0],
                            y = fobject.CornerRadiuses[0],
                            z = fobject.CornerRadiuses[0],
                            w = fobject.CornerRadiuses[0]
                        };
                    }
                default:
                    {
                        return new Vector4
                        {
                            x = fobject.CornerRadiuses[0],
                            y = fobject.CornerRadiuses[1],
                            z = fobject.CornerRadiuses[2],
                            w = fobject.CornerRadiuses[3]
                        };
                    }
            }
        }

        public Paint GetFirstPaint(List<Paint> paints)
        {
            if (paints.IsEmpty())
                return default;

            IEnumerable<Paint> gradients = paints.Where(x => x.Type.Contains("GRADIENT"));

            if (gradients.IsEmpty())
                return default;

            Paint firstByPriority = gradients.OrderBy(p => p.Type.GetPriority()).First();
            return firstByPriority;
        }

        private bool ContainsMultipleFills(FObject fobject, out ReasonKey reason)
        {
            reason = ReasonKey.None;
            FGraphic graphic = fobject.Data.Graphic;

            if (fobject.Fills.IsEmpty() || fobject.Strokes.IsEmpty())
            {
                reason = ReasonKey.Dl_NoFillsOrStrokes;
                return false;
            }

            bool fillsMoreThanOne = fobject.Fills.Count(x => x.IsVisible()) > 1;
            bool strokesMoreThanOne = fobject.Strokes.Count(x => x.IsVisible()) > 1;
            bool fillsAndStroke = graphic.HasFill && graphic.HasStroke;

            if (fillsMoreThanOne)
            {
                reason = ReasonKey.Dl_MultipleFills;
                return true;
            }
            else if (strokesMoreThanOne)
            {
                reason = ReasonKey.Dl_MultipleStrokes;
                return true;
            }
            else if (fillsAndStroke)
            {
                if (monoBeh.UsingMPUIKit() || monoBeh.UsingFlexibleImage())
                {
                    reason = ReasonKey.Dl_ProceduralFillAndStrokeSupported;
                    return false;
                }

                reason = ReasonKey.Dl_FillAndStroke;
                return true;
            }

            return false;
        }

        public bool? IsDownloadableByFills(FObject fobject, out ReasonKey reason)
        {
            reason = ReasonKey.None;
            FGraphic graphic = fobject.Data.Graphic;

            if (fobject.ContainsImageEmojiVideo())
            {
                reason = ReasonKey.Dl_ContainsImageEmojiVideo;
                return true;
            }

            if (monoBeh.IsUGUI())
            {
                if (!monoBeh.UsingMPUIKit() && !monoBeh.UsingFlexibleImage())
                {
                    if (graphic.HasStroke && graphic.HasFill && !graphic.FillAlpha1)
                    {
                        reason = ReasonKey.Dl_FillTransparencyPlusStroke;
                        return true;
                    }
                }

                bool sizeLowerThan48 = fobject.Size.x < 48 || fobject.Size.y < 48;

                if (sizeLowerThan48 && graphic.HasStroke && fobject.StrokeAlign == StrokeAlign.OUTSIDE)
                {
                    reason = ReasonKey.Dl_SmallSizeOutsideStroke;
                    return true;
                }
            }

            bool procedural =
                monoBeh.UsingAnyProceduralImage() ||
                monoBeh.IsUITK() ||
                monoBeh.IsNova();

            if (monoBeh.Settings.ImageSpritesSettings.DownloadOptions
                .HasFlag(SpriteDownloadOptions.MultipleFills) || !procedural)
            {
                if (ContainsMultipleFills(fobject, out ReasonKey reason1))
                {
                    reason = reason1;
                    return true;
                }
            }

            if (monoBeh.Settings.ImageSpritesSettings.DownloadOptions
                .HasFlag(SpriteDownloadOptions.UnsupportedGradients) || !procedural)
            {
                if (ContainsUnsupportedGradients(fobject, out ReasonKey reason1))
                {
                    reason = reason1;
                    return true;
                }
            }

            if (monoBeh.Settings.ImageSpritesSettings.DownloadOptions
                .HasFlag(SpriteDownloadOptions.SupportedGradients) || !procedural)
            {
                if (ContainsSupportedGradients(fobject, out ReasonKey reason1))
                {
                    reason = reason1;
                    return true;
                }
            }

            if (monoBeh.IsUGUI())
            {
                if (monoBeh.UsingUnityImage() || monoBeh.UsingRawImage() || monoBeh.UsingSpriteRenderer() || monoBeh.UsingSVG())
                {
                    if (!fobject.IsRectangle())
                    {
                        reason = ReasonKey.Dl_NotRectangle;
                        return true;
                    }

                    if (fobject.ContainsRoundedCorners())
                    {
                        reason = ReasonKey.Dl_ContainsRoundedCorners;
                        return true;
                    }

                    if (fobject.HasVisibleProperty(x => x.Strokes))
                    {
                        reason = ReasonKey.Dl_HasStrokes;
                        return true;
                    }
                }
                else if (monoBeh.UsingDttPui())
                {
                    if (graphic.HasStroke && graphic.Fill.HasGradient)
                    {
                        reason = ReasonKey.Dl_GradientFillPlusStroke;
                        return true;
                    }
                }
            }

            return null;
        }

        private bool ContainsSupportedGradients(FObject fobject, out ReasonKey reason)
        {
            reason = ReasonKey.None;
            FGraphic graphic = fobject.Data.Graphic;

            // Nova/UITK do not support gradients
            if (monoBeh.IsNova() || monoBeh.IsUITK())
                return false;

            switch (monoBeh.Settings.ImageSpritesSettings.ImageComponent)
            {
                // Standard image components: gradients are not supported
                case ImageComponent.UnityImage:
                case ImageComponent.RawImage:
                case ImageComponent.SpriteRenderer:
                    return false;

                // Only linear gradient is supported
                case ImageComponent.ProceduralImage:
                case ImageComponent.SubcShape:
                case ImageComponent.MPImage:
                case ImageComponent.RoundedImage:
                    if (graphic.Fill.HasGradient || graphic.Stroke.HasGradient)
                    {
                        if (graphic.Fill.GradientPaint.Type == PaintType.GRADIENT_LINEAR)
                        {
                            reason = ReasonKey.Dl_GradientType;
                            return true;
                        }
                    }
                    break;
                case ImageComponent.FlexibleImage:
                    if (graphic.Fill.HasGradient || graphic.Stroke.HasGradient)
                    {
                        Paint? gf = graphic.Fill.HasGradient ? graphic.Fill.GradientPaint : null;

                        if (gf == null)
                        {
                            gf = graphic.Stroke.HasGradient ? graphic.Stroke.GradientPaint : null;
                        }

                        if (gf != null)
                        {
                            reason = ReasonKey.Dl_GradientType;
                            return true;
                        }
                    }
                    break;
            }

            return false;
        }

        private bool ContainsUnsupportedGradients(FObject fobject, out ReasonKey reason1)
        {
            reason1 = ReasonKey.None;

            FGraphic graphic = fobject.Data.Graphic;

            if (monoBeh.IsNova() || monoBeh.IsUITK())
            {
                if (graphic.Fill.HasGradient || graphic.Stroke.HasGradient)
                {
                    reason1 = ReasonKey.Dl_HasGradients;
                    return true;
                }
            }

            switch (monoBeh.Settings.ImageSpritesSettings.ImageComponent)
            {
                case ImageComponent.UnityImage:
                case ImageComponent.RawImage:
                case ImageComponent.SpriteRenderer:
                    if (graphic.Fill.HasGradient || graphic.Stroke.HasGradient)
                    {
                        reason1 = ReasonKey.Dl_HasGradients;
                        return true;
                    }
                    break;
                case ImageComponent.ProceduralImage:
                    switch (graphic.Fill.GradientPaint.Type)
                    {
                        case PaintType.GRADIENT_RADIAL:
                        case PaintType.GRADIENT_ANGULAR:
                        case PaintType.GRADIENT_DIAMOND:
                            {
                                reason1 = ReasonKey.Dl_GradientType;
                                return true;
                            }
                    }
                    break;
                case ImageComponent.SubcShape:
                case ImageComponent.MPImage:
                    switch (graphic.Fill.GradientPaint.Type)
                    {
                        case PaintType.GRADIENT_ANGULAR:
                        case PaintType.GRADIENT_DIAMOND:
                            {
                                reason1 = ReasonKey.Dl_GradientType;
                                return true;
                            }
                    }
                    break;
                case ImageComponent.RoundedImage:
                    switch (graphic.Fill.GradientPaint.Type)
                    {
                        case PaintType.GRADIENT_DIAMOND:
                            {
                                reason1 = ReasonKey.Dl_GradientType;
                                return true;
                            }
                    }
                    break;
            }

            return false;
        }

        public float ToRadialAngle(FObject fobject, List<Vector2> gradientHandlePositions)
        {
            return 0f;
        }

        public float ToLinearAngle(FObject fobject, List<Vector2> gradientHandlePositions)
        {
            float angle = 0f;

            if (gradientHandlePositions.IsEmpty() || gradientHandlePositions.Count < 3)
            {
                Debug.LogError($"Can't calculate angle.");
                angle = 0f;
            }
            else if (!fobject.CanUseUnityImage(monoBeh) && (monoBeh.UsingMPUIKit() || monoBeh.UsingDttPui()))
            {
                Vector2 p1 = new Vector2(gradientHandlePositions[0].x, gradientHandlePositions[0].y * -1);
                Vector2 p2 = new Vector2(gradientHandlePositions[2].x, gradientHandlePositions[2].y * -1);
                Vector2 distance = p2 - p1;

                float radians = Mathf.PI / 2 - Mathf.Atan2(-distance.y, distance.x);

                float degrees = radians * Mathf.Rad2Deg;
                angle = (degrees + 360) % 360;
            }
            else if (!fobject.CanUseUnityImage(monoBeh) && monoBeh.UsingFlexibleImage())
            {
                Vector2 p0 = gradientHandlePositions[0];
                Vector2 p1 = gradientHandlePositions[1];
                Vector2 p2 = gradientHandlePositions[2];

                Vector2 axis = p1 - p0;
                Vector2 width = p2 - p0;

                float axisLen = axis.magnitude;
                float widthLen = width.magnitude;
                float ratio = widthLen > 0f ? axisLen / widthLen : 1f;

                float scaledVx = width.x * Mathf.Pow(ratio, 0.75f);
                float radians = Mathf.Atan2(-scaledVx, width.y);

                float degrees = radians * Mathf.Rad2Deg;
                angle = (degrees + 360) % 360;
            }
            else
            {
                float radians = Mathf.Atan2(
                    gradientHandlePositions[2].y - gradientHandlePositions[0].y,
                    gradientHandlePositions[2].x - gradientHandlePositions[0].x
                );

                float degrees = radians * Mathf.Rad2Deg;
                angle = (degrees + 360) % 360;
            }

            return angle.Round(0);
        }
    }
}