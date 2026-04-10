using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using TextCoreFontAsset = UnityEngine.TextCore.Text.FontAsset;

#if TextMeshPro
using TMPro;
#endif

#pragma warning disable CS0649

namespace DA_Assets.FCU
{
    [Serializable]
    public class FontDownloader : FcuBase
    {
        public CancellationTokenSource DownloadFontsCts;
        public CancellationTokenSource AddTtfFontsCts;
        public CancellationTokenSource AddTmpFontsCts;
        public CancellationTokenSource AddUitkFontAssetsCts;

        public override void Init(FigmaConverterUnity monoBeh)
        {
            base.Init(monoBeh);

            this.TmpDownloader.Init(monoBeh);
            this.TtfDownloader.Init(monoBeh);
            this.GFontsApi.Init(monoBeh);
            this.UitkFontAssetCreator.Init(monoBeh);
            this.UniTextFontCreator.Init(monoBeh);
        }

        public async Task DownloadFonts(List<FObject> fobjects, CancellationToken token)
        {
            if (FcuConfig.GoogleFontsApiKey.IsEmpty())
            {
                Debug.Log(FcuLocKey.log_no_google_fonts_api_key.Localize());
                return;
            }

            // Auto-detect FontSubsets from actual text content in the Figma project.
            // DetectFromFObjects runs Task.Run + Parallel.ForEach internally — no main thread blocking.
            // Detected subsets are merged (|=) into the user's existing selection — never reset.
            HashSet<FontSubset> detectedSubsets = await FontSubsetDetector.DetectFromFObjects(fobjects);
            foreach (FontSubset s in detectedSubsets)
                this.GFontsApi.FontSubsets |= s;
            Debug.Log($"[FCU] Auto-detected subsets: {string.Join(", ", detectedSubsets)}");

            List<FontMetadata> figmaFonts = GetFigmaProjectFonts(fobjects);

            monoBeh.FontLoader.RemoveDubAndMissingFonts();

            await monoBeh.FontLoader.AddToTmpMeshFontsList(token);
            await monoBeh.FontLoader.AddToTtfFontsList(token);
            await monoBeh.FontLoader.AddToUitkFontAssetsList(token);
            await monoBeh.FontLoader.AddToUniTextFontsList(token);

            await this.TtfDownloader.Download(figmaFonts, token);

            if (monoBeh.UsingTextMesh())
            {
                await this.TmpDownloader.CreateFonts(figmaFonts, token);
            }

            if (monoBeh.UsingUI_Toolkit_Text())
            {
                await this.UitkFontAssetCreator.CreateFonts(figmaFonts, token);
            }

#if UNITEXT
            if (monoBeh.UsingUniText())
            {
                await this.UniTextFontCreator.CreateFonts(figmaFonts, token);
            }
#endif

            monoBeh.FontLoader.RemoveDubAndMissingFonts();
        }

        public UnityFonts FindUnityFonts<T>(List<FontMetadata> figmaFonts, List<T> fontArray) where T : UnityEngine.Object
        {
            UnityFonts uf = new UnityFonts
            {
                Existing = new List<FontStruct>(),
                Missing = new List<FontStruct>(),
            };

            foreach (FontMetadata figmaFont in figmaFonts)
            {
                foreach (FontSubset fontSubset in monoBeh.FontDownloader.GFontsApi.SelectedFontAssets)
                {
                    T _fontItem = null;

                    foreach (T fontItem in fontArray)
                    {
                        if (fontSubset == FontSubset.Latin)
                        {
                            if (figmaFont.FontNameToString(true, true, null, true) == fontItem.name.FormatFontName())
                            {
                                _fontItem = fontItem;
                                break;
                            }
                        }
                        else
                        {
                            if (figmaFont.FontNameToString(true, true, fontSubset, true) == fontItem.name.FormatFontName())
                            {
                                _fontItem = fontItem;
                                break;
                            }
                        }
                    }

                    if (_fontItem != null)
                    {
                        uf.Existing.Add(new FontStruct
                        {
                            FontMetadata = figmaFont,
                            FontSubset = fontSubset,
                            Font = _fontItem
                        });
                    }
                    else
                    {
                        uf.Missing.Add(new FontStruct
                        {
                            FontMetadata = figmaFont,
                            FontSubset = fontSubset,
                            Font = _fontItem
                        });
                    }
                }
            }

            return uf;
        }

#if TextMeshPro
        public List<TMP_FontAsset> GetProjectTmpFonts(List<FObject> fobjects)
        {
            List<FontMetadata> figmaFonts = GetFigmaProjectFonts(fobjects);

            return FindUnityFonts(figmaFonts, monoBeh.FontLoader.TmpFonts)
                .Existing
                .Select(x => x.Font as TMP_FontAsset)
                .Where(x => x != null)
                .Distinct()
                .ToList();
        }
#endif

