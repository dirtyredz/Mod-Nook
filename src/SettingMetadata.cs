using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Configuration;

namespace ModNook
{
    /// <summary>
    /// Reads a <see cref="ConfigEntryBase"/>'s metadata - label, choices, range, display value - out
    /// of its key, type, tags and description. Interpretation only: no UI and no instance state (it
    /// logs a warning through the plugin logger when a tag is malformed), so it can be reused by the
    /// row factory and by dialogs built outside it.
    /// </summary>
    internal static class SettingMetadata
    {
        /// <summary>
        /// The label a row shows. A mod can override the config key with a
        /// <c>ModNook.Label=</c> tag; otherwise the key is split on its camel case, so
        /// <c>HoverBackgroundAlpha</c> reads as "Hover background alpha".
        /// </summary>
        internal static string Label(ConfigEntryBase entry)
        {
            var tagged = Tags.Value(entry, "Label");
            if (!string.IsNullOrEmpty(tagged))
            {
                return tagged;
            }

            return Humanise(entry.Definition.Key);
        }

        internal static string Humanise(object value)
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
        internal static List<object> ExplicitChoices(ConfigEntryBase entry)
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
            if (acceptable != null && acceptable.GetType().IsGenericType &&
                acceptable.GetType().GetGenericTypeDefinition() == typeof(AcceptableValueList<>))
            {
                var values = acceptable.GetType().GetProperty("AcceptableValues")?.GetValue(acceptable);
                var list = (values as System.Collections.IEnumerable)?.Cast<object>().ToList();
                if (list != null)
                {
                    return list;
                }
            }

            // Last resort: a mod that documents its valid values in English instead of
            // registering them with BepInEx - e.g. "DARK MOON, BLOOD VELVET, ... or ROSE
            // QUARTZ" in the description of a plain string setting. Only trusted when the
            // setting's own current value is one of the parsed items, which is what stops this
            // from firing on ordinary descriptive prose.
            return DescriptionChoices(entry);
        }

        /// <summary>
        /// Parses a fixed set of choices straight out of <paramref name="entry"/>'s description,
        /// for mods that never call <see cref="ConfigDescription"/> with an
        /// <see cref="AcceptableValueList{T}"/>. Requires the setting's current value to appear
        /// among the parsed items - without that anchor, "a comma-separated list of things" in a
        /// description is indistinguishable from an actual enumeration of valid values.
        /// </summary>
        private static List<object> DescriptionChoices(ConfigEntryBase entry)
        {
            if (entry.SettingType != typeof(string))
            {
                return null;
            }

            var current = (entry.BoxedValue as string)?.Trim();
            if (string.IsNullOrEmpty(current))
            {
                return null;
            }

            var description = entry.Description?.Description;
            if (string.IsNullOrEmpty(description))
            {
                return null;
            }

            var sentence = SentenceContaining(description, current);
            if (sentence == null)
            {
                return null;
            }

            // Whatever leads up to the last colon - "The mirror's own color scheme:" - is the
            // introduction, not part of the list itself.
            var colon = sentence.LastIndexOf(':');
            var listPart = colon >= 0 ? sentence.Substring(colon + 1) : sentence;

            listPart = Regex.Replace(listPart, @"\s+or\s+", ", ", RegexOptions.IgnoreCase);
            listPart = Regex.Replace(listPart, @"\s+and\s+", ", ", RegexOptions.IgnoreCase);

            var tokens = listPart
                .Split(',')
                .Select(t => t.Trim().Trim('.', '"', '\''))
                .Where(t => t.Length > 0)
                .ToList();

            if (tokens.Count < 2 || tokens.Count > 12)
            {
                return null;
            }

            // Sanity checks against ordinary prose rather than a real enumeration: a genuine list
            // of named values is short, has no sentence fragments in it, and has no repeats.
            if (tokens.Any(t => t.Length > 40 || t.Contains('.')) ||
                tokens.Distinct(StringComparer.OrdinalIgnoreCase).Count() != tokens.Count)
            {
                return null;
            }

            if (!tokens.Any(t => string.Equals(t, current, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            return tokens.Cast<object>().ToList();
        }

        /// <summary>
        /// The sentence of <paramref name="text"/> that contains <paramref name="needle"/> as a
        /// whole word/phrase, or null. A plain <c>IndexOf</c> would also match e.g. "DARK" inside
        /// a hypothetical "DARKNESS" - the boundary checks are what keep this to whole values.
        /// </summary>
        private static string SentenceContaining(string text, string needle)
        {
            foreach (var sentence in text.Split('.'))
            {
                var index = sentence.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    continue;
                }

                var before = index == 0 ? ' ' : sentence[index - 1];
                var afterIndex = index + needle.Length;
                var after = afterIndex >= sentence.Length ? ' ' : sentence[afterIndex];

                if (!char.IsLetterOrDigit(before) && !char.IsLetterOrDigit(after))
                {
                    return sentence;
                }
            }

            return null;
        }

        /// <summary>
        /// A numeric range, which is what earns a setting a slider. Without one there is no honest
        /// way to draw a track, which is why an unbounded number falls through to read-only.
        /// </summary>
        internal static bool TryRange(ConfigEntryBase entry, out double min, out double max)
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

        internal static string Summarise(ConfigEntryBase entry)
        {
            var value = entry.BoxedValue?.ToString() ?? string.Empty;
            return value.Length <= 40 ? value : value.Substring(0, 37) + "...";
        }

        private static bool IsNumeric(Type type)
        {
            return type == typeof(float) || type == typeof(double) || type == typeof(decimal) ||
                   IsIntegral(type);
        }

        internal static bool IsIntegral(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(short) ||
                   type == typeof(byte) || type == typeof(sbyte) || type == typeof(uint) ||
                   type == typeof(ulong) || type == typeof(ushort);
        }

        /// <summary>
        /// Picks a step a player can actually land on. A 0-1 range wants 0.05, a 0-200 range wants
        /// 5 - roughly a hundredth of the span, rounded to something round.
        /// </summary>
        internal static float NiceStep(double span)
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
}
