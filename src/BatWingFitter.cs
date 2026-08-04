using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace ModNook
{
    /// <summary>
    /// Puts a relabelled button's bat wings back against its text.
    ///
    /// <para>
    /// Two things make this harder than setting an x offset. The wings are anchored to the button's
    /// own edges rather than to its centre, so an offset means something different for each one -
    /// they have to be re-anchored to the middle before a distance from the label means anything.
    /// And the pause menu's captions are auto-sized, so the width TextMeshPro would <em>prefer</em>
    /// is not the width it actually draws; asking for the preferred width is what threw the wings
    /// out into the margin. Only the rendered size is any use, and that does not exist until the
    /// layout has run - hence the wait.
    /// </para>
    /// </summary>
    internal sealed class BatWingFitter : MonoBehaviour
    {
        /// <summary>Distance from the edge of the text to the inner edge of a wing.</summary>
        private const float Gap = 26f;

        private int framesWaited;

        private void LateUpdate()
        {
            // One full frame, so the layout group has sized the row and TextMeshPro has generated
            // its mesh at the size it will really be drawn.
            if (framesWaited++ < 1)
            {
                return;
            }

            try
            {
                Fit();
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning($"Could not refit the bat wings: {e.Message}");
            }

            // A one-shot. Leaving it running would fight anything else that moves the wings.
            enabled = false;
        }

        private void Fit()
        {
            var wings = GetComponentsInChildren<AnimatedBatWing>(true);
            var label = GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();

            if (wings.Length == 0 || label == null || string.IsNullOrEmpty(label.text))
            {
                return;
            }

            label.ForceMeshUpdate();

            var width = label.GetRenderedValues(true).x;
            if (width <= 1f)
            {
                return;
            }

            var half = width * 0.5f;
            var labelCentre = label.transform.position.x;

            foreach (var wing in wings)
            {
                if (!(wing.transform is RectTransform rect))
                {
                    continue;
                }

                // Side is taken from where the wing currently sits in the world, because once the
                // anchors are normalised there is nothing left to infer it from.
                var side = rect.position.x < labelCentre ? -1f : 1f;
                var y = rect.anchoredPosition.y;

                rect.anchorMin = new Vector2(0.5f, rect.anchorMin.y);
                rect.anchorMax = new Vector2(0.5f, rect.anchorMax.y);
                rect.anchoredPosition = new Vector2(side * (half + Gap), y);
            }
        }
    }
}
