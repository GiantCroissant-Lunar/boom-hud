using DA_Assets.DAI;
using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.Singleton;
using DA_Assets.UpdateChecker;
using DA_Assets.UpdateChecker.Models;
using DA_Assets.Shared;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static DA_Assets.DAI.DAInspectorUITK;

#pragma warning disable IDE0003
#pragma warning disable CS0649

namespace DA_Assets.FCU
{
    [CustomEditor(typeof(FigmaConverterUnity)), CanEditMultipleObjects]
    public class FcuEditor : DAEditor<FcuEditor, FigmaConverterUnity>
    {
        [SerializeField] Texture2D _logoDarkTheme;
        public Texture2D LogoDarkTheme => _logoDarkTheme;

        [SerializeField] Texture2D _logoLightTheme;
        public Texture2D LogoLightTheme => _logoLightTheme;

        private HeaderSection _headerSection;
        internal HeaderSection Header => monoBeh.Link(ref _headerSection, this);

        private FramesSection _frameListSection;
        internal FramesSection FrameList => monoBeh.Link(ref _frameListSection, this);

        internal LayoutUpdaterWindow LayoutUpdaterWindow => LayoutUpdaterWindow.GetInstance(
            this,
            monoBeh,
            new Vector2(900, 600),
            false,
            title: FcuLocKey.layout_updater_title.Localize());

        internal RateLimitWindow RateLimitWindow => RateLimitWindow.GetInstance(
            this,
            monoBeh,
            new Vector2(700, 370),
            false,
            title: FcuLocKey.rate_limit_window_title.Localize());

#if TextMeshPro
        internal TmpFontMetricsWindow TmpFontMetricsWindow => TmpFontMetricsWindow.GetInstance(
            this,
            monoBeh,
            new Vector2(900, 600),
            false,
            title: "TMP Font Metrics");
#endif

        internal UitkFontMetricsWindow UitkFontMetricsWindow => UitkFontMetricsWindow.GetInstance(
            this,
            monoBeh,
            new Vector2(900, 600),
            false,
            title: "UITK Font Metrics");

        internal SpriteDuplicateFinderWindow SpriteDuplicateFinderWindow => SpriteDuplicateFinderWindow.GetInstance(
            this,
            monoBeh,
            new Vector2(900, 600),
            false,
            title: FcuLocKey.layout_updater_button_sprite_duplicate_finder.Localize());

        internal FcuSettingsWindow SettingsWindow => FcuSettingsWindow.GetInstance(
            this,
            monoBeh,
            new Vector2(800, 600),
            false,
            title: FcuLocKey.common_button_settings.Localize());

        private ScrollView _framesScroll;
        private VisualElement _framesHost;
        private SquareIconButton _btnRecent;
        private VisualElement _root;
        private TextField _projectUrlField;

        protected override void OnDisable()
        {
            base.OnDisable();

            FcuConfig.Instance.Localizator.OnLanguageChanged -= RebuildUI;
            FigmaConverterUnity.OnResetPerformed -= OnFcuReset;
            monoBeh.InspectorDrawer.OnFramesChanged -= RefreshFrames;
            monoBeh.InspectorDrawer.OnScrollContentUpdated -= this.FrameList.UpdateScrollContent;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            FcuConfig.Instance.Localizator.OnLanguageChanged += RebuildUI;
            FigmaConverterUnity.OnResetPerformed += OnFcuReset;
            monoBeh.InspectorDrawer.OnFramesChanged += RefreshFrames;
            monoBeh.InspectorDrawer.OnScrollContentUpdated += this.FrameList.UpdateScrollContent;

            monoBeh.EditorDelegateHolder.SetSpriteRects = SpriteEditorUtility.SetSpriteRects;
            monoBeh.EditorDelegateHolder.ShowDifferenceChecker = ShowDifferenceChecker;
            monoBeh.EditorDelegateHolder.ShowRateLimitWindow = ShowRateLimitDialog;
#if TextMeshPro
            monoBeh.EditorDelegateHolder.ShowTmpFontMetricsWindow = ShowTmpFontMetricsWindow;
#endif
            monoBeh.EditorDelegateHolder.ShowUitkFontMetricsWindow = ShowUitkFontMetricsWindow;
            monoBeh.EditorDelegateHolder.ShowSpriteDuplicateFinder = ShowSpriteDuplicateFinder;
            monoBeh.EditorDelegateHolder.SetGameViewSize = GameViewUtils.SetGameViewSize;
            monoBeh.EditorDelegateHolder.StartProgress = (target, category, totalItems, indeterminate) =>
                EditorProgressBarManager.StartProgress(target, category, totalItems, indeterminate);
            monoBeh.EditorDelegateHolder.UpdateProgress = (target, category, itemsDone) =>
                EditorProgressBarManager.UpdateProgress(target, category, itemsDone);
            monoBeh.EditorDelegateHolder.CompleteProgress = (target, category) =>
                EditorProgressBarManager.CompleteProgress(target, category);
            monoBeh.EditorDelegateHolder.StopAllProgress = target =>
                EditorProgressBarManager.StopAllProgress(target);

            _ = monoBeh.Authorizer.TryRestoreSession();
        }

