#if FCU_EXISTS
using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using UnityEngine;
using UnityEngine.UIElements;

#if ULB_EXISTS
using DA_Assets.ULB;
#endif

namespace DA_Assets.FCU
{
    [Serializable]
    public class BaseStyleBuilder : FcuBase
    {
        public override void Init(FigmaConverterUnity monoBeh)
        {
            base.Init(monoBeh);

            this.ImageStyleBuilder.Init(monoBeh);
            this.TextStyleBuilder.Init(monoBeh);
        }

        /// <summary>
        /// hash, style name
        /// </summary>
        private static Dictionary<int, string> _styles = new Dictionary<int, string>();
        private readonly Stack<RotationScope> _rotationScopeRoots = new Stack<RotationScope>();

        private readonly struct RotationScope
        {
            public RotationScope(FObject root, bool includeRootRotate)
            {
                Root = root;
                IncludeRootRotate = includeRootRotate;
            }

            public FObject Root { get; }
            public bool IncludeRootRotate { get; }
        }

        public static void ClearStyles()
        {
            _styles.Clear();
            _styles = null;
            _styles = new Dictionary<int, string>();
        }

        public void PushRotationScopeRoot(FObject root, bool includeRootRotate)
        {
            if (root.Data != null)
            {
                _rotationScopeRoots.Push(new RotationScope(root, includeRootRotate));
            }
        }

        public void PopRotationScopeRoot()
        {
            if (_rotationScopeRoots.Count > 0)
            {
                _rotationScopeRoots.Pop();
            }
        }

        public void SetStyle(FObject fobject, StringBuilder styleBuilder)
        {
            fobject.Data.XmlElement.SetAttribute("name", fobject.Data.Names.ObjectName);

#if ULB_EXISTS
            //Debug.Log($"{monoBeh} | {monoBeh?.Settings} | {monoBeh?.Settings?.UITK_Settings} | {monoBeh?.Settings?.UITK_Settings?.UitkLinkingMode}");
            if (monoBeh.Settings.UITK_Settings.UitkLinkingMode == UitkLinkingMode.Guid)
                fobject.Data.XmlElement.SetAttribute("guid", fobject.Data.Names.UitkGuid);
#endif

            SetLocalStyle(fobject);
            SetGlobalStyle(fobject, styleBuilder);

            if (fobject.Type == NodeType.TEXT)
            {
                fobject.Data.XmlElement.SetAttribute("text", fobject.GetText());
                ApplyUitkTextSettings(fobject);
            }
        }

        /// <summary>
        /// Writes only positional inline styles (position, left, top, right, bottom, width, height)
        /// onto the XmlElement. Used for &lt;ui:Instance&gt; elements in the parent UXML
        /// so that all visual styles live inside the template itself.
        /// </summary>
        public void SetPositionalStyle(FObject fobject)
        {
            if (fobject.Data.FRect.IsDefault())
            {
                fobject.Data.FRect = monoBeh.TransformSetter.GetGlobalRect(fobject);
            }

            FRect rect = fobject.Data.FRect;

            var sb = new StringBuilder();

            FObject parent = fobject.Data.Parent;
            bool isTemplateBackedInstance =
                (fobject.Type == NodeType.INSTANCE || fobject.Type == NodeType.COMPONENT) &&
                !fobject.Children.IsEmpty();

            if (parent.Data != null && parent.ContainsTag(FcuTag.AutoLayoutGroup)
                && fobject.LayoutPositioning != LayoutPositioning.ABSOLUTE)
            {
                // Inside an autolayout — use relative positioning with width/height.
                sb.AddLocalStyle("position", "relative");

                // For downloadable sprites, GetGlobalRect returns screen-space dimensions
                // (absoluteRenderBounds), which may have swapped width/height when the
                // element is inside a 90°-rotated auto-layout ancestor.
                // Use fobject.Size (Figma component-space, pre-rotation) instead, so the
                // flex item contributes the correct amount to the column/row layout.
                // For non-downloadable elements GetGlobalRect already returns local size
                // (state 4), so fobject.Size and rect.size are equivalent there.
                Vector2 layoutSize = rect.size;

                // HUG: the element shrinks to its content — use auto.
                if (fobject.LayoutSizingVertical == "HUG" && !isTemplateBackedInstance)
                    sb.AddLocalStyle("height", "auto");
                else
                    sb.AddLocalStyle("height", $"{layoutSize.y.Round(0)}px");

                if (fobject.LayoutSizingHorizontal == "HUG" && !isTemplateBackedInstance)
                    sb.AddLocalStyle("width", "auto");
                else
                {
                    sb.AddLocalStyle("width", $"{layoutSize.x.Round(0)}px");
                    // FIXED width — prevent flex from shrinking the Instance below its declared size.
                    if (parent.LayoutMode == LayoutMode.HORIZONTAL)
                        sb.AddLocalStyle("flex-shrink", "0");
                }

                if (fobject.LayoutSizingVertical != "HUG" && parent.LayoutMode == LayoutMode.VERTICAL)
                    sb.AddLocalStyle("flex-shrink", "0");

                // Apply itemSpacing as static margin on every non-last Instance child of an AutoLayout.
                // For ScrollView children also add flex-shrink: 0 so they do not collapse.
                if (parent.ItemSpacing > 0)
                {
                    if (parent.OverflowDirection != OverflowDirection.NONE)
                        sb.AddLocalStyle("flex-shrink", "0");

                    bool isLast = true;
                    if (parent.Children != null)
                    {
                        for (int ci = parent.Children.Count - 1; ci >= 0; ci--)
                        {
                            FObject sibling = parent.Children[ci];
                            if (sibling.IsMask.ToBoolNullFalse()) continue;
                            isLast = sibling.Id == fobject.Id;
                            break;
                        }
                    }

                    if (!isLast)
                    {
                        if (parent.LayoutMode == LayoutMode.HORIZONTAL)
                            sb.AddLocalStyle("margin-right", $"{parent.ItemSpacing.Value.Round(0)}px");
                        else
                            sb.AddLocalStyle("margin-bottom", $"{parent.ItemSpacing.Value.Round(0)}px");
                    }
                }
            }
            else
            {
                // Outside autolayout (or ABSOLUTE inside autolayout): apply Figma constraint-based positioning.
                FRect pRect = parent.Data != null ? parent.Data.FRect : rect;
                SetConstraintBasedStyle(fobject, rect, pRect, sb);
            }

            // Instance elements also need USS rotate when they have non-zero Figma rotation.
            // GetAngleFromMatrix returns the local matrix angle in the inverse sign convention
            // used by UITK rotate, so emit the negated value.
            if (Mathf.Abs(rect.angle) > 0.001f)
            {
                sb.AddLocalStyle("rotate", $"{(-rect.angle).Round(3)}deg");
            }
            else if (fobject.IsDownloadableType())
            {
                // All downloadable sprites have their PNG baked at world-space (absolute)
                // orientation. Any accumulated CSS rotate from ancestor VEs / ui:Instance tags
                // cascades onto the sprite and shifts it away from its baked angle → wrong.
                // Counter-rotation cancels this cascade for every downloadable element,
                // whether it is a plain VE or a template instance ui:Instance.
                //
                // Note: isTemplateInstance is NOT used as a guard here. The original concern
                // was that counter-rotating a template instance would cancel the cascade for
                // its layout-based sub-template content. However, this code path is only
                // reached when fobject.IsDownloadableType() is true — meaning the entire
                // element is a downloadable PNG. In that case the sub-template (if any) is
                // itself a flat sprite root, and cancelling the cascade is correct.
                AppendDownloadableCounterRotation(fobject, sb, "BSB-Counter-Instance");
            }

            // Propagate overflow visibility to the TemplateContainer (ui:Instance).
            // By default UITK clips the template root to its layout box.
            // When the Figma component does NOT clip its children (clipsContent == false or unset),
            // allow the template content to draw beyond its layout bounds (e.g. armrests, backrests).
            if (!fobject.ClipsContent.ToBoolNullFalse())
            {
                sb.AddLocalStyle("overflow", "visible");
            }

            string inlineStyle = sb.ToString()
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ");

            fobject.Data.XmlElement.SetAttribute("style", inlineStyle);
        }


