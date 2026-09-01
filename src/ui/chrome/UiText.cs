using TMPro;
using UnityEngine;

namespace ModNook
{
    /// <summary>
    /// The panel's shared text/layout primitives - a TMP label on its own object with the game font
    /// applied, and the full-parent stretch every full-bleed child wants. Used by both the chrome
    /// builder (<see cref="PanelChrome"/>) and the controller's dynamic content.
    /// </summary>
    internal static class UiText
    {
        internal static TextMeshProUGUI NewText(
            Transform parent, string content, TextAlignmentOptions alignment, Color colour, float size)
        {
            var host = new GameObject("Text", typeof(RectTransform));
            host.transform.SetParent(parent, false);

            var text = host.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.alignment = alignment;
            text.color = colour;
            text.fontSize = size;

            // Text on our own object has no neighbour to inherit from, so the game's font has to be
            // applied by hand. See 10-visual-integration.md.
            GameFonts.Apply(text, preferOutline: false);

            return text;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
