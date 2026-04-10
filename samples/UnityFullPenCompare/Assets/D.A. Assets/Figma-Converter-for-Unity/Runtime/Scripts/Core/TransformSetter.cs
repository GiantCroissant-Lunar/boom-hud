using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;

#if NOVA_UI_EXISTS
using Nova;
#endif

#pragma warning disable CS1998

namespace DA_Assets.FCU
{
    [Serializable]
    public class TransformSetter : FcuBase
    {
        public float GetAbsoluteAngle(FObject fobject)
        {
            if (!ReferenceEquals(fobject, null) && fobject.Data != null && fobject.Data.HasTransformComputationCache)
            {
                return fobject.Data.CachedAbsoluteFigmaRotationAngle;
            }

            float totalAngle = 0;

            FObject current = fobject;

            while (true)
            {
                totalAngle += current.GetFigmaRotationAngle();

                if (!monoBeh.CurrentProject.TryGetParent(current, out current))
                {
                    break;
                }
            }

            return totalAngle;
        }

        public async Task SetTransformPos(List<FObject> fobjects)
        {
            foreach (FObject fobject in fobjects)
            {
                if (fobject.Data?.RectGameObject == null)
                {
                    continue;
                }

                RectTransform rt = fobject.Data.RectGameObject.GetComponent<RectTransform>();
                rt.SetSmartAnchor(AnchorType.BottomLeft);
                rt.SetSmartPivot(PivotType.TopLeft);

                fobject.Data.FRect = GetGlobalRect(fobject);

                rt.sizeDelta = fobject.Data.FRect.size;
                rt.anchoredPosition = fobject.Data.FRect.position;

                rt.SetSmartPivot(PivotType.MiddleCenter);
                rt.SetRotation(fobject.Data.FRect.absoluteAngle);

                if (fobject.ContainsTag(FcuTag.Frame))
                {
                    rt.SetSmartAnchor(AnchorType.TopLeft);
                }
                else if (!fobject.Data.Parent.ContainsTag(FcuTag.AutoLayoutGroup))
                {
                    rt.SetSmartAnchor(fobject.GetFigmaAnchor());
                }

                rt.SetSmartPivot(monoBeh.Settings.MainSettings.PivotType);
            }
        }