        private void ApplyUitkTextSettings(FObject fobject)
        {
            var settings = monoBeh.Settings?.UitkTextSettings;

            if (settings == null)
                return;

            XmlElement element = fobject.Data.XmlElement;

            if (element == null)
                return;

            SetBoolAttribute(element, "focusable", settings.Focusable);
#if UNITY_2021_3_OR_NEWER
            SetBoolAttribute(element, "enable-rich-text", settings.EnableRichText);
            SetBoolAttribute(element, "display-tooltip-when-elided", settings.DisplayTooltipWhenElided);
#endif
#if UNITY_2022_1_OR_NEWER
            SetBoolAttribute(element, "parse-escape-sequences", settings.ParseEscapeSequences);
#endif
#if UNITY_2023_2_OR_NEWER
            SetBoolAttribute(element, "emoji-fallback-support", settings.EmojiFallbackSupport);
#endif
#if UNITY_6000_0_OR_NEWER
            SetBoolAttribute(element, "double-click-selects-word", settings.DoubleClickSelectsWord);
            SetBoolAttribute(element, "triple-click-selects-line", settings.TripleClickSelectsLine);
            SetBoolAttribute(element, "selectable", settings.Selectable);
            SetLanguageDirection(element, settings.LanguageDirection);
#endif
        }

        private static void SetBoolAttribute(XmlElement element, string attributeName, bool value)
        {
            element.SetAttribute(attributeName, value ? "true" : "false");
        }

#if UNITY_6000_0_OR_NEWER
        private static void SetLanguageDirection(XmlElement element, LanguageDirection direction)
        {
            switch (direction)
            {
                case LanguageDirection.LTR:
                    element.SetAttribute("language-direction", "ltr");
                    break;
                case LanguageDirection.RTL:
                    element.SetAttribute("language-direction", "rtl");
                    break;
                default:
                    element.RemoveAttribute("language-direction");
                    break;
            }
        }
#endif

        private void SetGlobalStyle(FObject fobject, StringBuilder styleBuilder1)
        {
            StringBuilder tempSb = new StringBuilder();

            tempSb.AppendLine();

            if (fobject.Type == NodeType.TEXT)
            {
                string textStyle = this.TextStyleBuilder.CreateGlobalTextStyle(fobject);
                tempSb.Append(textStyle);
            }
            else
            {
                string imageStyle = this.ImageStyleBuilder.CreateImageGlobalStyle(fobject);
                tempSb.Append(imageStyle);
            }

            if (fobject.IsDrawableType())
            {
                string cornerStyle = CreateCornerGlobalStyle(fobject);
                tempSb.Append(cornerStyle);

                string strokeStyle = CreateStrokeStyle(fobject);
                tempSb.Append(strokeStyle);
            }

            string rawStyle = tempSb
                .ToString()
                .Replace(" ", "")
                .Replace("\n", "")
                .Replace("\r", "");

            int styleHash = rawStyle.GetDeterministicHashCode();

            if (_styles.TryGetValue(styleHash, out string styleName))
            {

            }
            else
            {
                styleName = fobject.Data.Names.UssClassName;

                tempSb.Insert(0, $".{styleName} {{");
                tempSb.AppendLine($"}}");
                tempSb.AppendLine();

                styleBuilder1.Append(tempSb);
                _styles.Add(styleHash, styleName);
            }


            fobject.Data.XmlElement.SetAttribute("class", styleName);
        }

