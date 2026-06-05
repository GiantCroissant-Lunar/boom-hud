using System.Linq;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Snapshots;
using BoomHud.Generators.VisualIR;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace BoomHud.Cli.Handlers.Baseline;

public static partial class ImageSimilarityHandler
{
    internal static double ComputeDeltaSimilarityPercent(double meanDelta)
    {
        var normalized = 1.0 - Math.Clamp(meanDelta / 255.0, 0.0, 1.0);
        return Math.Round(normalized * 100.0, 4);
    }

    internal static double ComputeOverallSimilarityPercent(DiffMetrics metrics)
    {
        var pixelIdentityPercent = Math.Max(0, 100.0 - metrics.ChangedPercent);
        var deltaSimilarityPercent = ComputeDeltaSimilarityPercent(metrics.MeanDelta);
        var weightedScore = (pixelIdentityPercent * 0.7) + (deltaSimilarityPercent * 0.3);
        return Math.Round(Math.Clamp(weightedScore, 0.0, 100.0), 2);
    }

    internal static ImageSimilaritySpatialAnalysis AnalyzeSpatialDiff(string baselinePath, string currentPath, int tolerance)
    {
        using var baseline = Image.Load<Rgba32>(baselinePath);
        using var current = Image.Load<Rgba32>(currentPath);

        var width = Math.Max(baseline.Width, current.Width);
        var height = Math.Max(baseline.Height, current.Height);
        var totalPixels = Math.Max(1, width * height);
        const int alphaThreshold = 16;

        var edgeBandWidth = Math.Max(1, (int)Math.Round(width * 0.2));
        var edgeBandHeight = Math.Max(1, (int)Math.Round(height * 0.2));
        var rightBandStart = Math.Max(0, width - edgeBandWidth);
        var bottomBandStart = Math.Max(0, height - edgeBandHeight);

        var centerStartX = width / 3;
        var centerEndX = width - centerStartX;
        var centerStartY = height / 3;
        var centerEndY = height - centerStartY;

        long baselineOpaque = 0;
        long currentOpaque = 0;

        long leftEdgeTotal = 0;
        long rightEdgeTotal = 0;
        long topEdgeTotal = 0;
        long bottomEdgeTotal = 0;
        long centerBandTotal = 0;

        long leftEdgeChanged = 0;
        long rightEdgeChanged = 0;
        long topEdgeChanged = 0;
        long bottomEdgeChanged = 0;
        long centerBandChanged = 0;

        long leftThirdTotal = 0;
        long centerThirdTotal = 0;
        long rightThirdTotal = 0;
        long topThirdTotal = 0;
        long middleThirdTotal = 0;
        long bottomThirdTotal = 0;

        long leftThirdChanged = 0;
        long centerThirdChanged = 0;
        long rightThirdChanged = 0;
        long topThirdChanged = 0;
        long middleThirdChanged = 0;
        long bottomThirdChanged = 0;

        baseline.ProcessPixelRows(current, (baselineAccessor, currentAccessor) =>
        {
            for (var y = 0; y < height; y++)
            {
                var baselineRow = (y < baseline.Height) ? baselineAccessor.GetRowSpan(y) : default;
                var currentRow = (y < current.Height) ? currentAccessor.GetRowSpan(y) : default;

                for (var x = 0; x < width; x++)
                {
                    var baselinePixel = (x < baseline.Width && y < baseline.Height)
                        ? baselineRow[x]
                        : new Rgba32(0, 0, 0, 0);

                    var currentPixel = (x < current.Width && y < current.Height)
                        ? currentRow[x]
                        : new Rgba32(0, 0, 0, 0);

                    if (baselinePixel.A > alphaThreshold)
                    {
                        baselineOpaque++;
                    }

                    if (currentPixel.A > alphaThreshold)
                    {
                        currentOpaque++;
                    }

                    var dr = Math.Abs(currentPixel.R - baselinePixel.R);
                    var dg = Math.Abs(currentPixel.G - baselinePixel.G);
                    var db = Math.Abs(currentPixel.B - baselinePixel.B);
                    var da = Math.Abs(currentPixel.A - baselinePixel.A);
                    var changed = Math.Max(Math.Max(dr, dg), Math.Max(db, da)) > tolerance;

                    var isLeftEdge = x < edgeBandWidth;
                    var isRightEdge = x >= rightBandStart;
                    var isTopEdge = y < edgeBandHeight;
                    var isBottomEdge = y >= bottomBandStart;
                    var isCenterBand = x >= centerStartX && x < centerEndX && y >= centerStartY && y < centerEndY;

                    if (isLeftEdge)
                    {
                        leftEdgeTotal++;
                        if (changed)
                        {
                            leftEdgeChanged++;
                        }
                    }

                    if (isRightEdge)
                    {
                        rightEdgeTotal++;
                        if (changed)
                        {
                            rightEdgeChanged++;
                        }
                    }

                    if (isTopEdge)
                    {
                        topEdgeTotal++;
                        if (changed)
                        {
                            topEdgeChanged++;
                        }
                    }

                    if (isBottomEdge)
                    {
                        bottomEdgeTotal++;
                        if (changed)
                        {
                            bottomEdgeChanged++;
                        }
                    }

                    if (isCenterBand)
                    {
                        centerBandTotal++;
                        if (changed)
                        {
                            centerBandChanged++;
                        }
                    }

                    if (x < width / 3)
                    {
                        leftThirdTotal++;
                        if (changed)
                        {
                            leftThirdChanged++;
                        }
                    }
                    else if (x < (width * 2) / 3)
                    {
                        centerThirdTotal++;
                        if (changed)
                        {
                            centerThirdChanged++;
                        }
                    }
                    else
                    {
                        rightThirdTotal++;
                        if (changed)
                        {
                            rightThirdChanged++;
                        }
                    }

                    if (y < height / 3)
                    {
                        topThirdTotal++;
                        if (changed)
                        {
                            topThirdChanged++;
                        }
                    }
                    else if (y < (height * 2) / 3)
                    {
                        middleThirdTotal++;
                        if (changed)
                        {
                            middleThirdChanged++;
                        }
                    }
                    else
                    {
                        bottomThirdTotal++;
                        if (changed)
                        {
                            bottomThirdChanged++;
                        }
                    }
                }
            }
        });

        var leftEdgeChangedPercent = Percent(leftEdgeChanged, leftEdgeTotal);
        var rightEdgeChangedPercent = Percent(rightEdgeChanged, rightEdgeTotal);
        var topEdgeChangedPercent = Percent(topEdgeChanged, topEdgeTotal);
        var bottomEdgeChangedPercent = Percent(bottomEdgeChanged, bottomEdgeTotal);
        var centerBandChangedPercent = Percent(centerBandChanged, centerBandTotal);

        var leftThirdChangedPercent = Percent(leftThirdChanged, leftThirdTotal);
        var centerThirdChangedPercent = Percent(centerThirdChanged, centerThirdTotal);
        var rightThirdChangedPercent = Percent(rightThirdChanged, rightThirdTotal);
        var topThirdChangedPercent = Percent(topThirdChanged, topThirdTotal);
        var middleThirdChangedPercent = Percent(middleThirdChanged, middleThirdTotal);
        var bottomThirdChangedPercent = Percent(bottomThirdChanged, bottomThirdTotal);

        var baselineOpaquePercent = Percent(baselineOpaque, totalPixels);
        var currentOpaquePercent = Percent(currentOpaque, totalPixels);

        return new ImageSimilaritySpatialAnalysis
        {
            BaselineOpaquePercent = baselineOpaquePercent,
            CandidateOpaquePercent = currentOpaquePercent,
            OpaqueCoverageDeltaPercent = Math.Round(currentOpaquePercent - baselineOpaquePercent, 4),
            LeftEdgeChangedPercent = leftEdgeChangedPercent,
            RightEdgeChangedPercent = rightEdgeChangedPercent,
            TopEdgeChangedPercent = topEdgeChangedPercent,
            BottomEdgeChangedPercent = bottomEdgeChangedPercent,
            CenterBandChangedPercent = centerBandChangedPercent,
            LeftThirdChangedPercent = leftThirdChangedPercent,
            CenterThirdChangedPercent = centerThirdChangedPercent,
            RightThirdChangedPercent = rightThirdChangedPercent,
            TopThirdChangedPercent = topThirdChangedPercent,
            MiddleThirdChangedPercent = middleThirdChangedPercent,
            BottomThirdChangedPercent = bottomThirdChangedPercent,
            DominantHorizontalRegion = MaxLabel(
                ("left", leftThirdChangedPercent),
                ("center", centerThirdChangedPercent),
                ("right", rightThirdChangedPercent)),
            DominantVerticalRegion = MaxLabel(
                ("top", topThirdChangedPercent),
                ("middle", middleThirdChangedPercent),
                ("bottom", bottomThirdChangedPercent)),
            DominantBand = MaxLabel(
                ("left-edge", leftEdgeChangedPercent),
                ("right-edge", rightEdgeChangedPercent),
                ("top-edge", topEdgeChangedPercent),
                ("bottom-edge", bottomEdgeChangedPercent),
                ("center-band", centerBandChangedPercent))
        };
    }

