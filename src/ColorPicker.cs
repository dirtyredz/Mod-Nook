using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Chicken.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModNook
{
    /// <summary>
    /// Edits a hex colour setting as a colour rather than as six characters of text.
    ///
    /// <para>
    /// Two ways in, because they answer different questions. The palette is the game's own
    /// <see cref="ColorLibrary"/>, so "something that looks like it belongs" is one click and
    /// cannot be got wrong. The sliders are the game's own <see cref="SliderButton"/>, for when the
    /// answer is a particular colour rather than a tasteful one.
    /// </para>
    /// <para>
    /// The stored value stays a hex string in the format it was already in, so the mod parses
    /// exactly what it always did and hand-editing still works.
    /// </para>
    /// </summary>
    internal sealed class ColorPicker : MonoBehaviour
    {
        private static ColorPicker open;

        /// <summary>True while the dialog is up, so the panel leaves cancel to it.</summary>
        internal static bool IsOpen => open != null;

        private ConfigEntryBase entry;
        private AnimatedButton buttonTemplate;
        private Action<string> onSave;

        private GameObject root;
        private CanvasGroup group;
        private Image preview;
        private TextMeshProUGUI hexText;

        private Color current = Color.white;

        /// <summary>Whether the value being edited carried an alpha pair to begin with.</summary>
        private bool hasAlpha;

        /// <summary>
        /// True when this setting reads as a colour. A hex-shaped value is the strongest signal; a
        /// description that says so covers the case where the value is currently blank.
        /// </summary>
        internal static bool Suits(ConfigEntryBase entry)
        {
            if (entry.SettingType != typeof(string))
            {
                return false;
            }

            if (Tags.Has(entry, "Color") || Tags.Has(entry, "Colour"))
            {
                return true;
            }

            if (LooksLikeHex(entry.BoxedValue as string))
            {
                return true;
            }

            var description = entry.Description?.Description;
            if (string.IsNullOrEmpty(description))
            {
                return false;
            }

            // "as hex" alone is not enough - a hex id is not a colour - so the word colour has to
            // appear too.
            return description.IndexOf("colour", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   description.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeHex(string value)
        {
            var text = value?.Trim().TrimStart('#');
            if (string.IsNullOrEmpty(text) || (text.Length != 6 && text.Length != 8))
            {
                return false;
            }

            return text.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
        }

        internal static void Open(
            RectTransform parent, ConfigEntryBase entry, AnimatedButton buttonTemplate,
            Action<string> onSave)
        {
            CloseAny();

            var host = new GameObject("ModNook_ColorPicker", typeof(RectTransform));
            host.transform.SetParent(parent, false);
            host.transform.SetAsLastSibling();

            var picker = host.AddComponent<ColorPicker>();
            picker.entry = entry;
            picker.buttonTemplate = buttonTemplate;
            picker.onSave = onSave;

            // Registered before it is built, not after. If building throws, a dialog assigned only
            // on success is one nothing knows about - CloseAny cannot reach it and IsOpen denies it
            // exists, while the half-built thing sits on screen blocking every click.
            open = picker;

            picker.Build(host);
        }

        /// <summary>
        /// Runs one section of the build, and logs rather than throws. A dialog that stops halfway
        /// through leaves the player looking at whatever happened to be built before the failure.
        /// </summary>
        private void Step(string name, Action build)
        {
            try
            {
                build();
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogError($"Colour picker '{name}' failed: {e}");
            }
        }

        internal static void CloseAny()
        {
            if (open != null)
            {
                open.Close();
            }

            open = null;
        }

        private void Build(GameObject host)
        {
            root = host;

            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.offsetMin = Vector2.zero;
            hostRect.offsetMax = Vector2.zero;

            var dim = host.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.01f, 0.04f, 0.8f);
            dim.raycastTarget = true;

            group = host.AddComponent<CanvasGroup>();

            var panel = new GameObject(
                "Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(host.transform, false);

            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(900f, 0f);

            var plate = panel.GetComponent<Image>();
            plate.sprite = PanelSprite.Get();
            plate.type = Image.Type.Sliced;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(44, 44, 32, 32);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Step("read", ReadCurrent);

            Step("title", () => Text(panel.transform, Rows.LabelOf(entry), 34f, Palette.Label));
            Step("preview", () => BuildPreview(panel.transform));
            Step("palette", () => BuildPalette(panel.transform));
            Step("sliders", () => BuildSliders(panel.transform));

            // Built through the same guard as everything else, so a section that fails takes only
            // itself down. Losing the sliders is a nuisance; losing the buttons behind them is a
            // dialog the player cannot leave.
            Step("buttons", () => BuildButtons(panel.transform));

            Step("footnote", () => Text(
                panel.transform,
                "Saved as a hex value, the same as the mod's own config file uses. Clear leaves the " +
                "setting empty, which is what a mod reads as \"use my default\".",
                20f, Palette.Muted));

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            ClampToScreen(panelRect, hostRect);
        }

        /// <summary>
        /// Scales the dialog down if it has grown taller than the screen.
        ///
        /// Its height comes from its contents and nothing was stopping that exceeding 1080px - at
        /// which point the buttons are below the bottom edge and the dialog looks like it has none.
        /// Shrinking is worse than laying out properly, but it is a guarantee rather than a size
        /// that happens to fit, and it says so in the log when it fires.
        /// </summary>
        private static void ClampToScreen(RectTransform panel, RectTransform bounds)
        {
            var available = bounds.rect.height * 0.92f;
            var needed = panel.rect.height;

            if (needed <= available || needed <= 0f)
            {
                return;
            }

            var scale = available / needed;
            panel.localScale = new Vector3(scale, scale, 1f);

            ModNookPlugin.Log.LogWarning(
                $"The colour picker needed {needed:0}px of a {bounds.rect.height:0}px screen and " +
                $"was scaled to {scale:0.00}. Its layout should be shortened instead.");
        }

        private void ReadCurrent()
        {
            var raw = (entry.BoxedValue as string)?.Trim();
            hasAlpha = !string.IsNullOrEmpty(raw) && raw.TrimStart('#').Length == 8;

            if (!string.IsNullOrEmpty(raw) &&
                ColorUtility.TryParseHtmlString(raw.StartsWith("#") ? raw : "#" + raw, out var parsed))
            {
                current = parsed;
                return;
            }

            // A blank or unreadable value still needs something on the sliders. The game's panel
            // plum is a better starting point than white.
            current = new Color32(0x4A, 0x2E, 0x8F, 0xFF);
        }

        private void BuildPreview(Transform parent)
        {
            var row = new GameObject("Preview", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;

            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 76f;
            element.minHeight = 76f;

            var swatch = new GameObject(
                "Swatch", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            swatch.transform.SetParent(row.transform, false);

            preview = swatch.GetComponent<Image>();
            preview.sprite = PanelSprite.Plain();
            preview.type = Image.Type.Sliced;
            preview.color = current;

            var swatchElement = swatch.AddComponent<LayoutElement>();
            swatchElement.preferredWidth = 120f;
            swatchElement.minWidth = 120f;
            swatchElement.flexibleWidth = 0f;

            hexText = Text(row.transform, Hex(), 30f, Palette.Label);
            hexText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        /// <summary>
        /// The game's own colours, so "something that fits" needs no colour theory. Falls back to
        /// the palette documented in 10-visual-integration.md if the library is not loaded.
        /// </summary>
        private void BuildPalette(Transform parent)
        {
            var colours = LibraryColours();
            if (colours.Count == 0)
            {
                return;
            }

            Text(parent, "The game's colours", 22f, Palette.Muted);

            var grid = new GameObject("Palette", typeof(RectTransform), typeof(GridLayoutGroup));
            grid.transform.SetParent(parent, false);

            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(54f, 54f);
            layout.spacing = new Vector2(8f, 8f);
            layout.childAlignment = TextAnchor.MiddleCenter;

            layout.cellSize = new Vector2(48f, 48f);

            var columns = Mathf.Max(1, Mathf.Min(colours.Count, PaletteColumns));
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;

            var rows = Mathf.CeilToInt(colours.Count / (float)columns);
            var element = grid.AddComponent<LayoutElement>();
            element.preferredHeight = rows * 56f;
            element.minHeight = element.preferredHeight;

            foreach (var colour in colours)
            {
                AddSwatch(grid.transform, colour);
            }
        }

        private void AddSwatch(Transform parent, Color colour)
        {
            var swatch = new GameObject(
                "Swatch", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            swatch.transform.SetParent(parent, false);

            var image = swatch.GetComponent<Image>();
            // Untinted, so the swatch is the colour it claims to be rather than that colour seen
            // through the panel plate's baked-in plum.
            image.sprite = PanelSprite.Plain();
            image.type = Image.Type.Sliced;
            image.color = colour;
            image.raycastTarget = true;

            var button = swatch.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                // Alpha is left as it was: picking a hue should not silently make a translucent
                // setting opaque.
                current = new Color(colour.r, colour.g, colour.b, current.a);
                Refresh();
                PushToSliders();
            });
        }

        private static List<Color> LibraryColours()
        {
            var colours = new List<Color>();

            try
            {
                var library = AddressableLibrary<ColorLibrary>.Instance;
                if (library?.Categories != null)
                {
                    foreach (var category in library.Categories)
                    {
                        if (category?.Colors == null)
                        {
                            continue;
                        }

                        colours.AddRange(category.Colors.Where(c => c != null).Select(c => c.Color));
                    }
                }
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning($"Colour library unavailable: {e.Message}");
            }

            if (colours.Count == 0)
            {
                // See 10-visual-integration.md - the palette measured from the shipped game.
                colours.AddRange(new[]
                {
                    (Color)new Color32(0x1B, 0x0F, 0x2E, 0xFF),
                    (Color)new Color32(0x2A, 0x1B, 0x3D, 0xFF),
                    (Color)new Color32(0x4A, 0x2E, 0x8F, 0xFF),
                    (Color)new Color32(0x5A, 0x2D, 0x91, 0xFF),
                    (Color)new Color32(0xC7, 0xA2, 0x5B, 0xFF),
                    (Color)new Color32(0xF7, 0xD9, 0x94, 0xFF),
                });
            }

            // Duplicates are common across categories. The cap matters more than it looks: the
            // dialog sizes itself to its contents with no ceiling, so every extra row of swatches
            // pushes the buttons further down - and past 1080px they leave the screen entirely,
            // which is how this became a dialog with no visible way out.
            return colours
                .GroupBy(c => ColorUtility.ToHtmlStringRGB(c))
                .Select(g => g.First())
                .Take(PaletteColumns * PaletteRows)
                .ToList();
        }

        private const int PaletteColumns = 14;
        private const int PaletteRows = 2;

        private readonly List<SliderButton> channels = new List<SliderButton>();

        private void BuildSliders(Transform parent)
        {
            var template = Templates.Slider;
            if (template == null)
            {
                return;
            }

            channels.Clear();

            AddChannel(parent, template, "Red", 0);
            AddChannel(parent, template, "Green", 1);
            AddChannel(parent, template, "Blue", 2);
        }

        private void AddChannel(Transform parent, SliderButton template, string label, int index)
        {
            var row = Templates.Clone(template, parent, $"Channel_{label}");
            Templates.SetValueWidgetLabel(row, label);

            row.Setup(
                new SliderButton.Settings
                {
                    MinValue = 0f,
                    MaxValue = 255f,
                    SliderStep = 1f,
                    ButtonStep = 1f,
                    ShowValueAsPercentage = false,
                },
                Channel(index));

            row.OnValueChanged.AddListener(value =>
            {
                var amount = Mathf.Clamp01(value / 255f);

                switch (index)
                {
                    case 0: current.r = amount; break;
                    case 1: current.g = amount; break;
                    default: current.b = amount; break;
                }

                Refresh();
            });

            channels.Add(row);
        }

        private float Channel(int index)
        {
            var amount = index == 0 ? current.r : index == 1 ? current.g : current.b;
            return Mathf.Round(amount * 255f);
        }

        /// <summary>
        /// Pushes a palette choice back onto the sliders. They were seeded at creation and nothing
        /// tells them the colour changed underneath.
        /// </summary>
        private void PushToSliders()
        {
            for (var i = 0; i < channels.Count; i++)
            {
                channels[i].SetValue(Channel(i));
            }
        }

        private void Refresh()
        {
            preview.color = current;
            hexText.text = Hex();
        }

        private string Hex()
        {
            // The format it arrived in. Widening a mod's six-digit setting to eight could hand it a
            // value its own parser does not accept.
            return "#" + (hasAlpha
                ? ColorUtility.ToHtmlStringRGBA(current)
                : ColorUtility.ToHtmlStringRGB(current));
        }

        private void BuildButtons(Transform parent)
        {
            var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 72f;
            element.minHeight = 72f;

            if (buttonTemplate == null)
            {
                // Never leave the dialog without a way out. Escape works regardless, but a panel
                // with no visible controls reads as broken rather than as keyboard-only.
                ModNookPlugin.Log.LogWarning(
                    "No button template for the colour picker; falling back to plain buttons.");

                PlainButton(row.transform, "Save", Save);
                PlainButton(row.transform, "Clear", Clear);
                PlainButton(row.transform, "Cancel", Close);
                return;
            }

            Templates.CloneButton(buttonTemplate, row.transform, "Save", "Save", Save);
            Templates.CloneButton(buttonTemplate, row.transform, "Clear", "Clear", Clear);
            Templates.CloneButton(buttonTemplate, row.transform, "Cancel", "Cancel", Close);
        }

        /// <summary>A button of our own, for when the game's template cannot be reached.</summary>
        private static void PlainButton(Transform parent, string label, Action onClick)
        {
            var host = new GameObject(
                label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            host.transform.SetParent(parent, false);

            var image = host.GetComponent<Image>();
            image.sprite = PanelSprite.Get();
            image.type = Image.Type.Sliced;

            var button = host.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());

            var text = Text(host.transform, label, 28f, Palette.Label);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Escape always leaves, whatever state the buttons are in. Every dialog in here needs one
        /// exit that does not depend on anything having been built correctly.
        /// </summary>
        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void Save()
        {
            onSave?.Invoke(Hex());
            Close();
        }

        /// <summary>
        /// Blank, not black. A mod that documents an empty value as "use my own default" loses that
        /// option entirely if the only thing this dialog can produce is a colour.
        /// </summary>
        private void Clear()
        {
            onSave?.Invoke(string.Empty);
            Close();
        }

        private void Close()
        {
            if (open == this)
            {
                open = null;
            }

            DestroyImmediate(root);
        }

        private static TextMeshProUGUI Text(
            Transform parent, string content, float size, Color colour)
        {
            var host = new GameObject("Text", typeof(RectTransform));
            host.transform.SetParent(parent, false);

            var text = host.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.alignment = TextAlignmentOptions.Center;
            text.color = colour;
            text.fontSize = size;
            text.enableWordWrapping = true;
            GameFonts.Apply(text, preferOutline: false);

            return text;
        }
    }
}