        public List<TextCoreFontAsset> GetProjectUitkFonts(List<FObject> fobjects)
        {
            List<FontMetadata> figmaFonts = GetFigmaProjectFonts(fobjects);

            return FindUnityFonts(figmaFonts, monoBeh.FontLoader.UitkFontAssets)
                .Existing
                .Select(x => x.Font as TextCoreFontAsset)
                .Where(x => x != null)
                .Distinct()
                .ToList();
        }

        private List<FontMetadata> GetFigmaProjectFonts(List<FObject> fobjects)
        {
            HashSet<FontMetadata> fonts = new HashSet<FontMetadata>();

            foreach (FObject fobject in fobjects)
            {
                if (fobject.ContainsTag(FcuTag.Text) == false)
                    continue;

                FontMetadata fm = fobject.GetFontMetadata();
                fonts.Add(fm);
            }

            return fonts.ToList();
        }

        public async Task DownloadAllProjectFonts(CancellationToken token)
        {
            FObject virtualPage = new FObject
            {
                Id = FcuConfig.PARENT_ID,
                Children = monoBeh.CurrentProject.FigmaProject.Document.Children,
                Data = new SyncData
                {
                    Names = new FNames
                    {
                        ObjectName = FcuTag.Page.ToString(),
                    },
                    Tags = new List<FcuTag>
                    {
                        FcuTag.Page
                    }
                },
            };

            await monoBeh.TagSetter.SetTags(virtualPage, token);
            await monoBeh.ProjectImporter.Importer.ConvertTreeToListAsync(virtualPage, monoBeh.CurrentProject.CurrentPage, token);
            await monoBeh.FontDownloader.DownloadFonts(monoBeh.CurrentProject.CurrentPage, token);
        }

        public string GetBaseFileName(FontStruct fs)
        {
            string baseFontName;

            if (fs.FontSubset == FontSubset.Latin)
            {
                baseFontName = fs.FontMetadata.FontNameToString(true, true, null, false);
            }
            else
            {
                baseFontName = fs.FontMetadata.FontNameToString(true, true, fs.FontSubset, false);
            }

            return baseFontName;
        }

        internal void PrintFontNames(FcuLocKey locKey, List<FontError> fonts)
        {
            List<string> fontNames = new List<string>();

            foreach (var item in fonts)
            {
                string fontName;

                if (item.FontStruct.FontSubset == FontSubset.Latin)
                {
                    fontName = item.FontStruct.FontMetadata.FontNameToString(true, true, null, false);
                }
                else
                {
                    fontName = item.FontStruct.FontMetadata.FontNameToString(true, true, item.FontStruct.FontSubset, false);
                }

                string nameWithReason;

                if (item.Error.err.IsEmpty() == false)
                {
                    nameWithReason = $"{fontName} - {item.Error.err}";
                }
                else if (item.Error.exception.IsDefault() == false)
                {
                    nameWithReason = $"{fontName} - {item.Error.exception}";
                }
                else
                {
                    nameWithReason = fontName;
                }

                fontNames.Add(nameWithReason);
            }

            string joined = $"\n{string.Join("\n", fontNames)}";
            Debug.Log(locKey.Localize(fontNames.Count, joined));
        }


