#if UNITY_EDITOR && TextMeshPro

using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;

namespace DA_Assets.FCU
{
    /// <summary>
    /// TMP-specific helper: collects issues for TMP_FontAsset and delegates
    /// the common show-window logic to <see cref="FontMetricsWindowHelperBase"/>.
    /// </summary>
    public static class TmpFontMetricsWindowHelper
    {
        public static async Task<FontMetricsWindowResult> ShowTmpFontMetricsWindow(
            FigmaConverterUnity monoBeh,
            List<FObject> fobjects,
            CancellationToken token)
        {
            if (!monoBeh.UsingTextMesh())
            {
                return new FontMetricsWindowResult { Action = FontMetricsWindowAction.ContinueImport };
            }

            List<SelectableObject<FontMetricsIssue>> issues = CollectIssues(monoBeh, fobjects);

            var result = await FontMetricsWindowHelperBase.ShowFontMetricsWindow(
                monoBeh,
                new FontMetricsWindowData { Fonts = issues },
                monoBeh.EditorDelegateHolder.ShowTmpFontMetricsWindow,
                token);

            if (result.Action == FontMetricsWindowAction.ContinueImport)
            {
                await monoBeh.FontLoader.AddToTmpMeshFontsList(token);
            }

            return result;
        }

        private static List<SelectableObject<FontMetricsIssue>> CollectIssues(
            FigmaConverterUnity monoBeh,
            List<FObject> fobjects)
        {
            return monoBeh.FontDownloader
                .GetProjectTmpFonts(fobjects)
                .Where(font => font != null)
                .Select(CreateIssue)
                .Where(x => x != null)
                .ToList();
        }

        private static SelectableObject<FontMetricsIssue> CreateIssue(TMP_FontAsset fontAsset)
        {
            if (FontMetricsAdjuster.IsTmpFontMetricsAdjusted(fontAsset, out string details))
            {
                return null;
            }

            return new SelectableObject<FontMetricsIssue>
            {
                Selected = true,
                Object = new TmpFontMetricsIssue
                {
                    FontAsset = fontAsset,
                    AssetPath = UnityEditor.AssetDatabase.GetAssetPath(fontAsset),
                    Details = details
                }
            };
        }
    }
}
#endif