        private string CreateCornerGlobalStyle(FObject fobject)
        {
            StringBuilder styleBuilder = new StringBuilder();

            if (fobject.Data.FRect.IsDefault())
            {
                fobject.Data.FRect = monoBeh.TransformSetter.GetGlobalRect(fobject);
            }

            FRect rect = fobject.Data.FRect;    

            Vector4 radii;

            if (fobject.Type == NodeType.ELLIPSE)
            {
                int ev = 10000;
                radii = new Vector4(ev, ev, ev, ev);
            }
            else
            {
                radii = monoBeh.GraphicHelpers.GetCornerRadius(fobject).Round();
                radii[0] = radii[0].NormalizeAngleToSize(rect.size.x, rect.size.y);
                radii[1] = radii[1].NormalizeAngleToSize(rect.size.x, rect.size.y);
                radii[2] = radii[2].NormalizeAngleToSize(rect.size.x, rect.size.y);
                radii[3] = radii[3].NormalizeAngleToSize(rect.size.x, rect.size.y);
            }

            if (radii != new Vector4(0, 0, 0, 0))
            {
                // Use a single variable when all four corners are equal;
                // fall back to literals when corners differ.
                bool allEqual = radii[0] == radii[1] && radii[1] == radii[2] && radii[2] == radii[3];

                if (allEqual)
                {
                    string rv = UssVariableCollector.CollectRadius(radii[0]);
                    styleBuilder.AddStyle("border-top-left-radius",     rv);
                    styleBuilder.AddStyle("border-top-right-radius",    rv);
                    styleBuilder.AddStyle("border-bottom-right-radius", rv);
                    styleBuilder.AddStyle("border-bottom-left-radius",  rv);
                }
                else
                {
                    styleBuilder.AddStyle("border-top-left-radius",     $"{radii[0]}px");
                    styleBuilder.AddStyle("border-top-right-radius",    $"{radii[3]}px");
                    styleBuilder.AddStyle("border-bottom-right-radius", $"{radii[2]}px");
                    styleBuilder.AddStyle("border-bottom-left-radius",  $"{radii[1]}px");
                }
            }

            return styleBuilder.ToString();
        }

        private string CreateStrokeStyle(FObject fobject)
        {
            StringBuilder styleBuilder = new StringBuilder();

            FGraphic graphic = fobject.Data.Graphic;

            if (graphic.HasStroke)
            {
                int w = (int)fobject.StrokeWeight.Round(0);

                string widthVar = UssVariableCollector.CollectBorderWidth(w);
                styleBuilder.AddStyle("border-left-width",   widthVar);
                styleBuilder.AddStyle("border-right-width",  widthVar);
                styleBuilder.AddStyle("border-top-width",    widthVar);
                styleBuilder.AddStyle("border-bottom-width", widthVar);

                string colorVar = UssVariableCollector.CollectColor(graphic.Stroke.SingleColor);
                styleBuilder.AddStyle("border-left-color",   colorVar);
                styleBuilder.AddStyle("border-right-color",  colorVar);
                styleBuilder.AddStyle("border-top-color",    colorVar);
                styleBuilder.AddStyle("border-bottom-color", colorVar);
            }

            return styleBuilder.ToString();
        }

        private void SetLocalStyle(FObject fobject)
        {
            StringBuilder styleBuilder = new StringBuilder();
            string baseStyle = CreateBaseLocalStyle(fobject);
            styleBuilder.Append(baseStyle);

            string imageStyle = this.ImageStyleBuilder.CreateImageLocalStyle(fobject);
            styleBuilder.Append(imageStyle);

            // Normalize inline style: remove line breaks so XmlDocument does not encode them
            // as &#xD;&#xA; inside the XML attribute, which Unity UXML parser rejects.
            string inlineStyle = styleBuilder
                .ToString()
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ");

            fobject.Data.XmlElement.SetAttribute("style", inlineStyle);
        }

