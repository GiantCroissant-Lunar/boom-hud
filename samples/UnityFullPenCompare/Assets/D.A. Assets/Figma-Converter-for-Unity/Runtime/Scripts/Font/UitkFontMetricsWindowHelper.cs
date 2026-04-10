#if UNITY_EDITOR

using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TextCoreFontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace DA_Assets.FCU
{
    /// <summary>
    /// UITK-specific helper: collects issues for TextCore.Text.FontAsset and delegates
    /// the common show-window logic to <see cref="FontMetricsWindowHelperBase"/>.
    /// </summary>
    public static class UitkFontMetricsWindowHelper
    {
        public static async Task<FontMetricsWindowResult> ShowUitkFontMetricsWindow(
            FigmaConverterUnity monoBeh,
            List<FObject> fobjects,
            CancellationToken token)
        {
            if (!monoBeh.UsingUI_Toolkit_Text())
            {
                return new FontMetricsWindowResult { Action = FontMetricsWindowAction.ContinueImport };
            }

            List<SelectableObject<FontMetricsIssue>> issues = CollectIssues(monoBeh, fobjects);

            var result = await FontMetricsWindowHelperBase.ShowFontMetricsWindow(
                monoBeh,
                new FontMetricsWindowData { Fonts = issues },
                monoBeh.EditorDelegateHolder.ShowUitkFontMetricsWindow,
                token);

            if (result.Action == FontMetricsWindowAction.ContinueImport)
            {
                await monoBeh.FontLoader.AddToUitkFontAssetsList(token);
            }

            return result;
        }

        private static List<SelectableObject<FontMetricsIssue>> CollectIssues(
            FigmaConverterUnity monoBeh,
            List<FObject> fobjects)
        {
            return monoBeh.FontDownloader
                .GetProjectUitkFonts(fobjects)
                .Where(font => font != null)
                .Select(CreateIssue)
                .Where(x => x != null)
                .ToList();
        }

        private static SelectableObject<FontMetricsIssue> CreateIssue(TextCoreFontAsset fontAsset)
        {
            if (FontMetricsAdjuster.IsUitkFontMetricsAdjusted(fontAsset, out string details))
            {
                return null;
            }

            return new SelectableObject<FontMetricsIssue>
            {
                Selected = true,
                Object = new UitkFontMetricsIssue
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
