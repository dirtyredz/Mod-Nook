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
    /// Turns one <see cref="ConfigEntryBase"/> into the native widget that suits its type, and
    /// wires that widget back to the setting.
    ///
    /// The mapping follows the game's own Settings screen rather than inventing one: every boolean
    /// and enum there is a <see cref="CycleButton"/>, and every bounded number is a
    /// <see cref="SliderButton"/>. Matching it means a player who has used Settings already knows
    /// how this works.
    /// </summary>
    internal static class Rows
    {
        /// <summary>
        /// Builds a row for <paramref name="entry"/> under <paramref name="parent"/>. Returns false
        /// when the type has no widget yet, so the caller can fall back to a read-only line.
        /// </summary>
        internal static bool Build(ConfigEntryBase entry, Transform parent, Action onChanged)
        {
            var label = Label(entry);

            if (entry.SettingType == typeof(bool))
            {
                return BuildBool(entry, parent, label, onChanged);
            }

            // Before the enum branch on purpose. KeyCode *is* an enum, so it would otherwise become
            // a cycle listing every key on the keyboard one arrow-press at a time - which is how
            // Minimap and Save Anywhere were rendering, since they bind a bare KeyCode rather than
            // BepInEx's KeyboardShortcut.
            if (entry.SettingType == typeof(KeyboardShortcut) || entry.SettingType == typeof(KeyCode))
            {
                return BuildKey(entry, parent, label, onChanged);
            }

            if (entry.SettingType.IsEnum)
            {
                return BuildEnum(entry, parent, label, onChanged);
            }

            var choices = ExplicitChoices(entry);
            if (choices != null && choices.Count > 1)
            {
                return BuildChoice(entry, parent, label, choices, onChanged);
            }

            if (TryRange(entry, out var min, out var max))
            {
                return BuildRange(entry, parent, label, min, max, onChanged);
            }

            return false;
        }

        // ------------------------------------------------------------------ widgets

        private static bool BuildBool(
            ConfigEntryBase entry, Transform parent, string label, Action onChanged)
        {
            var template = Templates.Toggle;
            if (template != null)
            {
                // The toggle carries its own centred caption, which put the setting's name inside
                // the pill while every other row has it out on the left. Its label is blanked and
                // ours goes beside it, so a page of mixed row types reads down one column.
                var host = LabelledRow(parent, entry, label, out var slot);

                var row = Templates.Clone(template, slot, $"Row_{entry.Definition.Key}");
                Templates.SetLabel(row.gameObject, string.Empty);

                var element = row.gameObject.GetComponent<LayoutElement>();
                if (element != null)
                {
                    element.preferredWidth = ToggleWidth;
                    element.flexibleWidth = 0f;
                }

                // Set before listening, so seeding the initial state does not read as a change and
                // rewrite the config file on every page build.
                row.ToggleValue = (bool)entry.BoxedValue;
                row.OnValueChange.AddListener(value =>
                {
                    try
                    {
                        entry.BoxedValue = value;
                        onChanged();
                    }
                    catch (Exception e)
                    {
                        ModNookPlugin.Log.LogWarning(
                            $"Could not apply {entry.Definition.Key} = {value}: {e.Message}");
                    }
                });

                _ = host;
                return true;
            }

            // "Off" first so the index lines up with the boolean: 0 is false, 1 is true.
            var options = new List<string> { "Off", "On" };

            return Cycle(entry, parent, label, options, (bool)entry.BoxedValue ? 1 : 0,
                index => entry.BoxedValue = index == 1, onChanged);
        }

        private static bool BuildEnum(
            ConfigEntryBase entry, Transform parent, string label, Action onChanged)
        {
            var values = Enum.GetValues(entry.SettingType).Cast<object>().ToList();
            var names = values.Select(Humanise).ToList();
            var current = Math.Max(0, values.FindIndex(v => Equals(v, entry.BoxedValue)));

            return Cycle(entry, parent, label, names, current,
                index => entry.BoxedValue = values[index], onChanged);
        }

        private static bool BuildChoice(
            ConfigEntryBase entry, Transform parent, string label, List<object> choices,
            Action onChanged)
        {
            var names = choices.Select(c => c?.ToString() ?? string.Empty).ToList();
            var current = Math.Max(0, choices.FindIndex(c => Equals(c, entry.BoxedValue)));

            return Cycle(entry, parent, label, names, current,
                index => entry.BoxedValue = choices[index], onChanged);
        }

        private static bool BuildRange(
            ConfigEntryBase entry, Transform parent, string label, double min, double max,
            Action onChanged)
        {
            var template = Templates.Slider;
            if (template == null)
            {
                return false;
            }

            var isWhole = IsIntegral(entry.SettingType);
            var span = max - min;

            // Integers step by one. Floats get a hundred stops, rounded to something a player can
            // land on deliberately rather than an arbitrary fraction.
            var sliderStep = isWhole ? 1f : NiceStep(span);
            var buttonStep = isWhole ? 1f : sliderStep;

            var row = Templates.Clone(template, parent, $"Row_{entry.Definition.Key}");
            Templates.SetLabel(row.gameObject, label, labelOnly: true);

            row.Setup(
                new SliderButton.Settings
                {
                    MinValue = (float)min,
                    MaxValue = (float)max,
                    SliderStep = sliderStep,
                    ButtonStep = buttonStep,
                    ShowValueAsPercentage = false,
                },
                Convert.ToSingle(entry.BoxedValue));

            row.OnValueChanged.AddListener(value =>
            {
                try
                {
                    entry.BoxedValue = Convert.ChangeType(
                        isWhole ? Math.Round((double)value) : value, entry.SettingType);
                    onChanged();
                }
                catch (Exception e)
                {
                    ModNookPlugin.Log.LogWarning(
                        $"Could not apply {entry.Definition.Key} = {value}: {e.Message}");
                }
            });

            return true;
        }

        private static bool Cycle(
            ConfigEntryBase entry, Transform parent, string label, List<string> options,
            int currentIndex, Action<int> apply, Action onChanged)
        {
            var template = Templates.Cycle;
            if (template == null)
            {
                return false;
            }

            var row = Templates.Clone(template, parent, $"Row_{entry.Definition.Key}");
            Templates.SetLabel(row.gameObject, label, labelOnly: true);

            row.Setup(options, currentIndex);
            row.OnValueChanged.AddListener(index =>
            {
                try
                {
                    apply(index);
                    onChanged();
                }
                catch (Exception e)
                {
                    ModNookPlugin.Log.LogWarning(
                        $"Could not apply {entry.Definition.Key} = {options[index]}: {e.Message}");
                }
            });

            return true;
        }

        /// <summary>
        /// The fallback for everything without a dedicated widget - free-form strings, key
        /// bindings, comma-separated lists. The row shows the current value and opens the game's
        /// own text popup when clicked.
        ///
        /// <para>
        /// Editing goes through <see cref="ConfigEntryBase.SetSerializedValue"/>, which is the same
        /// path BepInEx uses to read the config file. That means this one row handles every type
        /// BepInEx can serialize, and a value the mod would reject from its file is rejected here
        /// too rather than being written and breaking on next launch.
        /// </para>
        /// </summary>
        internal static void BuildText(ConfigEntryBase entry, Transform parent, Action onChanged)
        {
            var row = ClickableRow(entry, parent, out var plate, out var value);

            var popup = TextPopup;
            if (popup == null)
            {
                // No popup means no way to type. Leave the row readable rather than pretending it
                // is interactive.
                return;
            }

            var button = row.AddComponent<Button>();
            button.targetGraphic = plate;

            // A comma-separated setting gets the list editor; everything else gets the text popup.
            if (ListEditor.Suits(entry) && OverlayRoot != null)
            {
                button.onClick.AddListener(() =>
                {
                    Tooltip.Hide();

                    ListEditor.Open(
                        OverlayRoot, entry, ButtonTemplate,
                        joined =>
                        {
                            try
                            {
                                entry.SetSerializedValue(joined);
                                value.text = Summarise(entry);
                                onChanged();
                            }
                            catch (Exception e)
                            {
                                value.text = Summarise(entry);
                                ModNookPlugin.Log.LogWarning(
                                    $"Could not save {entry.Definition.Key}: {e.Message}");
                            }
                        });
                });

                return;
            }

            button.onClick.AddListener(() => Edit(entry, value, onChanged));
        }

        /// <summary>
        /// A key binding. Shares the clickable row of a text setting but opens the capture dialog,
        /// because the alternative is asking a player to spell "LeftAlt" from memory.
        /// </summary>
        private static bool BuildKey(
            ConfigEntryBase entry, Transform parent, string label, Action onChanged)
        {
            if (OverlayRoot == null)
            {
                return false;
            }

            var row = ClickableRow(entry, parent, out var plate, out var value);

            var button = row.AddComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(() =>
            {
                // Deliberately no blocksRaycasts juggling here. The dialog is a child of the
                // overlay, so standing the overlay down switches off the dialog's own raycasts too
                // - clicks then fall through to the pause menu behind and press its buttons. The
                // dialog carries its own full-screen blocker instead.
                Tooltip.Hide();

                KeyCapture.Open(
                    OverlayRoot, entry, ButtonTemplate,
                    shortcut =>
                    {
                        try
                        {
                            // A KeyCode setting holds one key and cannot express modifiers, so it
                            // takes the main key and drops them.
                            entry.BoxedValue = entry.SettingType == typeof(KeyCode)
                                ? (object)shortcut.MainKey
                                : shortcut;

                            value.text = Summarise(entry);
                            onChanged();
                        }
                        catch (Exception e)
                        {
                            ModNookPlugin.Log.LogWarning(
                                $"Could not bind {entry.Definition.Key}: {e.Message}");
                        }
                    });
            });

            return true;
        }

        /// <summary>
        /// The shared chassis for settings edited through a dialog: label on the left, current value
        /// on the right, on a faint plate that says the row does something.
        /// </summary>
        private static GameObject ClickableRow(
            ConfigEntryBase entry, Transform parent, out Image plate, out TextMeshProUGUI value)
        {
            var row = new GameObject($"Row_{entry.Definition.Key}", typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 60f;
            element.minHeight = 60f;

            var label = AddText(
                row.transform, Label(entry), TextAlignmentOptions.MidlineLeft, Palette.Label);
            var labelElement = label.gameObject.AddComponent<LayoutElement>();
            labelElement.flexibleWidth = 1f;

            // The plate goes on the field, not the row. Across the whole row it reads as a grey bar
            // over the setting's name as much as its value, when the only part you can click is the
            // value.
            var field = new GameObject(
                "Field", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(HorizontalLayoutGroup));
            field.transform.SetParent(row.transform, false);

            var fieldLayout = field.GetComponent<HorizontalLayoutGroup>();
            fieldLayout.padding = new RectOffset(16, 16, 0, 0);
            fieldLayout.childControlWidth = true;
            fieldLayout.childForceExpandWidth = true;
            fieldLayout.childControlHeight = true;
            fieldLayout.childForceExpandHeight = true;

            var fieldElement = field.AddComponent<LayoutElement>();
            fieldElement.preferredWidth = FieldWidth;
            fieldElement.minWidth = FieldWidth;
            fieldElement.flexibleWidth = 0f;

            plate = field.GetComponent<Image>();
            plate.sprite = PanelSprite.Get();
            plate.type = Image.Type.Sliced;
            plate.color = new Color(1f, 1f, 1f, 0.12f);
            plate.raycastTarget = true;

            value = AddText(
                field.transform, Summarise(entry), TextAlignmentOptions.Midline, Palette.Muted);

            return field;
        }

        /// <summary>Set by the panel: where dialogs are parented, and what a button looks like.</summary>
        internal static RectTransform OverlayRoot;

        internal static AnimatedButton ButtonTemplate;

        /// <summary>
        /// Opens the game's text popup, handling everything that has to be borrowed and given back.
        /// Shared so the list editor can use the same dialog for adding an entry.
        /// </summary>
        internal static void Prompt(
            string title, string description, string initial, CanvasGroup alsoSuspend,
            Action<string> onConfirm)
        {
            var popup = TextPopup;
            if (popup == null)
            {
                return;
            }

            try
            {
                // Our overlay is the canvas's last sibling and blocks raycasts, so the popup opens
                // behind it - unclickable, and Escape never reaches it either. Stand down until the
                // popup closes.
                SuspendOverlay(popup, alsoSuspend);
                PopupEscape.Arm(popup);

                // The full overload, because the short one fills the field's prefix with the game's
                // "Name:" label - it is the creature-naming dialog's wording, and a setting is not
                // a name.
                popup.Show(
                    title, description, string.Empty, "Save", initial,
                    MaxTextLength, true, null, onConfirm);
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning($"Could not open the text popup: {e.Message}");
            }
        }

        private static void Edit(ConfigEntryBase entry, TextMeshProUGUI valueText, Action onChanged)
        {
            try
            {
                Prompt(
                    Label(entry), Brief(entry), entry.GetSerializedValue(), null,
                    typed =>
                    {
                        try
                        {
                            entry.SetSerializedValue(typed);
                            valueText.text = Summarise(entry);
                            onChanged();
                        }
                        catch (Exception e)
                        {
                            // Put the old value back on screen so the row never claims a change
                            // that did not happen.
                            valueText.text = Summarise(entry);
                            ModNookPlugin.Log.LogWarning(
                                $"'{typed}' is not a valid {entry.SettingType.Name} for " +
                                $"{entry.Definition.Key}: {e.Message}");
                        }
                    });
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning($"Could not open the text popup: {e.Message}");
            }
        }

        /// <summary>
        /// Long enough for a canvas allowlist or a per-screen override list. The game's own default
        /// is twenty characters, which is a creature name, not a setting.
        /// </summary>
        private const int MaxTextLength = 400;

        /// <summary>
        /// The popup grows to fit whatever description it is handed, and several of these mods
        /// write paragraphs - which pushed the dialog off the top and bottom of the screen with no
        /// way to reach its buttons. One line only; the full text lives on the row's info icon.
        /// </summary>
        private const int MaxPopupDescription = 110;

        private static string Brief(ConfigEntryBase entry)
        {
            var description = entry.Description?.Description;
            if (string.IsNullOrEmpty(description))
            {
                return string.Empty;
            }

            var firstLine = description.Split('\n')[0].Trim();
            return firstLine.Length <= MaxPopupDescription
                ? firstLine
                : firstLine.Substring(0, MaxPopupDescription - 3).TrimEnd() + "...";
        }

        /// <summary>
        /// Set by the panel so rows can get their own overlay out of a popup's way. Rows have no
        /// other handle on it, and a popup opening behind an opaque overlay is a soft lock.
        /// </summary>
        internal static CanvasGroup OverlayGroup;

        private static readonly System.Reflection.FieldInfo PrefixField =
            typeof(TextInputPopupScreen).GetField(
                "inputPrefixText",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>
        /// Hides the popup's prefix label rather than merely blanking it.
        ///
        /// Emptying the string leaves the object in the layout, so the gap where "Name:" used to be
        /// stays exactly as wide. It is switched back on afterwards because this is the game's own
        /// popup - the creature-naming dialog needs its label.
        /// </summary>
        private static GameObject HidePrefix(TextInputPopupScreen popup)
        {
            var prefix = PrefixField?.GetValue(popup) as Component;
            if (prefix == null || !prefix.gameObject.activeSelf)
            {
                return null;
            }

            prefix.gameObject.SetActive(false);
            return prefix.gameObject;
        }

        private static void SuspendOverlay(TextInputPopupScreen popup, CanvasGroup also = null)
        {
            var prefix = HidePrefix(popup);
            Tooltip.Hide();

            if (OverlayGroup != null)
            {
                OverlayGroup.blocksRaycasts = false;
            }

            // A dialog stacked on top of the overlay has its own blocker, which would swallow the
            // popup just as the overlay would.
            if (also != null)
            {
                also.blocksRaycasts = false;
            }

            RestoreOn(popup, prefix, OverlayGroup, also);
        }

        /// <summary>
        /// Puts back everything we borrowed, however the player leaves the popup - OnScreenHide
        /// covers Escape and cancel, not just confirm.
        /// </summary>
        private static void RestoreOn(
            TextInputPopupScreen popup, GameObject prefix, CanvasGroup group, CanvasGroup also)
        {
            Action restore = null;
            restore = () =>
            {
                if (group != null)
                {
                    group.blocksRaycasts = true;
                }

                if (also != null)
                {
                    also.blocksRaycasts = true;
                }

                if (prefix != null)
                {
                    prefix.SetActive(true);
                }

                popup.OnScreenHide.RemoveListener(restore);
            };

            popup.OnScreenHide.AddListener(restore);
        }

        private static TextInputPopupScreen TextPopup => UIScreen<TextInputPopupScreen>.Instance;

        /// <summary>
        /// A small circled "i" carrying the author's description on hover.
        ///
        /// The description is the most useful text on the page, but printed under every row it was
        /// also most of the page. An icon keeps the list scannable and the explanation one hover
        /// away. Nothing is added when a setting has no description, so the column stays even.
        /// </summary>
        internal static void AddInfoIcon(ConfigEntryBase entry, Transform parent)
        {
            var description = entry.Description?.Description;

            var host = new GameObject(
                "Info", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            host.transform.SetParent(parent, false);

            var element = host.AddComponent<LayoutElement>();
            element.preferredWidth = IconSize;
            element.minWidth = IconSize;
            element.preferredHeight = IconSize;
            element.minHeight = IconSize;
            // The row forces its children to fill the width so the widget can stretch. Without an
            // explicit zero the icon takes that invitation too, and a circle drawn into the leftover
            // space is the oval.
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;

            var image = host.GetComponent<Image>();
            image.sprite = PanelSprite.Circle();

            if (string.IsNullOrEmpty(description))
            {
                // Keep the space so every row's widget ends at the same x, but show nothing.
                image.enabled = false;
                return;
            }

            // Dark disc, gold glyph - the game's own contrast, rather than pale on pale.
            image.color = new Color32(0x2A, 0x1B, 0x3D, 0xE0);
            image.raycastTarget = true;

            var glyph = AddText(host.transform, "i", TextAlignmentOptions.Center, Palette.Label);
            glyph.fontSize = 28f;
            glyph.fontStyle = FontStyles.Bold;
            glyph.raycastTarget = false;
            Stretch((RectTransform)glyph.transform);

            host.AddComponent<TooltipTrigger>().Text = description;
        }

        private const float IconSize = 44f;

        /// <summary>
        /// Width of the clickable value field. Roughly matches where a CycleButton's value sits, so
        /// text settings line up with the widget rows above and below them.
        /// </summary>
        private const float FieldWidth = 420f;

        /// <summary>Width of a checkbox once its own caption has been blanked.</summary>
        private const float ToggleWidth = 110f;

        /// <summary>
        /// A row with the setting's name on the left and an empty slot on the right, sized and
        /// placed to match where a CycleButton or SliderButton puts its control. Used by rows whose
        /// widget does not carry its own label in the right place.
        /// </summary>
        private static GameObject LabelledRow(
            Transform parent, ConfigEntryBase entry, string label, out Transform slot)
        {
            var row = new GameObject($"Row_{entry.Definition.Key}", typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 60f;
            element.minHeight = 60f;

            var text = AddText(
                row.transform, label, TextAlignmentOptions.MidlineLeft, Palette.Label);
            var textElement = text.gameObject.AddComponent<LayoutElement>();
            textElement.flexibleWidth = 1f;

            var host = new GameObject("Slot", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            host.transform.SetParent(row.transform, false);

            var hostLayout = host.GetComponent<HorizontalLayoutGroup>();
            hostLayout.childControlWidth = true;
            hostLayout.childForceExpandWidth = false;
            hostLayout.childControlHeight = true;
            hostLayout.childForceExpandHeight = false;
            hostLayout.childAlignment = TextAnchor.MiddleCenter;

            var hostElement = host.AddComponent<LayoutElement>();
            hostElement.preferredWidth = FieldWidth;
            hostElement.minWidth = FieldWidth;
            hostElement.flexibleWidth = 0f;

            slot = host.transform;
            return row;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI AddText(
            Transform parent, string content, TextAlignmentOptions alignment, Color colour)
        {
            var host = new GameObject("Text", typeof(RectTransform));
            host.transform.SetParent(parent, false);

            var text = host.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.alignment = alignment;
            text.color = colour;
            text.fontSize = 28f;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;

            // Our own text object has no neighbour to inherit from, so the game's font has to be
            // applied by hand. See 10-visual-integration.md.
            GameFonts.Apply(text, preferOutline: false);

            return text;
        }

        // ------------------------------------------------------------------ reading the entry

        /// <summary>
        /// The label a row shows. A mod can override the config key with a
        /// <c>ModNook.Label=</c> tag; otherwise the key is split on its camel case, so
        /// <c>HoverBackgroundAlpha</c> reads as "Hover background alpha".
        /// </summary>
        /// <summary>The display label for a setting, for dialogs built outside this class.</summary>
        internal static string LabelOf(ConfigEntryBase entry)
        {
            return Label(entry);
        }

        private static string Label(ConfigEntryBase entry)
        {
            var tagged = Tags.Value(entry, "Label");
            if (!string.IsNullOrEmpty(tagged))
            {
                return tagged;
            }

            return Humanise(entry.Definition.Key);
        }

        private static string Humanise(object value)
        {
            var raw = value?.ToString() ?? string.Empty;
            if (raw.Length == 0)
            {
                return raw;
            }

            var builder = new System.Text.StringBuilder(raw.Length + 8);
            for (var i = 0; i < raw.Length; i++)
            {
                var c = raw[i];

                // A capital that follows a lower-case letter starts a new word. Runs of capitals
                // are left alone so UI and HUD do not become U I and H U D.
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(raw[i - 1]))
                {
                    builder.Append(' ');
                    builder.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// An explicit list of values, either from BepInEx's own AcceptableValueList or from a
        /// <c>ModNook.Values=a|b|c</c> tag.
        /// </summary>
        private static List<object> ExplicitChoices(ConfigEntryBase entry)
        {
            var tagged = Tags.Value(entry, "Values");
            if (!string.IsNullOrEmpty(tagged))
            {
                try
                {
                    return tagged
                        .Split('|')
                        .Select(part => Convert.ChangeType(part.Trim(), entry.SettingType))
                        .ToList();
                }
                catch (Exception e)
                {
                    ModNookPlugin.Log.LogWarning(
                        $"ModNook.Values on {entry.Definition.Key} could not be read: {e.Message}");
                }
            }

            var acceptable = entry.Description?.AcceptableValues;
            if (acceptable == null || !acceptable.GetType().IsGenericType)
            {
                return null;
            }

            if (acceptable.GetType().GetGenericTypeDefinition() != typeof(AcceptableValueList<>))
            {
                return null;
            }

            var values = acceptable.GetType().GetProperty("AcceptableValues")?.GetValue(acceptable);
            return (values as System.Collections.IEnumerable)?.Cast<object>().ToList();
        }

        /// <summary>
        /// A numeric range, which is what earns a setting a slider. Without one there is no honest
        /// way to draw a track, which is why an unbounded number falls through to read-only.
        /// </summary>
        private static bool TryRange(ConfigEntryBase entry, out double min, out double max)
        {
            min = 0;
            max = 0;

            if (!IsNumeric(entry.SettingType))
            {
                return false;
            }

            var acceptable = entry.Description?.AcceptableValues;
            if (acceptable == null || !acceptable.GetType().IsGenericType)
            {
                return false;
            }

            if (acceptable.GetType().GetGenericTypeDefinition() != typeof(AcceptableValueRange<>))
            {
                return false;
            }

            try
            {
                var type = acceptable.GetType();
                min = Convert.ToDouble(type.GetProperty("MinValue")?.GetValue(acceptable));
                max = Convert.ToDouble(type.GetProperty("MaxValue")?.GetValue(acceptable));
                return max > min;
            }
            catch
            {
                return false;
            }
        }

        private static string Summarise(ConfigEntryBase entry)
        {
            var value = entry.BoxedValue?.ToString() ?? string.Empty;
            return value.Length <= 40 ? value : value.Substring(0, 37) + "...";
        }

        private static bool IsNumeric(Type type)
        {
            return type == typeof(float) || type == typeof(double) || type == typeof(decimal) ||
                   IsIntegral(type);
        }

        private static bool IsIntegral(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(short) ||
                   type == typeof(byte) || type == typeof(sbyte) || type == typeof(uint) ||
                   type == typeof(ulong) || type == typeof(ushort);
        }

        /// <summary>
        /// Picks a step a player can actually land on. A 0-1 range wants 0.05, a 0-200 range wants
        /// 5 - roughly a hundredth of the span, rounded to something round.
        /// </summary>
        private static float NiceStep(double span)
        {
            var rough = span / 100.0;
            var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
            var normalised = rough / magnitude;

            double snapped;
            if (normalised <= 1.5) snapped = 1;
            else if (normalised <= 3.5) snapped = 2;
            else if (normalised <= 7.5) snapped = 5;
            else snapped = 10;

            return (float)Math.Max(snapped * magnitude, span / 1000.0);
        }
    }

    /// <summary>The game's window palette, for the few elements we draw ourselves.</summary>
    internal static class Palette
    {
        internal static readonly Color Label = new Color32(0xF7, 0xD9, 0x94, 0xFF);
        internal static readonly Color Muted = new Color32(0xB0, 0x9A, 0xC8, 0xFF);
    }
}