        private void SetAutolayoutPropsForCurrent(FObject fobject, StringBuilder styleBuilder)
        {
            if (fobject.ContainsTag(FcuTag.AutoLayoutGroup))
            {
                var padding = fobject.Data.FRect.padding;
                styleBuilder.AddLocalStyle("padding-left",   $"{padding.left}px");
                styleBuilder.AddLocalStyle("padding-right",  $"{padding.right}px");
                styleBuilder.AddLocalStyle("padding-top",    $"{padding.top}px");
                styleBuilder.AddLocalStyle("padding-bottom", $"{padding.bottom}px");

                // For ScrollView, set the mode attribute and skip GapContainer gap attributes.
                bool isScrollView = fobject.OverflowDirection != OverflowDirection.NONE;
                if (isScrollView)
                {
                    string scrollMode = fobject.OverflowDirection switch
                    {
                        OverflowDirection.HORIZONTAL_SCROLLING              => "Horizontal",
                        OverflowDirection.VERTICAL_SCROLLING                => "Vertical",
                        OverflowDirection.HORIZONTAL_AND_VERTICAL_SCROLLING => "VerticalAndHorizontal",
                        _                                                   => "Vertical"
                    };
                    fobject.Data.XmlElement.SetAttribute("mode", scrollMode);
                    // Hide Unity's default scroll arrows and scrollbar track.
                    fobject.Data.XmlElement.SetAttribute("horizontal-scroller-visibility", "Hidden");
                    fobject.Data.XmlElement.SetAttribute("vertical-scroller-visibility", "Hidden");
                }

                if (fobject.LayoutMode == LayoutMode.VERTICAL)
                {
                    styleBuilder.AddLocalStyle("flex-direction", "column");

                    var childAlignment = fobject.GetVertLayoutAnchor().ToUITK(isHorizontal: false);

                    // In a ScrollView the counter-axis alignment (align-items) must never be
                    // flex-end: Figma's counterAxisAlignItems=MAX maps to flex-end, which in a
                    // vertical scroll layout would push every child to the right edge. Use
                    // flex-start as a safe default so children stack from the left.
                    string alignItems = childAlignment.alignItems;
                    if (isScrollView && alignItems == "flex-end")
                        alignItems = "flex-start";
                    styleBuilder.AddLocalStyle("align-items", $"{alignItems}");

                    // TextAnchor has no space-between equivalent, so override explicitly.
                    if (fobject.PrimaryAxisAlignItems == PrimaryAxisAlignItem.SPACE_BETWEEN)
                        styleBuilder.AddLocalStyle("justify-content", "space-between");
                    else
                        styleBuilder.AddLocalStyle("justify-content", $"{childAlignment.justifyContent}");
                }
                else if (fobject.LayoutMode == LayoutMode.HORIZONTAL)
                {
                    styleBuilder.AddLocalStyle("flex-direction", "row");

                    var childAlignment = fobject.GetHorLayoutAnchor().ToUITK(isHorizontal: true);

                    styleBuilder.AddLocalStyle("align-items", $"{childAlignment.alignItems}");

                    // TextAnchor has no space-between equivalent, so override explicitly.
                    if (fobject.PrimaryAxisAlignItems == PrimaryAxisAlignItem.SPACE_BETWEEN)
                        styleBuilder.AddLocalStyle("justify-content", "space-between");
                    else
                        styleBuilder.AddLocalStyle("justify-content", $"{childAlignment.justifyContent}");
                }

                // Reflect Figma's wrap setting so items can flow onto multiple lines.
                if (fobject.LayoutWrap == LayoutWrap.WRAP)
                {
                    styleBuilder.AddLocalStyle("flex-wrap", "wrap");

                    // SPACE_BETWEEN distributes wrapped rows/columns evenly.
                    if (fobject.CounterAxisAlignContent == CounterAxisAlignContent.SPACE_BETWEEN)
                        styleBuilder.AddLocalStyle("align-content", "space-between");
                }
            }
        }

