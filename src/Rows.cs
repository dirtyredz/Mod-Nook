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
        internal static bool Build(
            ConfigEntryBase entry, Transform parent, Action onChanged, OverlayContext overlay)
        {
            var label = SettingMetadata.Label(entry);

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
                return BuildKey(entry, parent, label, onChanged, overlay);
            }

            if (entry.SettingType.IsEnum)
            {
                return BuildEnum(entry, parent, label, onChanged);
            }

            var choices = SettingMetadata.ExplicitChoices(entry);
            if (choices != null && choices.Count > 1)
            {
                return BuildChoice(entry, parent, label, choices, onChanged);
            }

            if (SettingMetadata.TryRange(entry, out var min, out var max))
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
            var names = values.Select(SettingMetadata.Humanise).ToList();
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

            var isWhole = SettingMetadata.IsIntegral(entry.SettingType);
            var span = max - min;

            // Integers step by one. Floats get a hundred stops, rounded to something a player can
            // land on deliberately rather than an arbitrary fraction.
            var sliderStep = isWhole ? 1f : SettingMetadata.NiceStep(span);
            var buttonStep = isWhole ? 1f : sliderStep;

            var row = Templates.Clone(template, parent, $"Row_{entry.Definition.Key}");
            Templates.SetValueWidgetLabel(row, label);

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
            Templates.SetValueWidgetLabel(row, label);

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
        internal static void BuildText(
            ConfigEntryBase entry, Transform parent, Action onChanged, OverlayContext overlay)
        {
            var row = ClickableRow(entry, parent, out var plate, out var value);

            if (!TextPopupDialog.IsAvailable)
            {
                // No popup means no way to type. Leave the row readable rather than pretending it
                // is interactive.
                return;
            }

            var button = row.AddComponent<Button>();
            button.targetGraphic = plate;

            // Colour before list: a description can mention both, and a hex value is the more
            // specific match of the two.
            if (ColorPicker.Suits(entry) && overlay?.Root != null)
            {
                button.onClick.AddListener(() =>
                {
                    Tooltip.Hide();

                    ColorPicker.Open(
                        overlay.Root, entry, overlay.ButtonTemplate,
                        hex => Apply(entry, value, hex, onChanged));
                });

                return;
            }

            // A comma-separated setting gets the list editor; everything else gets the text popup.
            if (ListEditor.Suits(entry) && overlay?.Root != null)
            {
                button.onClick.AddListener(() =>
                {
                    Tooltip.Hide();

                    ListEditor.Open(
                        overlay.Root, entry, overlay.ButtonTemplate, overlay.Group,
                        joined => Apply(entry, value, joined, onChanged));
                });

                return;
            }

            // A number with no range gets nudge-and-type instead of a raw text box.
            if (NumberEditor.Suits(entry) && overlay?.Root != null)
            {
                button.onClick.AddListener(() =>
                {
                    Tooltip.Hide();

                    NumberEditor.Open(
                        overlay.Root, entry, overlay.ButtonTemplate, overlay.Group,
                        text => Apply(entry, value, text, onChanged));
                });

                return;
            }

            button.onClick.AddListener(() => TextPopupDialog.Edit(entry, value, overlay?.Group, onChanged));
        }

        /// <summary>
        /// A key binding. Shares the clickable row of a text setting but opens the capture dialog,
        /// because the alternative is asking a player to spell "LeftAlt" from memory.
        /// </summary>
        private static bool BuildKey(
            ConfigEntryBase entry, Transform parent, string label, Action onChanged,
            OverlayContext overlay)
        {
            if (overlay?.Root == null)
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
                    overlay.Root, entry, overlay.ButtonTemplate,
                    shortcut =>
                    {
                        try
                        {
                            // A KeyCode setting holds one key and cannot express modifiers, so it
                            // takes the main key and drops them.
                            entry.BoxedValue = entry.SettingType == typeof(KeyCode)
                                ? (object)shortcut.MainKey
                                : shortcut;

                            value.text = SettingMetadata.Summarise(entry);
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
                row.transform, SettingMetadata.Label(entry), TextAlignmentOptions.MidlineLeft, Palette.Label);
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
                field.transform, SettingMetadata.Summarise(entry), TextAlignmentOptions.Midline, Palette.Muted);

            return field;
        }

        /// <summary>
        /// Writes a dialog's result back, and shows the old value again if the mod rejects it - so
        /// the row never claims a change that did not happen.
        /// </summary>
        private static void Apply(
            ConfigEntryBase entry, TextMeshProUGUI value, string serialized, Action onChanged)
        {
            try
            {
                entry.SetSerializedValue(serialized);
                value.text = SettingMetadata.Summarise(entry);
                onChanged();
            }
            catch (Exception e)
            {
                value.text = SettingMetadata.Summarise(entry);
                ModNookPlugin.Log.LogWarning(
                    $"Could not save {entry.Definition.Key}: {e.Message}");
            }
        }

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

    }
}
