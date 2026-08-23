using System;
using System.Globalization;
using BepInEx.Configuration;
using Chicken.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModNook
{
    /// <summary>
    /// Edits a number that has no <c>AcceptableValueRange</c> - the case that otherwise falls back to
    /// the free-form text popup.
    ///
    /// <para>
    /// A bounded number gets the game's slider; an unbounded one has no track to draw, so this gives
    /// it what a slider still offers without one: nudge buttons around the current value, plus a
    /// direct-entry path for a value that is nowhere near it. No invented min/max - the only limits
    /// are the numeric type's own, which is what keeps a byte from wrapping past 255.
    /// </para>
    /// <para>
    /// Saving goes out as an invariant-format string through the same
    /// <see cref="ConfigEntryBase.SetSerializedValue"/> path every other editor uses, so the value
    /// lands in the config file exactly as the mod's own parser would read it.
    /// </para>
    /// </summary>
    internal sealed class NumberEditor : ModalDialog
    {
        private ConfigEntryBase entry;
        private AnimatedButton buttonTemplate;
        private CanvasGroup overlayGroup;
        private Action<string> onSave;

        private bool isIntegral;
        private double value;
        private double min;
        private double max;
        private double fineStep;
        private double coarseStep;

        private TextMeshProUGUI valueText;

        /// <summary>True when this setting is a number with no range, so it wants nudge-and-type.</summary>
        internal static bool Suits(ConfigEntryBase entry)
        {
            return SettingMetadata.IsNumeric(entry.SettingType) &&
                   !SettingMetadata.TryRange(entry, out _, out _);
        }

        internal static void Open(
            RectTransform parent, ConfigEntryBase entry, AnimatedButton buttonTemplate,
            CanvasGroup overlayGroup, Action<string> onSave)
        {
            Show<NumberEditor>(parent, "ModNook_NumberEditor", editor =>
            {
                editor.entry = entry;
                editor.buttonTemplate = buttonTemplate;
                editor.overlayGroup = overlayGroup;
                editor.onSave = onSave;
            });
        }

        protected override void Build()
        {
            isIntegral = SettingMetadata.IsIntegral(entry.SettingType);
            Range(entry.SettingType, out min, out max);
            value = Clamp(ReadCurrent());

            // Step is picked once, from the starting value's magnitude, so it stays predictable as
            // you nudge - roughly a tenth of the value's own scale, with a coarse ×10 beside it.
            fineStep = StepFor(value);
            coarseStep = fineStep * 10.0;

            var panel = (RectTransform)BuildShell(
                760f, new RectOffset(48, 48, 36, 36), 14f, TextAnchor.MiddleCenter);

            UiText.NewText(panel.transform, SettingMetadata.Label(entry), TextAlignmentOptions.Center, Palette.Label, 34f);

            valueText = UiText.NewText(panel.transform, Format(value), TextAlignmentOptions.Center, Palette.Label, 44f);

            if (buttonTemplate != null)
            {
                var steps = ButtonRow(panel.transform);
                // ASCII hyphen, not the Unicode minus (U+2212) - the game's font atlas has no glyph
                // for the latter, so it renders blank (the "+" is fine, being ASCII).
                Templates.CloneButton(buttonTemplate, steps, "MinusCoarse", "-" + Format(coarseStep), () => Nudge(-coarseStep));
                Templates.CloneButton(buttonTemplate, steps, "MinusFine", "-" + Format(fineStep), () => Nudge(-fineStep));
                Templates.CloneButton(buttonTemplate, steps, "PlusFine", "+" + Format(fineStep), () => Nudge(fineStep));
                Templates.CloneButton(buttonTemplate, steps, "PlusCoarse", "+" + Format(coarseStep), () => Nudge(coarseStep));

                var actions = ButtonRow(panel.transform);
                if (TextPopupDialog.IsAvailable)
                {
                    Templates.CloneButton(buttonTemplate, actions, "Type", "Type…", TypeValue);
                }

                Templates.CloneButton(buttonTemplate, actions, "Save", "Save", Save);
                Templates.CloneButton(buttonTemplate, actions, "Cancel", "Cancel", Close);
            }
            else
            {
                ModNookPlugin.Log.LogWarning(
                    "No button template for the number editor; showing the value read-only.");
            }

            UiText.NewText(
                panel.transform,
                isIntegral
                    ? "Whole numbers only. Nudge with the buttons or Type… for an exact value."
                    : "Nudge with the buttons or Type… for an exact value.",
                TextAlignmentOptions.Center, Palette.Muted, 20f);

            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        private Transform ButtonRow(Transform parent)
        {
            var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 72f;
            element.minHeight = 72f;

            return row.transform;
        }

        private void Nudge(double by)
        {
            value = Clamp(value + by);
            valueText.text = Format(value);
        }

        private void TypeValue()
        {
            TextPopupDialog.Prompt(
                SettingMetadata.Label(entry),
                isIntegral ? "Enter a whole number." : "Enter a number.",
                Format(value), overlayGroup, Group,
                typed =>
                {
                    if (double.TryParse(
                            typed?.Trim(), NumberStyles.Float | NumberStyles.AllowThousands,
                            CultureInfo.InvariantCulture, out var parsed))
                    {
                        value = Clamp(parsed);
                        valueText.text = Format(value);
                    }
                    else
                    {
                        // A non-number leaves the value untouched rather than writing garbage the
                        // mod would reject on next launch.
                        ModNookPlugin.Log.LogInfo($"Ignored non-numeric entry '{typed}'.");
                    }
                });
        }

        private void Save()
        {
            onSave?.Invoke(Format(value));
            Close();
        }

        private double ReadCurrent()
        {
            try
            {
                return Convert.ToDouble(entry.BoxedValue, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        private double Clamp(double v)
        {
            if (v < min)
            {
                return min;
            }

            return v > max ? max : v;
        }

        /// <summary>Formats for both the display and the saved value - whole for integers, trimmed otherwise.</summary>
        private string Format(double v)
        {
            if (isIntegral)
            {
                return Math.Round(v).ToString("0", CultureInfo.InvariantCulture);
            }

            // "0.######" trims trailing zeros so 2.50 reads as 2.5, while still round-tripping through
            // the mod's own parser.
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A step about a tenth of the value's own scale, so 2.5 nudges by 0.1 and 5000 by 100.
        /// Integers never step by less than one.
        /// </summary>
        private double StepFor(double v)
        {
            if (v == 0.0)
            {
                return isIntegral ? 1.0 : 0.1;
            }

            var order = Math.Floor(Math.Log10(Math.Abs(v)));
            var step = Math.Pow(10, order - 1);

            return isIntegral ? Math.Max(1.0, Math.Round(step)) : step;
        }

        private static void Range(Type type, out double min, out double max)
        {
            if (type == typeof(byte)) { min = byte.MinValue; max = byte.MaxValue; }
            else if (type == typeof(sbyte)) { min = sbyte.MinValue; max = sbyte.MaxValue; }
            else if (type == typeof(short)) { min = short.MinValue; max = short.MaxValue; }
            else if (type == typeof(ushort)) { min = ushort.MinValue; max = ushort.MaxValue; }
            else if (type == typeof(int)) { min = int.MinValue; max = int.MaxValue; }
            else if (type == typeof(uint)) { min = uint.MinValue; max = uint.MaxValue; }
            else if (type == typeof(long)) { min = long.MinValue; max = long.MaxValue; }
            else if (type == typeof(ulong)) { min = ulong.MinValue; max = ulong.MaxValue; }
            else if (type == typeof(float)) { min = float.MinValue; max = float.MaxValue; }
            else { min = double.MinValue; max = double.MaxValue; }
        }
    }
}