    internal static ImageSimilarityRecursiveScoreNode BuildRecursiveAnalysis(string baselinePath, string currentPath, int tolerance)
    {
        using var baseline = Image.Load<Rgba32>(baselinePath);
        using var current = Image.Load<Rgba32>(currentPath);

        var rootBounds = new Rectangle(0, 0, Math.Max(baseline.Width, current.Width), Math.Max(baseline.Height, current.Height));
        return BuildRecursiveNode(baseline, current, rootBounds, tolerance, depth: 0);
    }

    internal static RecursiveFidelityScoreNode? ConvertRecursiveAnalysis(ImageSimilarityRecursiveScoreNode? node)
    {
        if (node == null)
        {
            return null;
        }

        return new RecursiveFidelityScoreNode
        {
            Level = node.Level,
            RegionId = BuildRegionId(node),
            OverallSimilarityPercent = node.OverallSimilarityPercent,
            Phases = node.Phases
                .Select(static phase => new RecursiveFidelityPhaseScore
                {
                    Phase = phase.Phase,
                    SimilarityPercent = phase.SimilarityPercent
                })
                .ToList(),
            Children = node.Children
                .Select(ConvertRecursiveAnalysis)
                .Where(static child => child != null)
                .Cast<RecursiveFidelityScoreNode>()
                .ToList()
        };
    }

