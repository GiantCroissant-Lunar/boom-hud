using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DA_Assets.FCU.Drawers.CanvasDrawers
{
    [Serializable]
    public class UnityImageDrawer : FcuBase
    {
        public void Draw(FObject fobject, Sprite sprite, GameObject target)
        {
            MaskableGraphic graphic;

            if (monoBeh.UsingRawImage())
            {
                target.TryAddGraphic(out RawImage img);
                graphic = img;

                if (sprite != null)
                {
                    img.texture = sprite.texture;
                }
            }
            else
            {
                target.TryAddGraphic(out Image img);
                graphic = img;

                img.sprite = sprite;
                img.type = monoBeh.Settings.UnityImageSettings.Type;
                img.preserveAspect = monoBeh.Settings.UnityImageSettings.PreserveAspect;
            }

            graphic.raycastTarget = monoBeh.Settings.UnityImageSettings.RaycastTarget;
            graphic.maskable = monoBeh.Settings.UnityImageSettings.Maskable;
#if UNITY_2020_1_OR_NEWER
            graphic.raycastPadding = monoBeh.Settings.UnityImageSettings.RaycastPadding;
#endif

            if (monoBeh.UseImageLinearMaterial())
            {
                graphic.material = FcuConfig.ImageLinearMaterial;
            }
            else
            {
                graphic.material = null;
            }

            SetColor(fobject, graphic);
            monoBeh.CanvasDrawer.ImageDrawer.TryAddCornerRounder(fobject, target);
        }

        public void SetColor(FObject fobject, MaskableGraphic img)
        {
            FGraphic graphic = fobject.Data.Graphic;

            FcuLogger.Debug($"SetUnityImageColor | {fobject.Data.NameHierarchy} | {fobject.Data.FcuImageType} | graphic.HasFills: {graphic.HasFill} | graphic.HasStrokes: {graphic.HasStroke}", FcuDebugSettingsFlags.LogComponentDrawer);

            if (fobject.ContainsTag(FcuTag.BtnDisabled))
            {
                //TODO
                //Despite assigning the White color here,
                //it gets tinted to a different color if the White tinting is removed in method UnityButtonDrawer.SetImageColor.
                img.color = Color.white;
            }
            else if (fobject.IsDrawableType())
            {
                bool strokeOnly = graphic.HasStroke && !graphic.HasFill;

                if (strokeOnly)
                {
                    img.color = default;
                    fobject.SetReason(ReasonKey.Fill_Transparent);
                }
                else if (graphic.Fill.HasSolid)
                {
                    img.color = graphic.Fill.SolidPaint.Color;
                    fobject.SetReason(ReasonKey.Fill_SolidColor);
                }
                else if (graphic.HasStroke && graphic.Fill.HasGradient)
                {
                    img.color = graphic.Fill.SingleColor;
                    fobject.SetReason(ReasonKey.Fill_SolidColor);
                }
                else if (graphic.Fill.HasGradient)
                {
                    img.color = Color.white;
                    monoBeh.CanvasDrawer.ImageDrawer.AddGradient(fobject, graphic.Fill.GradientPaint);
                    fobject.SetReason(ReasonKey.Fill_GradientComponent);
                }

                if (graphic.HasFill)
                {
                    if (graphic.HasStroke)
                    {
                        monoBeh.CanvasDrawer.ImageDrawer.AddUnityOutline(fobject);
                        fobject.SetReason(ReasonKey.Stroke_UnityOutline);
                    }
                    else
                    {
                        fobject.SetReason(ReasonKey.None);
                    }
                }

                if (!graphic.HasStroke)
                {
                    fobject.Data.GameObject.TryDestroyComponent<UnityEngine.UI.Outline>();
                    fobject.SetReason(ReasonKey.None);
                }
            }
            else if (fobject.IsGenerativeType())
            {
                if (graphic.HasFill && graphic.HasStroke)//no need colorize
                {
                    monoBeh.CanvasDrawer.ImageDrawer.AddUnityOutline(fobject);
                    fobject.SetReason(ReasonKey.Fill_BakedInSprite);
                    fobject.SetReason(ReasonKey.Stroke_UnityOutline);
                }
                else if (graphic.HasFill)
                {
                    if (graphic.Fill.HasSolid)
                    {
                        img.color = graphic.Fill.SolidPaint.Color;
                        fobject.SetReason(ReasonKey.Fill_SolidColor);
                    }
                    else if (graphic.Fill.HasGradient)
                    {
                        img.color = Color.white;
                        monoBeh.CanvasDrawer.ImageDrawer.AddGradient(fobject, graphic.Fill.GradientPaint);
                        fobject.SetReason(ReasonKey.Fill_GradientComponent);
                    }
                    
                    fobject.SetReason(ReasonKey.None);
                }
                else if (graphic.HasStroke)
                {
                    if (graphic.Stroke.HasSolid)
                    {
                        img.color = graphic.Stroke.SolidPaint.Color;
                        fobject.SetReason(ReasonKey.Fill_SolidColor);
                    }
                    else
                    {
                        img.color = Color.white;
                        monoBeh.CanvasDrawer.ImageDrawer.AddGradient(fobject, graphic.Stroke.GradientPaint);
                        fobject.SetReason(ReasonKey.Fill_GradientComponent);
                    }
                    
                    fobject.SetReason(ReasonKey.Stroke_BakedInSprite);
                }
            }
            else if (fobject.IsDownloadableType())
            {
                if (fobject.Data.Graphic.HasSingleColor)
                {
                    img.color = fobject.Data.Graphic.SpriteSingleColor;
                    fobject.SetReason(ReasonKey.Fill_SingleColorTint);
                }
                else if (fobject.Data.Graphic.HasSingleGradient && !monoBeh.Settings.ImageSpritesSettings.DownloadOptions.HasFlag(SpriteDownloadOptions.SupportedGradients))
                {
                    monoBeh.CanvasDrawer.ImageDrawer.AddGradient(fobject, fobject.Data.Graphic.SpriteSingleLinearGradient);
                    fobject.SetReason(ReasonKey.Fill_GradientComponent);
                }
                else
                {
                    img.color = Color.white;
                    fobject.SetReason(ReasonKey.Fill_BakedInSprite);
                }
                
                fobject.SetReason(ReasonKey.Stroke_BakedInSprite);
            }
        }
    }
}