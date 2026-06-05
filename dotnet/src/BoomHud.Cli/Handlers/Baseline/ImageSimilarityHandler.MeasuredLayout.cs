using System.Text.Json;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Snapshots;
using BoomHud.Gen.Pencil;
using BoomHud.Generators.VisualIR;

namespace BoomHud.Cli.Handlers.Baseline;

public static partial class ImageSimilarityHandler
{
    internal static MeasuredLayoutReport BuildMeasuredLayoutReport(VisualDocument visualDocument, ActualLayoutSnapshot actualLayout)
    {
        var componentMap = visualDocument.Components.ToDictionary(component => component.Id, StringComparer.Ordinal);
        var normalizedActualLayout = actualLayout with
        {
            Root = NormalizeActualLayoutTree(actualLayout.Root)
        };
        var expectedRoot = ExpandEffectiveVisualNode(ResolveExpectedLayoutRoot(visualDocument, normalizedActualLayout), componentMap);
        var comparisons = new List<MeasuredLayoutComparison>();
        var issues = new List<MeasuredLayoutIssue>();

        CompareLayoutNode(expectedRoot, normalizedActualLayout.Root, null, null, "root", comparisons, issues, childIndex: null);

        return new MeasuredLayoutReport
        {
            Version = "1.0",
            DocumentName = visualDocument.DocumentName,
            BackendFamily = normalizedActualLayout.BackendFamily,
            CaptureId = normalizedActualLayout.CaptureId,
            TargetName = normalizedActualLayout.TargetName,
            ExpectedRootStableId = expectedRoot.StableId,
            ActualRootName = normalizedActualLayout.Root.Name,
            Comparisons = comparisons,
            Issues = issues,
            SemanticClassSummaries = BuildSemanticClassSummaries(comparisons, issues),
            SourceSemanticSummaries = BuildSourceSemanticSummaries(comparisons, issues)
        };
    }

    private static ActualLayoutNode NormalizeActualLayoutTree(ActualLayoutNode node)
    {
        var normalizedChildren = node.Children
            .Where(static child => !IsSyntheticOverlayChrome(child))
            .Select(NormalizeActualLayoutTree)
            .ToList();

        return node with
        {
            Children = normalizedChildren
        };
    }

    private static bool IsSyntheticOverlayChrome(ActualLayoutNode node)
        => string.Equals(node.Name, "__Border", StringComparison.Ordinal);

    private static VisualNode ResolveExpectedLayoutRoot(VisualDocument visualDocument, ActualLayoutSnapshot actualLayout)
    {
        var candidates = FlattenVisualNodes(visualDocument.Root).ToList();
        var targetName = actualLayout.TargetName;
        var actualRootName = actualLayout.Root.Name;

        static string NormalizeName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            foreach (var suffix in new[] { "Root", "View", "Panel", "Container", "Host" })
            {
                if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && trimmed.Length > suffix.Length)
                {
                    trimmed = trimmed[..^suffix.Length];
                    break;
                }
            }

            return new string(trimmed.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        }

        var normalizedTarget = NormalizeName(targetName);
        var normalizedActualRoot = NormalizeName(actualRootName);
        var normalizedDocument = NormalizeName(visualDocument.DocumentName);

