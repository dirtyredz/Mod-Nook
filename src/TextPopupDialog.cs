using System;
using BepInEx.Configuration;
using Chicken.UI;
using TMPro;
using UnityEngine;

namespace ModNook
{
    /// <summary>
    /// The fallback editor for settings with no dedicated widget: opens the game's own text popup
    /// (<see cref="TextInputPopupScreen"/>) and handles everything that has to be borrowed from it and
    /// given back - the overlay's raycast blocker, the popup's "Name:" prefix, Escape handling.
    ///
    /// <para>
    /// Shared so the list editor can reuse the same popup for adding an entry. The overlay's raycast
    /// blocker is still owned by <see cref="Rows.OverlayGroup"/>; this suspends and restores it, which
    /// is the one back-reference left until that state moves to an explicit overlay context.
    /// </para>
    /// </summary>
    internal static class TextPopupDialog
    {
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

        internal static void Edit(ConfigEntryBase entry, TextMeshProUGUI valueText, Action onChanged)
        {
            try
            {
                Prompt(
                    SettingMetadata.Label(entry), Brief(entry), entry.GetSerializedValue(), null,
                    typed =>
                    {
                        try
                        {
                            entry.SetSerializedValue(typed);
                            valueText.text = SettingMetadata.Summarise(entry);
                            onChanged();
                        }
                        catch (Exception e)
                        {
                            // Put the old value back on screen so the row never claims a change
                            // that did not happen.
                            valueText.text = SettingMetadata.Summarise(entry);
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

            if (Rows.OverlayGroup != null)
            {
                Rows.OverlayGroup.blocksRaycasts = false;
            }

            // A dialog stacked on top of the overlay has its own blocker, which would swallow the
            // popup just as the overlay would.
            if (also != null)
            {
                also.blocksRaycasts = false;
            }

            RestoreOn(popup, prefix, Rows.OverlayGroup, also);
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

        internal static TextInputPopupScreen TextPopup => UIScreen<TextInputPopupScreen>.Instance;
    }
}
