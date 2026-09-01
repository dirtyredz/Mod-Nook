using UnityEngine;

namespace ModNook
{
    /// <summary>
    /// Gives the game's text popup a way out.
    ///
    /// <para>
    /// <c>TextInputPopupScreen</c> has a confirm button and nothing else - no cancel, no close. It
    /// is built for naming a creature, where the game decides when to open it and any name will do.
    /// Confirm is disabled whenever the field is invalid, so a setting whose current value is empty
    /// opens a dialog that cannot be confirmed and cannot be dismissed: the player is stuck with
    /// the game paused behind it.
    /// </para>
    /// <para>
    /// Escape closes it, discarding the edit. Nothing is written unless the player confirms.
    /// </para>
    /// </summary>
    internal sealed class PopupEscape : MonoBehaviour
    {
        private TextInputPopupScreen popup;

        internal static void Arm(TextInputPopupScreen popup)
        {
            if (popup == null)
            {
                return;
            }

            var escape = popup.GetComponent<PopupEscape>() ?? popup.gameObject.AddComponent<PopupEscape>();
            escape.popup = popup;
            escape.enabled = true;
        }

        private void Update()
        {
            if (popup == null || !popup.IsShowing)
            {
                enabled = false;
                return;
            }

            // Fully qualified: the game defines its own global Input type, which wins over
            // UnityEngine's on an unqualified name.
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                popup.Hide();
                enabled = false;
            }
        }
    }
}