        private void OnFcuReset(FigmaConverterUnity resetInstance)
        {
            // Rebuild the inspector only for the FCU instance this editor is showing.
            if (resetInstance == monoBeh)
            {
                ForceRebuild();
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            if (monoBeh.Settings.MainSettings.WindowMode)
            {
                return DrawWindowedGUI();
            }
            else
            {
                return DrawGUI();
            }
        }

        public VisualElement DrawGUI()
        {
            _root = uitk.CreateRoot(uitk.ColorScheme.FCU_BG);
            BuildContent();
            return _root;
        }

        internal void RebuildUI()
        {
            _root.Clear();
            BuildContent();
        }
        private void BuildContent()
        {
            var headerCard = Card();
            headerCard.Add(Header.BuildHeaderUI());
            _root.Add(headerCard);
            _root.Add(uitk.Space10());

            var actionsCard = Card();
            actionsCard.Add(BuildUrlAndActionsRow());
            _root.Add(actionsCard);

            bool hasContent = !monoBeh.InspectorDrawer.SelectableDocument.IsProjectEmpty();
            var framesCard = BuildFramesCard(hasContent);
            _root.Add(uitk.Space5());
            _root.Add(framesCard);
            _root.Add(BuildBottomExtraInfo());
            _root.Add(uitk.Space5());
            _root.Add(BuildFooter());
        }

        // Builds the footer with FCU version info and optionally UITK Converter version.
        private VisualElement BuildFooter()
        {
            var fcuInfo = new FooterAssetInfo
            {
                AssetType = DA_Assets.UpdateChecker.Models.AssetType.FCU,
                ProductVersion = FcuConfig.Instance.ProductVersion
            };

            string fuitkVersion = FuitkConfigReflectionHelper.GetProductVersion();
            if (fuitkVersion != null)
            {
                return uitk.CreateFooterWithVersionInfo(
                    FcuConfig.Instance.Localizator.Language,
                    fcuInfo,
                    new FooterAssetInfo
                    {
                        AssetType = AssetType.UITK_CONV,
                        ProductVersion = fuitkVersion
                    });
            }

            return uitk.CreateFooterWithVersionInfo(FcuConfig.Instance.Localizator.Language, fcuInfo);
        }

        // Schedules any window to open on the next editor frame to avoid layout conflicts.
        private void ShowWindowDelayed(Action showAction)
        {
            void DelayedShow()
            {
                EditorApplication.delayCall -= DelayedShow;
                showAction();
            }

            EditorApplication.delayCall += DelayedShow;
        }

        private void ShowDifferenceChecker(LayoutUpdaterInput data, Action<LayoutUpdaterOutput> callback)
            => ShowWindowDelayed(() => { this.LayoutUpdaterWindow.SetData(data, callback); this.LayoutUpdaterWindow.Show(); });

        private void ShowRateLimitDialog(RateLimitWindowData data, Action<RateLimitWindowResult> callback)
            => ShowWindowDelayed(() => { this.RateLimitWindow.SetData(data, callback); this.RateLimitWindow.Show(); });
#if TextMeshPro
        private void ShowTmpFontMetricsWindow(FontMetricsWindowData data, Action<FontMetricsWindowResult> callback)
            => ShowWindowDelayed(() =>
            {
                this.TmpFontMetricsWindow.SetData(data, callback);
                this.TmpFontMetricsWindow.Show();
            });
#endif
        private void ShowUitkFontMetricsWindow(FontMetricsWindowData data, Action<FontMetricsWindowResult> callback)
            => ShowWindowDelayed(() =>
            {
                this.UitkFontMetricsWindow.SetData(data, callback);
                this.UitkFontMetricsWindow.Show();
            });
        private void ShowSpriteDuplicateFinder(List<List<SpriteUsageFinder.UsedSprite>> groups, Action<List<List<SpriteUsageFinder.UsedSprite>>> callback)
            => ShowWindowDelayed(() => { this.SpriteDuplicateFinderWindow.SetData(groups, callback); this.SpriteDuplicateFinderWindow.Show(); });

        private ScrollView CreateScroll()
        {
            var sv = new ScrollView
            {
                style =
                {
                    backgroundColor = uitk.ColorScheme.FCU_BG
                }
            };
#if UNITY_2020_1_OR_NEWER
            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
#else
            sv.showHorizontal = false;
#endif
            return sv;
        }

        private VisualElement BuildFramesCard(bool hasContent)
        {
            var framesCard = Card();

            _framesScroll = CreateScroll();
            _framesHost = new VisualElement();

            if (hasContent)
                _framesHost.Add(FrameList.BuildFramesSectionUI());

            _framesScroll.Add(_framesHost);
            framesCard.Add(_framesScroll);

            return framesCard;
        }

        private void ToggleWindowModeAndRebuild()
        {
            monoBeh.Settings.MainSettings.WindowMode = !monoBeh.Settings.MainSettings.WindowMode;

            if (monoBeh.Settings.MainSettings.WindowMode)
                SettingsWindow.Show();
            else
                SettingsWindow.CreateTabs();

            ForceRebuild();
        }

        private void SetDisplay(VisualElement ve, bool visible)
        {
            ve.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private VisualElement Card()
        {
            var ve = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    backgroundColor = uitk.ColorScheme.FCU_BG
                }
            };

            return ve;
        }

