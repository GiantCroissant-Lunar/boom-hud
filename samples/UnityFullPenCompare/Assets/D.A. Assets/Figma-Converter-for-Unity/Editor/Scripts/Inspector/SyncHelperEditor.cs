using DA_Assets.DAI;
using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DA_Assets.FCU
{
    [CustomEditor(typeof(SyncHelper)), CanEditMultipleObjects]
    internal class SyncHelperEditor : Editor
    {
        [SerializeField] DAInspectorUITK _uitk;
        private FigmaConverterUnity fcu;
        private SyncHelper syncHelper;
        private static bool _sortActiveFirst = false;

        private void OnEnable()
        {
            syncHelper = (SyncHelper)target;

            if (syncHelper.Data != null)
            {
                fcu = syncHelper.Data.FigmaConverterUnity as FigmaConverterUnity;
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = _uitk.CreateRoot(_uitk.ColorScheme.BG);

            var debugToggle = _uitk.Toggle(FcuLocKey.common_label_debug.Localize());
            debugToggle.value = syncHelper.Debug;

            var defaultInspectorContainer = BuildDefaultInspectorContainer();

            RegisterDebugToggle(debugToggle, defaultInspectorContainer);

            if (fcu == null)
            {
                root.Add(_uitk.HelpBox(new HelpBoxData
                {
                    Message = FcuLocKey.label_fcu_is_null.Localize(
                        nameof(FigmaConverterUnity),
                        FcuConfig.CreatePrefabs,
                        FcuConfig.SetFcuToSyncHelpers),
                    MessageType = MessageType.Warning
                }));
            }

            if (syncHelper.Data != null)
            {
                var outerHeader = BuildInfoCard(debugToggle, defaultInspectorContainer, out var outerArrow);

                var outerBody = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        paddingTop = DAI_UitkConstants.MarginPadding
                    }
                };

                BuildReasonsSection(outerBody);

                var outerFoldout = new AnimatedFoldout(
                    "SyncHelper_Main",
                    outerHeader,
                    outerBody,
                    startExpanded: true,
                    _uitk.FoldoutCurve,
                    _uitk.FoldoutDuration,
                    _uitk.ColorScheme.BG);

                outerFoldout.Toggled += expanded =>
                {
                    outerArrow.text = expanded ? "▼" : "▶";
                };

                root.Add(outerFoldout);
            }

            root.Add(_uitk.Space10());

            // Info help box
            root.Add(_uitk.HelpBox(new HelpBoxData
            {
                Message = FcuLocKey.label_dont_remove_fcu_meta.Localize(),
                MessageType = MessageType.Info,
                FontSize = (int)DAI_UitkConstants.FontSizeTiny
            }));

            root.Add(_uitk.Space5());

            root.Add(defaultInspectorContainer);

            return root;
        }

        private VisualElement BuildInfoCard(Toggle debugToggle, VisualElement defaultInspectorContainer, out Label arrowLabel)
        {
            var card = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    backgroundColor = _uitk.ColorScheme.GROUP
                }
            };

            UIHelpers.SetDefaultPadding(card);
            UIHelpers.SetRadius(card, DAI_UitkConstants.CornerRadius);
            UIHelpers.SetBorderWidth(card, DAI_UitkConstants.BorderWidth);
            UIHelpers.SetBorderColor(card, _uitk.ColorScheme.OUTLINE);

            // Arrow + hierarchy name row (no wrap - arrow stays on same line)
            arrowLabel = new Label("▼")
            {
                style =
                {
                    fontSize = DAI_UitkConstants.FontSizeTiny,
                    color = _uitk.ColorScheme.TEXT_SECOND,
                    marginRight = 6,
                    flexShrink = 0
                }
            };

            if (!syncHelper.Data.NameHierarchy.IsEmpty())
            {
                var headerRow = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center
                    }
                };

                headerRow.Add(arrowLabel);

                var hierarchyLabel = new Label(syncHelper.Data.NameHierarchy)
                {
                    style =
                    {
                        whiteSpace = WhiteSpace.Normal,
                        fontSize = DAI_UitkConstants.FontSizeNormal,
                        color = _uitk.ColorScheme.TEXT,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        flexShrink = 1
                    }
                };

                // Make clickable if we have Figma URL
                if (!syncHelper.Data.ProjectId.IsEmpty() && !syncHelper.Data.Id.IsEmpty())
                    RegisterHierarchyLabelContextMenu(hierarchyLabel);

                headerRow.Add(hierarchyLabel);

                // Spacer to push tags to the right
                var spacer = new VisualElement
                {
                    style =
                    {
                        flexGrow = 1
                    }
                };
                headerRow.Add(spacer);

                // Tags as badges (right-aligned)
                if (syncHelper.Data.Tags != null && syncHelper.Data.Tags.Count > 0)
                {
                    foreach (FcuTag tag in syncHelper.Data.Tags)
                    {
                        var badge = new Label(tag.ToString())
                        {
                            style =
                            {
                                fontSize = DAI_UitkConstants.FontSizeTiny,
                                backgroundColor = _uitk.ColorScheme.BUTTON,
                                color = _uitk.ColorScheme.TEXT_SECOND,
                                paddingLeft = 6,
                                paddingRight = 6,
                                paddingTop = 2,
                                paddingBottom = 2,
                                marginLeft = 4,
                                flexShrink = 0,
                                unityTextAlign = TextAnchor.MiddleCenter
                            }
                        };

                        UIHelpers.SetRadius(badge, 4f);
                        UIHelpers.SetBorderWidth(badge, DAI_UitkConstants.BorderWidth);
                        UIHelpers.SetBorderColor(badge, _uitk.ColorScheme.OUTLINE);
                        headerRow.Add(badge);
                    }
                }

                card.Add(headerRow);
            }

            debugToggle.style.marginTop = DAI_UitkConstants.MarginPadding;
            card.Add(debugToggle);
            card.Add(defaultInspectorContainer);

            return card;
        }

        private void BuildReasonsSection(VisualElement root)
        {
            var reasonCategories = CollectReasonCategories();

            if (reasonCategories.Count == 0)
                return;

            int totalReasons = reasonCategories.Sum(c => c.items.Length);
            var header = BuildReasonsHeader(totalReasons, out var arrowLabel, out var sortButton, out var sortIcon);

            // Foldout body
            var body = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    paddingTop = DAI_UitkConstants.MarginPadding
                }
            };

            // Initial build
            RebuildReasonsBody(body, reasonCategories);

            // Sort click handler: intercept in TrickleDown phase to prevent foldout toggle
            sortButton.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopImmediatePropagation();
                _sortActiveFirst = !_sortActiveFirst;
                sortIcon.style.color = _sortActiveFirst
                    ? _uitk.ColorScheme.ACCENT_SECOND
                    : _uitk.ColorScheme.TEXT_SECOND;
                RebuildReasonsBody(body, reasonCategories);
            }, TrickleDown.TrickleDown);

            // Create animated foldout
            var foldout = new AnimatedFoldout(
                "SyncHelper_Reasons",
                header,
                body,
                startExpanded: true,
                _uitk.FoldoutCurve,
                _uitk.FoldoutDuration,
                _uitk.ColorScheme.BG);

            // Update arrow on toggle
            foldout.Toggled += expanded =>
            {
                arrowLabel.text = expanded ? "▼" : "▶";
            };

            root.Add(foldout);
        }

        // Collects all reason categories grouped by tag and pipeline prefix.
        private List<(string title, (string key, string desc, bool isActive)[] items)> CollectReasonCategories()
        {
            var result = new List<(string title, (string key, string desc, bool isActive)[] items)>();

            if (syncHelper.Data.Reasons == null || syncHelper.Data.Reasons.Count == 0)
                return result;

            // Per-tag reasons: grouped by relatedTag name
            var tagReasons = syncHelper.Data.Reasons
                .Where(r => r.key != ReasonKey.None && r.relatedTag != FcuTag.None)
                .GroupBy(r => r.relatedTag.ToString())
                .OrderBy(g => g.Key);

            foreach (var group in tagReasons)
            {
                var items = group
                    .Select(r => (
                        key: r.key.ToString(),
                        desc: r.key.GetDescription(syncHelper.Data),
                        isActive: syncHelper.Data.Tags.Contains(r.relatedTag)))
                    .ToArray();
                result.Add((group.Key, items));
            }

            // Pipeline reasons: grouped by enum name prefix
            var pipelineReasons = syncHelper.Data.Reasons
                .Where(r => r.key != ReasonKey.None && r.relatedTag == FcuTag.None)
                .GroupBy(r => r.key.GetReasonGroupFromPrefix())
                .OrderBy(g => g.Key);

            foreach (var group in pipelineReasons)
            {
                var items = group
                    .Select(r => (
                        key: r.key.ToString(),
                        desc: r.key.GetDescription(syncHelper.Data),
                        isActive: true)) // Pipeline reasons always describe an active decision
                    .ToArray();
                result.Add((group.Key, items));
            }

            return result;
        }

        // Builds the foldout header row: arrow, title, count badge, spacer, sort button.
        private VisualElement BuildReasonsHeader(int totalReasons, out Label arrowLabel, out VisualElement sortButton, out Label sortIcon)
        {
            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = _uitk.ColorScheme.GROUP,
                    height = DAI_UitkConstants.ButtonHeight
                }
            };
            UIHelpers.SetDefaultPadding(header);
            UIHelpers.SetRadius(header, DAI_UitkConstants.CornerRadius);
            UIHelpers.SetBorderWidth(header, DAI_UitkConstants.BorderWidth);
            UIHelpers.SetBorderColor(header, _uitk.ColorScheme.OUTLINE);

            arrowLabel = new Label("▼")
            {
                style =
                {
                    fontSize = DAI_UitkConstants.FontSizeTiny,
                    color = _uitk.ColorScheme.TEXT_SECOND,
                    marginRight = 6,
                    alignSelf = Align.Center,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            header.Add(arrowLabel);

            header.Add(new Label("Reasons")
            {
                style =
                {
                    fontSize = DAI_UitkConstants.FontSizeNormal,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = _uitk.ColorScheme.TEXT,
                    alignSelf = Align.Center,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            });

            var countBadge = new Label(totalReasons.ToString())
            {
                style =
                {
                    fontSize = DAI_UitkConstants.FontSizeTiny,
                    backgroundColor = _uitk.ColorScheme.ACCENT_SECOND,
                    color = Color.white,
                    paddingLeft = 6, paddingRight = 6,
                    paddingTop = 3, paddingBottom = 3,
                    marginLeft = 8, minWidth = 18, minHeight = 18,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    alignSelf = Align.Center
                }
            };
            UIHelpers.SetRadius(countBadge, 9f);
            header.Add(countBadge);

            header.Add(new VisualElement { style = { flexGrow = 1 } });

            sortButton = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    paddingLeft = 16, paddingRight = 8,
                    paddingTop = 4, paddingBottom = 4,
                    alignSelf = Align.Stretch
                }
            };

            sortIcon = new Label("↕")
            {
                style =
                {
                    fontSize = DAI_UitkConstants.FontSizeNormal,
                    color = _sortActiveFirst ? _uitk.ColorScheme.ACCENT_SECOND : _uitk.ColorScheme.TEXT_SECOND,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            sortButton.Add(sortIcon);
            header.Add(sortButton);

            return header;
        }

        // Clears and repopulates the reasons body container respecting the current sort flag.
        private void RebuildReasonsBody(
            VisualElement body,
            List<(string title, (string key, string desc, bool isActive)[] items)> categories)
        {
            body.Clear();
            var sorted = _sortActiveFirst
                ? categories.OrderByDescending(c => c.items.Any(i => i.isActive)).ToList()
                : categories;
            foreach (var (title, items) in sorted)
                body.Add(BuildCategoryContainer(title, items));
        }

        // Builds a styled card for one reason category with a title and all its item panels.
        private VisualElement BuildCategoryContainer(string title, (string key, string desc, bool isActive)[] items)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    backgroundColor = _uitk.ColorScheme.GROUP,
                    marginBottom = DAI_UitkConstants.SpacingXXS * 3
                }
            };
            UIHelpers.SetPadding(container, DAI_UitkConstants.MarginPadding);
            UIHelpers.SetRadius(container, DAI_UitkConstants.CornerRadius);
            UIHelpers.SetBorderWidth(container, DAI_UitkConstants.BorderWidth);
            UIHelpers.SetBorderColor(container, _uitk.ColorScheme.OUTLINE);

            var catTitle = new Label(title)
            {
                style =
                {
                    fontSize = DAI_UitkConstants.FontSizeNormal,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = _uitk.ColorScheme.TEXT,
                    marginBottom = DAI_UitkConstants.SpacingXXS,
                    marginLeft = 2
                }
            };
            container.Add(catTitle);

            foreach (var (key, desc, isActive) in items)
                container.Add(BuildReasonItemPanel(key, desc, isActive));

            return container;
        }

        // Builds a single reason row: description label + spacer + active/inactive key badge.
        private VisualElement BuildReasonItemPanel(string key, string desc, bool isActive)
        {
            var panel = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart,
                    backgroundColor = _uitk.ColorScheme.BUTTON,
                    marginBottom = DAI_UitkConstants.SpacingXXS,
                    paddingTop = 5,
                    paddingBottom = 5,
                    paddingLeft = 8,
                    paddingRight = 8
                }
            };
            UIHelpers.SetRadius(panel, 4f);
            UIHelpers.SetBorderWidth(panel, DAI_UitkConstants.BorderWidth);
            UIHelpers.SetBorderColor(panel, _uitk.ColorScheme.OUTLINE);

            panel.Add(new Label(desc)
            {
                style =
                {
                    fontSize = DAI_UitkConstants.FontSizeNormal,
                    color = _uitk.ColorScheme.TEXT_SECOND,
                    whiteSpace = WhiteSpace.Normal,
                    flexShrink = 1
                }
            });

            panel.Add(new VisualElement { style = { flexGrow = 1 } });

            var keyBadge = new Label(key)
            {
                style =
                {
                    fontSize = DAI_UitkConstants.FontSizeTiny,
                    backgroundColor = isActive
                        ? new Color(_uitk.ColorScheme.ACCENT_SECOND.r, _uitk.ColorScheme.ACCENT_SECOND.g, _uitk.ColorScheme.ACCENT_SECOND.b, 0.25f)
                        : _uitk.ColorScheme.GROUP,
                    color = isActive ? Color.white : _uitk.ColorScheme.TEXT_SECOND,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = 2,
                    marginLeft = 8,
                    flexShrink = 0,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            UIHelpers.SetRadius(keyBadge, 4f);
            UIHelpers.SetBorderWidth(keyBadge, DAI_UitkConstants.BorderWidth);
            UIHelpers.SetBorderColor(keyBadge, _uitk.ColorScheme.OUTLINE);
            panel.Add(keyBadge);

            return panel;
        }

        // Builds a serialized property inspector for all non-script fields, hidden by default.
        private VisualElement BuildDefaultInspectorContainer()
        {
            var container = new VisualElement();
            InspectorElement.FillDefaultInspector(container, serializedObject, this);
            container.style.display = syncHelper.Debug ? DisplayStyle.Flex : DisplayStyle.None;
            return container;
        }

        // Registers the debug toggle callback: records undo, syncs visibility, and marks dirty.
        private void RegisterDebugToggle(Toggle debugToggle, VisualElement container)
        {
            debugToggle.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(syncHelper, "Toggle Debug");
                syncHelper.Debug = evt.newValue;
                container.style.display = syncHelper.Debug ? DisplayStyle.Flex : DisplayStyle.None;
                EditorUtility.SetDirty(syncHelper);
            });
        }

        // Registers a right-click context menu on the hierarchy label with Copy and Open Figma URL actions.
        private void RegisterHierarchyLabelContextMenu(Label label)
        {
            label.tooltip = FcuLocKey.sync_helper_link_view_in_figma.Localize();
            label.RegisterCallback<ContextClickEvent>(evt =>
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Copy Name Hierarchy"), false, () =>
                {
                    EditorGUIUtility.systemCopyBuffer = syncHelper.Data.NameHierarchy;
                    Debug.Log($"[FigmaConverterUnity] Copied to clipboard: {syncHelper.Data.NameHierarchy}");
                });
                menu.AddItem(new GUIContent("Open Figma URL"), false, () =>
                {
                    string figmaUrl = $"https://www.figma.com/design/{syncHelper.Data.ProjectId}?node-id={syncHelper.Data.Id.Replace(":", "-")}";
                    Application.OpenURL(figmaUrl);
                });
                menu.ShowAsContext();
            });
        }
    }
}
