using System.Collections.Generic;
using UnityEngine;

#if TextMeshPro
using TMPro;
#endif

namespace DA_Assets.FCU
{
    public enum FontMetricsWindowAction
    {
        None = 0,
        ContinueImport = 1,
        StopImport = 2
    }

    public struct FontMetricsWindowData
    {
        public List<SelectableObject<FontMetricsIssue>> Fonts { get; set; }
    }

    public struct FontMetricsWindowResult
    {
        public FontMetricsWindowAction Action { get; set; }
    }

    /// <summary>
    /// Base issue class — framework-agnostic.
    /// </summary>
    public class FontMetricsIssue
    {
        public Object FontAsset { get; set; }
        public string AssetPath { get; set; }
        public string Details { get; set; }
    }

#if TextMeshPro
    public sealed class TmpFontMetricsIssue : FontMetricsIssue
    {
        public new TMP_FontAsset FontAsset
        {
            get => base.FontAsset as TMP_FontAsset;
            set => base.FontAsset = value;
        }
    }
#endif

    public sealed class UitkFontMetricsIssue : FontMetricsIssue
    {
        public new UnityEngine.TextCore.Text.FontAsset FontAsset
        {
            get => base.FontAsset as UnityEngine.TextCore.Text.FontAsset;
            set => base.FontAsset = value;
        }
    }
}
