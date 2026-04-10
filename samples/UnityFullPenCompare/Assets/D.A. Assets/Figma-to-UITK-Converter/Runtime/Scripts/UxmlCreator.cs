#if FCU_EXISTS
using DA_Assets.FCU.Model;
using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEngine;
using UnityEngine.UIElements;
using DA_Assets.Logging;
using DA_Assets.Shared;

#if ULB_EXISTS
using DA_Assets.ULB;
#endif

namespace DA_Assets.FCU
{
    [Serializable]
    public class UxmlCreator : FcuBase
    {
        // Shared USS project URI — computed once per DrawProject call and reused by all frames
        // and instance templates so every <Style src> value is correctly encoded.
        private string _currentStyleUri;

        private static string _projectName;
        private static string _ussName;
        private const string _uxmlExtension = "uxml";
        private const string _ussExtension = "uss";
        private const string _csExtension = "cs";

        // Per-render context — set by CreateFrameUXML, consumed by DrawFObject → GenerateInstanceTemplate.
        private string _currentOutputFolder;
        private string _currentStyleName;
        private string _currentTemplatesFolder;

        public override void Init(FigmaConverterUnity monoBeh)
        {
            base.Init(monoBeh);
            this.BaseStyleBuilder.Init(monoBeh);
            this.ComponentTemplateWriter.Init(monoBeh);
        }

        public void Draw(FObject virtualPage)
        {
            _projectName = monoBeh.NameSetter.GetFcuName(virtualPage, FcuNameType.Folder);
            _ussName = monoBeh.NameSetter.GetFcuName(virtualPage, FcuNameType.Class);

            Debug.Log(FuitkLocKey.log_instantiate_game_objects.Localize());

            DrawProject(virtualPage);
        }

        private void DrawProject(FObject virtualPage)
        {
            int projectNumber = GetMaxProjectNumber(virtualPage);

            string outputFolder = Path.Combine(monoBeh.Settings.UITK_Settings.UitkOutputPath, $"{_projectName}-{projectNumber}");
            outputFolder.CreateFolderIfNotExists();

            string styleName = $"{_ussName}_Style_{projectNumber}.{_ussExtension}";
            string stylePath = Path.Combine(outputFolder, styleName);

            // Template Resources folder — created lazily on first INSTANCE/COMPONENT encountered.
            string templatesFolder = Path.Combine(outputFolder, "Resources");

            _currentStyleUri = MakeProjectAssetUri(stylePath);

            // Clear USS variable registry before rendering so variables don't leak between imports.
            UssVariableCollector.Clear();

            StringBuilder styleBuilder = new StringBuilder();

            // Ensure the shared style file exists before any UXML is saved.
            // This avoids semantic "invalid asset" warnings when Unity parses a UXML
            // that already references the USS but the USS file has not been written yet.
            if (!File.Exists(stylePath))
            {
                File.WriteAllText(stylePath, string.Empty);
            }

            foreach (FObject frame in virtualPage.Children)
            {
                FObject vPage = virtualPage;
                vPage.Children = new List<FObject> { frame };

                string uxmlPath = CreateFrameUXML(vPage, styleBuilder, projectNumber, outputFolder, styleName, templatesFolder);
                frame.Data.Names.UxmlPath = uxmlPath;
            }

            // Prepend :root { } block with collected CSS custom properties (colors, font-sizes, spacing).
            string rootBlock = UssVariableCollector.GenerateRootBlock();
            // Append a patch that forces flex-direction:row on horizontal ScrollView content containers.
            // Unity does not reliably apply this via the --horizontal USS modifier class in all versions.
            const string horizontalScrollFix =
                "\n/* --- Converter patch: horizontal ScrollView content-container fix --- */\n" +
                ".unity-scroll-view__content-container--horizontal {\n" +
                "    flex-direction: row;\n" +
                "}\n";

            // Overwrite the placeholder with the actual accumulated USS content.
            File.WriteAllText(stylePath, rootBlock + styleBuilder.ToString() + horizontalScrollFix);
        }