        private void SetAutolayoutPropsForChild(FObject fobject, StringBuilder styleBuilder)
        {
            FObject parent = fobject.Data.Parent;

            FRect rect = fobject.Data.FRect;
            FRect pRect = parent.Data.FRect;
            bool isText = fobject.Type == NodeType.TEXT;
            bool autoWidthText = isText && fobject.Style.TextAutoResize == TextAutoResize.WIDTH_AND_HEIGHT;
            bool autoHeightText = isText &&
                (fobject.Style.TextAutoResize == TextAutoResize.WIDTH_AND_HEIGHT ||
                 fobject.Style.TextAutoResize == TextAutoResize.HEIGHT);
            bool preserveTextBounds = isText && !autoWidthText && !autoHeightText;

            if (parent.ContainsTag(FcuTag.AutoLayoutGroup)
                && fobject.LayoutPositioning != LayoutPositioning.ABSOLUTE)
            {
                // Normal flow child inside an AutoLayout container.
                styleBuilder.AddLocalStyle("position", "relative");

                bool parentIsScrollView = parent.OverflowDirection != OverflowDirection.NONE;

                if (parent.LayoutMode == LayoutMode.VERTICAL)
                {
                    // Primary axis = vertical → height behaviour.
                    // flex-grow does not work correctly inside a ScrollView because the scroll
                    // container allows infinite content growth — the parent has no bounded height
                    // to distribute. Use a fixed pixel size from absoluteBoundingBox instead.
                    if (fobject.LayoutGrow == 1 && !parentIsScrollView)
                    {
                        styleBuilder.AddLocalStyle("height", "auto");
                        styleBuilder.AddLocalStyle("flex-grow", "1");
                    }
                    else if (fobject.LayoutSizingVertical == "HUG")
                    {
                        if (autoHeightText)
                        {
                            styleBuilder.AddLocalStyle("height", "auto");
                        }
                        else if (preserveTextBounds)
                        {
                            styleBuilder.AddLocalStyle("height", $"{rect.size.y.Round(0)}px");
                            styleBuilder.AddLocalStyle("flex-shrink", "0");
                        }
                        else
                        {
                            styleBuilder.AddLocalStyle("height", "auto");
                        }
                    }
                    else
                    {
                        styleBuilder.AddLocalStyle("height", $"{rect.size.y.Round(0)}px");
                        // FIXED height on the primary axis — prevent flex from shrinking the element.
                        styleBuilder.AddLocalStyle("flex-shrink", "0");
                    }

                    // Counter axis = horizontal → width behaviour.
                    if (fobject.LayoutAlign == LayoutAlign.STRETCH || fobject.LayoutSizingHorizontal == "FILL")
                    {
                        styleBuilder.AddLocalStyle("width", "auto");
                        styleBuilder.AddLocalStyle("align-self", "stretch");
                    }
                    else if (fobject.LayoutSizingHorizontal == "HUG")
                    {
                        if (autoWidthText)
                        {
                            styleBuilder.AddLocalStyle("width", "auto");
                        }
                        else if (preserveTextBounds)
                        {
                            styleBuilder.AddLocalStyle("width", $"{rect.size.x.Round(0)}px");
                        }
                        else
                        {
                            styleBuilder.AddLocalStyle("width", "auto");
                        }
                    }
                    else
                    {
                        styleBuilder.AddLocalStyle("width", $"{rect.size.x.Round(0)}px");
                    }
                }
                else if (parent.LayoutMode == LayoutMode.HORIZONTAL)
                {
                    // Primary axis = horizontal → width behaviour.
                    // Same reasoning as the vertical case above: flex-grow is not reliable in a
                    // ScrollView, so fall back to a fixed pixel width.
                    if (fobject.LayoutGrow == 1 && !parentIsScrollView)
                    {
                        styleBuilder.AddLocalStyle("width", "auto");
                        styleBuilder.AddLocalStyle("flex-grow", "1");
                    }
                    else if (fobject.LayoutSizingHorizontal == "HUG")
                    {
                        if (autoWidthText)
                        {
                            styleBuilder.AddLocalStyle("width", "auto");
                        }
                        else if (preserveTextBounds)
                        {
                            styleBuilder.AddLocalStyle("width", $"{rect.size.x.Round(0)}px");
                            styleBuilder.AddLocalStyle("flex-shrink", "0");
                        }
                        else
                        {
                            styleBuilder.AddLocalStyle("width", "auto");
                        }
                    }
                    else
                    {
                        styleBuilder.AddLocalStyle("width", $"{rect.size.x.Round(0)}px");
                        // FIXED width on the primary axis — prevent flex from shrinking the element.
                        styleBuilder.AddLocalStyle("flex-shrink", "0");
                    }

                    // Counter axis = vertical → height behaviour.
                    if (fobject.LayoutAlign == LayoutAlign.STRETCH || fobject.LayoutSizingVertical == "FILL")
                    {
                        styleBuilder.AddLocalStyle("height", "auto");
                        styleBuilder.AddLocalStyle("align-self", "stretch");
                    }
                    else if (fobject.LayoutSizingVertical == "HUG")
                    {
                        if (autoHeightText)
                        {
                            styleBuilder.AddLocalStyle("height", "auto");
                        }
                        else if (preserveTextBounds)
                        {
                            styleBuilder.AddLocalStyle("height", $"{rect.size.y.Round(0)}px");
                        }
                        else
                        {
                            styleBuilder.AddLocalStyle("height", "auto");
                        }
                    }
                    else
                    {
                        styleBuilder.AddLocalStyle("height", $"{rect.size.y.Round(0)}px");
                    }
                }


                // Apply itemSpacing as static margin on every non-last child of an AutoLayout.
                // For ScrollView children also add flex-shrink: 0 so they do not collapse.
                if (parent.ItemSpacing > 0)
                {
                    if (parentIsScrollView)
                        styleBuilder.AddLocalStyle("flex-shrink", "0");

                    // Determine whether this is the last visible child.
                    bool isLast = true;
                    if (parent.Children != null)
                    {
                        for (int ci = parent.Children.Count - 1; ci >= 0; ci--)
                        {
                            FObject sibling = parent.Children[ci];
                            if (sibling.IsMask.ToBoolNullFalse()) continue;
                            isLast = sibling.Id == fobject.Id;
                            break;
                        }
                    }

                    if (!isLast)
                    {
                        if (parent.LayoutMode == LayoutMode.HORIZONTAL)
                            styleBuilder.AddLocalStyle("margin-right", $"{parent.ItemSpacing.Value.Round(0)}px");
                        else
                            styleBuilder.AddLocalStyle("margin-bottom", $"{parent.ItemSpacing.Value.Round(0)}px");
                    }
                }
            }
            else
            {
                // Outside AutoLayout, or ABSOLUTE-positioned inside AutoLayout:
                // apply Figma constraint-aware CSS so the element adapts when its parent resizes.
                Vector2 strokeOffset = GetParentStrokeOffset(fobject);
                FRect adjustedRect = new FRect
                {
                    position = new Vector2(rect.position.x - strokeOffset.x, rect.position.y - strokeOffset.y),
                    size     = rect.size
                };
                SetConstraintBasedStyle(fobject, adjustedRect, pRect, styleBuilder);
            }
        }

