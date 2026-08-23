using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ModNook
{
    /// <summary>
    /// An in-game settings panel for BepInEx mods, built out of the game's own settings widgets.
    ///
    /// <para>
    /// Discovery is one-directional: this reads other plugins' <c>ConfigEntry</c> definitions and
    /// nothing references this assembly. A mod gets a page by doing what it already had to do -
    /// call <c>Config.Bind</c> - and keeps working normally when this is not installed.
    /// </para>
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Moonlight Peaks.exe")]
    public sealed class ModNookPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dirtyredz.moonlightpeaks.modnook";
        public const string PluginName = "Mod Nook";
        // Keep in step with <Version> in the csproj - pack.ps1 names the archive from that one
        // and BepInEx reports this one. See 12-versioning-and-release.md.
        public const string PluginVersion = ModBuildInfo.Version;

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> ShowDescriptions;
        internal static ConfigEntry<bool> VerboseLogging;

        private void Awake()
        {
            Log = Logger;

            ShowDescriptions = Config.Bind(
                "Display", "ShowDescriptions", true,
                "Show each setting's description under it, as the mod author wrote it.");

            VerboseLogging = Config.Bind(
                "Diagnostics", "VerboseLogging", false,
                "Log the pause and settings screen hierarchies at startup.");

            // A live gallery of every render path, on Mod Nook's own page. Inert demo values; also
            // the manual-test surface, so every widget/dialog/tag is reachable without another mod.
            ExampleSettings.Bind(Config);

            try
            {
                var harmony = new Harmony(PluginGuid);
                harmony.PatchAll(typeof(PauseScreenShowPatch));
                harmony.PatchAll(typeof(PauseScreenHidePatch));
                harmony.PatchAll(typeof(PauseMenuContinueInputPatch));

                var patched = string.Join(", ",
                    System.Linq.Enumerable.Select(
                        harmony.GetPatchedMethods(), m => m.DeclaringType?.Name + "." + m.Name));

                Log.LogInfo(
                    $"{PluginName} {PluginVersion} loaded. Patched: {patched}. " +
                    $"Open the pause menu and choose {PluginName}. Settings are read from other " +
                    "mods' own config files; nothing is written to your save.");
            }
            catch (Exception e)
            {
                Log.LogError($"Could not patch the pause screen, so the panel will not appear: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(PauseScreen), "OnShow")]
    internal static class PauseScreenShowPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PauseScreen __instance)
        {
            try
            {
                PanelController.Attach(__instance);
            }
            catch (Exception e)
            {
                // Never take the pause menu down with us - it is the only way out of the game.
                ModNookPlugin.Log.LogError($"Attaching to the pause screen failed: {e}");
            }
        }
    }

    /// <summary>
    /// Takes over the pause menu's "resume" input while the panel is open, and uses it to step back
    /// one page instead.
    ///
    /// <para>
    /// Two problems, one hook. The pause menu treats cancel as "resume", so without this the player
    /// leaves the panel and the pause menu on the same press. And the corner prompt does not raise a
    /// click - it calls <c>SimulateActionForAll</c> on the bound action - so watching for the Escape
    /// key directly catches the key but never the prompt.
    /// </para>
    /// <para>
    /// The condition below is the game's own, copied from the method being replaced, so the panel
    /// answers to whatever the player has cancel bound to on whatever they are playing with.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(PauseMenuState), "ProcessContinueInput")]
    internal static class PauseMenuContinueInputPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!PanelController.IsOpen)
            {
                return true;
            }

            try
            {
                if (Input.AllowUIInput &&
                    Input.Player.GetAnyButtonDownConditionallyConsumed(
                        () => !UIScreenUtility.IsShowingAnyScreenWithCategory(UIScreenCategory.Popups),
                        33, 21))
                {
                    PanelController.RequestBack();
                }
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning($"Cancel input could not be read: {e.Message}");
            }

            // Always skipped while open: resuming the game is never what cancel should do here.
            return false;
        }
    }

    [HarmonyPatch(typeof(PauseScreen), "OnHide")]
    internal static class PauseScreenHidePatch
    {
        [HarmonyPostfix]
        private static void Postfix(PauseScreen __instance)
        {
            try
            {
                PanelController.CloseFor(__instance);
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogError($"Closing the panel failed: {e}");
            }
        }
    }
}