        /// <summary>
        /// Generates a standalone template UXML file for an INSTANCE or COMPONENT node.
        /// INSTANCE and COMPONENT are equivalent in this system — deduplicated by Data.Hash.
        /// The file is written to the Resources sub-folder so it can be referenced
        /// from any frame UXML in the same project folder.
        ///
        /// The template contains a ROOT wrapper element (VisualElement / GapContainer /
        /// FcuGradientElement) carrying ALL visual styles of the node (padding, background,
        /// border-radius, gradient-data, etc.).  The <ui:Instance> in the parent UXML
        /// carries ONLY positional styles (position, left, top, width, height).
        /// </summary>
        private string GenerateInstanceTemplate(FObject instance, StringBuilder styleBuilder,
            string templatesFolder, string styleName, string outputFolder)
        {
            string alias = GetTemplateAlias(instance, GetTemplateVariantKey(instance));
            string templateFileName = $"{alias}.{_uxmlExtension}";
            string templatePath = Path.Combine(templatesFolder, templateFileName);

            XmlDocument doc = new XmlDocument();
            XmlElement root = CreateRootXmlElement(doc);

            // Link the shared style sheet — URI pre-computed via URIHelpersRef.MakeAssetUri.
            XmlElement styleElement = doc.CreateElement("Style");
            styleElement.SetAttribute("src", _currentStyleUri);
            root.AppendChild(styleElement);

            // Root must be in the document BEFORE DrawFObject so doc.DocumentElement is not null.
            doc.AppendChild(root);

            this.BaseStyleBuilder.PushRotationScopeRoot(instance, includeRootRotate: false);
            try
            {
                if (instance.Data.FRect.IsDefault())
                {
                    instance.Data.FRect = monoBeh.TransformSetter.GetGlobalRect(instance);
                }

                // Build the wrapper element that represents the node itself inside the template.
                // It carries all visual styles (padding, background, gradient-data, etc.).
                instance.Data.UitkType = GetUitkType(instance);
                instance.Data.XmlElement = CreateXmlElement(instance, doc);
                this.BaseStyleBuilder.SetStyle(instance, styleBuilder);
                // The wrapper lives inside <ui:Instance> which already provides position and size.
                // Strip canvas-level absolute coordinates so the wrapper fills its container.
                OverrideWrapperPositionToRelative(instance, instance.Data.XmlElement);

                root.AppendChild(instance.Data.XmlElement);

                // DrawFObject will append children INTO the wrapper (instance.Data.XmlElement).
                DrawFObject(instance, doc, styleBuilder, new List<FObject>());

                doc.Save(templatePath);
            }
            finally
            {
                this.BaseStyleBuilder.PopRotationScopeRoot();
            }

            Debug.Log($"[UITK] Instance template generated: {templateFileName}");
            return templatePath;
        }

        private string CreateFrameUXML(FObject virtualPage, StringBuilder styleBuilder, int projectNumber, string outputFolder, string styleName, string templatesFolder)
        {
            string frameName = virtualPage.Children.First().Data.Names.MethodName;
            string frameUxmlPath = Path.Combine(outputFolder, $"{frameName}-{projectNumber}.{_uxmlExtension}");
            string className = $"{frameName}_{projectNumber}";
            string scriptName = $"{className}.{_csExtension}";
            string scriptPath = Path.Combine(outputFolder, scriptName);

            // Store context so DrawFObject can call GenerateInstanceTemplate without extra params.
            _currentOutputFolder = outputFolder;
            _currentStyleName = styleName;
            _currentTemplatesFolder = templatesFolder;

            XmlDocument doc = new XmlDocument();
            XmlElement root = CreateRootXmlElement(doc);
            virtualPage.Data.XmlElement = root;

            XmlElement styleElement = doc.CreateElement("Style");
            // _currentStyleUri is set by DrawProject via URIHelpersRef.MakeAssetUri — it handles
            // correct percent-encoding of spaces and other special characters in the path.
            styleElement.SetAttribute("src", _currentStyleUri);
            root.AppendChild(styleElement);

            // Append root to doc BEFORE DrawFObject so doc.DocumentElement is not null
            // when DrawFObject tries to insert <ui:Template> declarations.
            doc.AppendChild(root);

            FObject frameRoot = virtualPage.Children.FirstOrDefault();
            this.BaseStyleBuilder.PushRotationScopeRoot(frameRoot, includeRootRotate: true);
            try
            {
                DrawFObject(virtualPage, doc, styleBuilder, new List<FObject>());

                doc.Save(frameUxmlPath);
            }
            finally
            {
                this.BaseStyleBuilder.PopRotationScopeRoot();
            }

            return frameUxmlPath;
        }

