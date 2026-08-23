using System;
using System.Linq;
using Chicken.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModNook
{
    /// <summary>
    /// Builds the panel's once-per-overlay chrome - the full-screen overlay, the cloned settings
    /// backdrop and header, the panel plate, the mods/detail body with its scrollers, and the footer -
    /// and hands the controller back the handles it drives (<see cref="Overlay"/>, <see cref="Context"/>,
    /// <see cref="Content"/>, <see cref="Sidebar"/>, <see cref="Title"/>, <see cref="ResetButton"/>,
    /// <see cref="UsingGamePrompt"/>).
    ///
    /// <para>
    /// This is construction only. Navigation, catalog selection, reset/persist and per-mod rendering
    /// stay on <see cref="PanelController"/>; the footer's Reset and Close buttons call back through the
    /// actions passed to <see cref="Build"/>.
    /// </para>
    /// </summary>
    internal sealed class PanelChrome
    {
        /// <summary>The full-screen overlay that hosts everything below.</summary>
        internal GameObject Overlay { get; private set; }

        /// <summary>Parent / raycast blocker / button template handed to the row factory and dialogs.</summary>
        internal OverlayContext Context { get; private set; }

        /// <summary>The detail scroller's content, where a mod's setting rows are built.</summary>
        internal RectTransform Content { get; private set; }

        /// <summary>The mod-list scroller's content, down the left.</summary>
        internal RectTransform Sidebar { get; private set; }

        /// <summary>The header title, rewritten by the controller as the page changes.</summary>
        internal TextMeshProUGUI Title { get; private set; }

        /// <summary>The corner Reset button, shown only on a mod's own page.</summary>
        internal GameObject ResetButton { get; private set; }

        /// <summary>True when the game's own corner prompt handles Close, so no ESC button was drawn.</summary>
        internal bool UsingGamePrompt { get; private set; }

        private readonly PauseScreen pauseScreen;
        private readonly Action onClose;
        private readonly Action onReset;

        /// <summary>Y of the cloned header's lower edge, in overlay space. Content starts below it.</summary>
        private float headerBottom;

        /// <summary>Breathing room between the header's rule and the first setting.</summary>
        private const float HeaderGap = 24f;

        /// <summary>Width of the mod list. Wide enough for the longest name we ship against.</summary>
        private const float SidebarWidth = 380f;

        private PanelChrome(PauseScreen pauseScreen, Action onClose, Action onReset)
        {
            this.pauseScreen = pauseScreen;
            this.onClose = onClose;
            this.onReset = onReset;
        }

        /// <summary>
        /// Builds the whole chrome once and returns it. <paramref name="onClose"/> and
        /// <paramref name="onReset"/> wire the footer buttons back to the controller.
        /// </summary>
        internal static PanelChrome Build(PauseScreen pauseScreen, Action onClose, Action onReset)
        {
            var chrome = new PanelChrome(pauseScreen, onClose, onReset);
            chrome.BuildOverlay();
            return chrome;
        }

        private void BuildOverlay()
        {
            // includeInactive: true is required on Linux/Proton where canvas ancestors may still
            // be inactive when OnShow fires, causing a null return and wrong overlay parenting.
            var canvas = pauseScreen.GetComponentInParent<Canvas>(true);
            var parent = canvas != null ? canvas.transform : pauseScreen.transform;

            ModNookPlugin.Log.LogInfo(canvas != null
                ? $"Overlay parented to canvas '{canvas.name}' sortOrder={canvas.sortingOrder}"
                : "No canvas found in parent chain; overlay parented to pause screen.");

            Overlay = new GameObject(
                "ModNook_Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Overlay.transform.SetParent(parent, false);
            UiText.Stretch((RectTransform)Overlay.transform);

            var dim = Overlay.GetComponent<Image>();
            dim.color = new Color(0.025f, 0.012f, 0.055f, 0.85f);
            dim.raycastTarget = true;

            // The game's own settings backdrop, bats and all.
            var hasBackdrop = AddBackdrop((RectTransform)Overlay.transform);
            if (hasBackdrop)
            {
                // Nothing between the artwork and the settings. The game's Settings screen draws
                // straight onto this background - no dim, no window - and a plum plate laid over
                // the bats is just the old panel with a picture hidden behind it. The image stays
                // enabled but fully clear, because it is also what catches stray clicks.
                dim.color = new Color(0f, 0f, 0f, 0f);
            }

            // The row factory and its dialogs get this context explicitly - parent to draw into, the
            // overlay's raycast blocker, and the button template - rather than reading it off statics.
            // The blocker lets a game popup stand the overlay down while it shows, or the popup opens
            // behind it and nothing, including Escape, reaches it.
            Context = new OverlayContext(
                (RectTransform)Overlay.transform, Overlay.AddComponent<CanvasGroup>())
            {
                ButtonTemplate = PauseMenu.ButtonTemplate(pauseScreen),
            };

            // The header goes on the overlay rather than in the panel, and keeps the exact rect it
            // has on the Settings screen. Both are full-screen, so copying its anchors lands it
            // where the game puts it - and its children are anchored for that size, so letting a
            // layout group resize it is what threw the title and ornaments apart.
            var hasHeader = CloneSettingsHeader((RectTransform)Overlay.transform);

            var panel = BuildPanel((RectTransform)Overlay.transform, hasBackdrop, hasHeader);
            Tooltip.Ensure((RectTransform)Overlay.transform);

            if (!hasHeader)
            {
                BuildHeader(panel);
            }

            BuildBody(panel);
            BuildFooter(panel);
        }

        /// <summary>
        /// Clones the game's settings backdrop behind the panel, so opening Mod Nook takes over the
        /// screen the way opening Settings does.
        ///
        /// <para>
        /// Cloned rather than shown. <c>SettingsBackgroundScreen</c> is a real screen the game owns:
        /// showing it would push it onto the shared show-stack, and it lives on a different canvas,
        /// so whether it landed behind or in front of this panel would be a matter of sorting order
        /// rather than intent. A copy parented here is always behind, and cannot disturb the
        /// game's own screen state.
        /// </para>
        /// </summary>
        private bool AddBackdrop(RectTransform parent)
        {
            try
            {
                // Not UIScreen<T>.Instance: that is only populated once the screen has registered
                // itself, and while the game is paused it has not. FindObjectsOfTypeAll reaches it
                // regardless - the same route that finds the cycle and slider templates, which live
                // under the same Settings parent.
                var source = Resources.FindObjectsOfTypeAll<SettingsBackgroundScreen>()
                    .FirstOrDefault(x => x != null && x.gameObject.scene.IsValid());

                if (source == null)
                {
                    ModNookPlugin.Log.LogWarning(
                        "No settings backdrop found; using a plain dim instead.");
                    return false;
                }

                ModNookPlugin.Log.LogInfo($"Backdrop source: {Templates.PathOf(source.transform)}");

                // The GameObject is held, not the component. SettingsBackgroundScreen *is* a
                // UIScreen, so the sweep below destroys the very component this was cloned through
                // - and every call made on that handle afterwards is a call on a dead object.
                var backdrop = Templates.CloneInactive(source, "ModNook_Backdrop").gameObject;

                // Every screen component has to go before this is ever activated. UIScreen registers
                // itself by type on Awake, so a live clone would take the real settings backdrop's
                // place in that registry and the game would start driving ours instead.
                foreach (var screen in backdrop.GetComponentsInChildren<UIScreen>(true))
                {
                    UnityEngine.Object.DestroyImmediate(screen);
                }

                // The backdrop is clickable in its own right - it dismisses the screen it belongs
                // to. Ours belongs to nothing, so the handler is removed rather than left to fire.
                foreach (var background in backdrop.GetComponentsInChildren<UIScreenBackground>(true))
                {
                    UnityEngine.Object.DestroyImmediate(background);
                }

                // A hidden screen is hidden by fading its CanvasGroup to nothing, and the source is
                // hidden while we are paused - so the clone arrives fully transparent. It also must
                // not take clicks: it is scenery behind the panel, not part of it.
                foreach (var canvasGroup in backdrop.GetComponentsInChildren<CanvasGroup>(true))
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }

                backdrop.transform.SetParent(parent, false);

                if (backdrop.transform is RectTransform rect)
                {
                    UiText.Stretch(rect);
                }

                backdrop.SetActive(true);
                backdrop.transform.SetAsFirstSibling();

                ModNookPlugin.Log.LogInfo("Settings backdrop cloned behind the panel.");
                return true;
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning(
                    $"Could not clone the settings backdrop, falling back to a plain dim: {e.Message}");
                return false;
            }
        }

        private RectTransform BuildPanel(RectTransform parent, bool hasBackdrop, bool hasHeader)
        {
            var panel = new GameObject(
                "Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(VerticalLayoutGroup));
            panel.transform.SetParent(parent, false);

            var rect = (RectTransform)panel.transform;
            // Proportional rather than a pixel size, so the panel is the same share of the screen
            // at any resolution. Close to full-bleed, matching how the game's own Settings takes
            // over the screen rather than floating a window in the middle of it.
            rect.anchorMin = new Vector2(0.08f, 0.06f);
            rect.anchorMax = new Vector2(0.92f, 0.94f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (hasHeader)
            {
                // The header is placed absolutely, outside this panel, so the panel has to stop
                // short of it rather than assume a share of the screen.
                var top = parent.rect.height * 0.5f + headerBottom - HeaderGap;
                rect.anchorMax = new Vector2(0.92f, Mathf.Clamp01(top / parent.rect.height));
                rect.offsetMax = Vector2.zero;
            }

            var plate = panel.GetComponent<Image>();

            if (hasBackdrop)
            {
                // The bat artwork is the panel. Drawing our own plate on top of it would hide the
                // thing we went to the trouble of cloning.
                plate.enabled = false;
            }
            else
            {
                // Without the backdrop the settings would sit on the bare game world, so the plate
                // is still what makes them readable.
                plate.sprite = PanelSprite.Get();
                plate.type = Image.Type.Sliced;
                plate.raycastTarget = true;
            }

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 28, 32);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            return rect;
        }

        /// <summary>
        /// A centred title over a full-width rule, matching the game's own Settings header. The
        /// navigation lives at the bottom of the screen there, not up here beside the title.
        /// </summary>
        private RectTransform BuildHeader(RectTransform panel)
        {
            var header = new GameObject("Header", typeof(RectTransform), typeof(VerticalLayoutGroup));
            header.transform.SetParent(panel, false);

            var layout = header.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var element = header.AddComponent<LayoutElement>();
            element.preferredHeight = 84f;
            element.minHeight = 84f;

            Title = UiText.NewText(header.transform, "Mods", TextAlignmentOptions.Center, Palette.Label, 42f);
            var titleElement = Title.gameObject.AddComponent<LayoutElement>();
            titleElement.preferredHeight = 54f;
            titleElement.minHeight = 54f;

            var rule = new GameObject("Rule", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rule.transform.SetParent(header.transform, false);

            var image = rule.GetComponent<Image>();
            image.color = new Color(Palette.Muted.r, Palette.Muted.g, Palette.Muted.b, 0.45f);
            image.raycastTarget = false;

            var ruleElement = rule.AddComponent<LayoutElement>();
            ruleElement.preferredHeight = 2f;
            ruleElement.minHeight = 2f;

            return (RectTransform)header.transform;
        }

        /// <summary>
        /// Clones the Settings screen's own header - ornament, title, ornament, and the rule beneath
        /// them - rather than approximating it with a line.
        ///
        /// The bar is a single reusable object on every settings page: <c>HeaderContainer</c>, with
        /// <c>OrnamentLeft</c>, <c>TitleText</c>, <c>OrnamentRight</c> and <c>Line</c> inside it. The
        /// flourishes either side are sprites, so no amount of drawing a rectangle gets there.
        /// </summary>
        private bool CloneSettingsHeader(RectTransform parent)
        {
            try
            {
                var source = Resources.FindObjectsOfTypeAll<SettingsGameplayScreen>()
                    .Where(x => x != null && x.gameObject.scene.IsValid())
                    .Select(x => x.transform.Find("Container/HeaderContainer") as RectTransform)
                    .FirstOrDefault(x => x != null);

                if (source == null)
                {
                    ModNookPlugin.Log.LogWarning(
                        "Settings header not found; using a plain rule instead.");
                    return false;
                }

                var header = Templates.CloneInactive(source, "ModNook_Header").gameObject;

                // Destroyed, not disabled: the title is ours to write, and a LocalizedTextField
                // rewrites its own text from Awake the moment the clone is activated.
                Templates.StripLocalization(header, destroy: true);

                foreach (var group in header.GetComponentsInChildren<CanvasGroup>(true))
                {
                    // The source lives on a hidden screen, so its group is faded out.
                    group.alpha = 1f;
                    group.blocksRaycasts = false;
                }

                var texts = header.GetComponentsInChildren<TextMeshProUGUI>(true);
                Title = texts.FirstOrDefault();

                if (Title == null)
                {
                    UnityEngine.Object.DestroyImmediate(header);
                    return false;
                }

                var rect = (RectTransform)header.transform;
                header.transform.SetParent(parent, false);

                // Verbatim from the source. Its children sit at offsets measured against this exact
                // rect on a full-screen parent, and the overlay is the same size, so copying the
                // rect reproduces the placement rather than approximating it.
                rect.anchorMin = source.anchorMin;
                rect.anchorMax = source.anchorMax;
                rect.pivot = source.pivot;
                rect.sizeDelta = source.sizeDelta;
                rect.anchoredPosition = source.anchoredPosition;
                rect.localScale = source.localScale;

                header.SetActive(true);

                // Measured from the rect's actual corners rather than computed from anchoredPosition.
                // The header is anchored to the top of the screen, so its position is an offset
                // downward from that edge - reading it as an offset from the centre put the panel
                // in the bottom third.
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                headerBottom = parent.InverseTransformPoint(corners[0]).y;

                ModNookPlugin.Log.LogInfo(
                    $"Settings header cloned at {rect.anchoredPosition:0} size {rect.rect.size:0}.");
                return true;
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning(
                    $"Could not clone the settings header: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Back on the left, Close on the right with its Esc badge - the game puts its own Close in
        /// the bottom right corner and labels the key that does the same thing.
        /// </summary>
        private RectTransform BuildFooter(RectTransform panel)
        {
            var footer = new GameObject("Footer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            footer.transform.SetParent(panel, false);

            var layout = footer.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var element = footer.AddComponent<LayoutElement>();
            element.preferredHeight = 70f;
            element.minHeight = 70f;

            // Reset sits in the opposite corner from Close, in the same style. No Back button: the
            // cancel prompt already steps back a level, so a second control for it would only take
            // up the middle of the bar.
            ResetButton = PromptButton.Build(
                (RectTransform)Overlay.transform, "Reset", onReset);

            // The game's own corner prompt handles Close when it is available - it draws the real
            // key cap for whatever the player has bound, which a hand-made "ESC" cannot. Only asked
            // here; it is registered and withdrawn as the panel opens and closes.
            UsingGamePrompt = InputPrompt.CanShow();
            if (!UsingGamePrompt)
            {
                var template = PauseMenu.ButtonTemplate(pauseScreen);

                var gap = new GameObject("Gap", typeof(RectTransform));
                gap.transform.SetParent(footer.transform, false);
                gap.AddComponent<LayoutElement>().flexibleWidth = 1f;

                AddKeyBadge(footer.transform, "ESC");
                CloneHeaderButton(template, footer.transform, "Close", onClose);
            }

            return (RectTransform)footer.transform;
        }

        /// <summary>The small outlined key cap the game shows beside a prompt.</summary>
        private static void AddKeyBadge(Transform parent, string key)
        {
            var badge = new GameObject(
                "KeyBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badge.transform.SetParent(parent, false);

            var image = badge.GetComponent<Image>();
            image.sprite = PanelSprite.Get();
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 0.18f);
            image.raycastTarget = false;

            var element = badge.AddComponent<LayoutElement>();
            element.preferredWidth = 74f;
            element.minWidth = 74f;
            element.preferredHeight = 48f;
            element.minHeight = 48f;
            element.flexibleWidth = 0f;

            var text = UiText.NewText(badge.transform, key, TextAlignmentOptions.Center, Palette.Label, 24f);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static AnimatedButton CloneHeaderButton(
            AnimatedButton template, Transform parent, string label, Action onClick)
        {
            if (template == null)
            {
                return null;
            }

            var button = Templates.CloneButton(
                template, parent, $"ModNook_{label}", label, onClick);

            var element = button.gameObject.GetComponent<LayoutElement>();
            if (element != null)
            {
                element.preferredWidth = 200f;
            }

            return button;
        }

        /// <summary>
        /// The body: mods down the left, the selected mod's settings on the right.
        ///
        /// Replaces a list page that you entered and backed out of. With both on screen the mods
        /// are always reachable, comparing two of them costs one click instead of three, and the
        /// header stops changing under you.
        /// </summary>
        private void BuildBody(RectTransform panel)
        {
            var body = new GameObject("Body", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            body.transform.SetParent(panel, false);

            var layout = body.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;

            var element = body.AddComponent<LayoutElement>();
            element.flexibleHeight = 1f;

            Sidebar = BuildScroller(body.transform, "Sidebar", TextAnchor.UpperLeft);
            var sidebarElement = Sidebar.gameObject.GetComponentInParent<ScrollRect>()
                .gameObject.GetComponent<LayoutElement>();
            sidebarElement.preferredWidth = SidebarWidth;
            sidebarElement.minWidth = SidebarWidth;
            sidebarElement.flexibleWidth = 0f;

            var divider = new GameObject(
                "Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            divider.transform.SetParent(body.transform, false);

            var dividerImage = divider.GetComponent<Image>();
            dividerImage.color = new Color(Palette.Muted.r, Palette.Muted.g, Palette.Muted.b, 0.3f);
            dividerImage.raycastTarget = false;

            var dividerElement = divider.AddComponent<LayoutElement>();
            dividerElement.preferredWidth = 2f;
            dividerElement.minWidth = 2f;
            dividerElement.flexibleWidth = 0f;

            Content = BuildScroller(body.transform, "Detail", TextAnchor.UpperCenter);
            var detailElement = Content.gameObject.GetComponentInParent<ScrollRect>()
                .gameObject.GetComponent<LayoutElement>();
            detailElement.flexibleWidth = 1f;
        }

        /// <summary>
        /// A mod with nineteen settings does not fit on any screen, so the content area scrolls.
        /// The rows inside it are still native widgets; only the viewport is ours.
        /// </summary>
        private RectTransform BuildScroller(
            Transform parent, string name, TextAnchor alignment)
        {
            var scroll = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            scroll.transform.SetParent(parent, false);

            var scrollElement = scroll.AddComponent<LayoutElement>();
            scrollElement.flexibleHeight = 1f;

            var viewport = new GameObject(
                "Viewport", typeof(RectTransform), typeof(RectMask2D),
                typeof(CanvasRenderer), typeof(Image));
            viewport.transform.SetParent(scroll.transform, false);
            UiText.Stretch((RectTransform)viewport.transform);

            // An invisible but raycastable viewport. Scroll events travel up from whatever the
            // pointer is over, so with nothing here they only reach the ScrollRect while the
            // pointer happens to be on a row - over the gap between two rows they hit the panel
            // behind instead and scrolling just stops until the mouse moves.
            var catcher = viewport.GetComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            var contentObject = new GameObject(
                "Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport.transform, false);

            var scrollContent = (RectTransform)contentObject.transform;
            scrollContent.anchorMin = new Vector2(0f, 1f);
            scrollContent.anchorMax = new Vector2(1f, 1f);
            scrollContent.pivot = new Vector2(0.5f, 1f);
            scrollContent.offsetMin = Vector2.zero;
            scrollContent.offsetMax = Vector2.zero;

            var layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childAlignment = alignment;
            // Rows are full-width settings rows whose label is anchored left and control right, so
            // they are given the viewport's width and left to place their own contents.
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            // Height is controlled, paired with the per-row LayoutElement that Templates.Clone
            // carries over. Without that a row measures as zero and every row stacks on one line.
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            // The content grows to whatever the rows need, which is what lets bigger text scroll
            // instead of clipping.
            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var rect = scroll.GetComponent<ScrollRect>();
            rect.viewport = (RectTransform)viewport.transform;
            rect.content = scrollContent;
            rect.horizontal = false;
            rect.vertical = true;
            rect.movementType = ScrollRect.MovementType.Clamped;
            rect.scrollSensitivity = 40f;

            return scrollContent;
        }
    }
}
