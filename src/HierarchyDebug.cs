using System;
using System.Linq;
using Chicken.UI;
using UnityEngine;

namespace ModNook
{
    /// <summary>
    /// A one-off `VerboseLogging` dump of the pause screen and the game's Settings screens. The
    /// pause/settings structure is documented nowhere, and this is what made the panel's resize and
    /// the header/prompt clones targetable rather than guessed at. Diagnostics only — off unless the
    /// player turns `Diagnostics.VerboseLogging` on.
    /// </summary>
    internal static class HierarchyDebug
    {
        private static bool dumped;

        internal static void Dump(PauseScreen screen)
        {
            if (dumped || !ModNookPlugin.VerboseLogging.Value)
            {
                return;
            }
            dumped = true;

            try
            {
                ModNookPlugin.Log.LogInfo("Pause screen hierarchy:");
                Describe(screen.transform, 0);

                // The game's Settings screen, for the header decorations and the Esc prompt in its
                // bottom corner. Both are drawn by hand here; they should be clones.
                var settings = UnityEngine.Object
                    .FindObjectsOfType<SettingsMenuScreen>(true)
                    .FirstOrDefault();

                if (settings != null)
                {
                    ModNookPlugin.Log.LogInfo("Settings menu hierarchy:");
                    Describe(settings.transform, 0);
                }

                // The decorated title bar is on the content screen, not the tab bar above it - the
                // menu screen dump only reached the bumper icons.
                var gameplay = UnityEngine.Object
                    .FindObjectsOfType<SettingsGameplayScreen>(true)
                    .FirstOrDefault();

                if (gameplay != null)
                {
                    ModNookPlugin.Log.LogInfo("Settings content hierarchy:");
                    Describe(gameplay.transform, 0);
                }
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning($"Hierarchy dump failed: {e.Message}");
            }
        }

        private static void Describe(Transform node, int depth)
        {
            // Deep enough to reach inside an individual button. The buttons themselves sit at
            // depth 5, so anything shallower stops exactly above what needs looking at.
            if (depth > 8)
            {
                return;
            }

            var rect = node as RectTransform;
            var components = string.Join(
                ", ",
                node.GetComponents<Component>()
                    .Where(c => c != null && !(c is RectTransform))
                    .Select(c => c.GetType().Name)
                    .ToArray());

            ModNookPlugin.Log.LogInfo(
                $"{new string(' ', depth * 2)}{node.name}  " +
                $"[{(rect == null ? "no rect" : rect.rect.size.ToString("0"))}]  {components}");

            for (var i = 0; i < node.childCount; i++)
            {
                Describe(node.GetChild(i), depth + 1);
            }
        }
    }
}