        private XmlElement CreateRootXmlElement(XmlDocument doc)
        {
            XmlElement root = doc.CreateElement("ui", "UXML", "UnityEngine.UIElements");
            root.SetAttribute("xmlns:uie", "UnityEditor.UIElements");

#if ULB_EXISTS
            if (monoBeh.Settings.UITK_Settings.UitkLinkingMode == UitkLinkingMode.Guid || monoBeh.Settings.UITK_Settings.UitkLinkingMode == UitkLinkingMode.Guids)
            {
                Type type = typeof(UitkLinkerBase);
                root.SetAttribute("xmlns:uida", type.Namespace);
            }
#endif

            root.SetAttribute("xsi", "http://www.w3.org/2001/XMLSchema-instance");
            root.SetAttribute("engine", "UnityEngine.UIElements");
            root.SetAttribute("fcu", typeof(UxmlCreator).Namespace); // DA_Assets.FCU

            return root;
        }

        private static string MakeProjectAssetUri(string assetPath)
        {
            string normalizedPath = assetPath.Replace("\\", "/");
            return $"project://database/{Uri.EscapeUriString(normalizedPath)}";
        }

        private static void RemoveInlineStyleProperty(XmlElement element, string propertyName)
        {
            string currentStyle = element.GetAttribute("style") ?? "";

            var filteredParts = currentStyle
                .Split(';')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .Where(p =>
                {
                    int colonIdx = p.IndexOf(':');
                    if (colonIdx < 0)
                        return true;

                    string prop = p.Substring(0, colonIdx).Trim();
                    return !prop.Equals(propertyName, StringComparison.OrdinalIgnoreCase);
                });

            string nextStyle = string.Join("; ", filteredParts);

            if (nextStyle.Length > 0 && !nextStyle.EndsWith(";"))
            {
                nextStyle += ";";
            }

            element.SetAttribute("style", nextStyle);
        }

        private void DrawFObject(FObject parent, XmlDocument doc, StringBuilder styleBuilder, List<FObject> drawn)
        {
            foreach (FObject fobject in parent.Children)
            {
                if (fobject.Data.IsEmpty)
                    continue;

                if (fobject.IsMask.ToBoolNullFalse())
                    continue;

                // INSTANCE and COMPONENT nodes are equivalent — deduplicated by Data.Hash.
                // Other types (FRAME, GROUP, etc.) are never templated even if their hash matches.
                if ((fobject.Type == NodeType.INSTANCE || fobject.Type == NodeType.COMPONENT)
                    && !fobject.Children.IsEmpty())
                {
                    string variantKey = GetTemplateVariantKey(fobject);
                    string alias;
                    string templatePath;

                    if (!this.ComponentTemplateWriter.TryGetRegistered(variantKey, out alias, out templatePath))
                    {
                        // First encounter of this hash — generate the template from the node's children.
                        alias = GetTemplateAlias(fobject, variantKey);
                        // Ensure the Resources folder exists (lazy creation).
                        _currentTemplatesFolder.CreateFolderIfNotExists();
                        templatePath = GenerateInstanceTemplate(fobject, styleBuilder, _currentTemplatesFolder, _currentStyleName, _currentOutputFolder);
                        this.ComponentTemplateWriter.Register(variantKey, alias, templatePath);
                    }

                    // Declare the template at doc root level (once per alias per document).
                    XmlElement docRoot = doc.DocumentElement;
                    bool alreadyDeclared = false;
                    foreach (XmlNode node in docRoot.ChildNodes)
                    {
                        if (node is XmlElement el &&
                            el.LocalName == "Template" &&
                            el.GetAttribute("name") == alias)
                        {
                            alreadyDeclared = true;
                            break;
                        }
                    }

                    if (!alreadyDeclared)
                    {
                        XmlElement templateDecl = doc.CreateElement("ui", "Template", "UnityEngine.UIElements");
                        templateDecl.SetAttribute("name", alias);
                        templateDecl.SetAttribute("src", MakeProjectAssetUri(templatePath));
                        // Insert after <Style> (first child).
                        docRoot.InsertAfter(templateDecl, docRoot.FirstChild);
                    }

                    // <ui:Instance> carries ONLY positional styles — all visual styles live
                    // inside the template wrapper element (GenerateInstanceTemplate).
                    fobject.Data.UitkType = "Instance";
                    fobject.Data.XmlElement = doc.CreateElement("ui", "Instance", "UnityEngine.UIElements");
                    fobject.Data.XmlElement.SetAttribute("template", alias);
                    fobject.Data.XmlElement.SetAttribute("name", fobject.Name);
                    this.BaseStyleBuilder.SetPositionalStyle(fobject);

                    // Intrinsic downloadable templates keep their corrective rotate on the
                    // wrapper inside the template. Repeating that rotate on the outer
                    // ui:Instance double-applies it and breaks cases like chair legs.
                    if (ShouldPreserveIntrinsicSpriteBox(fobject, out _, out _, out _, out _))
                    {
                        RemoveInlineStyleProperty(fobject.Data.XmlElement, "rotate");
                    }

                    if (fobject.Data.Parent.Data.XmlElement != null)
                        fobject.Data.Parent.Data.XmlElement.AppendChild(fobject.Data.XmlElement);

                    drawn.Add(fobject);
                    // Do NOT recurse — the template UXML handles the internal structure.
                    continue;
                }

                fobject.Data.UitkType = GetUitkType(fobject);
                fobject.Data.XmlElement = CreateXmlElement(fobject, doc);

                this.BaseStyleBuilder.SetStyle(fobject, styleBuilder);

                if (fobject.Data.Parent.Data.XmlElement != null)
                    fobject.Data.Parent.Data.XmlElement.AppendChild(fobject.Data.XmlElement);

                drawn.Add(fobject);

                if (fobject.Children.IsEmpty())
                    continue;

                DrawFObject(fobject, doc, styleBuilder, drawn);
            }
        }

