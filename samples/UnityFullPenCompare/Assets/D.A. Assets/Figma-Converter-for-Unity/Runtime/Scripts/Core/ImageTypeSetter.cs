using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using DA_Assets.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#pragma warning disable CS0649

namespace DA_Assets.FCU
{
    [Serializable]
    public class ImageTypeSetter : FcuBase
    {
        [SerializeField] ConcurrentBag<string> downloadableIds = new ConcurrentBag<string>();
        [SerializeField] ConcurrentBag<string> generativeIds = new ConcurrentBag<string>();
        [SerializeField] ConcurrentBag<string> drawableIds = new ConcurrentBag<string>();
        [SerializeField] ConcurrentBag<string> noneIds = new ConcurrentBag<string>();

        public ConcurrentBag<string> DownloadableIds => downloadableIds;
        public ConcurrentBag<string> GenerativeIds => generativeIds;
        public ConcurrentBag<string> DrawableIds => drawableIds;
        public ConcurrentBag<string> NoneIds => noneIds;

        public void ClearAllIds()
        {
            downloadableIds = new ConcurrentBag<string>();
            generativeIds = new ConcurrentBag<string>();
            drawableIds = new ConcurrentBag<string>();
            noneIds = new ConcurrentBag<string>();
        }

        private void SetImageFormat(FObject fobject)
        {
            ImageFormat imageFormat;
            string reason;

            if (monoBeh.UsingSvgImage())
            {
                var svgCondition = monoBeh.Settings.ImageSpritesSettings.SvgCondition;
                bool hasImageOrVideo = svgCondition.HasFlag(SvgCondition.ImageOrVideo) && fobject.IsAnyImageOrVideoOrEmojiTypeInChildren();
                bool hasAnyEffect = svgCondition.HasFlag(SvgCondition.AnyEffect) && fobject.IsAnyEffectInChildren();

                if (hasImageOrVideo)
                {
                    imageFormat = ImageFormat.PNG;
                    reason = "svgCondition_imageOrVideo";
                }
                else if (hasAnyEffect)
                {
                    imageFormat = ImageFormat.PNG;
                    reason = "svgCondition_anyEffect";
                }
                else
                {
                    imageFormat = monoBeh.Settings.ImageSpritesSettings.ImageFormat;
                    reason = "svgCondition_passed";
                }
            }
            else
            {
                imageFormat = monoBeh.Settings.ImageSpritesSettings.ImageFormat;
                reason = "notUsingSvgImageComponent";
            }

            FcuLogger.Debug($"SetImageFormat | {fobject.Data.NameHierarchy} | {imageFormat} | {reason}", FcuDebugSettingsFlags.LogIsDownloadable);

            fobject.Data.ImageFormat = imageFormat;
        }

        public async Task SetInsideDownloadableFlags(List<FObject> fobjects, CancellationToken token)
        {
            await Task.Run(() =>
            {
                Parallel.ForEach(fobjects, fobject =>
                {
                    if (fobject.ContainsTag(FcuTag.Image) == false)
                        return;
                    SetInsideDownloadableFlag(fobject);
                });
            }, token);
        }

        private void SetInsideDownloadableFlag(FObject fobject)
        {
            FObject parent;
            FObject current = fobject;

            while (monoBeh.CurrentProject.TryGetParent(current, out parent))
            {
                if (monoBeh.ImageTypeSetter.DownloadableIds.Contains(parent.Id))
                {
                    fobject.Data.InsideDownloadable = true;
                    return;
                }
                current = parent;
            }

            fobject.Data.InsideDownloadable = false;
        }

        public async Task SetImageTypes(List<FObject> fobjects, CancellationToken token)
        {
            Debug.Log(FcuLocKey.log_set_image_types.Localize());

            downloadableIds = new ConcurrentBag<string>();
            generativeIds = new ConcurrentBag<string>();
            drawableIds = new ConcurrentBag<string>();
            noneIds = new ConcurrentBag<string>();

            await Task.Run(() =>
            {
                Parallel.ForEach(fobjects, fobject =>
                {
                    if (fobject.ContainsTag(FcuTag.Image) == false)
                        return;

                    SetImageFormat(fobject);

                    bool isDownloadable = IsDownloadable(fobject);
                    bool isGenerative = IsGenerative(fobject, isDownloadable);
                    bool isDrawable = IsDrawable(fobject);

                    if (fobject.Data.ForceImage)
                    {
                        fobject.Data.FcuImageType = FcuImageType.Downloadable;
                        downloadableIds.Add(fobject.Id);
                    }
                    else if (isGenerative)
                    {
                        fobject.Data.FcuImageType = FcuImageType.Generative;
                        generativeIds.Add(fobject.Id);
                    }
                    else if (isDownloadable)
                    {
                        fobject.Data.FcuImageType = FcuImageType.Downloadable;
                        downloadableIds.Add(fobject.Id);
                    }
                    else if (isDrawable)
                    {
                        fobject.Data.FcuImageType = FcuImageType.Drawable;
                        drawableIds.Add(fobject.Id);
                    }
                    else
                    {
                        fobject.Data.FcuImageType = FcuImageType.None;
                        noneIds.Add(fobject.Id);
                    }

                    FcuLogger.Debug($"SetImageType | {fobject.Data.NameHierarchy} | {fobject.Data.FcuImageType}", FcuDebugSettingsFlags.LogIsDownloadable);
                });

                FcuLogger.Debug($"SetImageType | {downloadableIds.Count} | {generativeIds.Count} | {drawableIds.Count} | {noneIds.Count}", FcuDebugSettingsFlags.LogIsDownloadable);
            }, token);
        }

