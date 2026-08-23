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
        private OverlayContext overlayContext;
        private RectTransform content;
        private RectTransform sidebar;
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

            HierarchyDebug.Dump(screen);
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
            // have no physical Escape key. One handle closes whatever modal is up, so a new
            // dialog kind is covered the moment it exists.
            if (ModalDialog.IsAnyOpen) { ModalDialog.CloseCurrent(); return; }

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
            ModalDialog.CloseCurrent();
            Tooltip.Hide();

            // The prompt bar is shared, so ours has to be withdrawn or it stays in the corner of
            // every other menu the player opens.
            InputPrompt.Hide();

            if (overlay != null)
            {
                overlay.SetActive(false);

                // Whatever a dialog borrowed, the panel gets back.
                if (overlayContext?.Group != null)
                {
                    overlayContext.Group.blocksRaycasts = true;
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
                overlayContext.ButtonTemplate = PauseMenu.ButtonTemplate(pauseScreen);

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

                        if (!Rows.Build(entry, host, () => Persist(mod), overlayContext))
                        {
                            Rows.BuildText(entry, host, () => Persist(mod), overlayContext);
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

            Confirm.Ask(
                $"Reset {mod.Name} to its default settings?", overlayContext?.Group, () => Reset(mod));
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

            // The chrome - overlay, backdrop, header, panel, body scrollers and footer - is built
            // once by PanelChrome; the controller keeps the handles it drives. The footer's Reset and
            // Close buttons route back here through the actions passed in.
            var chrome = PanelChrome.Build(pauseScreen, Close, AskReset);
            overlay = chrome.Overlay;
            overlayContext = chrome.Context;
            content = chrome.Content;
            sidebar = chrome.Sidebar;
            title = chrome.Title;
            resetButton = chrome.ResetButton;
            usingGamePrompt = chrome.UsingGamePrompt;
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

            var heading = UiText.NewText(content, text, TextAlignmentOptions.MidlineLeft, Palette.Muted, 30f);

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

    }
}
