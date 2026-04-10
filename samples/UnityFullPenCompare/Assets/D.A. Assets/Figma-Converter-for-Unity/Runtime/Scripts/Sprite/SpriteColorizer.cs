using DA_Assets.DAI;
using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DA_Assets.FCU
{
    [Serializable]
    public class SpriteColorizer : FcuBase
    {
        /// <summary>
        /// Post-processes downloaded sprites by colorizing them to white (replacing all pixel RGB values with white
        /// while preserving alpha), so that the sprite's tint color can be fully controlled at runtime via the
        /// component's <c>color</c> property without the original image color interfering.
        /// <para>SVG imports are skipped entirely since SVG content is not rasterized at this stage.</para>
        /// <para>Filtering rules differ by UI framework: SpriteRenderer and UITK/Nova accept only single-color graphics;
        /// all other components also allow a single-gradient fill, unless <see cref="SpriteDownloadOptions.SupportedGradients"/> is set.</para>
        /// </summary>
        /// <param name="fobjects">The list of Figma objects whose sprites should be colorized.</param>
        /// <param name="token">Cancellation token for cooperative cancellation during async iteration.</param>
        public async Task ColorizeSprites(List<FObject> fobjects, CancellationToken token)
        {
            if (monoBeh.UsingSVG())
                return;

            foreach (FObject fobject in fobjects)
            {
                token.ThrowIfCancellationRequested();

                if (fobject.Data.FcuImageType != FcuImageType.Downloadable)
                    continue;

                if (monoBeh.UsingSpriteRenderer())
                {
                    // When using SpriteRenderer, only allow graphics that consist of a single solid color.
                    if (!fobject.Data.Graphic.HasSingleColor)
                        continue;
                }
                else if (monoBeh.IsUITK() || monoBeh.IsNova())
                {
                    // For UITK or Nova, only single-color graphics are allowed.
                    if (!fobject.Data.Graphic.HasSingleColor)
                        continue;

                    if (fobject.Data.Graphic.HasSingleGradient)
                        continue;
                }
                else
                {
                    // For all other image components, allow either a single color or (conditionally) a single gradient.
                    if (!fobject.Data.Graphic.HasSingleColor && !fobject.Data.Graphic.HasSingleGradient)
                        continue;

                    // Skip coloring if downloading of supported gradients is enabled.
                    if (fobject.Data.Graphic.HasSingleGradient
                        && monoBeh.Settings.ImageSpritesSettings.DownloadOptions.HasFlag(SpriteDownloadOptions.SupportedGradients))
                        continue;
                }

                if (File.Exists(fobject.Data.SpritePath.GetFullAssetPath()) == false)
                    continue;

                byte[] rawData = File.ReadAllBytes(fobject.Data.SpritePath.GetFullAssetPath());

                if (fobject.Data.SpriteSize.x < 1 || fobject.Data.SpriteSize.y < 1)
                {
                    return;
                }

                Texture2D tex = null;

                try
                {
                    tex = new Texture2D(fobject.Data.SpriteSize.x, fobject.Data.SpriteSize.y, TextureFormat.RGBA32, false);
                    tex.LoadImage(rawData);

                    tex.Colorize(Color.white);

                    byte[] bytes = Array.Empty<byte>();

                    switch (monoBeh.Settings.ImageSpritesSettings.ImageFormat)
                    {
                        case ImageFormat.PNG:
                            bytes = tex.EncodeToPNG();
                            break;
                        case ImageFormat.JPG:
                            bytes = tex.EncodeToJPG();
                            break;
                    }

                    File.WriteAllBytes(fobject.Data.SpritePath, bytes);

                    fobject.Data.ManualWhiteColor = true;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                finally
                {
                    DestroyTexture(tex);
                }

                await Task.Yield();
            }
        }

        /// <summary>
        /// Destroys a <see cref="Texture2D"/> instance safely in both Editor and Play mode.
        /// Uses <c>DestroyImmediate</c> in Editor outside of Play mode to avoid memory leaks
        /// from temporary textures that would otherwise not be garbage-collected.
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
