using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DA_Assets.FCU
{
    [Serializable]
    public class SpriteSlicer : FcuBase
    {
        /// <summary>
        /// Applies 9-slice metadata to sprites whose Figma object has exactly 9 named children.
        /// The border values (left, top, right, bottom) are derived from the sizes of the four corner
        /// children (indices 0, 2, 6, 8) and scaled to the downloaded image resolution.
        /// The sprite is re-read after slicing so that Unity's border data is applied to the live asset.
        /// </summary>
        /// <param name="fobjects">All Figma objects from the current page.</param>
        /// <param name="token">Cancellation token for cooperative cancellation during async iteration.</param>
        public async Task SliceSprites(List<FObject> fobjects, CancellationToken token)
        {
            foreach (FObject fobject in fobjects)
            {
                token.ThrowIfCancellationRequested();

                if (!fobject.IsSprite())
                    continue;

                if (fobject.Children.IsEmpty())
                    continue;

                if (fobject.Children.Count != 9)
                    continue;

                Sprite sprite = monoBeh.SpriteProcessor.GetSprite(fobject);

                if (sprite == null)
                    continue;

                FObject child0 = fobject.Children[0];
                FObject child1 = fobject.Children[1];
                FObject child2 = fobject.Children[2];
                FObject child3 = fobject.Children[3];
                FObject child4 = fobject.Children[4];
                FObject child5 = fobject.Children[5];
                FObject child6 = fobject.Children[6];
                FObject child7 = fobject.Children[7];
                FObject child8 = fobject.Children[8];

                float imageScale = fobject.Data.Scale;

                int left = (int)(child0.Size.x * imageScale);
                int top = (int)(child0.Size.y * imageScale);
                int right = (int)(child2.Size.x * imageScale);
                int bottom = (int)(child6.Size.y * imageScale);

                Vector4 border = new Vector4(left, bottom, right, top);

                if (fobject.Data.ManualWhiteColor)
                {
                    Texture2D texture = null;

                    try
                    {
                        texture = CreateMinimal9Slice(sprite, border, token);
                        monoBeh.SpriteProcessor.SaveSprite(texture, fobject);
                        texture = null;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex); 
                        return;
                    }
                    finally
                    {
                        DestroyTexture(texture);
                    }

                    sprite = monoBeh.SpriteProcessor.GetSprite(fobject);
                }

                monoBeh.EditorDelegateHolder.SetSpriteRects(sprite, border);
                await Task.Yield();
            }
        }

        /// <summary>
        /// Automatically computes and applies 9-slice borders for sprites tagged with
        /// <see cref="FcuTag.AutoSlice9"/> based on their Figma corner radii, stroke weight, and effects.
        /// Each border is sized to fully encompass the largest corner radius on that edge plus any
        /// stroke/effect expansion, then clamped to half the texture dimension.
        /// Sprites where the computed borders would consume the entire texture are skipped.
        /// </summary>
        /// <param name="fobjects">All Figma objects from the current page.</param>
        /// <param name="token">Cancellation token for cooperative cancellation during async iteration.</param>
        public async Task AutoSliceSprites(List<FObject> fobjects, CancellationToken token)
        {
            foreach (FObject fobject in fobjects)
            {
                token.ThrowIfCancellationRequested();

                if (!fobject.ContainsTag(FcuTag.AutoSlice9))
                    continue;

                Sprite sprite = monoBeh.SpriteProcessor.GetSprite(fobject);

                if (sprite == null)
                    continue;

                float imageScale = fobject.Data.Scale;
                int strokeExtra = 0;

                if (fobject.Data.Graphic.HasStroke)
                {
                    strokeExtra = Mathf.CeilToInt(fobject.StrokeWeight * imageScale);
                }

                // Uniform effects (offset 0,0) expand borders so blur stays in fixed zones.
                float maxEffectExtent = 0f;
                if (fobject.Effects != null)
                {
                    foreach (Effect e in fobject.Effects)
                    {
                        if (!e.IsVisible()) continue;

                        switch (e.Type)
                        {
                            case EffectType.DROP_SHADOW:
                            case EffectType.INNER_SHADOW:
                                float extent = e.Radius + (e.Spread ?? 0f);
                                maxEffectExtent = Mathf.Max(maxEffectExtent, extent);
                                break;
                            case EffectType.LAYER_BLUR:
                                maxEffectExtent = Mathf.Max(maxEffectExtent, e.Radius);
                                break;
                        }
                    }
                }
                int effectExtra = Mathf.CeilToInt(maxEffectExtent * imageScale);

                // Get per-corner radii (Figma order: topLeft, topRight, bottomRight, bottomLeft).
                float topLeft, topRight, bottomRight, bottomLeft;

                if (fobject.CornerRadiuses.IsEmpty())
                {
                    float r = fobject.CornerRadius.ToFloat();
                    topLeft = topRight = bottomRight = bottomLeft = r;
                }
                else
                {
                    topLeft = fobject.CornerRadiuses[0];
                    topRight = fobject.CornerRadiuses[1];
                    bottomRight = fobject.CornerRadiuses[2];
                    bottomLeft = fobject.CornerRadiuses[3];
                }

                int extra = strokeExtra + effectExtra;

                int texW = sprite.texture.width;
                int texH = sprite.texture.height;

                // Each border must be large enough to contain the largest corner + effects on that edge.
                int left = Mathf.Max(Mathf.CeilToInt(Mathf.Max(topLeft, bottomLeft) * imageScale) + extra, 1);
                int right = Mathf.Max(Mathf.CeilToInt(Mathf.Max(topRight, bottomRight) * imageScale) + extra, 1);
                int top = Mathf.Max(Mathf.CeilToInt(Mathf.Max(topLeft, topRight) * imageScale) + extra, 1);
                int bottom = Mathf.Max(Mathf.CeilToInt(Mathf.Max(bottomLeft, bottomRight) * imageScale) + extra, 1);

                left   = Mathf.Min(left,   texW / 2);
                right  = Mathf.Min(right,  texW / 2);
                top    = Mathf.Min(top,    texH / 2);
                bottom = Mathf.Min(bottom, texH / 2);

                // If borders cover the entire texture, 9-slice has no stretchable center — skip.
                if (left + right >= texW || top + bottom >= texH)
                    continue;

                Vector4 border = new Vector4(left, bottom, right, top);

                if (fobject.Data.ManualWhiteColor)
                {
                    Texture2D texture = null;

                    try
                    {
                        texture = CreateMinimal9Slice(sprite, border, token);
                        monoBeh.SpriteProcessor.SaveSprite(texture, fobject);
                        texture = null;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        return;
                    }
                    finally
                    {
                        DestroyTexture(texture);
                    }

                    sprite = monoBeh.SpriteProcessor.GetSprite(fobject);
                }

                monoBeh.EditorDelegateHolder.SetSpriteRects(sprite, border);
                await Task.Yield();
            }
        }

        /// <summary>
        /// Creates a minimal 9-slice texture from <paramref name="sourceSprite"/> by copying only the
        /// four corners into a new <see cref="Texture2D"/> whose dimensions equal
        /// <c>(leftBorder + rightBorder) × (bottomBorder + topBorder)</c>.
        /// The resulting texture has no center or edge strips — it contains only the corner pixels
        /// needed to configure Unity's sprite border without inflating atlas memory.
        /// </summary>
        /// <param name="sourceSprite">The original sprite to sample corner pixels from.</param>
        /// <param name="borders">Border sizes as <c>Vector4(left, bottom, right, top)</c> in pixels.</param>
        /// <param name="token">Cancellation token checked per pixel row to allow early exit.</param>
        /// <returns>A new <see cref="Texture2D"/> containing only the four corner regions.</returns>
        public Texture2D CreateMinimal9Slice(Sprite sourceSprite, Vector4 borders, CancellationToken token)
        {
            int leftBorder = (int)borders.x;
            int bottomBorder = (int)borders.y;
            int rightBorder = (int)borders.z;
            int topBorder = (int)borders.w;

            int newWidth = leftBorder + rightBorder;
            int newHeight = topBorder + bottomBorder;

            Texture2D newTexture = new Texture2D(newWidth, newHeight, sourceSprite.texture.format, false);

            Color[] sourcePixels = sourceSprite.texture.GetPixels();
            int sourceWidth = sourceSprite.texture.width;
            int sourceHeight = sourceSprite.texture.height;

            Color[] newPixels = new Color[newWidth * newHeight];

            for (int y = 0; y < topBorder; y++)
            {
                token.ThrowIfCancellationRequested();

                for (int x = 0; x < leftBorder; x++)
                {
                    int sourceIndex = x + (sourceHeight - 1 - y) * sourceWidth;
                    int destIndex = x + (newHeight - 1 - y) * newWidth;
                    newPixels[destIndex] = sourcePixels[sourceIndex];
                }
            }

            for (int y = 0; y < topBorder; y++)
            {
                token.ThrowIfCancellationRequested();

                for (int x = 0; x < rightBorder; x++)
                {
                    int sourceX = sourceWidth - rightBorder + x;
                    int sourceIndex = sourceX + (sourceHeight - 1 - y) * sourceWidth;
                    int destIndex = (leftBorder + x) + (newHeight - 1 - y) * newWidth;
                    newPixels[destIndex] = sourcePixels[sourceIndex];
                }
            }

            for (int y = 0; y < bottomBorder; y++)
            {
                token.ThrowIfCancellationRequested();

                for (int x = 0; x < leftBorder; x++)
                {
                    int sourceIndex = x + y * sourceWidth;
                    int destIndex = x + y * newWidth;
                    newPixels[destIndex] = sourcePixels[sourceIndex];
                }
            }

            for (int y = 0; y < bottomBorder; y++)
            {
                token.ThrowIfCancellationRequested();

                for (int x = 0; x < rightBorder; x++)
                {
                    int sourceX = sourceWidth - rightBorder + x;
                    int sourceIndex = sourceX + y * sourceWidth;
                    int destIndex = (leftBorder + x) + y * newWidth;
                    newPixels[destIndex] = sourcePixels[sourceIndex];
                }
            }

            newTexture.SetPixels(newPixels);
            newTexture.Apply();

            return newTexture;
        }

        /// <summary>
        /// Destroys a temporary <see cref="Texture2D"/> created during slicing, using the correct
        /// destruction method for the current runtime context.
        /// Uses <c>DestroyImmediate</c> in Editor outside of Play mode to prevent memory leaks
        /// from textures that Unity's garbage collector would not reclaim during an Editor session.
        /// </summary>
        /// <param name="texture">The texture to destroy. Skipped silently if <c>null</c>.</param>
        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
                return;

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
#else
            UnityEngine.Object.Destroy(texture);
#endif
        }
    }
}