        /// <summary>
        /// Translates Figma constraints (horizontal / vertical) into UITK-compatible CSS.
        /// All elements outside AutoLayout keep <c>position: absolute</c>, but their
        /// left / right / top / bottom / width / height values become adaptive:
        /// <list type="bullet">
        ///   <item>LEFT / TOP  → fixed px offset from the near edge (default behaviour, unchanged)</item>
        ///   <item>RIGHT / BOTTOM → fixed px offset anchored to the far edge</item>
        ///   <item>LEFT_RIGHT / TOP_BOTTOM  → both edges fixed, size becomes <c>auto</c></item>
        ///   <item>CENTER → percentage offset from the near edge (closest approximation in UITK)</item>
        ///   <item>SCALE → percentage offset AND percentage size</item>
        /// </list>
        /// </summary>
        private static void SetConstraintBasedStyle(
            FObject fobject, FRect rect, FRect pRect, StringBuilder sb)
        {
            string h = fobject.Constraints.Horizontal ?? "LEFT";
            string v = fobject.Constraints.Vertical   ?? "TOP";

            // Compute world-space offset between visual centres (rect.position + size/2).
            // This is the only geometrically meaningful pivot for rotation.
            // For an un-rotated parent the result is identical to the old formula:
            //   (rect.pos - pRect.pos) == (childCx - parentCx) - (child_W - parent_W)/2
            float childCx  = rect.position.x  + rect.size.x  * 0.5f;
            float childCy  = rect.position.y  + rect.size.y  * 0.5f;
            float parentCx = pRect.position.x + pRect.size.x * 0.5f;
            float parentCy = pRect.position.y + pRect.size.y * 0.5f;

            float rawDx = childCx - parentCx;
            float rawDy = childCy - parentCy;

            // Use GetAngleFromMatrix (relativeTransform) instead of GetFigmaRotationAngle,
            // because the JSON 'rotation' field can diverge from the relativeTransform matrix
            // for GROUP nodes (e.g. 108° vs 90°). The relativeTransform always gives the true
            // geometric rotation relative to the parent — which is what we need here.
            float parentAngleDeg = GetAccumulatedMatrixAngle(fobject.Data.Parent);
            float parentAngleRad = Mathf.Deg2Rad * parentAngleDeg;

            float localDx, localDy;
            if (Mathf.Abs(parentAngleRad) > 0.0001f)
            {
                // GetAngleFromMatrix returns -atan2 (negated). To invert the parent rotation
                // we use +parentAngleRad (which equals -raw_atan2); this, combined with
                // CSS rotate = -rect.angle (= +raw_atan2), gives a net 0° position effect
                // while producing the correct visual rotation for children.
                float cos = Mathf.Cos(parentAngleRad);
                float sin = Mathf.Sin(parentAngleRad);
                localDx = rawDx * cos - rawDy * sin;
                localDy = rawDx * sin + rawDy * cos;
            }
            else
            {
                localDx = rawDx;
                localDy = rawDy;
            }

            // Convert center-relative offset to CSS top-left.
            // width/height use Figma's own size (pre-rotation) — CSS rotate is a visual-only
            // transform and does not change the layout box size in UITK (same as in CSS).
            float left   = localDx - rect.size.x * 0.5f + pRect.size.x * 0.5f;
            float top    = localDy - rect.size.y * 0.5f + pRect.size.y * 0.5f;
            float right  = pRect.size.x - left - rect.size.x;
            float bottom = pRect.size.y - top  - rect.size.y;




            // For an element that itself has OUTSIDE stroke we must expand the layout
            // box so that UITK's INSIDE-only border renders at the correct visual position
            // (i.e. OUTSIDE the original Figma content area).
            float outsideSw = (fobject.Data.Graphic.HasStroke &&
                               fobject.StrokeAlign == StrokeAlign.OUTSIDE)
                              ? fobject.StrokeWeight : 0f;

            float cssW = rect.size.x + 2f * outsideSw;
            float cssH = rect.size.y + 2f * outsideSw;
            float adjLeft   = left   - outsideSw;
            float adjTop    = top    - outsideSw;
            float adjRight  = right  - outsideSw;
            float adjBottom = bottom - outsideSw;

            // Percentage values are only meaningful when the parent has non-zero dimensions.
            float wPct = pRect.size.x > 0f ? (cssW / pRect.size.x) * 100f : 0f;
            float hPct = pRect.size.y > 0f ? (cssH / pRect.size.y) * 100f : 0f;
            float lPct = pRect.size.x > 0f ? (adjLeft / pRect.size.x) * 100f : 0f;
            float tPct = pRect.size.y > 0f ? (adjTop  / pRect.size.y) * 100f : 0f;

            sb.AddLocalStyle("position", "absolute");

            // Root frames (FcuTag.Frame) sit directly on a Figma Page — their absolute canvas
            // coordinates must not be used as UITK left/top values.
            bool isRootFrame = fobject.ContainsTag(FcuTag.Frame);
            if (isRootFrame)
            {
                sb.AddLocalStyle("left",   "0px");
                sb.AddLocalStyle("right",  "auto");
                sb.AddLocalStyle("width",  $"{cssW.Round(0)}px");
                sb.AddLocalStyle("top",    "0px");
                sb.AddLocalStyle("bottom", "auto");
                sb.AddLocalStyle("height", $"{cssH.Round(0)}px");
                return;
            }


            // --- Horizontal constraint ---
            switch (h)
            {
                case "RIGHT":
                    // Anchor to the right edge; left is not constrained.
                    sb.AddLocalStyle("left",  "auto");
                    sb.AddLocalStyle("right", $"{adjRight.Round(0)}px");
                    sb.AddLocalStyle("width", $"{cssW.Round(0)}px");
                    break;

                case "LEFT_RIGHT":
                    // Both edges are pinned; width adjusts automatically.
                    sb.AddLocalStyle("left",  $"{adjLeft.Round(0)}px");
                    sb.AddLocalStyle("right", $"{adjRight.Round(0)}px");
                    sb.AddLocalStyle("width", "auto");
                    break;

                case "CENTER":
                    // No CSS margin: auto for absolute elements in UITK.
                    // Best approximation: left expressed as a percentage of parent width.
                    sb.AddLocalStyle("left",  $"{lPct.Round(2)}%");
                    sb.AddLocalStyle("right", "auto");
                    sb.AddLocalStyle("width", $"{cssW.Round(0)}px");
                    break;

                case "SCALE":
                    // Both position and size are proportional to the parent.
                    sb.AddLocalStyle("left",  $"{lPct.Round(2)}%");
                    sb.AddLocalStyle("right", "auto");
                    sb.AddLocalStyle("width", $"{wPct.Round(2)}%");
                    break;

                default: // LEFT
                    sb.AddLocalStyle("left",  $"{adjLeft.Round(0)}px");
                    sb.AddLocalStyle("right", "auto");
                    sb.AddLocalStyle("width", $"{cssW.Round(0)}px");
                    break;
            }

            // --- Vertical constraint ---
            switch (v)
            {
                case "BOTTOM":
                    sb.AddLocalStyle("top",    "auto");
                    sb.AddLocalStyle("bottom", $"{adjBottom.Round(0)}px");
                    sb.AddLocalStyle("height", $"{cssH.Round(0)}px");
                    break;

                case "TOP_BOTTOM":
                    sb.AddLocalStyle("top",    $"{adjTop.Round(0)}px");
                    sb.AddLocalStyle("bottom", $"{adjBottom.Round(0)}px");
                    sb.AddLocalStyle("height", "auto");
                    break;

                case "CENTER":
                    sb.AddLocalStyle("top",    $"{tPct.Round(2)}%");
                    sb.AddLocalStyle("bottom", "auto");
                    sb.AddLocalStyle("height", $"{cssH.Round(0)}px");
                    break;

                case "SCALE":
                    sb.AddLocalStyle("top",    $"{tPct.Round(2)}%");
                    sb.AddLocalStyle("bottom", "auto");
                    sb.AddLocalStyle("height", $"{hPct.Round(2)}%");
                    break;

                default: // TOP
                    sb.AddLocalStyle("top",    $"{adjTop.Round(0)}px");
                    sb.AddLocalStyle("bottom", "auto");
                    sb.AddLocalStyle("height", $"{cssH.Round(0)}px");
                    break;
            }
        }

