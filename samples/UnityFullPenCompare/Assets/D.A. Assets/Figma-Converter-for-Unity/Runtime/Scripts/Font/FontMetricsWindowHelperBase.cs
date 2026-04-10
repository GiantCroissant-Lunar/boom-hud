#if UNITY_EDITOR

using DA_Assets.FCU.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DA_Assets.FCU
{
    /// <summary>
    /// Shared logic for showing font metrics windows (TMP / UITK).
    /// Handles the wait loop, play-mode skip, and delegate null-check.
    /// </summary>
    public static class FontMetricsWindowHelperBase
    {
        public static async Task<FontMetricsWindowResult> ShowFontMetricsWindow(
            FigmaConverterUnity monoBeh,
            FontMetricsWindowData data,
            Action<FontMetricsWindowData, Action<FontMetricsWindowResult>> showDelegate,
            CancellationToken token)
        {
            var result = new FontMetricsWindowResult
            {
                Action = FontMetricsWindowAction.ContinueImport
            };

            if (monoBeh.IsPlaying())
            {
                return result;
            }

            if (showDelegate == null)
            {
                return result;
            }

            if (data.Fonts == null || data.Fonts.Count == 0)
            {
                return result;
            }

            await monoBeh.AssetTools.ReselectFcu(token);

            showDelegate(data, output => result = output);

            while (result.Action == FontMetricsWindowAction.None)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(1000, token);
            }

            return result;
        }
    }
}
#endif