    private static string BuildRegionId(ImageSimilarityRecursiveScoreNode node)
    {
        if (string.Equals(node.Level, "screen/frame", StringComparison.Ordinal))
        {
            return "root";
        }

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{node.Level}@{node.Bounds.X},{node.Bounds.Y},{node.Bounds.Width}x{node.Bounds.Height}");
    }

    private static ImageSimilarityRecursiveScoreNode BuildRecursiveNode(
        Image<Rgba32> baseline,
        Image<Rgba32> current,
        Rectangle bounds,
        int tolerance,
        int depth)
    {
        var metrics = AnalyzeRegion(baseline, current, bounds, tolerance);
        var overallSimilarityPercent = ComputeOverallSimilarityPercent(new DiffMetrics
        {
            BaselineWidth = bounds.Width,
            BaselineHeight = bounds.Height,
            CurrentWidth = bounds.Width,
            CurrentHeight = bounds.Height,
            TotalPixels = metrics.TotalPixels,
            ChangedPixels = metrics.ChangedPixels,
            ChangedPercent = metrics.ChangedPercent,
            MeanDelta = metrics.MeanDelta,
            MaxDelta = metrics.MaxDelta
        });

        var phases = new List<ImageSimilarityPhaseScore>
        {
            new() { Phase = "structural-match", SimilarityPercent = Math.Round(Math.Clamp(100.0 - metrics.ChangedPercent, 0.0, 100.0), 2) },
            new() { Phase = "outer-frame-match", SimilarityPercent = Math.Round(Math.Clamp(100.0 - metrics.AverageEdgeChangedPercent, 0.0, 100.0), 2) },
            new() { Phase = "inner-layout-match", SimilarityPercent = Math.Round(Math.Clamp(100.0 - ((metrics.CenterChangedPercent * 0.7) + (Math.Abs(metrics.OpaqueCoverageDeltaPercent) * 0.3)), 0.0, 100.0), 2) },
            new() { Phase = "text-icon-metrics", SimilarityPercent = Math.Round(Math.Clamp((ComputeDeltaSimilarityPercent(metrics.MeanDelta) * 0.65) + ((100.0 - Math.Abs(metrics.CenterChangedPercent - metrics.AverageEdgeChangedPercent)) * 0.35), 0.0, 100.0), 2) },
            new() { Phase = "polish-offsets", SimilarityPercent = Math.Round(Math.Clamp(((100.0 - metrics.ChangedPercent) * 0.4) + (ComputeDeltaSimilarityPercent(metrics.MeanDelta) * 0.6), 0.0, 100.0), 2) }
        };

        var children = new List<ImageSimilarityRecursiveScoreNode>();
        if (depth < 3 && bounds.Width >= 16 && bounds.Height >= 16)
        {
            foreach (var childBounds in SplitQuadrants(bounds))
            {
                if (childBounds.Width <= 0 || childBounds.Height <= 0)
                {
                    continue;
                }

                children.Add(BuildRecursiveNode(baseline, current, childBounds, tolerance, depth + 1));
            }
        }

        return new ImageSimilarityRecursiveScoreNode
        {
            Level = ResolveLevelName(depth),
            Bounds = new ImageSimilarityBounds
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height
            },
            OverallSimilarityPercent = overallSimilarityPercent,
            Phases = phases,
            Children = children
        };
    }

    private static RegionAnalysis AnalyzeRegion(Image<Rgba32> baseline, Image<Rgba32> current, Rectangle bounds, int tolerance)
    {
        const int alphaThreshold = 16;

        long changedPixels = 0;
        long totalPixels = Math.Max(1, bounds.Width * bounds.Height);
        long deltaSum = 0;
        var maxDelta = 0;
        long baselineOpaque = 0;
        long currentOpaque = 0;
        long edgeChanged = 0;
        long edgeTotal = 0;
        long centerChanged = 0;
        long centerTotal = 0;

        var edgeBandWidth = Math.Max(1, (int)Math.Round(bounds.Width * 0.2));
        var edgeBandHeight = Math.Max(1, (int)Math.Round(bounds.Height * 0.2));
        var centerStartX = bounds.X + (bounds.Width / 3);
        var centerEndX = bounds.X + bounds.Width - (bounds.Width / 3);
        var centerStartY = bounds.Y + (bounds.Height / 3);
        var centerEndY = bounds.Y + bounds.Height - (bounds.Height / 3);

        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            var baselineRow = (y < baseline.Height) ? baseline.DangerousGetPixelRowMemory(y).Span : default;
            var currentRow = (y < current.Height) ? current.DangerousGetPixelRowMemory(y).Span : default;

            for (var x = bounds.X; x < bounds.Right; x++)
            {
                var baselinePixel = (x < baseline.Width && y < baseline.Height)
                    ? baselineRow[x]
                    : new Rgba32(0, 0, 0, 0);

                var currentPixel = (x < current.Width && y < current.Height)
                    ? currentRow[x]
                    : new Rgba32(0, 0, 0, 0);

                if (baselinePixel.A > alphaThreshold)
                {
                    baselineOpaque++;
                }

                if (currentPixel.A > alphaThreshold)
                {
                    currentOpaque++;
                }

                var dr = Math.Abs(currentPixel.R - baselinePixel.R);
                var dg = Math.Abs(currentPixel.G - baselinePixel.G);
                var db = Math.Abs(currentPixel.B - baselinePixel.B);
                var da = Math.Abs(currentPixel.A - baselinePixel.A);
                var delta = Math.Max(Math.Max(dr, dg), Math.Max(db, da));
                var changed = delta > tolerance;
                deltaSum += delta;
                maxDelta = Math.Max(maxDelta, delta);

                if (changed)
                {
                    changedPixels++;
                }

                var isEdge =
                    x < bounds.X + edgeBandWidth
                    || x >= bounds.Right - edgeBandWidth
                    || y < bounds.Y + edgeBandHeight
                    || y >= bounds.Bottom - edgeBandHeight;

                if (isEdge)
                {
                    edgeTotal++;
                    if (changed)
                    {
                        edgeChanged++;
                    }
                }

                var isCenter = x >= centerStartX && x < centerEndX && y >= centerStartY && y < centerEndY;
                if (isCenter)
                {
                    centerTotal++;
                    if (changed)
                    {
                        centerChanged++;
                    }
                }
            }
        }

        var changedPercent = Percent(changedPixels, totalPixels);
        var meanDelta = totalPixels == 0 ? 0 : Math.Round((double)deltaSum / totalPixels, 4);
        var baselineOpaquePercent = Percent(baselineOpaque, totalPixels);
        var currentOpaquePercent = Percent(currentOpaque, totalPixels);

        return new RegionAnalysis(
            TotalPixels: (int)totalPixels,
            ChangedPixels: (int)changedPixels,
            ChangedPercent: changedPercent,
            MeanDelta: meanDelta,
            MaxDelta: maxDelta,
            AverageEdgeChangedPercent: Percent(edgeChanged, edgeTotal),
            CenterChangedPercent: Percent(centerChanged, centerTotal),
            OpaqueCoverageDeltaPercent: Math.Round(currentOpaquePercent - baselineOpaquePercent, 4));
    }

    private static IReadOnlyList<Rectangle> SplitQuadrants(Rectangle bounds)
    {
        var leftWidth = bounds.Width / 2;
        var rightWidth = bounds.Width - leftWidth;
        var topHeight = bounds.Height / 2;
        var bottomHeight = bounds.Height - topHeight;

        return
        [
            new Rectangle(bounds.X, bounds.Y, leftWidth, topHeight),
            new Rectangle(bounds.X + leftWidth, bounds.Y, rightWidth, topHeight),
            new Rectangle(bounds.X, bounds.Y + topHeight, leftWidth, bottomHeight),
            new Rectangle(bounds.X + leftWidth, bounds.Y + topHeight, rightWidth, bottomHeight)
        ];
    }

    private static string ResolveLevelName(int depth)
        => depth switch
        {
            0 => "screen/frame",
            1 => "panel",
            2 => "card/cluster",
            _ => "atomic-motif"
        };

    private static double Percent(long numerator, long denominator)
    {
        if (denominator <= 0)
        {
            return 0;
        }

        return Math.Round((double)numerator / denominator * 100.0, 4);
    }

    private static string MaxLabel(params (string Label, double Value)[] values)
    {
        return values.OrderByDescending(item => item.Value).First().Label;
    }

    private static double GetDominantBandPercent(ImageSimilaritySpatialAnalysis analysis)
    {
        return analysis.DominantBand switch
        {
            "left-edge" => analysis.LeftEdgeChangedPercent,
            "right-edge" => analysis.RightEdgeChangedPercent,
            "top-edge" => analysis.TopEdgeChangedPercent,
            "bottom-edge" => analysis.BottomEdgeChangedPercent,
            "center-band" => analysis.CenterBandChangedPercent,
            _ => 0
        };
    }

    private sealed record RegionAnalysis(
        int TotalPixels,
        int ChangedPixels,
        double ChangedPercent,
        double MeanDelta,
        int MaxDelta,
        double AverageEdgeChangedPercent,
        double CenterChangedPercent,
        double OpaqueCoverageDeltaPercent);
}
