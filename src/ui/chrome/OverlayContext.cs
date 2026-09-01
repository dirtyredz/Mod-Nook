using Chicken.UI;
using UnityEngine;

namespace ModNook
{
    /// <summary>
    /// The panel's overlay, handed explicitly to the row factory and its dialogs so they no longer
    /// read it off static fields on <see cref="Rows"/>.
    ///
    /// <para>
    /// <see cref="Root"/> is where a dialog parents itself; <see cref="Group"/> is the overlay's
    /// raycast blocker, stood down while a game popup shows on top of the panel and restored after;
    /// <see cref="ButtonTemplate"/> is the game button that widget clones are cut from.
    /// </para>
    /// </summary>
    internal sealed class OverlayContext
    {
        /// <summary>Where a dialog parents itself - the overlay's own transform.</summary>
        internal RectTransform Root { get; }

        /// <summary>
        /// The overlay's raycast blocker. A game popup opening behind an opaque, raycast-blocking
        /// overlay is a soft lock, so this is stood down while one shows and restored on its hide.
        /// </summary>
        internal CanvasGroup Group { get; }

        /// <summary>
        /// The game button widget clones are cut from. Re-resolved every time the panel opens: the
        /// pause screen is destroyed and rebuilt across sessions, so a template captured once becomes
        /// a destroyed object - and a dialog that checks it for null then renders with no buttons,
        /// which is a dialog you cannot leave.
        /// </summary>
        internal AnimatedButton ButtonTemplate { get; set; }

        internal OverlayContext(RectTransform root, CanvasGroup group)
        {
            Root = root;
            Group = group;
        }
    }
}
