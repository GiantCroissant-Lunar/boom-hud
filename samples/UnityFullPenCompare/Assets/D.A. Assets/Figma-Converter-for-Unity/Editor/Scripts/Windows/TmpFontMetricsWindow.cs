#if TextMeshPro
namespace DA_Assets.FCU
{
    /// <summary>
    /// TMP-specific font metrics window. Inherits all UI from
    /// <see cref="FontMetricsWindowBase{TWindow}"/> and only provides
    /// TMP-specific overrides.
    /// </summary>
    internal class TmpFontMetricsWindow : FontMetricsWindowBase<TmpFontMetricsWindow>
    {
        protected override string WindowTitle => "TMP Font Metrics Review";

        protected override string[] HelpSteps => new[]
        {
            "FCU detected TMP font assets used by this import whose metrics do not match the current cap-height-to-baseline trim algorithm.",
            "Each listed font failed the actual metric check against the current algorithm.",
            "Apply to the selected fonts if you want FCU to rewrite their Face Info values.",
            "Continue without changes if you want to keep the current font assets untouched."
        };

        protected override string SummaryNoun => "TMP font asset(s)";

        protected override void ApplyMetrics(FontMetricsIssue issue)
        {
            if (issue is TmpFontMetricsIssue tmpIssue && tmpIssue.FontAsset != null)
            {
                FontMetricsAdjuster.ApplyAndSaveTmpFontMetrics(tmpIssue.FontAsset);
            }
        }
    }
}
#endif