        public FRect GetGlobalRect(FObject fobject)
        {
            if (!ReferenceEquals(fobject, null) && fobject.Data != null && fobject.Data.HasCachedGlobalRect)
            {
                return fobject.Data.FRect;
            }

            FRect rect = new FRect();
            Vector2 position = new Vector2();
            Vector2 size = new Vector2();

            fobject.GetBoundingSize(out Vector2 bSize);
            fobject.GetBoundingPosition(out Vector2 bPos);

            // Use GetAngleFromMatrix (relativeTransform) rather than GetFigmaRotationAngle,
            // because the JSON 'rotation' field can diverge from the relativeTransform matrix
            // for GROUP-type nodes. The relativeTransform always gives the true geometric
            // rotation relative to the parent, which is what CSS rotate needs.
            //
            // NOTE: for Downloadable nodes, angles are initially set to 0 because sprites are
            // downloaded in the node's local (unrotated) coordinate space. However, when a
            // Downloadable node is itself rotated (or has a rotated ancestor) AND has no
            // absoluteRenderBounds, we fall through to the local-rect path (state 6) below
            // which restores the real angles so the RectTransform rotates the sprite correctly.
            rect.angle = fobject.Data.FcuImageType == FcuImageType.Downloadable
                ? 0
                : (fobject.Data.HasTransformComputationCache ? fobject.Data.CachedMatrixAngle : fobject.GetAngleFromMatrix());
            rect.absoluteAngle = fobject.Data.FcuImageType == FcuImageType.Downloadable ? 0 : GetAbsoluteAngle(fobject);

            bool uguiOrNova = monoBeh.IsUGUI() || monoBeh.IsNova();

            int state = 0;

            bool useLocalLayoutRectForUitkDownloadable =
                monoBeh.IsUITK() &&
                fobject.IsDownloadableType() &&
                HasRotatedAncestor(fobject) &&
                ShouldUseLocalLayoutRectForUitkDownloadable(fobject);

            if (useLocalLayoutRectForUitkDownloadable)
            {
                state = 5;

                // For UITK template/layout boxes we need the node's local Figma size, not the
                // page-space AABB of the baked sprite. Keep the same visual center as the
                // bounding box and swap the box dimensions back to the local component size.
                size = fobject.Size;
                position = new Vector2(
                    bPos.x + (bSize.x - size.x) / 2f,
                    bPos.y + (bSize.y - size.y) / 2f);
            }
            else if (fobject.IsDownloadableType())
            {
                bool hasRenderSize = fobject.GetRenderSize(out Vector2 rSize);
                bool hasRenderPos = fobject.GetRenderPosition(out Vector2 rPos);
                bool ancestorClips = HasAncestorWithClipContent(fobject);

                // When a Downloadable node is rotated (itself or via an ancestor) and we
                // cannot use absoluteRenderBounds (state 1), the fallback to
                // absoluteBoundingBox gives an AABB whose width/height may be swapped and
                // whose position is the page-space AABB corner rather than the logical
                // origin. Figma API exports sprites in the node's own local (unrotated)
                // coordinate space (size = fobject.Size), so we must use the local size and
                // rotate via RectTransform just like non-downloadable nodes.
                // UITK handles this via state 5 above; Canvas/Nova need the same treatment.
                bool canUseRenderBounds = hasRenderSize && hasRenderPos && !ancestorClips;
                bool useLocalRectForRotatedDownloadable =
                    uguiOrNova &&
                    !canUseRenderBounds &&
                    HasSelfOrAncestorRotation(fobject);

                if (useLocalRectForRotatedDownloadable)
                {
                    state = 6;

                    size = fobject.Size;
                    position = new Vector2(
                        bPos.x + (bSize.x - size.x) / 2f,
                        bPos.y + (bSize.y - size.y) / 2f);

                    // Restore actual angles so SetTransformPos can rotate the RectTransform.
                    rect.angle = fobject.Data.HasTransformComputationCache
                        ? fobject.Data.CachedMatrixAngle
                        : fobject.GetAngleFromMatrix();
                    rect.absoluteAngle = GetAbsoluteAngle(fobject);
                }
                else if (canUseRenderBounds)
                {
                    state = 1;

                    // Render bounds are accurate and not clipped by parent.
                    // Already includes effects, arcs, rotation.
                    size = rSize;
                    position = rPos;
                }
                else
                {
                    // Whether the node has its own effects determines the expansion strategy:
                    // - Has own effects: use them directly (same as before this fix series).
                    // - No own effects (GROUP/INSTANCE): recursively collect child effects.
                    //   CollectEffectExpansion skips children when a mask sibling is present,
                    //   preventing over-expansion caused by shadows that are visually clipped by the mask.
                    //   This also correctly handles the case where absoluteRenderBounds is clipped by an
                    //   ancestor's clipsContent — we compute the theoretical full expansion from effect
                    //   parameters rather than relying on the (possibly clipped) render bounds.
                    Vector4 expansion;

                    bool hasOwnEffects = fobject.Effects != null && fobject.Effects.Count > 0;

                    if (hasOwnEffects)
                    {
                        // Use own effects — same behaviour as before this fix.
                        expansion = CalculateEffectExpansion(fobject.Effects);
                    }
                    else
                    {
                        // No own effects (GROUP/INSTANCE): collect child effects with mask awareness.
                        expansion = CalculateEffectExpansionRecursive(fobject);
                    }

                    if (fobject.IsArcDataFilled())
                    {
                        state = 2;

                        // absoluteBoundingBox gives full ellipse AABB — compute arc AABB instead.
                        ComputeArcAABB(fobject, bPos, bSize, out Vector2 arcPos, out Vector2 arcSize);
                        size = new Vector2(
                            arcSize.x + expansion.x + expansion.z,
                            arcSize.y + expansion.y + expansion.w);
                        position = new Vector2(
                            arcPos.x - expansion.x,
                            arcPos.y - expansion.y);
                    }
                    else
                    {
                        state = 3;

                        size = new Vector2(
                            bSize.x + expansion.x + expansion.z,
                            bSize.y + expansion.y + expansion.w);
                        position = new Vector2(
                            bPos.x - expansion.x,
                            bPos.y - expansion.y);
                    }
                }
            }
            else
            {
                state = 4;

                // Non-downloadable: rotation applied via RectTransform.rotation.
                // Use logical Size (unrotated), position adjusted to center of bounding box.
                size = fobject.Size;
                position = new Vector2(
                    bPos.x + (bSize.x - size.x) / 2f,
                    bPos.y + (bSize.y - size.y) / 2f);
            }

            if (fobject.TryFixSizeWithStroke(size.y, out float newY))
            {
                size.y = newY;
            }

            FcuLogger.Debug($"{nameof(GetGlobalRect)} | {fobject.Data.NameHierarchy} | state: {state} | {size} | {position} | {rect.absoluteAngle}", FcuDebugSettingsFlags.LogTransform);

            rect.size = size;
            rect.position = new Vector2(position.x, (uguiOrNova ? -position.y : position.y));

            List<Vector2> childSizes = new List<Vector2>();
            foreach (int index in fobject.Data.ChildIndexes)
            {
                if (monoBeh.CurrentProject.TryGetByIndex(index, out FObject child))
                {
                    childSizes.Add(GetGlobalRect(child).size);
                }
            }

            rect.padding = GetPadding(fobject).AdjustPadding(fobject, size, childSizes.ToArray());

            fobject.Data.FRect = rect;
            fobject.Data.HasCachedGlobalRect = true;

            return rect;
        }

