using System;
using System.Reflection;
using Chicken.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ModNook
{
    /// <summary>
    /// Everything to do with the game's pause menu itself: sourcing its button template, adding
    /// ours, and making room for it.
    ///
    /// The pause panel's background is a fixed size that does not grow with its contents. The game
    /// ships five buttons and the panel fits five buttons. Every mod that adds one eats into the
    /// margin, and with two such mods installed the last entry sits on or past the bottom edge.
    /// Rather than shave spacing to buy room - which only defers the problem to the next mod - this
    /// grows the panel to fit whatever is actually in it.
    /// </summary>
    internal static class PauseMenu
    {
        private static readonly FieldInfo SettingsButtonField =
            typeof(PauseScreen).GetField("settingsButton", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Breathing room kept above and below the buttons when no original is measurable.</summary>
        private const float MinimumMargin = 48f;

        /// <summary>The game's own pause-menu button, used as the template for ours.</summary>
        internal static AnimatedButton ButtonTemplate(PauseScreen screen)
        {
            return SettingsButtonField?.GetValue(screen) as AnimatedButton;
        }

        /// <summary>
        /// Clones the native pause button. Decorations are stripped because the bat wings are
        /// authored to the width of the label they shipped with.
        /// </summary>
        internal static AnimatedButton AddButton(
            PauseScreen screen, string label, Action onClick)
        {
            var template = ButtonTemplate(screen);
            if (template == null)
            {
                ModNookPlugin.Log.LogError(
                    "PauseScreen.settingsButton not found - cannot add a pause-menu button.");
                return null;
            }

            // The one button that keeps the game's selection marker. It sits in the pause menu
            // beside Continue and Settings, and without the wings it is visibly the odd one out.
            // Everything inside the panel declines it.
            var button = Templates.CloneButton(
                template, template.transform.parent, "ModNook_Button", label, onClick,
                keepWings: true);
            button.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

            return button;
        }

        /// <summary>
        /// Grows the pause panel's backing plate until the button column fits inside it, with the
        /// panel's original margins preserved. Does nothing if the column already fits.
        /// </summary>
        internal static void FitToContents(PauseScreen screen)
        {
            try
            {
                var template = ButtonTemplate(screen);
                var column = template?.transform.parent as RectTransform;
                if (column == null)
                {
                    return;
                }

                // Let the layout settle before measuring; the button we just added has not been
                // through a layout pass yet.
                LayoutRebuilder.ForceRebuildLayoutImmediate(column);

                var panel = FindPanel(column);
                if (panel == null)
                {
                    ModNookPlugin.Log.LogWarning(
                        "The pause menu's panel could not be identified, so it was left alone. " +
                        "Extra buttons may overflow it.");
                    return;
                }

                // Measure the layout root rather than the button column: the column shares the
                // panel with the logo, so the buttons alone never account for the space in use.
                var stack = LayoutRoot(column, panel);
                LayoutRebuilder.ForceRebuildLayoutImmediate(stack);

                var needed = LayoutUtility.GetPreferredHeight(stack);
                var available = panel.rect.height;
                var overflow = needed + MinimumMargin - available;

                ModNookPlugin.Log.LogInfo(
                    $"Pause menu: {column.childCount} buttons, '{stack.name}' needs {needed:0}px, " +
                    $"panel '{panel.name}' is {available:0}px.");

                if (overflow <= 1f)
                {
                    return;
                }

                GrowPanel(panel, overflow);

                ModNookPlugin.Log.LogInfo(
                    $"Grew the pause panel by {overflow:0}px so its {column.childCount} buttons fit.");
            }
            catch (Exception e)
            {
                // A pause menu that is slightly too small beats one that threw on the way up.
                ModNookPlugin.Log.LogWarning($"Could not resize the pause panel: {e.Message}");
            }
        }

        /// <summary>
        /// The outermost part of the pause window - the last ancestor before the canvas.
        ///
        /// Looking for the nearest ancestor that draws a background does not work here: the plate
        /// (<c>Background</c>) is a <em>sibling</em> of the button stack, not one of its parents, so
        /// that walk sails past it and hits the canvas. The whole window is the thing to resize,
        /// and everything inside it is sized from that.
        /// </summary>
        private static RectTransform FindPanel(RectTransform column)
        {
            RectTransform outermost = null;

            for (var current = column.parent as RectTransform;
                 current != null;
                 current = current.parent as RectTransform)
            {
                if (current.GetComponent<Canvas>() != null)
                {
                    break;
                }

                outermost = current;
            }

            return outermost;
        }

        /// <summary>
        /// The highest layout group between the button column and the panel, which is what actually
        /// governs how tall the window's contents are.
        /// </summary>
        private static RectTransform LayoutRoot(RectTransform column, RectTransform panel)
        {
            var found = column;

            for (var current = column;
                 current != null && current != panel;
                 current = current.parent as RectTransform)
            {
                if (current.GetComponent<VerticalLayoutGroup>() != null)
                {
                    found = current;
                }
            }

            return found;
        }

        /// <summary>
        /// Grows the window and everything inside it that was sized to match.
        ///
        /// Children anchored to stretch follow their parent for free; children carrying their own
        /// fixed height do not, and would leave the plate at the old size behind taller contents.
        /// Matching on the previous height is what tells the two apart.
        /// </summary>
        private static void GrowPanel(RectTransform panel, float amount)
        {
            var was = panel.rect.height;
            Grow(panel, amount);

            foreach (var child in panel.GetComponentsInChildren<RectTransform>(true))
            {
                if (child == panel || !Mathf.Approximately(child.rect.height, was))
                {
                    continue;
                }

                var stretches = !Mathf.Approximately(child.anchorMin.y, child.anchorMax.y);
                if (!stretches)
                {
                    Grow(child, amount);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        /// <summary>
        /// Adds height without moving the element's centre, so a panel anchored mid-screen grows
        /// evenly rather than sliding down off it.
        /// </summary>
        private static void Grow(RectTransform rect, float amount)
        {
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, rect.sizeDelta.y + amount);
        }

    }
}
