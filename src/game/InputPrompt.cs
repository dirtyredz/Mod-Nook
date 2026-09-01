using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ModNook
{
    /// <summary>
    /// Registers a prompt with the game's own bottom-corner input bar - the "ESC Close" the Settings
    /// screen shows - instead of drawing an imitation of it.
    ///
    /// <para>
    /// <see cref="InputButtonScreen"/> is a shared screen every menu adds its entries to, and both
    /// <c>AddEntries</c> and the entry constructor are public. What it draws is the real key cap for
    /// whatever the player has that action bound to, on their current input device, with the game's
    /// own wording - none of which a hand-made badge reading "ESC" can be right about.
    /// </para>
    /// </summary>
    internal static class InputPrompt
    {
        private static readonly FieldInfo EntriesField =
            typeof(UIScreenInputButtons).GetField(
                "entries", BindingFlags.Instance | BindingFlags.NonPublic);

        private static InputButtonScreenEntry[] shown;

        /// <summary>
        /// Whether the game's prompt bar can be used, without registering anything. The panel asks
        /// once while building, to decide whether it needs a Close button of its own.
        /// </summary>
        internal static bool CanShow()
        {
            try
            {
                return Find<InputButtonScreen>() != null && FindCancelAction() != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Adds the game's cancel prompt to the corner. Returns false if nothing suitable could be
        /// borrowed, so the caller can fall back to drawing its own.
        /// </summary>
        internal static bool Show()
        {
            try
            {
                if (shown != null)
                {
                    return true;
                }

                var screen = Find<InputButtonScreen>();
                var action = FindCancelAction();

                if (screen == null || action == null)
                {
                    return false;
                }

                // A fresh entry rather than the borrowed one: the original carries enable-callbacks
                // belonging to the screen that owns it, and those would be asking about a screen
                // that is not on show.
                shown = new[]
                {
                    new InputButtonScreenEntry(action, InputButtonScreen.Anchoring.BottomRight),
                };

                screen.AddEntries(shown);
                return true;
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning($"Could not show the input prompt: {e.Message}");
                shown = null;
                return false;
            }
        }

        internal static void Hide()
        {
            try
            {
                if (shown == null)
                {
                    return;
                }

                Find<InputButtonScreen>()?.RemoveEntries(shown);
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning($"Could not hide the input prompt: {e.Message}");
            }
            finally
            {
                // Cleared either way. A stale entry would leave the prompt on screen for every
                // other menu the player opens afterwards.
                shown = null;
            }
        }

        /// <summary>
        /// Borrows the cancel action from a screen that already has one, rather than guessing at an
        /// asset name. Every menu that can be backed out of carries one in its bottom-right slot.
        /// </summary>
        private static InputActionAsset FindCancelAction()
        {
            var candidates = new List<InputButtonScreenEntry>();

            foreach (var host in Resources.FindObjectsOfTypeAll<UIScreenInputButtons>())
            {
                if (host == null || !host.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (EntriesField?.GetValue(host) is InputButtonScreenEntry[] entries)
                {
                    candidates.AddRange(entries.Where(e => e?.InputActionAsset != null));
                }
            }

            var match = candidates.FirstOrDefault(
                e => e.Anchoring == InputButtonScreen.Anchoring.BottomRight);

            return (match ?? candidates.FirstOrDefault())?.InputActionAsset;
        }

        /// <summary>
        /// UIScreen&lt;T&gt;.Instance is only populated once a screen has shown itself, which the
        /// prompt bar may not have done while the game is paused.
        /// </summary>
        private static T Find<T>() where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(x => x != null && x.gameObject.scene.IsValid());
        }
    }
}