        /// <summary>
        /// Recursively computes how far visual effects of child elements extend beyond
        /// the GROUP's own bounding box in world space.
        /// Unlike a naïve per-child max, this is position-aware:
        ///   a child positioned inside the group does not incorrectly expand the group's
        ///   left/top/right/bottom beyond what the child's shadow actually reaches in world space.
        /// Also respects mask children: when any direct child is a mask, subsequent siblings
        /// are visually clipped by the mask shape, so their effects produce no outward expansion.
        /// </summary>
        private static Vector4 CalculateEffectExpansionRecursive(FObject fobject)
        {
            if (fobject.Children.IsEmpty())
                return Vector4.zero;

            // Mask clips all sibling render extents — GROUP render size equals its logical size.
            if (fobject.Children.Any(c => c.IsObjectMask()))
                return Vector4.zero;

            float bLeft   = fobject.AbsoluteBoundingBox.X ?? 0f;
            float bTop    = fobject.AbsoluteBoundingBox.Y ?? 0f;
            float bRight  = bLeft + (fobject.AbsoluteBoundingBox.Width  ?? 0f);
            float bBottom = bTop  + (fobject.AbsoluteBoundingBox.Height ?? 0f);

            // Initialise world-space render edges to GROUP's own bounding box.
            float minLeft   = bLeft;
            float minTop    = bTop;
            float maxRight  = bRight;
            float maxBottom = bBottom;

            foreach (FObject child in fobject.Children)
                CollectRenderEdges(child, ref minLeft, ref minTop, ref maxRight, ref maxBottom);

            return new Vector4(
                Mathf.Max(0, bLeft   - minLeft),
                Mathf.Max(0, bTop    - minTop),
                Mathf.Max(0, maxRight  - bRight),
                Mathf.Max(0, maxBottom - bBottom));
        }