        private XmlElement CreateXmlElement(FObject fobject, XmlDocument doc)
        {
#if ULB_EXISTS
            Type type = typeof(UitkLinkerBase);

            if (monoBeh.Settings.UITK_Settings.UitkLinkingMode == UitkLinkingMode.Guid || monoBeh.Settings.UITK_Settings.UitkLinkingMode == UitkLinkingMode.Guids)
            {
                return doc.CreateElement("uida", fobject.Data.UitkType, type.Namespace);
            }
            else
#endif
            if (fobject.LayoutMode != LayoutMode.NONE)
            {
                // ScrollView is a standard UITK element — use the "ui" prefix.
                if (fobject.OverflowDirection != OverflowDirection.NONE)
                    return doc.CreateElement("ui", fobject.Data.UitkType, "UnityEngine.UIElements");

                // All non-scrollable AutoLayout containers are now plain VisualElements.
                return doc.CreateElement("ui", fobject.Data.UitkType, "UnityEngine.UIElements");
            }
            else
            {
                return doc.CreateElement("ui", fobject.Data.UitkType, "UnityEngine.UIElements");
            }
        }

        private int GetMaxProjectNumber(FObject virtualPage)
        {
            int maxNumber = 0;

            foreach (FObject frame in virtualPage.Children)
            {
                string frameName = frame.Data.Names.MethodName;
                int mn = AssetTools.GetMaxFileNumber(monoBeh.Settings.UITK_Settings.UitkOutputPath, frameName, _uxmlExtension);

                if (mn > maxNumber)
                {
                    maxNumber = mn;
                }
            }

            int newNumber = maxNumber + 1;
            return newNumber;
        }

        public string GetUitkType(FObject fobject)
        {
#if ULB_EXISTS
            if (monoBeh.Settings.UITK_Settings.UitkLinkingMode == UitkLinkingMode.Guid)
            {
                if (fobject.Type == NodeType.TEXT)
                    return nameof(LabelG);
                else
                    return nameof(VisualElementG);
            }
            else
#endif
            {
                if (fobject.Type == NodeType.TEXT)
                {
                    return nameof(Label);
                }
                else if (fobject.LayoutMode != LayoutMode.NONE)
                {
                    // Scrollable AutoLayout — use ScrollView instead of VisualElement.
                    if (fobject.OverflowDirection != OverflowDirection.NONE)
                        return "ScrollView";

                    return nameof(VisualElement);
                }
                else
                {
                    return nameof(VisualElement);
                }
            }
        }