        var scored = candidates
            .Select(node => new
            {
                Node = node,
                Score = Score(node)
            })
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Node.StableId, StringComparer.Ordinal)
            .ToList();

        return scored.FirstOrDefault(entry => entry.Score > 0)?.Node ?? visualDocument.Root;

        int Score(VisualNode node)
        {
            var score = 0;
            var normalizedSourceId = NormalizeName(node.SourceId);
            var normalizedSourceNodeId = NormalizeName(node.SourceNodeId);
            var normalizedStableId = NormalizeName(node.StableId);

            score += MatchScore(normalizedTarget, normalizedSourceId, 120);
            score += MatchScore(normalizedTarget, normalizedSourceNodeId, 100);
            score += MatchScore(normalizedTarget, normalizedStableId, 80);
            score += MatchScore(normalizedActualRoot, normalizedSourceId, 100);
            score += MatchScore(normalizedActualRoot, normalizedSourceNodeId, 80);
            score += MatchScore(normalizedDocument, normalizedSourceId, 40);
            score += MatchScore(normalizedDocument, normalizedSourceNodeId, 30);
            if (string.Equals(node.StableId, "root", StringComparison.Ordinal))
            {
                score += 10;
            }

            return score;
        }

        static int MatchScore(string left, string right, int weight)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return 0;
            }

            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                return weight;
            }

            return right.Contains(left, StringComparison.Ordinal) || left.Contains(right, StringComparison.Ordinal)
                ? Math.Max(1, weight / 2)
                : 0;
        }
    }

    private static IEnumerable<VisualNode> FlattenVisualNodes(VisualNode root)
    {
        yield return root;
        foreach (var child in root.Children)
        {
            foreach (var descendant in FlattenVisualNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static VisualNode ExpandEffectiveVisualNode(
        VisualNode node,
        IReadOnlyDictionary<string, VisualComponentDefinition> componentMap)
    {
        if (!string.IsNullOrWhiteSpace(node.ComponentRefId)
            && componentMap.TryGetValue(node.ComponentRefId, out var componentDefinition))
        {
            return ExpandVisualNodeFromTemplate(
                node,
                componentDefinition.Root,
                componentMap,
                node.StableId);
        }

        var expandedChildren = node.Children
            .Select(child => ExpandEffectiveVisualNode(child, componentMap))
            .ToList();

        return node with
        {
            Children = expandedChildren
        };
    }

    private static VisualNode ExpandVisualNodeFromTemplate(
        VisualNode instanceNode,
        VisualNode templateNode,
        IReadOnlyDictionary<string, VisualComponentDefinition> componentMap,
        string stableId)
    {
        var expandedChildren = templateNode.Children
            .Select((child, index) => ExpandTemplateChild(child, componentMap, $"{stableId}/{index}"))
            .ToList();

        return templateNode with
        {
            StableId = stableId,
            SourceId = instanceNode.SourceId ?? templateNode.SourceId,
            SourceNodeId = instanceNode.SourceNodeId ?? templateNode.SourceNodeId,
            ComponentRefId = instanceNode.ComponentRefId,
            SemanticClass = instanceNode.SemanticClass ?? templateNode.SemanticClass,
            MetricProfileId = instanceNode.MetricProfileId ?? templateNode.MetricProfileId,
            PropertyOverrides = instanceNode.PropertyOverrides,
            Children = expandedChildren
        };
    }

    private static VisualNode ExpandTemplateChild(
        VisualNode templateNode,
        IReadOnlyDictionary<string, VisualComponentDefinition> componentMap,
        string stableId)
    {
        if (!string.IsNullOrWhiteSpace(templateNode.ComponentRefId)
            && componentMap.TryGetValue(templateNode.ComponentRefId, out var nestedDefinition))
        {
            return ExpandVisualNodeFromTemplate(templateNode, nestedDefinition.Root, componentMap, stableId);
        }

        var expandedChildren = templateNode.Children
            .Select((child, index) => ExpandTemplateChild(child, componentMap, $"{stableId}/{index}"))
            .ToList();

        return templateNode with
        {
            StableId = stableId,
            Children = expandedChildren
        };
    }

    private static void CompareLayoutNode(
        VisualNode expected,
        ActualLayoutNode actual,
        VisualNode? expectedParent,
        ActualLayoutNode? actualParent,
        string localPath,
        List<MeasuredLayoutComparison> comparisons,
        List<MeasuredLayoutIssue> issues,
        int? childIndex)
    {
        var expectedStartInsetX = ResolveExpectedStartInsetX(expected, expectedParent, actualParent, childIndex);
        var expectedStartInsetY = ResolveExpectedStartInsetY(expected, expectedParent, actualParent, childIndex);
        var expectedAvailableWidth = ResolveExpectedAvailableWidth(expected, expectedParent, actualParent, childIndex);
        var expectedFontSize = expected.Icon?.ResolvedFontSize ?? expected.Typography?.ResolvedFontSize;
        var expectedWrapText = expected.Typography?.WrapText ?? false;
        var expectedClipContent = expected.Box.ClipContent
            || expected.EdgeContract.OverflowX == OverflowBehavior.Clip
            || expected.EdgeContract.OverflowY == OverflowBehavior.Clip;

        var comparison = new MeasuredLayoutComparison
        {
            LocalPath = localPath,
            ExpectedStableId = expected.StableId,
            ExpectedSourceId = expected.SourceId,
            ExpectedSemanticClass = expected.SemanticClass,
            ExpectedSourceSemanticRole = expected.SourceSemanticRole,
            ExpectedSourceAssetRealization = expected.SourceAssetRealization,
            ActualName = actual.Name,
            ActualNodeType = actual.NodeType,
            ExpectedWidthSizing = expected.EdgeContract.WidthSizing,
            ExpectedHeightSizing = expected.EdgeContract.HeightSizing,
            ExpectedParticipation = expected.EdgeContract.Participation,
            ActualX = actual.X,
            ActualY = actual.Y,
            ActualWidth = actual.Width,
            ActualHeight = actual.Height,
            ActualScaleX = actual.ScaleX,
            ActualScaleY = actual.ScaleY,
            ExpectedStartInsetX = expectedStartInsetX,
            ExpectedStartInsetY = expectedStartInsetY,
            ExpectedAvailableWidth = expectedAvailableWidth,
            ActualPreferredWidth = actual.PreferredWidth,
            ActualPreferredHeight = actual.PreferredHeight,
            ExpectedFontSize = expectedFontSize,
            ActualFontSize = actual.FontSize,
            ExpectedWrapText = expectedWrapText,
            ActualWrapText = actual.WrapText,
            ExpectedClipContent = expectedClipContent,
            ActualClipContent = actual.ClipContent,
            ExpectedChildCount = expected.Children.Count,
            ActualChildCount = actual.Children.Count
        };

        comparisons.Add(comparison);
        issues.AddRange(BuildMeasuredIssues(comparison));

        if (expected.Children.Count != actual.Children.Count)
        {
            issues.Add(new MeasuredLayoutIssue
            {
                Category = "child-structure-mismatch",
                Severity = "warning",
                LocalPath = localPath,
                ExpectedSemanticClass = expected.SemanticClass,
                ExpectedSourceSemanticRole = expected.SourceSemanticRole,
                ExpectedSourceAssetRealization = expected.SourceAssetRealization,
                Summary = $"Expected {expected.Children.Count} child nodes but realized {actual.Children.Count}.",
                SuggestedAction = "Inspect synthesis decomposition or backend child mounting for this subtree before tuning smaller metrics."
            });
        }

        var sharedChildCount = Math.Min(expected.Children.Count, actual.Children.Count);
        for (var index = 0; index < sharedChildCount; index++)
        {
            CompareLayoutNode(
                expected.Children[index],
                actual.Children[index],
                expected,
                actual,
                $"{localPath}/{index}",
                comparisons,
                issues,
                childIndex: index);
        }
    }

    private static MeasuredLayoutSemanticClassSummary[] BuildSemanticClassSummaries(
        IReadOnlyList<MeasuredLayoutComparison> comparisons,
        IReadOnlyList<MeasuredLayoutIssue> issues)
    {
        var issueLookup = issues
            .GroupBy(static issue => issue.LocalPath, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.ToList(),
                StringComparer.Ordinal);

        return comparisons
            .GroupBy(
                static comparison => string.IsNullOrWhiteSpace(comparison.ExpectedSemanticClass) ? "unspecified" : comparison.ExpectedSemanticClass!,
                StringComparer.Ordinal)
            .Select(group =>
            {
                var groupIssues = new List<MeasuredLayoutIssue>();
                foreach (var comparison in group)
                {
                    if (issueLookup.TryGetValue(comparison.LocalPath, out var matchedIssues))
                    {
                        groupIssues.AddRange(matchedIssues);
                    }
                }

                return new MeasuredLayoutSemanticClassSummary
                {
                    SemanticClass = group.Key,
                    ComparisonCount = group.Count(),
                    IssueCount = groupIssues.Count,
                    IssueCategories = groupIssues
                        .Select(static issue => issue.Category)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(static category => category, StringComparer.Ordinal)
                        .ToArray()
                };
            })
            .OrderBy(static summary => summary.SemanticClass, StringComparer.Ordinal)
            .ToArray();
    }

    private static MeasuredLayoutSourceSemanticSummary[] BuildSourceSemanticSummaries(
        IReadOnlyList<MeasuredLayoutComparison> comparisons,
        IReadOnlyList<MeasuredLayoutIssue> issues)
    {
        var issueLookup = issues
            .GroupBy(static issue => issue.LocalPath, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.ToList(),
                StringComparer.Ordinal);

        return comparisons
            .GroupBy(
                static comparison => (
                    Role: string.IsNullOrWhiteSpace(comparison.ExpectedSourceSemanticRole) ? "unspecified" : comparison.ExpectedSourceSemanticRole!,
                    Asset: comparison.ExpectedSourceAssetRealization),
                EqualityComparer<(string Role, string? Asset)>.Default)
            .Select(group =>
            {
                var groupIssues = new List<MeasuredLayoutIssue>();
                foreach (var comparison in group)
                {
                    if (issueLookup.TryGetValue(comparison.LocalPath, out var matchedIssues))
                    {
                        groupIssues.AddRange(matchedIssues);
                    }
                }

                return new MeasuredLayoutSourceSemanticSummary
                {
                    SourceSemanticRole = group.Key.Role,
                    SourceAssetRealization = group.Key.Asset,
                    ComparisonCount = group.Count(),
                    IssueCount = groupIssues.Count,
                    IssueCategories = groupIssues
                        .Select(static issue => issue.Category)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(static category => category, StringComparer.Ordinal)
                        .ToArray()
                };
            })
            .OrderBy(static summary => summary.SourceSemanticRole, StringComparer.Ordinal)
            .ThenBy(static summary => summary.SourceAssetRealization ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<MeasuredLayoutIssue> BuildMeasuredIssues(MeasuredLayoutComparison comparison)
    {
        const double insetTolerance = 6;
        const double widthTolerance = 8;
        const double heightTolerance = 8;
        const double fontSizeTolerance = 1;
        const double scaleTolerance = 0.05;
        var shellCandidate = IsShellContainerCandidate(comparison);

        MeasuredLayoutIssue CreateIssue(string category, string severity, string summary, string? suggestedAction)
            => new()
            {
                Category = category,
                Severity = severity,
                LocalPath = comparison.LocalPath,
                ExpectedSemanticClass = comparison.ExpectedSemanticClass,
                ExpectedSourceSemanticRole = comparison.ExpectedSourceSemanticRole,
                ExpectedSourceAssetRealization = comparison.ExpectedSourceAssetRealization,
                Summary = summary,
                SuggestedAction = suggestedAction
            };

        if (shellCandidate
            && (Math.Abs(comparison.ActualScaleX - 1d) > scaleTolerance
                || Math.Abs(comparison.ActualScaleY - 1d) > scaleTolerance))
        {
            yield return CreateIssue(
                "realization-scale-mismatch",
                "warning",
                $"Node is realized with local scale ({comparison.ActualScaleX:F2}, {comparison.ActualScaleY:F2}) instead of the expected neutral scale.",
                "Inspect fixture host scaling or parent transform adjustments before retuning shell layout or metrics.");
        }

        if (comparison.ExpectedStartInsetX.HasValue && comparison.ActualX + insetTolerance < comparison.ExpectedStartInsetX.Value)
        {
            yield return CreateIssue(
                "start-edge-underflow",
                "warning",
                $"Left inset realized at {comparison.ActualX:F1}px but the Visual IR contract expects about {comparison.ExpectedStartInsetX.Value:F1}px.",
                "Inspect parent padding, child margin, and start-edge participation before changing gap or text metrics.");
        }

        if (comparison.ExpectedStartInsetX.HasValue && comparison.ActualX > comparison.ExpectedStartInsetX.Value + insetTolerance)
        {
            yield return CreateIssue(
                "start-edge-overshift",
                "info",
                $"Left inset realized at {comparison.ActualX:F1}px, overshooting the expected start inset of {comparison.ExpectedStartInsetX.Value:F1}px.",
                "Inspect absolute offset retention and parent padding accumulation for this node.");
        }

        if (comparison.ExpectedStartInsetY.HasValue && comparison.ActualY + insetTolerance < comparison.ExpectedStartInsetY.Value)
        {
            yield return CreateIssue(
                "start-edge-underflow",
                "warning",
                $"Top inset realized at {comparison.ActualY:F1}px but the Visual IR contract expects about {comparison.ExpectedStartInsetY.Value:F1}px.",
                "Inspect parent padding, child margin, and start-edge participation before changing gap or text metrics.");
        }

        if (comparison.ExpectedStartInsetY.HasValue && comparison.ActualY > comparison.ExpectedStartInsetY.Value + insetTolerance)
        {
            yield return CreateIssue(
                "start-edge-overshift",
                "info",
                $"Top inset realized at {comparison.ActualY:F1}px, overshooting the expected start inset of {comparison.ExpectedStartInsetY.Value:F1}px.",
                "Inspect absolute offset retention and parent padding accumulation for this node.");
        }

        var widthStretchRelevant = shellCandidate
            && comparison.ExpectedWidthSizing != AxisSizing.Fill
            && comparison.ActualPreferredWidth > 0
            && comparison.ActualWidth > comparison.ActualPreferredWidth + widthTolerance;
        var heightCollapseRelevant = shellCandidate
            && comparison.ActualPreferredHeight > 0
            && comparison.ActualHeight + heightTolerance < comparison.ActualPreferredHeight;

        if (comparison.ExpectedWidthSizing == AxisSizing.Fill
            && comparison.ExpectedAvailableWidth.HasValue
            && comparison.ExpectedAvailableWidth.Value > 0
            && comparison.ActualWidth < comparison.ExpectedAvailableWidth.Value - widthTolerance)
        {
            yield return CreateIssue(
                "fill-underflow",
                "warning",
                $"Node is expected to fill about {comparison.ExpectedAvailableWidth.Value:F1}px but only realized {comparison.ActualWidth:F1}px.",
                "Inspect fill/stretch realization, layout-group child control flags, and content-hug conflicts on the parent.");
        }

        if (heightCollapseRelevant)
        {
            yield return CreateIssue(
                "height-collapsed-vs-preferred",
                "warning",
                $"Shell height realized at {comparison.ActualHeight:F1}px but the subtree prefers about {comparison.ActualPreferredHeight:F1}px.",
                "Preserve preferred shell height before tuning text or icon metrics.");
        }

        if (widthStretchRelevant)
        {
            yield return CreateIssue(
                "width-stretched-vs-preferred",
                "warning",
                $"Shell width realized at {comparison.ActualWidth:F1}px but the subtree prefers about {comparison.ActualPreferredWidth:F1}px.",
                "Preserve preferred shell width or reduce inherited fill/stretch before tuning smaller metrics.");
        }

        if (widthStretchRelevant || heightCollapseRelevant)
        {
            yield return CreateIssue(
                "cross-axis-stretch-mismatch",
                "warning",
                "The realized shell is stretching or collapsing away from its preferred box on one axis.",
                "Inspect layout-group child control flags and cross-axis stretch before changing local content metrics.");
        }

        if (heightCollapseRelevant
            || (widthStretchRelevant && comparison.ExpectedWidthSizing == AxisSizing.Hug))
        {
            yield return CreateIssue(
                "shell-padding-or-child-stack-mismatch",
                "warning",
                "The shell child stack is larger than the realized box, so padding, gap, or child stack realization is likely compressing this subtree.",
                "Tighten shell padding or main-axis gap before retuning text, icon, or edge metrics.");
        }

        if (LooksLikePortraitOrStatusShell(comparison)
            && (heightCollapseRelevant
                || (comparison.ExpectedStartInsetY.HasValue && Math.Abs(comparison.ActualY - comparison.ExpectedStartInsetY.Value) > insetTolerance)))
        {
            yield return CreateIssue(
                "portrait-or-status-row-shell-drift",
                "warning",
                $"The portrait or status-row shell '{comparison.ActualName}' is drifting away from its preferred box.",
                "Preserve shell bounds for portrait and status-row motifs before adjusting fonts or icon alignment.");
        }

        if (comparison.ExpectedWidthSizing == AxisSizing.Hug
            && comparison.ExpectedAvailableWidth.HasValue
            && comparison.ActualPreferredWidth > 0
            && comparison.ActualWidth >= comparison.ExpectedAvailableWidth.Value - widthTolerance
            && comparison.ActualWidth > comparison.ActualPreferredWidth + widthTolerance)
        {
            yield return CreateIssue(
                "hug-stretched-to-fill",
                "warning",
                $"Node is expected to hug content but realized {comparison.ActualWidth:F1}px against an available width of {comparison.ExpectedAvailableWidth.Value:F1}px.",
                "Inspect child-control-width, flexible width, and content-size-fitter interaction for this subtree.");
        }

        if (!comparison.ExpectedWrapText
            && comparison.ActualPreferredWidth > 0
            && comparison.ActualWidth + widthTolerance < comparison.ActualPreferredWidth)
        {
            yield return CreateIssue(
                "wrap-pressure-risk",
                "info",
                $"Preferred text width is {comparison.ActualPreferredWidth:F1}px but realized width is {comparison.ActualWidth:F1}px, so the node is at risk of wrapping or compression.",
                string.Equals(comparison.ExpectedSourceSemanticRole, "right-aligned-quantity", StringComparison.Ordinal)
                    ? "Treat this as a row-end width issue first: preserve nowrap and content-hug for the quantity before tuning font size."
                    : "Treat this as a layout-width issue first, then tune font size or line height only if width realization is already correct.");
        }

        if (comparison.ExpectedFontSize.HasValue
            && comparison.ActualFontSize > 0
            && Math.Abs(comparison.ExpectedFontSize.Value - comparison.ActualFontSize) >= fontSizeTolerance)
        {
            yield return CreateIssue(
                "font-size-drift",
                "info",
                $"Expected font size is {comparison.ExpectedFontSize.Value:F1}px but realized font size is {comparison.ActualFontSize:F1}px.",
                string.Equals(comparison.ExpectedSourceAssetRealization, "IconGlyph", StringComparison.Ordinal)
                    ? "Inspect icon metric-profile selection, baseline offset, and optical centering before adjusting local layout heuristics."
                    : "Inspect metric-profile selection for this semantic class before adjusting local layout heuristics.");
        }

        if (comparison.ExpectedClipContent != comparison.ActualClipContent)
        {
            yield return CreateIssue(
                "clip-mismatch",
                "info",
                comparison.ExpectedClipContent
                    ? "The Visual IR expects clipping, but the realized node is not clipping content."
                    : "The realized node is clipping content even though the Visual IR contract is visible overflow.",
                string.Equals(comparison.ExpectedSourceAssetRealization, "ImageAsset", StringComparison.Ordinal)
                    ? "Inspect image shell overflow or mask emission before changing content metrics."
                    : "Inspect overflow or mask emission for this subtree before changing content metrics.");
        }
    }

    private static bool IsShellContainerCandidate(MeasuredLayoutComparison comparison)
    {
        if (string.Equals(comparison.ActualNodeType, "Text", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (comparison.ExpectedChildCount > 0 || comparison.ActualChildCount > 0)
        {
            return true;
        }

        return LooksLikePortraitOrStatusShell(comparison)
            || comparison.ActualName.StartsWith("Member", StringComparison.Ordinal)
            || comparison.ActualName.StartsWith("HeroRow", StringComparison.Ordinal);
    }

    private static bool LooksLikePortraitOrStatusShell(MeasuredLayoutComparison comparison)
        => comparison.ActualName.StartsWith("Portrait", StringComparison.Ordinal)
            || comparison.ActualName.StartsWith("HeroRow", StringComparison.Ordinal)
            || comparison.ActualName.StartsWith("StatusRow", StringComparison.Ordinal)
            || comparison.ActualName.StartsWith("StatusBuff", StringComparison.Ordinal);

    private static double? ResolveExpectedStartInsetX(VisualNode node, VisualNode? parent, ActualLayoutNode? actualParent, int? childIndex)
    {
        if (node.EdgeContract.Participation == LayoutParticipation.Overlay)
        {
            return ResolvePixelDimension(node.Box.Left) ?? 0;
        }

        if (parent == null)
        {
            return 0;
        }

        var inset = (parent.Box.Padding?.Left ?? 0) + (node.Box.Margin?.Left ?? 0);
        if (childIndex.HasValue
            && parent.Box.LayoutType == LayoutType.Horizontal
            && actualParent != null
            && actualParent.Children.Count == parent.Children.Count)
        {
            inset += ResolveHorizontalGap(parent) * childIndex.Value;
            for (var index = 0; index < childIndex.Value; index++)
            {
                inset += actualParent.Children[index].Width;
                inset += (parent.Children[index].Box.Margin?.Left ?? 0) + (parent.Children[index].Box.Margin?.Right ?? 0);
            }
        }

        return inset;
    }

    private static double? ResolveExpectedStartInsetY(VisualNode node, VisualNode? parent, ActualLayoutNode? actualParent, int? childIndex)
    {
        if (node.EdgeContract.Participation == LayoutParticipation.Overlay)
        {
            return ResolvePixelDimension(node.Box.Top) ?? 0;
        }

        if (parent == null)
        {
            return 0;
        }

        var inset = (parent.Box.Padding?.Top ?? 0) + (node.Box.Margin?.Top ?? 0);
        if (childIndex.HasValue
            && parent.Box.LayoutType == LayoutType.Vertical
            && actualParent != null
            && actualParent.Children.Count == parent.Children.Count)
        {
            inset += ResolveVerticalGap(parent) * childIndex.Value;
            for (var index = 0; index < childIndex.Value; index++)
            {
                inset += actualParent.Children[index].Height;
                inset += (parent.Children[index].Box.Margin?.Top ?? 0) + (parent.Children[index].Box.Margin?.Bottom ?? 0);
            }
        }

        return inset;
    }

    private static double? ResolveExpectedAvailableWidth(
        VisualNode node,
        VisualNode? parent,
        ActualLayoutNode? actualParent,
        int? childIndex)
    {
        if (parent == null || actualParent == null)
        {
            return null;
        }

        var availableWidth = actualParent.Width
            - (parent.Box.Padding?.Left ?? 0)
            - (parent.Box.Padding?.Right ?? 0)
            - (node.Box.Margin?.Left ?? 0)
            - (node.Box.Margin?.Right ?? 0);

        if (childIndex.HasValue
            && parent.Box.LayoutType == LayoutType.Horizontal
            && childIndex.Value >= 0
            && childIndex.Value < parent.Children.Count
            && actualParent.Children.Count == parent.Children.Count)
        {
            availableWidth -= ResolveHorizontalGap(parent) * Math.Max(0, actualParent.Children.Count - 1);

            for (var index = 0; index < parent.Children.Count; index++)
            {
                if (index == childIndex.Value)
                {
                    continue;
                }

                var sibling = parent.Children[index];
                var siblingActual = actualParent.Children[index];
                if (sibling.EdgeContract.WidthSizing == AxisSizing.Fill)
                {
                    continue;
                }

                availableWidth -= siblingActual.Width;
                availableWidth -= (sibling.Box.Margin?.Left ?? 0) + (sibling.Box.Margin?.Right ?? 0);
            }
        }

        return availableWidth > 0 ? availableWidth : null;
    }

    private static double ResolveHorizontalGap(VisualNode node)
    {
        if (node.Box.Gap == null)
        {
            return 0;
        }

        var gap = node.Box.Gap.Value;
        if (gap.Left > 0 && gap.Right > 0)
        {
            return (gap.Left + gap.Right) / 2d;
        }

        return gap.Left > 0 ? gap.Left : gap.Right;
    }

    private static double ResolveVerticalGap(VisualNode node)
    {
        if (node.Box.Gap == null)
        {
            return 0;
        }

        var gap = node.Box.Gap.Value;
        if (gap.Top > 0 && gap.Bottom > 0)
        {
            return (gap.Top + gap.Bottom) / 2d;
        }

        return gap.Top > 0 ? gap.Top : gap.Bottom;
    }

    private static double? ResolvePixelDimension(Dimension? dimension)
    {
        if (dimension == null)
        {
            return null;
        }

        return dimension.Value.Unit switch
        {
            DimensionUnit.Pixels => dimension.Value.Value,
            DimensionUnit.Cells => dimension.Value.Value,
            _ => null
        };
    }
}
