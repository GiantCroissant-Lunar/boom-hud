using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

#if UNITEXT
using LightSide;
using Style = DA_Assets.FCU.Model.Style;
#endif

namespace DA_Assets.FCU.Drawers.CanvasDrawers
{
    public partial class UniTextDrawer
    {
#if UNITEXT
        /// <summary>
        /// Converts Figma gradient text fills into UniText GradientModifier registrations.
        /// Creates or reuses a UniTextGradients asset and registers named gradients.
        /// </summary>
        private static void PopulateGradient(UniText text, FObject fobject)
        {
            FGraphic graphic = fobject.Data.Graphic;

            if (!graphic.Fill.HasGradient)
                return;

            Paint gradientPaint = graphic.Fill.GradientPaint;

            if (gradientPaint.GradientStops.IsEmpty())
                return;

            // Build Unity Gradient from Figma gradient stops.
            Gradient unityGradient = FigmaGradientToUnity(gradientPaint);
            if (unityGradient == null)
                return;

            // Determine gradient shape from Figma paint type.
            string shape = GetGradientShape(gradientPaint.Type);

            // Compute gradient angle from handle positions.
            float angle = GetGradientAngle(gradientPaint);

            // Generate a deterministic name based on gradient data.
            string gradientName = GenerateGradientName(gradientPaint);

            // Ensure the UniTextGradients asset exists and register the gradient.
            EnsureGradientRegistered(gradientName, unityGradient);

            // Build the GradientModifier parameter: name[,shape][,angle]
            string parameter;
            if (Math.Abs(angle) < 0.01f && shape == "linear")
                parameter = gradientName;
            else
                parameter = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:F0}", gradientName, shape, angle);

            RegisterRangeRule(text, new GradientModifier())
                .data.Add(new RangeRule.Data { range = string.Empty, parameter = parameter });
        }

        private static Gradient FigmaGradientToUnity(Paint paint)
        {
            var stops = paint.GradientStops;
            if (stops == null || stops.Count == 0)
                return null;

            var colorKeys = new GradientColorKey[stops.Count];
            var alphaKeys = new GradientAlphaKey[stops.Count];

            for (int i = 0; i < stops.Count; i++)
            {
                colorKeys[i] = new GradientColorKey(stops[i].Color, stops[i].Position);
                alphaKeys[i] = new GradientAlphaKey(stops[i].Color.a, stops[i].Position);
            }

            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }

        private static string GetGradientShape(PaintType paintType)
        {
            switch (paintType)
            {
                case PaintType.GRADIENT_RADIAL:
                case PaintType.GRADIENT_DIAMOND:
                    return "radial";
                case PaintType.GRADIENT_ANGULAR:
                    return "angular";
                default:
                    return "linear";
            }
        }

        private static float GetGradientAngle(Paint paint)
        {
            if (paint.GradientHandlePositions == null || paint.GradientHandlePositions.Count < 2)
                return 0f;

            Vector2 p0 = paint.GradientHandlePositions[0];
            Vector2 p1 = paint.GradientHandlePositions[1];

            float dx = p1.x - p0.x;
            // Negate Y: Figma Y-down → Unity Y-up.
            float dy = -(p1.y - p0.y);

            // GradientModifier uses standard math convention: angle measured from X axis.
            // projection = x * cos(angle) + y * sin(angle)
            // So angle = atan2(dy, dx).
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

            // Normalize to 0-360 range.
            if (angle < 0f) angle += 360f;
            return angle;
        }

        private static string GenerateGradientName(Paint paint)
        {
            // Use a hash of gradient stops and type for a deterministic name.
            int hash = paint.Type.GetHashCode();
            if (paint.GradientStops != null)
            {
                foreach (var stop in paint.GradientStops)
                {
                    hash = hash * 31 + stop.Color.GetHashCode();
                    hash = hash * 31 + stop.Position.GetHashCode();
                }
            }

            return $"fcu_{(hash & 0x7FFFFFFF):X8}";
        }

        private static void EnsureGradientRegistered(string name, Gradient gradient)
        {
            var gradientsAsset = LightSide.UniTextSettings.Gradients;

            if (gradientsAsset == null)
            {
                Debug.LogWarning("[UniTextDrawer] UniTextSettings.Gradients is not assigned. Cannot register gradient.");
                return;
            }

            // Check if gradient already exists to avoid duplicates.
            if (gradientsAsset.TryGetGradient(name, out _))
                return;

            gradientsAsset.Add(name, gradient);

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.SaveAssetIfDirty(gradientsAsset);
#endif
        }
#endif
    }
}