        [SerializeField] public TmpDownloader TmpDownloader = new TmpDownloader();
        [SerializeField] public TtfDownloader TtfDownloader = new TtfDownloader();
        [SerializeField] public DaGoogleFontsApi GFontsApi = new DaGoogleFontsApi();
        [SerializeField] public UitkFontAssetCreator UitkFontAssetCreator = new UitkFontAssetCreator();
        [SerializeField] public UniTextFontCreator UniTextFontCreator = new UniTextFontCreator();
    }

    [Serializable]
    public class UitkFontAssetCreator : FcuBase
    {
        public async Task CreateFonts(List<FontMetadata> figmaFonts, CancellationToken token)
        {
#if UNITY_EDITOR
            UnityFonts unityUitkFonts = monoBeh.FontDownloader.FindUnityFonts(figmaFonts, monoBeh.FontLoader.UitkFontAssets);

            if (unityUitkFonts.Missing.Count == 0)
                return;

            List<FontStruct> generated = new List<FontStruct>();
            List<FontError> notGenerated = new List<FontError>();

            foreach (FontStruct missingFont in unityUitkFonts.Missing)
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
            while (FcuLogger.WriteLogBeforeEqual(generated, unityUitkFonts.Missing, FcuLocKey.log_generating_tmp_fonts, ref tempCount))
            {
                await Task.Delay(1000, token);
            }

            if (notGenerated.Count > 0)
            {
                monoBeh.FontDownloader.PrintFontNames(FcuLocKey.cant_generate_fonts, notGenerated);
            }

            UnityEditor.AssetDatabase.Refresh();
            await monoBeh.FontLoader.AddToUitkFontAssetsList(token);
#endif
            await Task.Yield();
        }

        public async Task CreateFromTtfFolder(CancellationToken token)
        {
#if UNITY_EDITOR
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

            string uitkFontAssetsPath = monoBeh.FontLoader.UitkFontAssetsPath;
            uitkFontAssetsPath.GetFullAssetPath().CreateFolderIfNotExists();

            int created = 0;
            int skipped = 0;

            foreach (Font ttfFont in ttfFonts)
            {
                if (ttfFont == null)
                    continue;

                string baseName = ttfFont.name;
                string fontAssetPath = Path.Combine(uitkFontAssetsPath, $"{baseName}.asset").ToUnityPath();

                if (File.Exists(fontAssetPath.GetFullAssetPath()))
                {
                    skipped++;
                    continue;
                }

                if (CreateUitkFontAsset(ttfFont, baseName))
                {
                    created++;
                }
            }

            Debug.Log($"UITK FontAsset: created {created} font(s), skipped {skipped} existing.");

            UnityEditor.AssetDatabase.Refresh();
            await monoBeh.FontLoader.AddToUitkFontAssetsList(token);
#endif
            await Task.Yield();
        }

#if UNITY_EDITOR
        private void GenerateFont(FontStruct fs, Return<FontStruct> @return)
        {
            string baseFontName = monoBeh.FontDownloader.GetBaseFileName(fs);

            Font ttfFont = monoBeh.FontLoader.TtfFonts
                .FirstOrDefault(x => x != null && baseFontName.FormatFontName() == x.name.FormatFontName());

            if (ttfFont == null)
            {
                @return.Invoke(new DAResult<FontStruct>
                {
                    Error = new WebError(0, "Can't find ttf font for UITK FontAsset conversion"),
                    Success = false
                });

                return;
            }

            bool success = CreateUitkFontAsset(ttfFont, baseFontName);

            @return.Invoke(new DAResult<FontStruct>
            {
                Object = fs,
                Success = success,
                Error = success ? default : new WebError(0, "Failed to create UITK FontAsset.")
            });
        }

