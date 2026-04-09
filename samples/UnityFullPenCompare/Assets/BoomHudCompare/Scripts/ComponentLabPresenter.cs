using System.Collections.Generic;
using BoomHud.Unity.UIToolkit;
using Generated.Hud;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BoomHud.Compare
{
    [ExecuteAlways]
    public sealed class ComponentLabPresenter : BoomHudUiToolkitHost
    {
        private const string GeneratedBasePath = "BoomHudGenerated/";
        private const float PartyMemberWidth = 122f;
        private const float DefaultHpWidth = 90f;
        private const float DefaultMpWidth = 60f;

        private static readonly PartyMemberSpec[] s_partyMembers =
        {
            new PartyMemberSpec("Aelric", "sword", "ATK 12", "DEF 8", 96f, 60f, 0f),
            new PartyMemberSpec("Lyra", "sparkles", "ATK 6", "MAG 14", 70f, 90f, 0.16f),
            new PartyMemberSpec("Theron", "shield", "ATK 14", "DEF 12", 110f, 40f, 0.32f),
            new PartyMemberSpec("Selene", "moon", "ATK 5", "MAG 16", 100f, 80f, 0.48f),
            new PartyMemberSpec("Elara", "cross", "ATK 4", "MAG 11", 80f, 100f, 0.64f),
            new PartyMemberSpec("Darius", "flame", "ATK 16", "DEF 10", 116f, 50f, 0.80f),
        };

        private readonly List<AnimatedCharPortraitPreview> _charPortraitPreviews = new List<AnimatedCharPortraitPreview>();
        private float _timelineStartSeconds;

        protected override void BindView(VisualElement root)
        {
            _charPortraitPreviews.Clear();
            _timelineStartSeconds = GetCurrentClockSeconds();

            root.Clear();
            root.style.width = Length.Percent(100f);
            root.style.height = Length.Percent(100f);
            root.style.flexGrow = 1f;
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingLeft = 24f;
            root.style.paddingTop = 24f;
            root.style.paddingRight = 24f;
            root.style.paddingBottom = 24f;
            root.style.backgroundColor = new Color(0.07f, 0.07f, 0.07f, 1f);

            var header = new Label("BoomHud Component Lab");
            header.style.fontSize = 24f;
            header.style.color = new Color(0.96f, 0.96f, 0.96f, 1f);
            header.style.marginBottom = 8f;
            root.Add(header);

            var subtitle = new Label("Generated components mounted individually in code for fidelity tuning.");
            subtitle.style.fontSize = 14f;
            subtitle.style.color = new Color(0.70f, 0.70f, 0.70f, 1f);
            subtitle.style.marginBottom = 18f;
            root.Add(subtitle);

            var scrollView = new ScrollView();
            scrollView.style.width = Length.Percent(100f);
            scrollView.style.height = Length.Percent(100f);
            scrollView.style.flexGrow = 1f;
            scrollView.style.alignSelf = Align.Stretch;
            root.Add(scrollView);

            var gallery = new VisualElement();
            gallery.style.width = Length.Percent(100f);
            gallery.style.flexDirection = FlexDirection.Row;
            gallery.style.flexWrap = Wrap.Wrap;
            gallery.style.alignItems = Align.FlexStart;
            scrollView.Add(gallery);

            AddActionButtonPreview(gallery);
            AddStatusIconPreview(gallery);
            AddStatBarPreview(gallery);
            AddMessageLogPreview(gallery);
            AddCharPortraitPreview(gallery);
            AddPartyMemberLayoutPreview(gallery);
            AddMinimapPreview(gallery);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
#endif
        }

        private void Update()
        {
            if (_charPortraitPreviews.Count == 0)
            {
                return;
            }

            var elapsedSeconds = GetCurrentClockSeconds() - _timelineStartSeconds;
            for (var index = 0; index < _charPortraitPreviews.Count; index++)
            {
                _charPortraitPreviews[index].Evaluate(elapsedSeconds);
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
#endif
        }

        private static void AddActionButtonPreview(VisualElement gallery)
        {
            var surface = CreatePreviewCard(gallery, "ActionButton", 220f, 120f);
            var componentRoot = InstantiateGeneratedRoot(surface, "ActionButtonView");
            if (componentRoot == null)
            {
                return;
            }

            _ = new ActionButtonView(componentRoot);
        }

        private static void AddStatusIconPreview(VisualElement gallery)
        {
            var surface = CreatePreviewCard(gallery, "StatusIcon", 220f, 120f);
            var componentRoot = InstantiateGeneratedRoot(surface, "StatusIconView");
            if (componentRoot == null)
            {
                return;
            }

            _ = new StatusIconView(componentRoot);
        }

        private static void AddStatBarPreview(VisualElement gallery)
        {
            var surface = CreatePreviewCard(gallery, "StatBar", 280f, 120f);
            var componentRoot = InstantiateGeneratedRoot(surface, "StatBarView");
            if (componentRoot == null)
            {
                return;
            }

            var view = new StatBarView(componentRoot);
            view.Fill.style.width = 80f;
        }

        private static void AddMessageLogPreview(VisualElement gallery)
        {
            var surface = CreatePreviewCard(gallery, "MessageLog", 360f, 180f);
            var componentRoot = InstantiateGeneratedRoot(surface, "MessageLogView");
            if (componentRoot == null)
            {
                return;
            }

            var view = new MessageLogView(componentRoot);
            view.Line1.text = "You hear distant footsteps.";
            view.Line2.text = "Aelric blocks the attack.";
            view.Line3.text = "Lyra channels Firebolt.";
            view.Line4.text = "Critical hit for 28 damage.";
            view.Line5.text = "Treasure chest spotted.";
        }

        private void AddCharPortraitPreview(VisualElement gallery)
        {
            var surface = CreatePreviewCard(gallery, "CharPortrait", 280f, 220f);
            surface.style.justifyContent = Justify.FlexStart;
            surface.style.alignItems = Align.Center;
            surface.style.overflow = Overflow.Visible;
            var componentRoot = InstantiateGeneratedRoot(surface, "CharPortraitView");
            if (componentRoot == null)
            {
                return;
            }

            var view = new CharPortraitView(componentRoot);
            ApplyPartyMemberPresentation(view, s_partyMembers[0], 1f, 1f, useScanningText: false);

            RegisterAnimatedCharPortrait(view, s_partyMembers[0], PreviewPlaybackMode.Loop);
        }

        private void AddPartyMemberLayoutPreview(VisualElement gallery)
        {
            var surface = CreatePreviewCard(gallery, "Party Member Layout", 360f, 560f);
            surface.style.flexDirection = FlexDirection.Column;
            surface.style.alignItems = Align.Center;
            surface.style.justifyContent = Justify.FlexStart;
            surface.style.paddingLeft = 8f;
            surface.style.paddingTop = 6f;
            surface.style.paddingRight = 8f;
            surface.style.paddingBottom = 6f;
            surface.style.overflow = Overflow.Visible;

            for (var rowIndex = 0; rowIndex < 3; rowIndex++)
            {
                var row = new VisualElement();
                row.style.width = 252f;
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.Center;
                row.style.alignItems = Align.FlexStart;
                row.style.marginBottom = 6f;
                if (rowIndex > 0)
                {
                    row.style.paddingLeft = 12f;
                }
                surface.Add(row);

                for (var columnIndex = 0; columnIndex < 2; columnIndex++)
                {
                    var memberIndex = rowIndex * 2 + columnIndex;
                    if (memberIndex >= s_partyMembers.Length)
                    {
                        break;
                    }

                    var slot = CreatePartyMemberSlot(row);
                    var componentRoot = InstantiateGeneratedRoot(slot, "CharPortraitView");
                    if (componentRoot == null)
                    {
                        continue;
                    }

                    var view = new CharPortraitView(componentRoot);
                    view.Root.style.width = Length.Percent(100f);
                    view.Root.style.minWidth = PartyMemberWidth;
                    ApplyPartyMemberPresentation(view, s_partyMembers[memberIndex], 1f, 1f, useScanningText: false);

                    RegisterAnimatedCharPortrait(view, s_partyMembers[memberIndex], PreviewPlaybackMode.HoldEnd);
                }
            }
        }

        private static void AddMinimapPreview(VisualElement gallery)
        {
            var surface = CreatePreviewCard(gallery, "Minimap", 360f, 380f);
            var componentRoot = InstantiateGeneratedRoot(surface, "MinimapView");
            if (componentRoot == null)
            {
                return;
            }

            _ = new MinimapView(componentRoot);
        }

        private static VisualElement CreatePreviewCard(VisualElement gallery, string title, float width, float height)
        {
            var card = new VisualElement();
            card.style.width = width;
            card.style.minHeight = height;
            card.style.paddingLeft = 14f;
            card.style.paddingTop = 14f;
            card.style.paddingRight = 14f;
            card.style.paddingBottom = 14f;
            card.style.backgroundColor = new Color(0.11f, 0.11f, 0.11f, 1f);
            card.style.borderLeftWidth = 1f;
            card.style.borderTopWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            card.style.borderTopColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            card.style.borderRightColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            card.style.borderBottomColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            card.style.borderTopLeftRadius = 6f;
            card.style.borderTopRightRadius = 6f;
            card.style.borderBottomLeftRadius = 6f;
            card.style.borderBottomRightRadius = 6f;
            card.style.marginRight = 16f;
            card.style.marginBottom = 16f;

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 14f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.94f, 0.94f, 0.94f, 1f);
            titleLabel.style.marginBottom = 10f;
            card.Add(titleLabel);

            var surface = new VisualElement();
            surface.style.flexGrow = 1f;
            surface.style.minHeight = height - 50f;
            surface.style.paddingLeft = 10f;
            surface.style.paddingTop = 10f;
            surface.style.paddingRight = 10f;
            surface.style.paddingBottom = 10f;
            surface.style.backgroundColor = Color.black;
            surface.style.alignItems = Align.Center;
            surface.style.justifyContent = Justify.Center;
            surface.style.overflow = Overflow.Visible;
            card.Add(surface);

            gallery.Add(card);
            return surface;
        }

        private static VisualElement CreatePartyMemberSlot(VisualElement row)
        {
            var slot = new VisualElement();
            slot.style.width = PartyMemberWidth;
            slot.style.minWidth = PartyMemberWidth;
            slot.style.maxWidth = PartyMemberWidth;
            if (row.childCount > 0)
            {
                slot.style.marginLeft = 8f;
            }
            slot.style.alignItems = Align.Stretch;
            slot.style.justifyContent = Justify.FlexStart;
            slot.style.flexShrink = 0f;
            slot.style.overflow = Overflow.Visible;
            row.Add(slot);
            return slot;
        }

        private void RegisterAnimatedCharPortrait(CharPortraitView view, PartyMemberSpec spec, PreviewPlaybackMode playbackMode)
        {
            _charPortraitPreviews.Add(new AnimatedCharPortraitPreview(view, spec, playbackMode));
        }

        private static void ApplyPartyMemberPresentation(CharPortraitView view, PartyMemberSpec spec, float hpProgress, float mpProgress, bool useScanningText)
        {
            view.Root.style.width = PartyMemberWidth;
            view.Name.text = useScanningText ? "SCANNING" : spec.Name;
            view.ClassIcon.text = ResolveLucideGlyph(spec.IconToken);
            view.Stat1.text = spec.Stat1;
            view.Stat2.text = spec.Stat2;
            view.HpFill.style.width = spec.HpWidth * Mathf.Clamp01(hpProgress);
            view.MpFill.style.width = spec.MpWidth * Mathf.Clamp01(mpProgress);
        }

        private static string ResolveLucideGlyph(string iconToken)
        {
            return iconToken switch
            {
                "cross" => "\uE1E5",
                "flame" => "\uE0D2",
                "flask-conical" => "\uE0D5",
                "moon" => "\uE11E",
                "shield" => "\uE158",
                "sparkles" => "\uE412",
                "sword" => "\uE2B3",
                "swords" => "\uE2B4",
                "wand" => "\uE246",
                "wand-2" => "\uE357",
                "wand-sparkles" => "\uE357",
                _ => iconToken,
            };
        }

        private static VisualElement? InstantiateGeneratedRoot(VisualElement surface, string viewName)
        {
            var visualTree = Resources.Load<VisualTreeAsset>(GeneratedBasePath + viewName);
            if (visualTree == null)
            {
                surface.Add(CreateMissingAssetLabel(viewName + ".uxml"));
                return null;
            }

            var styleSheet = Resources.Load<StyleSheet>(GeneratedBasePath + viewName);
            if (styleSheet != null && !surface.styleSheets.Contains(styleSheet))
            {
                surface.styleSheets.Add(styleSheet);
            }

            var staging = new VisualElement();
            visualTree.CloneTree(staging);
            if (staging.childCount == 0)
            {
                surface.Add(CreateMissingAssetLabel(viewName + " has no root element."));
                return null;
            }

            var componentRoot = staging.ElementAt(0);
            surface.Add(componentRoot);
            return componentRoot;
        }

        private static Label CreateMissingAssetLabel(string assetName)
        {
            var label = new Label("Missing generated asset: " + assetName);
            label.style.color = new Color(1f, 0.45f, 0.45f, 1f);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return label;
        }

        private static float GetCurrentClockSeconds()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return (float)EditorApplication.timeSinceStartup;
            }
#endif

            return Time.realtimeSinceStartup;
        }

        private sealed class AnimatedCharPortraitPreview
        {
            private readonly CharPortraitView _view;
            private readonly PartyMemberSpec _spec;
            private readonly PreviewPlaybackMode _playbackMode;
            private readonly float _clipDurationSeconds;

            public AnimatedCharPortraitPreview(CharPortraitView view, PartyMemberSpec spec, PreviewPlaybackMode playbackMode)
            {
                _view = view;
                _spec = spec;
                _playbackMode = playbackMode;
                _clipDurationSeconds = Mathf.Max(0.001f, CharPortraitMotion.GetClipDurationSeconds(CharPortraitMotion.DefaultClipId));
            }

            public void Evaluate(float elapsedSeconds)
            {
                var localTimeSeconds = _playbackMode == PreviewPlaybackMode.HoldEnd
                    ? _clipDurationSeconds
                    : Mathf.Repeat(Mathf.Max(0f, elapsedSeconds) + _spec.PhaseOffsetSeconds, _clipDurationSeconds);
                CharPortraitMotion.TryApplyAtTime(_view, CharPortraitMotion.DefaultClipId, localTimeSeconds);

                var scanningThresholdSeconds = 24f / CharPortraitMotion.FramesPerSecond;
                var useScanningText = localTimeSeconds < scanningThresholdSeconds;
                var hpProgress = ResolveProgress(ResolveStyleWidth(_view.HpFill.style.width), DefaultHpWidth);
                var mpProgress = ResolveProgress(ResolveStyleWidth(_view.MpFill.style.width), DefaultMpWidth);

                ApplyPartyMemberPresentation(_view, _spec, hpProgress, mpProgress, useScanningText);
            }

            private static float ResolveStyleWidth(StyleLength styleLength)
            {
                return styleLength.keyword == StyleKeyword.Auto ? 0f : styleLength.value.value;
            }

            private static float ResolveProgress(float animatedWidth, float referenceWidth)
            {
                if (referenceWidth <= 0f)
                {
                    return 0f;
                }

                if (float.IsNaN(animatedWidth) || animatedWidth <= 0f)
                {
                    return 0f;
                }

                return Mathf.Clamp01(animatedWidth / referenceWidth);
            }
        }

        private readonly struct PartyMemberSpec
        {
            public PartyMemberSpec(string name, string iconToken, string stat1, string stat2, float hpWidth, float mpWidth, float phaseOffsetSeconds)
            {
                Name = name;
                IconToken = iconToken;
                Stat1 = stat1;
                Stat2 = stat2;
                HpWidth = hpWidth;
                MpWidth = mpWidth;
                PhaseOffsetSeconds = phaseOffsetSeconds;
            }

            public string Name { get; }

            public string IconToken { get; }

            public string Stat1 { get; }

            public string Stat2 { get; }

            public float HpWidth { get; }

            public float MpWidth { get; }

            public float PhaseOffsetSeconds { get; }
        }

        private enum PreviewPlaybackMode
        {
            Loop,
            HoldEnd,
        }
    }
}