        /// <summary>
        /// Accumulates the world-space render edges of fobject into the running min/max values.
        /// Uses absoluteBoundingBox for world position so child effects are referenced correctly
        /// relative to the parent GROUP bounding box — rather than relative to the child itself.
        /// </summary>
        private static void CollectRenderEdges(
            FObject fobject,
            ref float minLeft, ref float minTop, ref float maxRight, ref float maxBottom)
        {
            float bLeft   = fobject.AbsoluteBoundingBox.X ?? 0f;
            float bTop    = fobject.AbsoluteBoundingBox.Y ?? 0f;
            float bRight  = bLeft + (fobject.AbsoluteBoundingBox.Width  ?? 0f);
            float bBottom = bTop  + (fobject.AbsoluteBoundingBox.Height ?? 0f);

            Vector4 exp = CalculateEffectExpansion(fobject.Effects);

            // Extend running edges by this node's own render extent.
            minLeft   = Mathf.Min(minLeft,   bLeft   - exp.x);
            minTop    = Mathf.Min(minTop,     bTop    - exp.y);
            maxRight  = Mathf.Max(maxRight,  bRight  + exp.z);
            maxBottom = Mathf.Max(maxBottom, bBottom + exp.w);

            if (fobject.Children.IsEmpty())
                return;

            // If any direct child is a mask, it clips siblings — stop collecting.
            if (fobject.Children.Any(c => c.IsObjectMask()))
                return;

            foreach (FObject child in fobject.Children)
                CollectRenderEdges(child, ref minLeft, ref minTop, ref maxRight, ref maxBottom);
        }


        /// <summary>
        /// Calculates how far visual effects extend beyond the object's bounding box.
        /// Returns Vector4(left, top, right, bottom) expansion in Figma coordinates.
        /// </summary>
        public static Vector4 CalculateEffectExpansion(List<Effect> effects)
        {
            if (effects == null || effects.Count == 0)
                return Vector4.zero;

            float left = 0, top = 0, right = 0, bottom = 0;

            foreach (Effect effect in effects)
            {
                if (effect.Visible.HasValue && !effect.Visible.Value)
                    continue;

                float eLeft = 0, eTop = 0, eRight = 0, eBottom = 0;

                switch (effect.Type)
                {
                    case EffectType.DROP_SHADOW:
                        {
                            float radius = effect.Radius;
                            float spread = effect.Spread ?? 0f;
                            float extent = radius + spread;

                            eLeft = Mathf.Max(0, extent - effect.Offset.x);
                            eRight = Mathf.Max(0, extent + effect.Offset.x);
                            eTop = Mathf.Max(0, extent - effect.Offset.y);
                            eBottom = Mathf.Max(0, extent + effect.Offset.y);
                            break;
                        }
                    case EffectType.LAYER_BLUR:
                        {
                            eLeft = eRight = eTop = eBottom = effect.Radius;
                            break;
                        }
                        // INNER_SHADOW and BACKGROUND_BLUR do not expand the bounding box.
                }

                // Union of all effects: take max expansion per side.
                left = Mathf.Max(left, eLeft);
                top = Mathf.Max(top, eTop);
                right = Mathf.Max(right, eRight);
                bottom = Mathf.Max(bottom, eBottom);
            }

            return new Vector4(left, top, right, bottom);
        }

        private bool HasAncestorWithClipContent(FObject fobject)
        {
            int parentIndex = fobject.Data.ParentIndex;
            while (parentIndex >= 0 && monoBeh.CurrentProject.TryGetByIndex(parentIndex, out FObject parent))
            {
                if (parent.ClipsContent.ToBoolNullFalse())
                    return true;
                parentIndex = parent.Data.ParentIndex;
            }
            return false;
        }