        private bool CreateUitkFontAsset(Font ttfFont, string baseName)
        {
            try
            {
                string assetPath = Path.Combine(monoBeh.FontLoader.UitkFontAssetsPath, $"{baseName}.asset").ToUnityPath();

                TextCoreFontAsset existingAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextCoreFontAsset>(assetPath);
                if (existingAsset != null)
                {
                    FontMetricsAdjuster.ApplyAndSaveUitkFontMetrics(existingAsset);
                    return true;
                }

                TextCoreFontAsset fontAsset = CreateTextCoreFontAsset(ttfFont);
                if (fontAsset == null)
                {
                    Debug.LogError($"Failed to create UITK FontAsset from {baseName}");
                    return false;
                }

                assetPath = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(assetPath);
                UnityEditor.AssetDatabase.CreateAsset(fontAsset, assetPath);

                // Save atlas textures as sub-assets so m_AtlasTextures survives serialization.
                if (fontAsset.atlasTextures != null)
                {
                    foreach (var tex in fontAsset.atlasTextures)
                    {
                        if (tex != null && !UnityEditor.AssetDatabase.IsSubAsset(tex))
                        {
                            tex.name = $"{baseName} Atlas";
                            UnityEditor.AssetDatabase.AddObjectToAsset(tex, fontAsset);
                        }
                    }
                }

                // Save material as sub-asset.
                if (fontAsset.material != null && !UnityEditor.AssetDatabase.IsSubAsset(fontAsset.material))
                {
                    fontAsset.material.name = $"{baseName} Material";
                    UnityEditor.AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }

                FontMetricsAdjuster.ApplyAndSaveUitkFontMetrics(fontAsset);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        private static TextCoreFontAsset CreateTextCoreFontAsset(Font sourceFont)
        {
            if (sourceFont == null)
                return null;

            MethodInfo[] factoryMethods = typeof(TextCoreFontAsset)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(x => x.Name == "CreateFontAsset" && typeof(TextCoreFontAsset).IsAssignableFrom(x.ReturnType))
                .ToArray();

            MethodInfo singleArg = factoryMethods.FirstOrDefault(x =>
            {
                ParameterInfo[] parameters = x.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(Font);
            });

            if (singleArg != null)
            {
                return singleArg.Invoke(null, new object[] { sourceFont }) as TextCoreFontAsset;
            }

            MethodInfo configured = factoryMethods
                .Where(x =>
                {
                    ParameterInfo[] parameters = x.GetParameters();
                    return parameters.Length > 0 && parameters[0].ParameterType == typeof(Font);
                })
                .OrderByDescending(x => x.GetParameters().Length)
                .FirstOrDefault();

            if (configured == null)
                return null;

            object[] args = BuildCreateFontAssetArgs(configured, sourceFont);
            return configured.Invoke(null, args) as TextCoreFontAsset;
        }

        private static object[] BuildCreateFontAssetArgs(MethodInfo factoryMethod, Font sourceFont)
        {
            ParameterInfo[] parameters = factoryMethod.GetParameters();
            object[] args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];

                if (parameter.ParameterType == typeof(Font))
                {
                    args[i] = sourceFont;
                    continue;
                }

                if (parameter.ParameterType == typeof(int))
                {
                    string parameterName = parameter.Name ?? string.Empty;

                    if (parameterName.IndexOf("sampling", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        args[i] = 90;
                    }
                    else if (parameterName.IndexOf("padding", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        args[i] = 9;
                    }
                    else if (parameterName.IndexOf("width", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             parameterName.IndexOf("height", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        args[i] = 1024;
                    }
                    else
                    {
                        args[i] = 0;
                    }

                    continue;
                }

                if (parameter.ParameterType == typeof(bool))
                {
                    args[i] = true;
                    continue;
                }

                if (parameter.ParameterType.IsEnum)
                {
                    args[i] = ResolveEnumDefault(parameter.ParameterType);
                    continue;
                }

                args[i] = parameter.HasDefaultValue ? parameter.DefaultValue : null;
            }

            return args;
        }

        private static object ResolveEnumDefault(Type enumType)
        {
            string[] names = Enum.GetNames(enumType);

            if (enumType.Name == "AtlasPopulationMode")
            {
                string dynamicName = names.FirstOrDefault(x => string.Equals(x, "Dynamic", StringComparison.OrdinalIgnoreCase));
                if (dynamicName != null)
                {
                    return Enum.Parse(enumType, dynamicName);
                }
            }

            string sdfName = names.FirstOrDefault(x => x.IndexOf("SDFAA", StringComparison.OrdinalIgnoreCase) >= 0);
            if (sdfName != null)
            {
                return Enum.Parse(enumType, sdfName);
            }

            return Enum.GetValues(enumType).GetValue(0);
        }
#endif
    }
}
