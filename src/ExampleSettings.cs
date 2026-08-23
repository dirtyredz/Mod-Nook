using BepInEx.Configuration;
using UnityEngine;

namespace ModNook
{
    /// <summary>
    /// A living demonstration: one setting for every render path Mod Nook knows, bound into Mod
    /// Nook's own config so its own page shows the whole vocabulary at once.
    ///
    /// <para>
    /// Doubles as the manual-test surface - every widget, dialog and tag is reachable here regardless
    /// of which other mods are installed. The values are inert; nothing reads them. Authors can copy a
    /// line to see exactly what a given `Config.Bind` shape (or `ModNook.*` tag) turns into.
    /// </para>
    /// </summary>
    internal static class ExampleSettings
    {
        internal enum Mood
        {
            Cheerful,
            Spooky,
            Gloomy,
        }

        private const string Section = "Examples";

        internal static void Bind(ConfigFile config)
        {
            // ---- vanilla types (no tags needed) ----

            config.Bind(Section, "Toggle", true,
                "A bool renders as the game's checkbox toggle.");

            config.Bind(Section, "Mood", Mood.Spooky,
                "An enum renders as a cycle button with humanised names.");

            config.Bind(Section, "Palette", "Blue",
                new ConfigDescription(
                    "An AcceptableValueList renders as a cycle.",
                    new AcceptableValueList<string>("Red", "Green", "Blue")));

            config.Bind(Section, "Volume", 5,
                new ConfigDescription(
                    "A bounded int (AcceptableValueRange) renders as a slider.",
                    new AcceptableValueRange<int>(0, 10)));

            config.Bind(Section, "Opacity", 0.5f,
                new ConfigDescription(
                    "A bounded float renders as a slider with a fractional step.",
                    new AcceptableValueRange<float>(0f, 1f)));

            config.Bind(Section, "MaxItems", 42,
                "An unbounded int (no range) opens the number editor - nudge or type.");

            config.Bind(Section, "GrowthRate", 2.5f,
                "An unbounded float opens the number editor; the step scales to the value.");

            config.Bind(Section, "Hotkey", new KeyboardShortcut(KeyCode.F5),
                "A KeyboardShortcut opens the key-capture dialog and can hold modifiers.");

            config.Bind(Section, "QuickKey", KeyCode.G,
                "A bare KeyCode opens key-capture too (checked before enum, so it isn't a cycle).");

            config.Bind(Section, "FreeText", "hello",
                "Anything without a dedicated widget opens the game's text popup.");

            // ---- string sub-types Mod Nook detects ----

            config.Bind(Section, "AccentColour", "#4A2E8F",
                "A hex-shaped string is auto-detected and opens the colour picker.");

            config.Bind(Section, "AllowedTags", "alpha,beta,gamma",
                "A comma-separated string is auto-detected and opens the list editor.");

            config.Bind(Section, "MoonPhase", "DARK MOON",
                "Prose choices, read from this line: DARK MOON, BLOOD VELVET or ROSE QUARTZ.");

            // ---- ModNook.* tags (all optional; inert when Mod Nook isn't installed) ----

            config.Bind(Section, "Difficulty", "Medium",
                new ConfigDescription(
                    "A ModNook.Values tag forces a cycle over a fixed set.",
                    null, "ModNook.Values=Low|Medium|High"));

            config.Bind(Section, "ThemeColour", "",
                new ConfigDescription(
                    "An empty string tagged ModNook.Color still opens the colour picker.",
                    null, "ModNook.Color"));

            config.Bind(Section, "Canvases", "one,two",
                new ConfigDescription(
                    "A ModNook.List tag forces the list editor even without the word comma.",
                    null, "ModNook.List"));

            config.Bind(Section, "InternalKey", true,
                new ConfigDescription(
                    "A ModNook.Label tag renames this in the panel (the key stays InternalKey).",
                    null, "ModNook.Label=Friendly label"));

            config.Bind(Section, "Secret", true,
                new ConfigDescription(
                    "A ModNook.Hidden tag keeps this off the panel - you should NOT see this row.",
                    null, "ModNook.Hidden"));
        }
    }
}
