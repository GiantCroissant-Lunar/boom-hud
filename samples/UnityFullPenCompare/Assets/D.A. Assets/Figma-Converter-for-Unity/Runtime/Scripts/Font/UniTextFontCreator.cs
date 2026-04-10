using DA_Assets.DAI;
using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#if UNITEXT
using LightSide;
#endif

namespace DA_Assets.FCU
{
    [Serializable]
    public class UniTextFontCreator : FcuBase
    {
        /// <summary>
        /// Creates UniTextFont + UniTextFontStack assets for fonts that are present
        /// as TTF but missing as UniText assets. Called automatically during import
        /// when the text component is set to UniText.
        /// </summary>
        public async Task CreateFonts(List<FontMetadata> figmaFonts, CancellationToken token)
        {
#if UNITEXT && UNITY_EDITOR
            UnityFonts unityUniTextFonts = monoBeh.FontDownloader.FindUnityFonts(figmaFonts, monoBeh.FontLoader.UniTextFontStacks);

            if (unityUniTextFonts.Missing.Count == 0)
                return;

            List<FontStruct> generated = new List<FontStruct>();
            List<FontError> notGenerated = new List<FontError>();

            foreach (FontStruct missingFont in unityUniTextFonts.Missing)
            {
                GenerateFont(missingFont, @return =>
                {
                    generated.Add(@return.Object);

                    if (@return.Success == false)
                    {
                        notGenerated.Add(new FontError
                        {
                            FontStruct = missingFont,
                            Error = @return.Error
                        });
                    }
                });
            }

            int tempCount = -1;
            while (FcuLogger.WriteLogBeforeEqual(generated, unityUniTextFonts.Missing, FcuLocKey.log_generating_tmp_fonts, ref tempCount))
            {
                await Task.Delay(1000, token);
            }

            if (notGenerated.Count > 0)
            {
                monoBeh.FontDownloader.PrintFontNames(FcuLocKey.cant_generate_fonts, notGenerated);
            }

            UnityEditor.AssetDatabase.Refresh();
            await monoBeh.FontLoader.AddToUniTextFontsList(token);
#endif
            await Task.Yield();
        }

        /// <summary>
        /// Converts ALL TTF fonts from the TTF folder into UniTextFont + UniTextFontStack
        /// assets and fills the UniTextFontStacks list. Intended for the "Create from TTF" button.
        /// </summary>
        public async Task CreateFromTtfFolder(CancellationToken token)
        {
#if UNITEXT && UNITY_EDITOR
            List<Font> ttfFonts = monoBeh.FontLoader.TtfFonts;

            if (ttfFonts.IsEmpty())
            {
                await monoBeh.FontLoader.AddToTtfFontsList(token);
                ttfFonts = monoBeh.FontLoader.TtfFonts;
            }

            if (ttfFonts.IsEmpty())
            {
                Debug.LogWarning(FcuLocKey.log_no_ttf_fonts_to_convert.Localize());
                return;
            }

            string uniTextFontsPath = monoBeh.FontLoader.UniTextFontsPath;
            uniTextFontsPath.GetFullAssetPath().CreateFolderIfNotExists();

            int created = 0;
            int skipped = 0;

            foreach (Font ttfFont in ttfFonts)
            {
                if (ttfFont == null)
                    continue;

                string baseName = ttfFont.name;
                string fontStackPath = Path.Combine(uniTextFontsPath, $"{baseName} FontStack.asset").ToUnityPath();

                if (File.Exists(fontStackPath))
                {
                    skipped++;
                    continue;
                }

                bool success = CreateUniTextAssets(ttfFont, baseName);
                if (success)
                    created++;
            }

            Debug.Log($"UniText: created {created} font(s), skipped {skipped} existing.");

            UnityEditor.AssetDatabase.Refresh();
            await monoBeh.FontLoader.AddToUniTextFontsList(token);
#endif
            await Task.Yield();
        }

#if UNITEXT && UNITY_EDITOR
        private void GenerateFont(FontStruct fs, Return<FontStruct> @return)
        {
            string baseFontName = monoBeh.FontDownloader.GetBaseFileName(fs);

            Font ttfFont = null;

            foreach (Font item in monoBeh.FontLoader.TtfFonts)
            {
                if (baseFontName.FormatFontName() == item.name.FormatFontName())
                {
                    ttfFont = item;
                    break;
                }
            }

            if (ttfFont == null)
            {
                @return.Invoke(new DAResult<FontStruct>
                {
                    Error = new WebError(0, $"Can't find ttf font for UniText conversion"),
                    Success = false
                });

                return;
            }

            bool success = CreateUniTextAssets(ttfFont, baseFontName);

            @return.Invoke(new DAResult<FontStruct>
            {
                Object = fs,
                Success = success,
                Error = success ? default : new WebError(0, "Failed to create UniText font asset.")
            });
        }

        /// <summary>
        /// Creates a UniTextFont asset and a UniTextFontStack asset from a Unity Font (TTF).
        /// </summary>
        /// <returns>True if both assets were created successfully.</returns>
        private bool CreateUniTextAssets(Font ttfFont, string baseName)
        {
            try
            {
                string ttfAssetPath = UnityEditor.AssetDatabase.GetAssetPath(ttfFont);

                if (ttfAssetPath.IsEmpty())
                    return false;

                string fullPath = Path.GetFullPath(ttfAssetPath);

                if (!File.Exists(fullPath))
                    return false;

                byte[] fontBytes = File.ReadAllBytes(fullPath);

                UniTextFont uniTextFont = UniTextFont.CreateFontAsset(fontBytes);

                if (uniTextFont == null)
                {
                    Debug.LogError($"Failed to create UniTextFont from {baseName}");
                    return false;
                }

                uniTextFont.sourceFont = ttfFont;

                string fontAssetPath = Path.Combine(monoBeh.FontLoader.UniTextFontsPath, $"{baseName}.asset").ToUnityPath();
                fontAssetPath = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(fontAssetPath);

                UnityEditor.AssetDatabase.CreateAsset(uniTextFont, fontAssetPath);

                UniTextFontStack fontStack = UnityEngine.ScriptableObject.CreateInstance<UniTextFontStack>();
                fontStack.families = new FontFamily[] { new FontFamily { primary = uniTextFont } };

                string fontStackPath = Path.Combine(monoBeh.FontLoader.UniTextFontsPath, $"{baseName} FontStack.asset").ToUnityPath();
                fontStackPath = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(fontStackPath);

                UnityEditor.AssetDatabase.CreateAsset(fontStack, fontStackPath);

                uniTextFont.SetDirtyExt();
                UnityEditor.AssetDatabase.SaveAssets();

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
#endif
        private FontSubset ParseSubsetFromName(string fontName)
        {
            foreach (FontSubset subset in Enum.GetValues(typeof(FontSubset)))
            {
                if (subset == FontSubset.Latin)
                    continue;

                if (fontName.IndexOf(subset.ToString(), StringComparison.OrdinalIgnoreCase) >= 0)
                    return subset;
            }

            return FontSubset.Latin;
        }
    }
}
