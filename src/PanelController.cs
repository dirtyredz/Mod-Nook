using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chicken.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModNook
{
    /// <summary>
    /// The panel itself: a mod list, and a page per mod.
    ///
    /// Lives on the pause screen so it shares its lifetime, but draws onto the shared canvas so it
    /// can cover the pause menu rather than sit inside it.
    /// </summary>
    internal sealed class PanelController : MonoBehaviour
    {
        private enum Page
        {
            Closed,
            ModList,
            Mod,
        }

        private PauseScreen pauseScreen;
        private AnimatedButton nookButton;
        
        private GameObject resetButton;

        private GameObject overlay;
        private RectTransform content;
        private TextMeshProUGUI title;

        private List<ModCatalog.ModEntry> mods = new List<ModCatalog.ModEntry>();
        private ModCatalog.ModEntry openMod;
        private Page page = Page.Closed;

        /// <summary>True when the corner prompt is the game's, so we drew no Close of our own.</summary>
        private bool usingGamePrompt;

        /// <summary>
        /// True while the panel is up. Read by the pause-input patch, which is what stops Escape
        /// from closing the pause menu out from under us.
        /// </summary>
        internal static bool IsOpen { get; private set; }

        internal static void Attach(PauseScreen screen)
        {
            if (screen == null)
            {
                return;
            }

            var controller = screen.GetComponent<PanelController>() ??
                             screen.gameObject.AddComponent<PanelController>();
            controller.Initialize(screen);
        }

        /// <summary>
        /// The overlay is parented to the canvas rather than the pause screen, so hiding the pause
        /// screen does not hide it. Without this it survives Esc and looks like it reopened itself.
        /// </summary>
        internal static void CloseFor(PauseScreen screen)
        {
            var controller = screen != null ? screen.GetComponent<PanelController>() : null;
            controller?.Close();
        }

        private void Initialize(PauseScreen screen)
        {
            pauseScreen = screen;

            // Every trip through the pause menu starts closed, whatever the last one left behind.
            Close();

            if (nookButton != null)
            {
                nookButton.gameObject.SetActive(true);
                StartCoroutine(FitWhenSettled());
                return;
            }

            PauseMenu.DumpHierarchy(screen);
            nookButton = PauseMenu.AddButton(screen, ModNookPlugin.PluginName, Open);
            StartCoroutine(FitWhenSettled());
        }

        /// <summary>
        /// Measures at the end of the frame rather than immediately.
        ///
        /// Every mod adds its pause button from the same <c>OnShow</c>, in load order, so measuring
        /// inline counts only the buttons added before us - the panel then grows a little on each
        /// visit instead of fitting the first time. Waiting until the frame is done means whatever
        /// else is being added has been added.
        /// </summary>
        private IEnumerator FitWhenSettled()
        {
            yield return new WaitForEndOfFrame();
            PauseMenu.FitToContents(pauseScreen);
        }

        /// <summary>
        /// Steps back one page, on behalf of the pause-input patch. Ignored while a dialog is up -
        /// those handle their own cancel and would otherwise be dismissed along with the page
        /// behind them.
        /// </summary>
        internal static void RequestBack()
        {
            if (active == null)
            {
                return;
            }

            // When a dialog is open, cancel closes it rather than stepping the panel back.
            // This lets gamepad cancel (B on Steam Deck) dismiss these dialogs, since they
            // have no physical Escape key.
            if (KeyCapture.IsOpen) { KeyCapture.CloseAny(); return; }
            if (ListEditor.IsOpen) { ListEditor.CloseAny(); return; }
            if (ColorPicker.IsOpen) { ColorPicker.CloseAny(); return; }

            active.Back();
        }

        private static PanelController active;

        /// <summary>Reset only makes sense on a mod's own page, not over the list of mods.</summary>
        private void SetResetVisible(bool visible)
        {
            if (resetButton != null)
            {
                resetButton.SetActive(visible);
            }
        }

        private void Close()
        {
            page = Page.Closed;
            IsOpen = false;

            if (active == this)
            {
                active = null;
            }

            // Before the overlay goes: the dialog is a child of it, so one left open would come
            // back with the overlay and sit over everything.
            KeyCapture.CloseAny();
            ListEditor.CloseAny();
            ColorPicker.CloseAny();
            Tooltip.Hide();

            // The prompt bar is shared, so ours has to be withdrawn or it stays in the corner of
            // every other menu the player opens.
            InputPrompt.Hide();

            if (overlay != null)
            {
                overlay.SetActive(false);

                // Whatever a dialog borrowed, the panel gets back.
                if (Rows.OverlayGroup != null)
                {
                    Rows.OverlayGroup.blocksRaycasts = true;
                }
            }
        }

        // ------------------------------------------------------------------ navigation

        private void Open()
        {
            try
            {
                EnsureOverlay();

                // Re-resolved on every open. The pause screen is destroyed and rebuilt across
                // sessions, so a template captured when the overlay was first built becomes a
                // destroyed object - and a dialog that checks it for null then renders with no
                // buttons at all, which is a dialog you cannot leave.
                Rows.ButtonTemplate = PauseMenu.ButtonTemplate(pauseScreen);

                mods = ModCatalog.Discover();
                ShowModList();
                overlay.SetActive(true);
                overlay.transform.SetAsLastSibling();
                IsOpen = true;
                active = this;

                if (usingGamePrompt)
                {
                    InputPrompt.Show();
                }
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogError($"Could not open the panel: {e}");
            }
        }

        /// <summary>
        /// Fills the sidebar and opens the first mod, so the panel never shows an empty right-hand
        /// side waiting to be told what to display.
        /// </summary>
        private void ShowModList()
        {
            page = Page.ModList;
            openMod = null;
            title.text = ModNookPlugin.PluginName;

            SetResetVisible(false);

            ClearContent();
            ClearSidebar();
            modButtons.Clear();

            if (mods.Count == 0)
            {
                AddHeading("No installed mod exposes any settings.");
                return;
            }

            foreach (var mod in mods)
            {
                AddModButton(mod);
            }

            ShowMod(mods[0]);
        }

        private void ClearSidebar()
        {
            for (var i = sidebar.childCount - 1; i >= 0; i--)
            {
                Destroy(sidebar.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// Marks the open mod in the sidebar by lighting the plate behind its row, leaving the
        /// button itself - and so the game's own hover colour - untouched.
        /// </summary>
        private void HighlightSelected()
        {
            foreach (var pair in modButtons)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                var plate = pair.Value.GetComponent<Image>();
                if (plate != null)
                {
                    plate.enabled = pair.Key == openMod;
                }
            }
        }

        private readonly Dictionary<ModCatalog.ModEntry, GameObject> modButtons =
            new Dictionary<ModCatalog.ModEntry, GameObject>();

        private void ShowMod(ModCatalog.ModEntry mod)
        {
            page = Page.Mod;
            openMod = mod;

            // The header keeps naming the panel; the mod names itself at the top of its own column,
            // the way the sidebar layouts we took this from do it.
            title.text = ModNookPlugin.PluginName;

            SetResetVisible(true);
            HighlightSelected();

            ClearContent();

            AddHeading(
                string.IsNullOrEmpty(mod.Version) ? mod.Name : $"{mod.Name}  {mod.Version}",
                first: true);

            var first = true;

            foreach (var section in mod.Sections)
            {
                // A single-section mod does not need a heading telling it so.
                if (mod.Sections.Count > 1)
                {
                    AddHeading(section.Name, first);
                }

                first = false;

                foreach (var entry in section.Entries)
                {
                    // Per setting, so one that cannot be rendered costs its own row and not every
                    // row after it. Without this a single throw ends the loop, and the page comes
                    // out silently missing its remaining settings.
                    try
                    {
                        // Widget and info icon share a row, so the icon sits at the end of the
                        // setting it explains rather than on a line of its own.
                        var host = NewSettingHost();

                        if (!Rows.Build(entry, host, () => Persist(mod)))
                        {
                            Rows.BuildText(entry, host, () => Persist(mod));
                        }

                        if (ModNookPlugin.ShowDescriptions.Value)
                        {
                            Rows.AddInfoIcon(entry, host);
                        }

                        ExpandWidget(host);
                    }
                    catch (Exception e)
                    {
                        ModNookPlugin.Log.LogWarning(
                            $"Could not build a row for {mod.Name}/{entry.Definition.Key}: {e}");
                    }
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        /// <summary>
        /// Puts every setting on the open mod's page back to the default its author chose.
        ///
        /// Behind a confirmation because it is not undoable from in here - the previous values are
        /// gone once the file is written.
        /// </summary>
        private void AskReset()
        {
            var mod = openMod;
            if (mod == null)
            {
                return;
            }

            Confirm.Ask($"Reset {mod.Name} to its default settings?", () => Reset(mod));
        }

        private void Reset(ModCatalog.ModEntry mod)
        {
            var changed = 0;

            foreach (var entry in mod.Sections.SelectMany(section => section.Entries))
            {
                try
                {
                    if (Equals(entry.BoxedValue, entry.DefaultValue))
                    {
                        continue;
                    }

                    entry.BoxedValue = entry.DefaultValue;
                    changed++;
                }
                catch (Exception e)
                {
                    // One setting that refuses to reset should not strand the rest at their old
                    // values with no word of why.
                    ModNookPlugin.Log.LogWarning(
                        $"Could not reset {mod.Name}/{entry.Definition.Key}: {e.Message}");
                }
            }

            Persist(mod);
            ModNookPlugin.Log.LogInfo($"Reset {changed} setting(s) in {mod.Name}.");

            // Rebuilt rather than refreshed: every widget was seeded with the old value when it was
            // created, and nothing tells them the config changed underneath.
            if (page == Page.Mod && openMod == mod)
            {
                ShowMod(mod);
            }
        }

        /// <summary>
        /// With the mods always on screen there is no page to step back to, so cancel closes the
        /// panel outright.
        /// </summary>
        private void Back()
        {
            Close();
        }

        /// <summary>
        /// Writes the change through immediately. A settings panel that needs a separate Save is a
        /// settings panel people lose work in, and BepInEx already writes the whole file at once.
        /// </summary>
        private static void Persist(ModCatalog.ModEntry mod)
        {
            try
            {
                mod.Config.Save();
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning($"Could not save {mod.Name}'s config: {e.Message}");
            }
        }

        // ------------------------------------------------------------------ construction

        private void EnsureOverlay()
        {
            if (overlay != null)
            {
                return;
            }

            // includeInactive: true is required on Linux/Proton where canvas ancestors may still
            // be inactive when OnShow fires, causing a null return and wrong overlay parenting.
            var canvas = pauseScreen.GetComponentInParent<Canvas>(true);
            var parent = canvas != null ? canvas.transform : pauseScreen.transform;

            ModNookPlugin.Log.LogInfo(canvas != null
                ? $"Overlay parented to canvas '{canvas.name}' sortOrder={canvas.sortingOrder}"
                : "No canvas found in parent chain; overlay parented to pause screen.");

            overlay = new GameObject(
                "ModNook_Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(parent, false);
            Stretch((RectTransform)overlay.transform);

            var dim = overlay.GetComponent<Image>();
            dim.color = new Color(0.025f, 0.012f, 0.055f, 0.85f);
            dim.raycastTarget = true;

            // The game's own settings backdrop, bats and all.
            var hasBackdrop = AddBackdrop((RectTransform)overlay.transform);
            if (hasBackdrop)
            {
                // Nothing between the artwork and the settings. The game's Settings screen draws
                // straight onto this background - no dim, no window - and a plum plate laid over
                // the bats is just the old panel with a picture hidden behind it. The image stays
                // enabled but fully clear, because it is also what catches stray clicks.
                dim.color = new Color(0f, 0f, 0f, 0f);
            }

            // Rows need a handle on this to stand the overlay down while a popup is open, or the
            // popup opens behind it and nothing, including Escape, reaches it.
            Rows.OverlayGroup = overlay.AddComponent<CanvasGroup>();
            Rows.OverlayRoot = (RectTransform)overlay.transform;
            Rows.ButtonTemplate = PauseMenu.ButtonTemplate(pauseScreen);

            // The header goes on the overlay rather than in the panel, and keeps the exact rect it
            // has on the Settings screen. Both are full-screen, so copying its anchors lands it
            // where the game puts it - and its children are anchored for that size, so letting a
            // layout group resize it is what threw the title and ornaments apart.
            var hasHeader = CloneSettingsHeader((RectTransform)overlay.transform);

            var panel = BuildPanel((RectTransform)overlay.transform, hasBackdrop, hasHeader);
            Tooltip.Ensure((RectTransform)overlay.transform);

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
                    DestroyImmediate(screen);
                }

                // The backdrop is clickable in its own right - it dismisses the screen it belongs
                // to. Ours belongs to nothing, so the handler is removed rather than left to fire.
                foreach (var background in backdrop.GetComponentsInChildren<UIScreenBackground>(true))
                {
                    DestroyImmediate(background);
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
                    Stretch(rect);
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

        /// <summary>Y of the cloned header's lower edge, in overlay space. Content starts below it.</summary>
        private float headerBottom;

        /// <summary>Breathing room between the header's rule and the first setting.</summary>
        private const float HeaderGap = 24f;

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

            title = NewText(header.transform, "Mods", TextAlignmentOptions.Center, Palette.Label, 42f);
            var titleElement = title.gameObject.AddComponent<LayoutElement>();
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
                title = texts.FirstOrDefault();

                if (title == null)
                {
                    DestroyImmediate(header);
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
            resetButton = PromptButton.Build(
                (RectTransform)overlay.transform, "Reset", AskReset);

            // The game's own corner prompt handles Close when it is available - it draws the real
            // key cap for whatever the player has bound, which a hand-made "ESC" cannot. Only asked
            // here; it is registered and withdrawn as the panel opens and closes.
            usingGamePrompt = InputPrompt.CanShow();
            if (!usingGamePrompt)
            {
                var template = PauseMenu.ButtonTemplate(pauseScreen);

                var gap = new GameObject("Gap", typeof(RectTransform));
                gap.transform.SetParent(footer.transform, false);
                gap.AddComponent<LayoutElement>().flexibleWidth = 1f;

                AddKeyBadge(footer.transform, "ESC");
                CloneHeaderButton(template, footer.transform, "Close", Close);
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

            var text = NewText(badge.transform, key, TextAlignmentOptions.Center, Palette.Label, 24f);
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
        /// A mod with nineteen settings does not fit on any screen, so the content area scrolls.
        /// The rows inside it are still native widgets; only the viewport is ours.
        /// </summary>
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

            sidebar = BuildScroller(body.transform, "Sidebar", TextAnchor.UpperLeft);
            var sidebarElement = sidebar.gameObject.GetComponentInParent<ScrollRect>()
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

            content = BuildScroller(body.transform, "Detail", TextAnchor.UpperCenter);
            var detailElement = content.gameObject.GetComponentInParent<ScrollRect>()
                .gameObject.GetComponent<LayoutElement>();
            detailElement.flexibleWidth = 1f;
        }

        /// <summary>Width of the mod list. Wide enough for the longest name we ship against.</summary>
        private const float SidebarWidth = 380f;

        private RectTransform sidebar;

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
            Stretch((RectTransform)viewport.transform);

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

        // ------------------------------------------------------------------ content

        private void ClearContent()
        {
            for (var i = content.childCount - 1; i >= 0; i--)
            {
                Destroy(content.GetChild(i).gameObject);
            }
        }

        private void AddModButton(ModCatalog.ModEntry mod)
        {
            var template = PauseMenu.ButtonTemplate(pauseScreen);
            if (template == null)
            {
                return;
            }

            // A row that owns the highlight, with the button living inside it. Selection used to be
            // a colour, which meant taking the label's colour away from the game - and that took
            // the orange hover with it. A plate behind the row says the same thing without touching
            // anything the button does.
            var row = new GameObject(
                $"ModRow_{mod.Guid}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(HorizontalLayoutGroup));
            row.transform.SetParent(sidebar, false);

            var plate = row.GetComponent<Image>();
            plate.sprite = PanelSprite.Get();
            plate.type = Image.Type.Sliced;
            plate.color = new Color32(0x6B, 0x3F, 0xA8, 0xC0);
            plate.enabled = false;
            plate.raycastTarget = false;

            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandHeight = true;

            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 68f;
            rowElement.minHeight = 68f;

            var button = Templates.CloneButton(
                template, row.transform, $"ModNook_Mod_{mod.Guid}", mod.Name,
                () => ShowMod(mod), 68f);

            // The label is centred by the button's own layout group, so setting the text's alignment
            // alone does nothing - the group has to be told to start from the left as well.
            var buttonLayout = button.GetComponent<HorizontalLayoutGroup>();
            if (buttonLayout != null)
            {
                buttonLayout.childAlignment = TextAnchor.MiddleLeft;
                buttonLayout.childControlWidth = true;
                buttonLayout.childForceExpandWidth = true;
                buttonLayout.padding = new RectOffset(24, 12, 0, 0);
            }

            foreach (var text in button.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.alignment = TextAlignmentOptions.MidlineLeft;

                // The template's own text is sized for a short caption like "Settings" and is left
                // to overflow rather than wrap or clip. A third-party mod's display name routinely
                // runs longer than the sidebar is wide, and without this it draws straight past the
                // row's plate and over whatever sits beside it instead of stopping at the edge.
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }

            modButtons[mod] = row;
        }

        /// <summary>
        /// One setting's row: the widget takes the width it is given, the info icon takes a fixed
        /// slot at the end. Height is left to the widget, which reports its own via the
        /// LayoutElement that <see cref="Templates.Place{T}"/> carried over from the template.
        /// </summary>
        private Transform NewSettingHost()
        {
            var host = new GameObject("Setting", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            host.transform.SetParent(content, false);

            var layout = host.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            // The indent lives here rather than on the rows we build, so it applies to the game's
            // own widgets too. Cloned sliders and cycles have no padding of their own, so their
            // labels sat flush left while every row we drew was indented past them.
            layout.padding = new RectOffset(20, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            // Force-expand overrides a child's own flexibleWidth - Unity floors it at 1 - so the
            // info icon inflated to fill the row no matter what it asked for. The widget is given
            // the flexible width explicitly instead, in ExpandWidget below.
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            return host.transform;
        }

        /// <summary>
        /// Gives the setting's widget all the width the info icon does not want.
        /// </summary>
        private static void ExpandWidget(Transform host)
        {
            if (host.childCount == 0)
            {
                return;
            }

            var widget = host.GetChild(0).gameObject;
            var element = widget.GetComponent<LayoutElement>() ?? widget.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
        }

        private void AddHeading(string text, bool first = false)
        {
            // Air above every group but the first, where the header already provides the break.
            if (!first)
            {
                AddSpacer(24f);
            }

            var heading = NewText(content, text, TextAlignmentOptions.MidlineLeft, Palette.Muted, 30f);

            var element = heading.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 48f;
            element.minHeight = 48f;

            // Underlined, so the rule belongs to the title rather than floating between groups.
            AddRule();
            AddSpacer(6f);
        }

        private void AddSpacer(float height)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(content, false);

            var element = spacer.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private void AddRule()
        {
            var rule = new GameObject("Rule", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rule.transform.SetParent(content, false);

            var image = rule.GetComponent<Image>();
            image.color = new Color(Palette.Muted.r, Palette.Muted.g, Palette.Muted.b, 0.3f);
            image.raycastTarget = false;

            var element = rule.AddComponent<LayoutElement>();
            element.preferredHeight = 2f;
            element.minHeight = 2f;
        }

        private static TextMeshProUGUI NewText(
            Transform parent, string content, TextAlignmentOptions alignment, Color colour, float size)
        {
            var host = new GameObject("Text", typeof(RectTransform));
            host.transform.SetParent(parent, false);

            var text = host.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.alignment = alignment;
            text.color = colour;
            text.fontSize = size;

            // Text on our own object has no neighbour to inherit from, so the game's font has to be
            // applied by hand. See 10-visual-integration.md.
            GameFonts.Apply(text, preferOutline: false);

            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