        private static Vector2 GetParentStrokeOffset(FObject fobject)
        {
            FObject parent = fobject.Data.Parent;

            if (!parent.Data.Graphic.HasStroke)
                return Vector2.zero;

            // In Figma the stroke alignment controls how much of the stroke
            // falls inside the element's bounding box and therefore shifts
            // the coordinate origin for its children:
            //   INSIDE  — full stroke width inside  → shift children by +strokeWeight
            //   CENTER  — half inside, half outside  → shift children by +strokeWeight/2
            //   OUTSIDE — stroke is outside the box, but the element's layout box in UITK
            //             is EXPANDED by +strokeWeight on each side, so children must also
            //             be offset by +strokeWeight to remain at their correct position
            //             inside the expanded container.
            float w = parent.StrokeWeight;
            switch (parent.StrokeAlign)
            {
                case StrokeAlign.INSIDE:  return new Vector2(w,        w);
                case StrokeAlign.CENTER:  return new Vector2(w * 0.5f, w * 0.5f);
                // OUTSIDE stroke grows outward and does NOT shift the coordinate
                // origin for children — the Instance expansion in SetConstraintBasedStyle
                // already handles the layout-box offset.
                default:                  return Vector2.zero; // OUTSIDE or unknown
            }
        }

        /// <summary>
        /// Returns the CSS rotate that should be counter-applied to a downloadable sprite
        /// so that its baked image angle is preserved correctly in the UITK hierarchy.
        ///
        /// Key distinction:
        ///   • A rotated intermediate VE inside the template (e.g. "legs" GROUP) has its CSS
        ///     rotate on its own VE element. The PNG for children is exported from Figma already
        ///     accounting for that GROUP's rotation. No counter needed — return 0.
        ///   • The template root (e.g. "chairs" COMPONENT) has its CSS rotate on the outer
        ///     ui:Instance tag, NOT on the template root VE. From inside the template the VE has
        ///     no rotate, but it still cascades visually. A counter IS needed for sprites that
        ///     are direct children of the template root.
        ///
        /// Detection: a template root has Data.Parent.Data == null (it is the top-level node).
        ///            An intermediate GROUP has a non-null grandparent → it's NOT a template root.
        /// </summary>
        private float GetAccumulatedParentCssRotation(FObject fobject)
        {
            // In UITK, CSS rotate cascades through the DOM tree:
            //   ancestorN(rotate) → … → ancestor1(rotate) → this sprite VE
            // Every ancestor with CSS rotate adds to the visual rotation of this element.
            //
            // Downloadable sprites have their absolute orientation baked into the PNG
            // (rect.angle == 0), so the visual rotation from CSS cascade is EXTRA and
            // must be countered.
            //
            // We walk the entire ancestor chain and accumulate the CSS rotate values
            // that each ancestor contributes:
            //   • Non-downloadable VEs/Instances → USS rotate = GetAngleFromMatrix()
            //   • Downloadable VEs → angle forced to 0 → no CSS rotate contribution
            //
            // The returned value represents the total CSS rotation that cascades onto
            // this sprite. The caller negates it to produce the counter-rotation.
            float total = 0f;
            FObject current = fobject.Data.Parent;

            while (current.Data != null)
            {
                bool matchesAnyScope = TryGetMatchingScope(current, out RotationScope matchedScope);
                float ancestorAngle = current.IsDownloadableType()
                    ? 0f
                    : current.GetAngleFromMatrix();
                bool hasContribution = Mathf.Abs(ancestorAngle) > 0.001f;
                bool hadContributionBefore = Mathf.Abs(total) > 0.001f;
                bool skipZeroAngleScopeBarrier =
                    matchesAnyScope &&
                    matchedScope.IncludeRootRotate == false &&
                    !hadContributionBefore &&
                    !hasContribution;
                bool breakAfterCurrentScope =
                    matchesAnyScope &&
                    !skipZeroAngleScopeBarrier;

                if (skipZeroAngleScopeBarrier)
                {
                    current = current.Data.Parent;
                    continue;
                }

                if (hasContribution)
                {
                    // USS rotate on this ancestor = -ancestorAngle (as emitted by
                    // CreateBaseLocalStyle / SetPositionalStyle).
                    total += -ancestorAngle;
                }

                if (breakAfterCurrentScope)
                {
                    break;
                }

                current = current.Data.Parent;
            }

            return total;
        }