        private static void OverrideWrapperPositionToRelative(FObject fobject, XmlElement element)
        {
            string currentStyle = element.GetAttribute("style") ?? "";
            bool preserveIntrinsicSpriteBox = ShouldPreserveIntrinsicSpriteBox(fobject, out float left, out float top, out float width, out float height);
            bool preserveWrapperRotate = preserveIntrinsicSpriteBox && fobject.IsDownloadableType();

            var parts = currentStyle
                .Split(';')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            // The template root VE lives inside a TemplateContainer whose size and position
            // are fully determined by the <ui:Instance> tag in the parent UXML.
            // The root VE must simply FILL this container — no Figma constraint logic,
            // no stroke-compensation offsets, no canvas-level coordinates.
            //
            // Strategy: position:absolute with all four edges pinned to 0 guarantees the
            // root VE always stretches to match its TemplateContainer regardless of what
            // Figma constraints the original node had.
            var result = new System.Collections.Generic.List<string>();

            if (preserveIntrinsicSpriteBox)
            {
                result.Add("position: absolute");
                result.Add($"left: {left.Round(0)}px");
                result.Add("right: auto");
                result.Add($"width: {width.Round(0)}px");
                result.Add($"top: {top.Round(0)}px");
                result.Add("bottom: auto");
                result.Add($"height: {height.Round(0)}px");
            }
            else
            {
                result.Add("position: absolute");
                result.Add("left: 0px");
                result.Add("right: 0px");
                result.Add("top: 0px");
                result.Add("bottom: 0px");
            }

            // Preserve all non-positional properties (padding, flex-direction, align-items,
            // background, overflow, etc.).
            foreach (string part in parts)
            {
                int colonIdx = part.IndexOf(':');
                if (colonIdx < 0) { result.Add(part); continue; }
                string prop = part.Substring(0, colonIdx).Trim().ToLowerInvariant();
                switch (prop)
                {
                    case "position":
                    case "left": case "right": case "top": case "bottom":
                    case "width": case "height":
                        break; // replaced above or dropped (rotate belongs on ui:Instance)
                    case "rotate":
                        if (preserveWrapperRotate)
                        {
                            result.Add(part);
                        }
                        break;
                    default:
                        result.Add(part);
                        break;
                }
            }

            element.SetAttribute("style", string.Join("; ", result) + ";");
        }

        private static bool ShouldPreserveIntrinsicSpriteBox(
            FObject fobject,
            out float left,
            out float top,
            out float width,
            out float height)
        {
            left = 0f;
            top = 0f;
            width = 0f;
            height = 0f;

            if (!fobject.IsDownloadableType())
                return false;

            if (!HasRotatedAncestor(fobject))
                return false;

            if (fobject.Data.SpriteSize.x <= 0 || fobject.Data.SpriteSize.y <= 0)
                return false;

            float scale = fobject.Data.Scale > 0f ? fobject.Data.Scale : 1f;
            width = fobject.Data.SpriteSize.x / scale;
            height = fobject.Data.SpriteSize.y / scale;

            Vector2 layoutSize = fobject.Data.FRect.size;

            bool sizeDiffers =
                Mathf.Abs(layoutSize.x - width) > 0.5f ||
                Mathf.Abs(layoutSize.y - height) > 0.5f;

            if (!sizeDiffers)
                return false;

            left = (layoutSize.x - width) * 0.5f;
            top = (layoutSize.y - height) * 0.5f;
            return true;
        }

        private static string GetTemplateVariantKey(FObject fobject)
        {
            if (ShouldPreserveIntrinsicSpriteBox(fobject, out float left, out float top, out float width, out float height))
            {
                return string.Join("|",
                    fobject.Data.Hash,
                    "intrinsic",
                    left.Round(3),
                    top.Round(3),
                    width.Round(3),
                    height.Round(3));
            }

            return $"{fobject.Data.Hash}|stretch";
        }

        private static string GetTemplateAlias(FObject fobject, string variantKey)
        {
            int variantHash = variantKey.GetDeterministicHashCode();
            return $"{fobject.Data.Names.ObjectName}_{variantHash}";
        }

        private static bool HasRotatedAncestor(FObject fobject)
        {
            FObject current = fobject.Data.Parent;

            while (current.Data != null)
            {
                if (Mathf.Abs(current.GetAngleFromMatrix()) > 0.001f)
                    return true;

                current = current.Data.Parent;
            }

            return false;
        }

        [SerializeField] public BaseStyleBuilder BaseStyleBuilder = new BaseStyleBuilder();
        [SerializeField] public ComponentTemplateWriter ComponentTemplateWriter = new ComponentTemplateWriter();
    }
}
#endif
