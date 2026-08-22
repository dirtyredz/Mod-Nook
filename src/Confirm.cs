using System;
using System.Linq;
using UnityEngine;

namespace ModNook
{
    /// <summary>
    /// Asks a yes/no question through the game's own popup - the same one the pause menu uses
    /// before quitting to the main menu.
    ///
    /// Reusing it means the confirmation looks and reads like every other confirmation in the game,
    /// and the wording of the buttons comes from the player's language rather than from here.
    /// </summary>
    internal static class Confirm
    {
        internal static void Ask(string question, CanvasGroup overlayGroup, Action onConfirm)
        {
            var popup = Find<GenericPopupScreen>();
            if (popup == null)
            {
                // No dialog available. Doing the thing unasked would be worse than not offering it,
                // so the action is dropped and the reason recorded.
                ModNookPlugin.Log.LogWarning(
                    "No confirmation popup available; the action was cancelled rather than assumed.");
                return;
            }

            try
            {
                // The popup lives on another canvas and our overlay blocks raycasts, so without
                // standing down it opens behind an opaque layer with no way to answer it.
                var group = overlayGroup;
                if (group != null)
                {
                    group.blocksRaycasts = false;
                }

                Tooltip.Hide();

                Action restore = null;
                restore = () =>
                {
                    if (group != null)
                    {
                        group.blocksRaycasts = true;
                    }

                    popup.OnScreenHide.RemoveListener(restore);
                };

                popup.OnScreenHide.AddListener(restore);

                popup.Show(
                    question,
                    new GenericPopupListWidget.ItemData
                    {
                        ButtonText = LocalizationLibrary.Translate("input-confirm"),
                        OnOptionSelected = () =>
                        {
                            try
                            {
                                onConfirm?.Invoke();
                            }
                            catch (Exception e)
                            {
                                ModNookPlugin.Log.LogError($"Confirmed action failed: {e}");
                            }
                        },
                    },
                    new GenericPopupListWidget.ItemData
                    {
                        ButtonText = LocalizationLibrary.Translate("input-cancel"),
                        IsCancelOption = true,
                    });
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning($"Could not ask for confirmation: {e.Message}");
            }
        }

        /// <summary>
        /// UIScreen&lt;T&gt;.Instance is only populated once a screen has shown itself, which a popup
        /// will not have done while the game sits paused.
        /// </summary>
        private static T Find<T>() where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(x => x != null && x.gameObject.scene.IsValid());
        }
    }
}