        private bool IsDownloadable(FObject fobject)
        {
            bool? result = null;
            ReasonKey reason = ReasonKey.None;

            if (fobject.Data.IsEmpty)
            {
                reason = ReasonKey.Dl_IsEmpty;
                result = false;
            }
            else if (fobject.Data.ForceImage)
            {
                reason = ReasonKey.Dl_ForceImage;
                result = true;
            }
            else if (fobject.Type == NodeType.VECTOR)
            {
                reason = ReasonKey.Dl_Vector;
                result = true;
            }
            else if (fobject.IsMask.ToBoolNullFalse())
            {
                reason = ReasonKey.Dl_IsMask;
                result = true;
            }
            else if (fobject.HaveUndownloadableTags(out ReasonKey _reason1))
            {
                reason = _reason1;
                result = false;
            }
            else if (fobject.IsArcDataFilled())
            {
                reason = ReasonKey.Dl_IsArcDataFilled;
                result = true;
            }

            if (result == null)
            {
                bool? res = monoBeh.GraphicHelpers.IsDownloadableByFills(fobject, out ReasonKey _reason2);

                if (res != null)
                {
                    reason = _reason2;
                    result = res;
                }
            }

            if (result == null)
            {
                if (!fobject.ContainsTag(FcuTag.Shadow))
                {
                    if (fobject.Effects.IsEmpty() == false)
                    {
                        int shadowCount = fobject.Effects.Count(x => x.IsVisible() && x.IsShadowType());

                        if (shadowCount > 0)
                        {
                            reason = ReasonKey.Dl_ContainsShadows;
                            result = true;
                        }
                    }
                }
            }

            if (result == null)
            {
                if (!fobject.ContainsTag(FcuTag.Blur))
                {
                    if (fobject.Effects.IsEmpty() == false)
                    {
                        int blurCount = fobject.Effects.Count(x => x.IsVisible() && x.IsBlurType());

                        if (blurCount > 0)
                        {
                            reason = ReasonKey.Dl_ContainsBlur;
                            result = true;
                        }
                    }
                }
            }

            if (result == null)
            {
                reason = ReasonKey.Dl_NoConditionMatched;
            }

            fobject.SetReason(reason);

            FcuLogger.Debug($"{nameof(IsDownloadable)} | {result} | {fobject.Data.NameHierarchy} | {reason}", FcuDebugSettingsFlags.LogIsDownloadable);
            return result.ToBoolNullFalse();
        }

        private bool IsGenerative(FObject fobject, bool isDownloadable)
        {
            bool? result = null;
            ReasonKey reason = ReasonKey.None;

            FGraphic graphic = fobject.Data.Graphic;

            if (monoBeh.UsingSVG())
            {
                reason = ReasonKey.Gen_UsingSVG;
                result = false;
            }
            else if (monoBeh.UsingAnyProceduralImage())
            {
                reason = ReasonKey.Gen_UsingProceduralImage;
                result = false;
            }
            else if (monoBeh.IsUITK())
            {
                reason = ReasonKey.Gen_IsUITK;
                result = false;
            }
            else if (monoBeh.IsNova())
            {
                reason = ReasonKey.Gen_IsNova;
                result = false;
            }
            else if (isDownloadable)
            {
                reason = ReasonKey.Gen_IsDownloadable;
                result = false;
            }
            else if (fobject.Data.IsOverlappedByStroke)
            {
                reason = ReasonKey.Gen_IsOverlappedByStroke;
                result = false;
            }
            else if (!fobject.Size.IsSupportedRenderSize(monoBeh.Settings.ImageSpritesSettings.ImageScale, out Vector2Int spriteSize, out Vector2Int _renderSize))
            {
                reason = ReasonKey.Gen_RenderSizeTooBig;
                result = false;
            }
            else if (!fobject.IsRectangle())
            {
                reason = ReasonKey.Gen_NotRectangle;
                result = false;
            }
            else if (graphic.HasFill && graphic.HasStroke)
            {
                reason = ReasonKey.Gen_HasFillAndStroke;
                result = false;
            }
            else if (graphic.HasStroke)
            {
                reason = ReasonKey.Gen_CanGenerateStrokeOnly;
                result = true;
            }
            else if (fobject.ContainsRoundedCorners())
            {
                reason = ReasonKey.Gen_ContainsRoundedCorners;
                result = true;
            }

            fobject.SetReason(reason);

            FcuLogger.Debug($"{nameof(IsGenerative)} | {result} | {fobject.Data.NameHierarchy} | {reason}", FcuDebugSettingsFlags.LogIsDownloadable);

            return result.ToBoolNullFalse();
        }

        private bool IsDrawable(FObject fobject)
        {
            bool result = true;
            string reason = "drawable";

            FcuLogger.Debug($"{nameof(IsDrawable)} | {result} | {fobject.Data.NameHierarchy} | {reason}", FcuDebugSettingsFlags.LogIsDownloadable);

            return result;
        }
    }
}
