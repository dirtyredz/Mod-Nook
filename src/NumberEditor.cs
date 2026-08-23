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
    /// The working value is a <see cref="decimal"/>, which represents every integral type exactly and
    /// carries more precision than a <see cref="float"/> or <see cref="double"/> setting will ever
    /// hold - so nothing is lost round-tripping through it. And nothing is written unless the value is
    /// actually changed: opening the editor and pressing Save leaves the setting byte-for-byte as it
    /// was, even for a value too large for the working type to load. Saving goes out as an invariant
    /// string through the same <see cref="ConfigEntryBase.SetSerializedValue"/> path every other
    /// editor uses.
    /// </para>
    /// </summary>
    internal sealed class NumberEditor : ModalDialog
    {
        private ConfigEntryBase entry;
        private AnimatedButton buttonTemplate;
        private CanvasGroup overlayGroup;
        private Action<string> onSave;

        private bool isIntegral;
        private decimal value;
        private decimal min;
        private decimal max;
        private decimal fineStep;
        private decimal coarseStep;

        /// <summary>True once the value is deliberately changed; nothing is written before that.</summary>
        private bool edited;

        /// <summary>Set when the current value won't fit the working decimal, so it shows raw and read-only.</summary>
        private bool unrepresentable;

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
            coarseStep = fineStep * 10m;

            var panel = (RectTransform)BuildShell(
                760f, new RectOffset(48, 48, 36, 36), 14f, TextAnchor.MiddleCenter);

            UiText.NewText(panel.transform, SettingMetadata.Label(entry), TextAlignmentOptions.Center, Palette.Label, 34f);

            valueText = UiText.NewText(
                panel.transform,
                unrepresentable ? (entry.BoxedValue?.ToString() ?? "0") : Format(value),
                TextAlignmentOptions.Center, Palette.Label, 44f);

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

        private void Nudge(decimal by)
        {
            value = Clamp(value + by);
            edited = true;
            unrepresentable = false;
            valueText.text = Format(value);
        }

        private void TypeValue()
        {
            TextPopupDialog.Prompt(
                SettingMetadata.Label(entry),
                isIntegral ? "Enter a whole number." : "Enter a number.",
                unrepresentable ? string.Empty : Format(value), overlayGroup, Group,
                typed =>
                {
                    if (decimal.TryParse(
                            typed?.Trim(), NumberStyles.Number,
                            CultureInfo.InvariantCulture, out var parsed))
                    {
                        value = Clamp(parsed);
                        edited = true;
                        unrepresentable = false;
                        valueText.text = Format(value);
                    }
                    else
                    {
                        // A non-number (or one too large for the working type) leaves the value
                        // untouched rather than writing garbage the mod would reject on next launch.
                        ModNookPlugin.Log.LogInfo($"Ignored unparseable number entry '{typed}'.");
                    }
                });
        }

        private void Save()
        {
            // Only write on a deliberate change. Leaving it untouched must never rewrite the value -
            // which also protects a setting whose value was too large to load into the editor.
            if (edited)
            {
                onSave?.Invoke(Format(value));
            }

            Close();
        }

        private decimal ReadCurrent()
        {
            try
            {
                return Convert.ToDecimal(entry.BoxedValue, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                // NaN, an infinity, or a magnitude beyond decimal's range. Show it raw and read-only;
                // the edited-guard means an untouched Save won't clobber it.
                unrepresentable = true;
                return 0m;
            }
        }

        private decimal Clamp(decimal v)
        {
            if (v < min)
            {
                return min;
            }

            return v > max ? max : v;
        }

        /// <summary>Formats for both the display and the saved value - whole for integers, plain otherwise.</summary>
        private string Format(decimal v)
        {
            if (isIntegral)
            {
                return decimal.Round(v, MidpointRounding.AwayFromZero)
                    .ToString("0", CultureInfo.InvariantCulture);
            }

            // Decimal's own ToString: no exponent, and the exact digits it holds - which is every
            // digit a float or double setting could have carried. A float/double parses it back fine.
            return v.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A step about a tenth of the value's own scale, so 2.5 nudges by 0.1 and 5000 by 100.
        /// Integers never step by less than one; a floored minimum keeps the buttons live for a value
        /// too small to derive a step from.
        /// </summary>
        private decimal StepFor(decimal v)
        {
            var minimum = isIntegral ? 1m : 0.000001m;

            if (v == 0m)
            {
                return isIntegral ? 1m : 0.1m;
            }

            var order = Math.Floor(Math.Log10((double)Math.Abs(v)));
            var step = (decimal)Math.Pow(10, order - 1);

            if (isIntegral)
            {
                step = Math.Round(step, MidpointRounding.AwayFromZero);
            }

            return step < minimum ? minimum : step;
        }

        private static void Range(Type type, out decimal min, out decimal max)
        {
            if (type == typeof(byte)) { min = byte.MinValue; max = byte.MaxValue; }
            else if (type == typeof(sbyte)) { min = sbyte.MinValue; max = sbyte.MaxValue; }
            else if (type == typeof(short)) { min = short.MinValue; max = short.MaxValue; }
            else if (type == typeof(ushort)) { min = ushort.MinValue; max = ushort.MaxValue; }
            else if (type == typeof(int)) { min = int.MinValue; max = int.MaxValue; }
            else if (type == typeof(uint)) { min = uint.MinValue; max = uint.MaxValue; }
            else if (type == typeof(long)) { min = long.MinValue; max = long.MaxValue; }
            else if (type == typeof(ulong)) { min = ulong.MinValue; max = ulong.MaxValue; }
            else
            {
                // float/double/decimal: every decimal value is within their range, so the working
                // type is its own bound. (A float/double can hold more than a decimal, but not less.)
                min = decimal.MinValue;
                max = decimal.MaxValue;
            }
        }
    }
}