        private VisualElement BuildUrlAndActionsRow()
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart,
                    backgroundColor = uitk.ColorScheme.FCU_BG,
                    flexWrap = Wrap.Wrap
                }
            };

            bool isOfflineMode = monoBeh.Settings.MainSettings.ImportMode == FCU.Model.ImportMode.Offline;
            var inputContainer = BuildInputContainer(isOfflineMode);

            var btnRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = uitk.ColorScheme.FCU_BG,
                    flexShrink = 0,
                    flexWrap = Wrap.Wrap
                }
            };

            _btnRecent = uitk.SquareIconButton(
                ShowRecentProjectsPopup_OnClick,
                EditorTextureUtils.RecolorToEditorSkin(gui.Resources.ImgViewRecent),
                FcuLocKey.tooltip_recent_projects.Localize());

            var btnDownload = uitk.SquareIconButton(
                OnDownloadButtonClick,
                EditorTextureUtils.RecolorToEditorSkin(gui.Resources.IconDownload),
                FcuLocKey.tooltip_download_project.Localize());

            var btnImport = uitk.SquareIconButton(
                monoBeh.EditorEventHandlers.ImportSelectedFrames_OnClick,
                EditorTextureUtils.RecolorToEditorSkin(gui.Resources.IconImport),
                FcuLocKey.tooltip_import_frames.Localize());

            var btnStop = uitk.SquareIconButton(
                monoBeh.EditorEventHandlers.StopImport_OnClick,
                gui.Resources.IconStop,
                FcuLocKey.tooltip_stop_import.Localize());

            var btnSettings = uitk.SquareIconButton(
                () => EditorApplication.delayCall += SettingsWindow.Show,
                EditorTextureUtils.RecolorToEditorSkin(gui.Resources.IconSettings),
                FcuLocKey.tooltip_open_settings_window.Localize());

            void UpdateSettingsVisibility()
            {
                bool show = !monoBeh.Settings.MainSettings.WindowMode;
                SetDisplay(btnSettings, show);
            }

            var btnToggle = uitk.SquareIconButton(
                ToggleWindowModeAndRebuild,
                EditorTextureUtils.RecolorToEditorSkin(gui.Resources.IconExpandWindow),
                FcuLocKey.tooltip_change_window_mode.Localize());

            var spaceAfterRecent = uitk.Space5();

            btnRow.Add(_btnRecent);
            btnRow.Add(spaceAfterRecent);
            btnRow.Add(btnDownload);
            btnRow.Add(uitk.Space5());
            btnRow.Add(btnImport);
            btnRow.Add(uitk.Space5());
            btnRow.Add(btnStop);
            btnRow.Add(uitk.Space5());
            btnRow.Add(btnSettings);

            _btnRecent.style.display = isOfflineMode ? DisplayStyle.None : DisplayStyle.Flex;
            spaceAfterRecent.style.display = isOfflineMode ? DisplayStyle.None : DisplayStyle.Flex;

            UpdateSettingsVisibility();

            btnRow.Add(uitk.Space5());
            btnRow.Add(btnToggle);

            row.Add(inputContainer);
            row.Add(uitk.Space5());
            row.Add(btnRow);
            return row;
        }

        // Validates state and triggers project download for online or offline mode.
        private void OnDownloadButtonClick()
        {
            bool offline = monoBeh.Settings.MainSettings.ImportMode == FCU.Model.ImportMode.Offline;

            if (offline)
            {
                if (string.IsNullOrEmpty(monoBeh.Settings.MainSettings.OfflineArchivePath))
                {
                    Debug.Log("[FCU Offline] Please drop a ZIP archive first.");
                    return;
                }
            }
            else
            {
                if (!monoBeh.Authorizer.IsAuthed())
                {
                    Debug.Log(FcuLocKey.log_not_authorized.Localize());
                    return;
                }

                if (string.IsNullOrEmpty(monoBeh.Settings.MainSettings.ProjectId))
                {
                    Debug.Log(FcuLocKey.log_incorrent_project_url.Localize());
                    return;
                }
            }

            monoBeh.EditorEventHandlers.DownloadProject_OnClick();
        }

        // Returns the appropriate input container depending on the import mode.
        private VisualElement BuildInputContainer(bool isOfflineMode)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    minWidth = 50,
                    flexBasis = 0
                }
            };

            if (isOfflineMode)
                container.Add(BuildOfflineDropZone());
            else
                container.Add(BuildOnlineUrlField());

            return container;
        }

        // Creates a drag-and-drop zone for selecting a ZIP archive in offline mode.
        private VisualElement BuildOfflineDropZone()
        {
            var dropZone = uitk.DropZoneElement("Drop ZIP archive here", ".zip");
            dropZone.style.height = 32;
            dropZone.style.marginTop = 0;
            dropZone.style.marginBottom = 0;

            // Remove rounded corners
            dropZone.style.borderTopLeftRadius = 0;
            dropZone.style.borderTopRightRadius = 0;
            dropZone.style.borderBottomLeftRadius = 0;
            dropZone.style.borderBottomRightRadius = 0;

            string currentPath = monoBeh.Settings.MainSettings.OfflineArchivePath;
            if (!string.IsNullOrEmpty(currentPath))
                dropZone.SetDroppedFile(System.IO.Path.GetFileName(currentPath));

            dropZone.OnFilesDropped += (paths) =>
            {
                if (paths.Length > 0)
                {
                    monoBeh.Settings.MainSettings.OfflineArchivePath = paths[0];
                    dropZone.SetDroppedFile(System.IO.Path.GetFileName(paths[0]));
                    Debug.Log($"[FCU Offline] Archive selected: {paths[0]}");
                }
            };

            return dropZone;
        }

        // Creates and styles the project URL text field for online mode.
        private VisualElement BuildOnlineUrlField()
        {
            string url = monoBeh?.Settings?.MainSettings?.ProjectUrl;
            string val = url == null ? "" : url;

            _projectUrlField = new TextField
            {
                value = val,
                multiline = false,
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    minWidth = 50,
                    flexBasis = 0,
                    height = 32,
                    backgroundColor = uitk.ColorScheme.FCU_BG,
                    overflow = Overflow.Hidden,
                    whiteSpace = WhiteSpace.NoWrap
                }
            };

            _projectUrlField.ClearClassList();
            var input = _projectUrlField.Q<VisualElement>(null, "unity-text-field__input") ?? _projectUrlField.Q<VisualElement>(null, "unity-text-input");
            input?.ClearClassList();
            if (input != null)
            {
                input.style.backgroundColor = uitk.ColorScheme.FCU_BG;
                input.style.height = Length.Percent(100);
                input.style.unityTextAlign = TextAnchor.MiddleLeft;
                input.style.paddingLeft = 8;
                input.style.paddingRight = 8;
                input.style.marginLeft = 0;
                input.style.marginRight = 0;
                input.style.overflow = Overflow.Hidden;
            }

            UIHelpers.SetRadius(_projectUrlField, 0);
            UIHelpers.SetBorderWidth(_projectUrlField, DAI_UitkConstants.BorderWidth);
            UIHelpers.SetBorderColor(_projectUrlField, uitk.ColorScheme.OUTLINE);
            UIHelpers.SetZeroMarginPadding(_projectUrlField);

            _projectUrlField.RegisterValueChangedCallback(e =>
            {
                monoBeh.Settings.MainSettings.ProjectUrl = e.newValue;
                _projectUrlField.SetValueWithoutNotify(monoBeh.Settings.MainSettings.ProjectUrl);
            });

            return _projectUrlField;
        }

        private void ShowRecentProjectsPopup_OnClick()
        {
            List<RecentProject> recentProjects = monoBeh.ProjectCacher.GetRecentProjects();

            List<GUIContent> options = new List<GUIContent>();

            if (recentProjects.IsEmpty())
            {
                options.Add(new GUIContent(
                    FcuLocKey.label_no_recent_projects.Localize(),
                    FcuLocKey.tooltip_no_recent_projects.Localize()));
            }
            else
            {
                foreach (RecentProject project in recentProjects)
                {
                    options.Add(new GUIContent(project.Name));
                }
            }

            Rect anchor = _btnRecent.worldBound;
            Rect pos = new Rect(anchor.xMin, anchor.yMax, 0, 0);
            EditorUtility.DisplayCustomMenu(pos, options.ToArray(), -1, (userData, ops, selected) =>
            {
                RecentProject recentProject = recentProjects[selected];
                monoBeh.Settings.MainSettings.ProjectUrl = recentProject.Url;
                _projectUrlField.SetValueWithoutNotify(monoBeh.Settings.MainSettings.ProjectUrl);
                monoBeh.EditorEventHandlers.DownloadProject_OnClick();
            }, null);
        }

        public UnityEngine.UIElements.VisualElement BuildBottomExtraInfo()
        {
            var root = new UnityEngine.UIElements.VisualElement();

            root.Add(new DeveloperMessagesElement(AssetType.FCU, FcuConfig.Instance.ProductVersion, uitk));

            if (monoBeh.IsJsonNetExists() == false)
            {
                root.Add(uitk.Space10());
                root.Add(BuildJsonNetHelpBox());
            }

            if (monoBeh.AssetTools.NeedShowRateMe)
                root.Add(BuildRateMe());

            return root;
        }

        // Creates the help box shown when Newtonsoft Json.NET is missing from the project.
        private VisualElement BuildJsonNetHelpBox()
        {
            var helpBoxData = new HelpBoxData
            {
                Message = FcuLocKey.helpbox_install_json_net.Localize(),
                MessageType = UnityEditor.MessageType.Error,
                OnClick = () => Application.OpenURL("https://da-assets.gitbook.io/docs/fcu-for-developers/json.net"),
            };

            return new CustomHelpBox(uitk, helpBoxData);
        }

        private VisualElement BuildRateMe()
        {
            string packageLink = GetRateMePackageLink();

            Func<string> descriptionProvider = () =>
            {
                int dc = UpdateService.GetFirstVersionDaysCount(AssetType.FCU);
                return FcuLocKey.label_rateme_desc.Localize(dc);
            };

            return uitk.BuildRateMe(
                packageLink,
                descriptionProvider,
                FcuConfig.RATEME_PREFS_KEY,
                () => FcuLocKey.tooltip_rateme_desc.Localize());
        }

        // Returns the Unity Asset Store review link based on the current language and UI framework.
        private string GetRateMePackageLink()
        {
            if (FcuConfig.Instance.Localizator.Language == DALanguage.zh)
                return "";

            int packageId = monoBeh.IsUGUI() ? 198134 : 272042;
            return "https://assetstore.unity.com/packages/tools/utilities/" + packageId + "#reviews";
        }


        private void RefreshFrames()
        {
            if (_framesScroll == null || _framesHost == null)
                return;

            _framesHost.Clear();
            _framesHost.Add(this.FrameList.BuildFramesSectionUI());

            _framesScroll.schedule.Execute(() =>
            {
                if (_framesScroll != null && _framesHost != null)
                    _framesScroll.ScrollTo(_framesHost);
            });

            Repaint();
        }

        void ForceRebuild() => ActiveEditorTracker.sharedTracker.ForceRebuild();

        public VisualElement DrawWindowedGUI()
        {
            var root = uitk.CreateRoot(uitk.ColorScheme.FCU_BG);

            var progressBarsContainer = BuildProgressBarsContainer();
            root.Add(progressBarsContainer);


            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = uitk.ColorScheme.FCU_BG
                }
            };

            var btnOpen = uitk.SquareIconButton(
                SettingsWindow.Show,
                gui.Resources.IconOpen,
                FcuLocKey.tooltip_open_fcu_window.Localize());

            var btnToggle = uitk.SquareIconButton(
                ToggleWindowModeAndRebuild,
                gui.Resources.IconExpandWindow,
                FcuLocKey.tooltip_change_window_mode.Localize());

            toolbar.Add(btnOpen);
            toolbar.Add(uitk.Space5());
            toolbar.Add(btnToggle);
            root.Add(toolbar);

            return root;
        }

        // Creates and registers the progress bars container for windowed mode.
        private VisualElement BuildProgressBarsContainer()
        {
            var container = new VisualElement { name = "ProgressBarsContainer" };
            container.style.marginTop = -15;
            container.style.marginLeft = -15;
            container.style.marginRight = -15;
            EditorProgressBarManager.RegisterContainer(monoBeh, container, uitk);
            return container;
        }
    }
}