        private bool TryGetMatchingScope(FObject current, out RotationScope matchedScope)
        {
            foreach (RotationScope candidate in _rotationScopeRoots)
            {
                if (candidate.Root.Id == current.Id)
                {
                    matchedScope = candidate;
                    return true;
                }
            }

            matchedScope = default;
            return false;
        }
        private static float GetAccumulatedMatrixAngle(FObject fobject)
        {
            float total = 0f;
            FObject current = fobject;

            while (current.Data != null)
            {
                total += current.GetAngleFromMatrix();
                current = current.Data.Parent;
            }

            return total;
        }
        private string CreateBaseLocalStyle(FObject fobject)
        {
            StringBuilder styleBuilder = new StringBuilder();

            if (fobject.Data.FRect.IsDefault())
            {
                fobject.Data.FRect = monoBeh.TransformSetter.GetGlobalRect(fobject);
            }

            FRect rect = fobject.Data.FRect;

            SetAutolayoutPropsForCurrent(fobject, styleBuilder);
            SetAutolayoutPropsForChild(fobject, styleBuilder);

            // Emit USS rotate for non-downloadable elements.
            // GetAngleFromMatrix returns the local matrix angle in the inverse sign convention
            // used by UITK rotate, so emit the negated value.
            // transform-origin defaults to 50% 50% in UITK which matches Figma's pivot (center).
            //
            // Downloadable sprites have rotation baked into the exported PNG (angle == 0).
            // If any ancestor emits a CSS rotate, that cascade would rotate the baked sprite
            // again, so downloadable VEs need a counter-rotation equal to the accumulated
            // parent CSS rotate.
            FcuLogger.Debug(
                $"[BSB-Rotate-VE] {fobject.Data.NameHierarchy} | type:{fobject.Type} | imgType:{fobject.Data.FcuImageType} | angle:{rect.angle} | willRotate:{Mathf.Abs(rect.angle) > 0.001f}",
                FcuDebugSettingsFlags.LogTransform);
            if (Mathf.Abs(rect.angle) > 0.001f)
            {
                styleBuilder.AddLocalStyle("rotate", $"{(-rect.angle).Round(3)}deg");
            }
            else if (fobject.IsDownloadableType())
            {
                AppendDownloadableCounterRotation(fobject, styleBuilder, "BSB-Counter-Leaf");
            }


            if (fobject.IsFrameMask() || fobject.IsClipMask())
            {
                styleBuilder.AddLocalStyle("overflow", "hidden");
            }
            else
            {
                bool find = false;

                foreach (int childIndex in fobject.Data.ChildIndexes)
                {
                    if (monoBeh.CurrentProject.TryGetByIndex(childIndex, out FObject childFO))
                    {
                        if (!childFO.IsMask.ToBoolNullFalse())
                            continue;

                        styleBuilder.AddLocalStyle("overflow", "hidden");

                        if (fobject.IsSprite())
                        {
                            if (!fobject.Data.SpritePath.IsEmpty())
                            {
#if FCU_UITK_EXT_EXISTS
                                this.ImageStyleBuilder.AppendSpriteLocalStyles(fobject, styleBuilder);

                                find = true;
                                break;
#endif
                            }
                        }
                    }
                }

                if (!find)
                {
                    styleBuilder.AddLocalStyle("overflow", "visible");
                }
            }

            if (fobject.IsVisible() == false)
            {
                styleBuilder.AddLocalStyle("display", "none");
            }

            if (fobject.Opacity.HasValue && Mathf.Abs(fobject.Opacity.Value - 1f) > 0.001f)
            {
                styleBuilder.AddLocalStyle("opacity", $"{fobject.Opacity.Value.Round(3)}");
            }

            return styleBuilder.ToString();
        }

        private void AppendDownloadableCounterRotation(FObject fobject, StringBuilder styleBuilder, string logTag)
        {
            float accParentCssRotate = GetAccumulatedParentCssRotation(fobject);
            string scopeName = _rotationScopeRoots.Count > 0
                ? _rotationScopeRoots.Peek().Root.Data.NameHierarchy
                : "<unbounded>";

            FcuLogger.Debug(
                $"[{logTag}] {fobject.Data.NameHierarchy} | type:{fobject.Type} | imgType:{fobject.Data.FcuImageType} | scopeRoot:{scopeName} | accParentRot:{accParentCssRotate} | counterApplied:{Mathf.Abs(accParentCssRotate) > 0.001f}",
                FcuDebugSettingsFlags.LogTransform);

            if (Mathf.Abs(accParentCssRotate) > 0.001f)
            {
                styleBuilder.AddLocalStyle("rotate", $"{(-accParentCssRotate).Round(3)}deg");
            }
        }

        [SerializeField] public ImageStyleBuilder ImageStyleBuilder = new ImageStyleBuilder();
        [SerializeField] public TextStyleBuilder TextStyleBuilder = new TextStyleBuilder();
    }
}
#endif





