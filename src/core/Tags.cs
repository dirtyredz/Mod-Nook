using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace ModNook
{
    /// <summary>
    /// Reads the optional <c>ModNook.*</c> strings a mod may put in its
    /// <see cref="ConfigDescription.Tags"/>. Everything here is optional; a mod that sets none of
    /// them still gets a full page built from the config alone.
    /// </summary>
    internal static class Tags
    {
        private const string Prefix = "ModNook.";

        /// <summary>True if a bare flag tag like <c>ModNook.Hidden</c> is present.</summary>
        internal static bool Has(ConfigEntryBase entry, string flag)
        {
            return Read(entry).Any(t =>
                string.Equals(t, Prefix + flag, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Value of a <c>ModNook.Key=value</c> tag, or null.</summary>
        internal static string Value(ConfigEntryBase entry, string key)
        {
            var wanted = Prefix + key + "=";
            var match = Read(entry).FirstOrDefault(
                t => t.StartsWith(wanted, StringComparison.OrdinalIgnoreCase));

            return match?.Substring(wanted.Length).Trim();
        }

        private static IEnumerable<string> Read(ConfigEntryBase entry)
        {
            var tags = entry?.Description?.Tags;
            if (tags == null)
            {
                yield break;
            }

            foreach (var tag in tags)
            {
                if (tag is string text)
                {
                    yield return text;
                }
            }
        }
    }
}