        private bool HasRotatedAncestor(FObject fobject)
        {
            if (!ReferenceEquals(fobject, null) && fobject.Data != null && fobject.Data.HasTransformComputationCache)
            {
                return fobject.Data.CachedHasRotatedAncestor;
            }

            int parentIndex = fobject.Data.ParentIndex;

            while (parentIndex >= 0 && monoBeh.CurrentProject.TryGetByIndex(parentIndex, out FObject parent))
            {
                if (Mathf.Abs(parent.GetAngleFromMatrix()) > 0.001f)
                    return true;

                parentIndex = parent.Data.ParentIndex;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the node itself or any of its ancestors is rotated.
        /// Used to detect when a Downloadable node must be positioned and rotated
        /// via RectTransform rather than relying on absoluteBoundingBox dimensions.
        /// </summary>
        private bool HasSelfOrAncestorRotation(FObject fobject)
        {
            float selfAngle = fobject.Data.HasTransformComputationCache
                ? fobject.Data.CachedMatrixAngle
                : fobject.GetAngleFromMatrix();

            if (Mathf.Abs(selfAngle) > 0.001f)
                return true;

            return HasRotatedAncestor(fobject);
        }

        private static bool ShouldUseLocalLayoutRectForUitkDownloadable(FObject fobject)
        {
            // This path exists to keep template/container layout boxes stable under rotated
            // ancestors. Flat downloadable leaves should keep using their actual render bounds;
            // otherwise a 3x38 baked sprite can be forced back into a 38x3 local Figma box,
            // which breaks the parent hover/layout bounds in UITK.
            return fobject.Children != null && fobject.Children.Count > 0;
        }

        /// <summary>
        /// Computes the axis-aligned bounding box (AABB) of an arc sector in absolute coordinates.
        /// Used when absoluteBoundingBox gives the full ellipse AABB instead of the visible arc.
        /// </summary>
        private static void ComputeArcAABB(
            FObject fobject,
            Vector2 bPos, Vector2 bSize,
            out Vector2 arcPos, out Vector2 arcSize)
        {
            float rx = fobject.Size.x / 2f;
            float ry = fobject.Size.y / 2f;
            float startAngle = fobject.ArcData.StartingAngle;
            float endAngle = fobject.ArcData.EndingAngle;
            float innerR = fobject.ArcData.InnerRadius;
            float rotRad = Mathf.Deg2Rad * fobject.GetFigmaRotationAngle();

            // Collect candidate points in local coords (centered at ellipse center).
            List<Vector2> points = new List<Vector2>(12);

            // Outer arc endpoints.
            points.Add(new Vector2(rx * Mathf.Cos(startAngle), ry * Mathf.Sin(startAngle)));
            points.Add(new Vector2(rx * Mathf.Cos(endAngle), ry * Mathf.Sin(endAngle)));

            // Inner arc endpoints (or center if no inner radius).
            if (innerR > 0f)
            {
                float irx = innerR * rx;
                float iry = innerR * ry;
                points.Add(new Vector2(irx * Mathf.Cos(startAngle), iry * Mathf.Sin(startAngle)));
                points.Add(new Vector2(irx * Mathf.Cos(endAngle), iry * Mathf.Sin(endAngle)));
                AddExtremaIfInRange(points, irx, iry, startAngle, endAngle);
            }
            else
            {
                points.Add(Vector2.zero);
            }

            // Outer arc extrema at cardinal angles.
            AddExtremaIfInRange(points, rx, ry, startAngle, endAngle);

            // Rotate all points and compute AABB.
            float cosR = Mathf.Cos(rotRad);
            float sinR = Mathf.Sin(rotRad);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (Vector2 p in points)
            {
                float rotX = p.x * cosR - p.y * sinR;
                float rotY = p.x * sinR + p.y * cosR;

                minX = Mathf.Min(minX, rotX);
                maxX = Mathf.Max(maxX, rotX);
                minY = Mathf.Min(minY, rotY);
                maxY = Mathf.Max(maxY, rotY);
            }

            // Translate to absolute coords (ellipse center = bPos + bSize/2).
            Vector2 center = new Vector2(bPos.x + bSize.x / 2f, bPos.y + bSize.y / 2f);
            arcPos = new Vector2(center.x + minX, center.y + minY);
            arcSize = new Vector2(maxX - minX, maxY - minY);
        }

        private static void AddExtremaIfInRange(
            List<Vector2> points, float rx, float ry,
            float startAngle, float endAngle)
        {
            // Cardinal angles where cos/sin reach extrema.
            float[] cardinals = { 0f, Mathf.PI / 2f, Mathf.PI, 3f * Mathf.PI / 2f };
            foreach (float angle in cardinals)
            {
                if (IsAngleInRange(angle, startAngle, endAngle))
                {
                    points.Add(new Vector2(rx * Mathf.Cos(angle), ry * Mathf.Sin(angle)));
                }
            }
        }

        private static bool IsAngleInRange(float angle, float start, float end)
        {
            float twoPi = 2f * Mathf.PI;
            float normalized = angle;
            while (normalized < start) normalized += twoPi;
            return normalized <= end;
        }

        private static RectOffsetCustom GetPadding(FObject fobject)
        {
            return new RectOffsetCustom
            {
                bottom = (int)fobject.PaddingBottom.ToFloat().Round(FcuConfig.Rounding.Padding),
                top = (int)fobject.PaddingTop.ToFloat().Round(FcuConfig.Rounding.Padding),
                left = (int)fobject.PaddingLeft.ToFloat().Round(FcuConfig.Rounding.Padding),
                right = (int)fobject.PaddingRight.ToFloat().Round(FcuConfig.Rounding.Padding)
            };
        }


        public async Task RestoreParentsRect(List<FObject> fobjects)
        {
            foreach (FObject fobject in fobjects)
            {
                if (fobject.Data?.RectGameObject == null)
                {
                    continue;
                }

                if (!fobject.ContainsTag(FcuTag.Frame))
                {
                    if (fobject.Data.RectGameObject != null)
                    {
                        fobject.Data.RectGameObject.transform.SetParent(fobject.Data.ParentTransformRect);
                    }
                    else
                    {
                        fobject.Data.RectGameObject.transform.SetParent(monoBeh.transform);
                    }
                }
            }
        }

        public async Task RestoreParents(List<FObject> fobjects)
        {
            foreach (FObject fobject in fobjects)
            {
                if (fobject.Data?.GameObject == null)
                {
                    continue;
                }

                if (fobject.Data.ParentTransform != null)
                {
                    fobject.Data.GameObject.transform.SetParent(fobject.Data.ParentTransform);
                }
                else
                {
                    fobject.Data.GameObject.transform.SetParent(monoBeh.transform);
                }
            }
        }

        internal async Task MoveUguiTransforms(List<FObject> currPage)
        {
            foreach (FObject fobject in currPage)
            {
                if (fobject.Data.GameObject == null)
                    continue;

                if (fobject.Data.RectGameObject == null)
                    continue;

                fobject.Data.GameObject.TryAddComponent(out RectTransform goRt);
                fobject.Data.RectGameObject.TryGetComponentSafe(out RectTransform rectRt);

                goRt.CopyFrom(rectRt);
            }

            await Task.Yield();
        }

        internal void MoveNovaTransforms(List<FObject> currPage)
        {
            Transform tempParent = MonoBehExtensions.CreateEmptyGameObject(nameof(tempParent), monoBeh.transform).transform;

            foreach (FObject fobject in currPage)
            {
                if (fobject.Data.GameObject == null)
                    continue;

                if (fobject.Data.RectGameObject == null)
                    continue;

                fobject.Data.RectGameObject.TryGetComponentSafe(out RectTransform rectRt);
                fobject.Data.UguiTransformData = UguiTransformData.Create(rectRt);

#if NOVA_UI_EXISTS
                if (fobject.ContainsTag(FcuTag.Text))
                {
                    fobject.Data.GameObject.TryAddComponent(out TextBlock textBlock);
                }
                else
                {
                    fobject.Data.GameObject.TryAddComponent(out UIBlock2D uiBlock2d);
                }

                UIBlock uiBlock = fobject.Data.GameObject.GetComponent<UIBlock>();
                uiBlock.Color = default;

                uiBlock.Layout.Size = new Length3
                {
                    X = fobject.Data.FRect.size.x,
                    Y = fobject.Data.FRect.size.y,
                };

                fobject.ExecuteWithTemporaryParent(tempParent, x => x.Data.GameObject, () =>
                {
                    SetFigmaRotation(fobject, fobject.Data.GameObject);
                });

                uiBlock.Layout.Position = new Length3
                {
                    X = fobject.Data.UguiTransformData.LocalPosition.x,
                    Y = fobject.Data.UguiTransformData.LocalPosition.y,
                };
#endif
            }

            tempParent.gameObject.Destroy();
        }

        public async Task SetNovaAnchors(List<FObject> fobjects, CancellationToken token)
        {
            int total = fobjects.Count;
            int processed = 0;

            IEnumerable<FrameGroup> fobjectsByFrame = fobjects
                .GroupBy(x => x.Data.RootFrame)
                .Select(g => new FrameGroup
                {
                    Childs = g.Select(x => x).ToList(),
                    RootFrame = g.First()
                });

            foreach (FrameGroup rootFrame in fobjectsByFrame)
            {
                if (rootFrame.RootFrame.Data.RectGameObject == null)
                    continue;

                _ = SetNovaAnchorsRoutine(rootFrame.Childs, () => processed++, token);
            }

            int tempCount = -1;
            while (FcuLogger.WriteLogBeforeEqual(
                ref processed,
                ref total,
                FcuLocKey.log_set_anchors.Localize(processed, total),
                ref tempCount))
            {
                await Task.Delay(1000, token);
            }
        }

        private async Task SetNovaAnchorsRoutine(List<FObject> fobjects, Action onProcess, CancellationToken token)
        {
#if NOVA_UI_EXISTS
            foreach (FObject fobject in fobjects)
            {
                if (fobject.Data.GameObject == null)
                    continue;

                fobject.Data.GameObject.TryGetComponentSafe(out UIBlock uiBlock);
                await uiBlock.SetNovaAnchor(fobject.GetFigmaAnchor(), token);

                onProcess.Invoke();
            }
#endif

            await Task.Yield();
        }

        internal async Task RestoreNovaFramePositions(List<FObject> fobjects, CancellationToken token)
        {
            IEnumerable<FrameGroup> fobjectsByFrame = fobjects
                .GroupBy(x => x.Data.RootFrame)
                .Select(g => new FrameGroup
                {
                    Childs = g.Select(x => x).ToList(),
                    RootFrame = g.First()
                });

            foreach (FrameGroup rootFrame in fobjectsByFrame)
            {
                if (rootFrame.RootFrame.Data.GameObject == null)
                    continue;

#if NOVA_UI_EXISTS
                rootFrame.RootFrame.Data.GameObject.TryGetComponentSafe(out UIBlock uiBlock);

                await uiBlock.SetNovaAnchor(AnchorType.TopLeft, token);
#endif
                await Task.Delay(100, token);

#if NOVA_UI_EXISTS
                uiBlock.Layout.Position = new Length3
                {
                    X = rootFrame.RootFrame.AbsoluteBoundingBox.X.ToFloat(),
                    Y = rootFrame.RootFrame.AbsoluteBoundingBox.Y.ToFloat(),
                };
#endif
            }
        }

        private void SetFigmaRotation(FObject fobject, GameObject target)
        {
            Transform rect = target.GetComponent<Transform>();
            rect.SetRotation(fobject.Data.FRect.absoluteAngle);
        }

        internal async Task SetSiblingIndex(List<FObject> fobjects)
        {
            foreach (var item in fobjects)
            {
                if (item.Data.GameObject == null)
                {
                    continue;
                }

                item.Data.GameObject.transform.SetSiblingIndex(item.Data.SiblingIndex);
            }
        }

        internal async Task SetStretchAllIfNeeded(List<FObject> fobjects)
        {
            if (monoBeh.Settings.MainSettings.PositioningMode == PositioningMode.GameView)
            {
                var frames = fobjects
                    .Where(x => x.ContainsTag(FcuTag.Frame));

                await Task.Yield();

                var frameSizeGroups = frames
                    .GroupBy(x => x.Size)
                    .Select(group => new
                    {
                        Size = group.Key,
                        Count = group.Count()
                    });

                await Task.Yield();

                var mostCommonSize = frameSizeGroups
                    .OrderByDescending(x => x.Count)
                    .FirstOrDefault();

                if (mostCommonSize.Size.x > 0 && mostCommonSize.Size.y > 0)
                {
                    monoBeh.EditorDelegateHolder.SetGameViewSize(mostCommonSize.Size);
                }

                foreach (FObject frame in frames)
                {
                    if (frame.Data.GameObject == null)
                        continue;

                    RectTransform rt = frame.Data.GameObject.GetComponent<RectTransform>();

                    rt.SetSmartAnchor(AnchorType.StretchAll);
                    rt.offsetMin = new Vector2(0, 0);
                    rt.offsetMax = new Vector2(0, 0);
                    rt.localScale = Vector3.one;
                }
            }
        }
    }
}
