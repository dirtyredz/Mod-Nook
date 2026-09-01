using System;
using System.Linq;
using System.Reflection;
using Chicken.UI;
using TMPro;
using UnityEngine;

namespace ModNook
{
    /// <summary>
    /// Builds a corner prompt in the style of the game's "Close", for an action of our own.
    ///
    /// <para>
    /// The game's prompt bar cannot be borrowed for this. Its entries take their label from the
    /// bound action's own description, so registering one would produce a button in the right style
    /// saying the wrong word - there is no action in the game called "Reset". Cloning the widget and
    /// relabelling it keeps the look while letting the text say what the button does.
    /// </para>
    /// <para>
    /// The key icon is hidden rather than faked: this one is clicked, and showing a key cap for a
    /// binding that does not exist would be a lie about how to use it.
    /// </para>
    /// </summary>
    internal static class PromptButton
    {
        private static readonly FieldInfo IconField =
            typeof(InputButtonListWidget).GetField(
                "inputIconWidget", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo TextField =
            typeof(InputButtonListWidget).GetField(
                "textField", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Clones the prompt into <paramref name="parent"/>, anchored to the bottom-left corner.
        /// Returns null when no prompt widget could be found, so the caller can fall back.
        /// </summary>
        internal static GameObject Build(
            RectTransform parent, string label, Action onClick)
        {
            try
            {
                var source = Resources.FindObjectsOfTypeAll<InputButtonListWidget>()
                    .FirstOrDefault(x => x != null && x.gameObject.scene.IsValid());

                if (source == null)
                {
                    ModNookPlugin.Log.LogWarning("No input prompt widget found to clone.");
                    return null;
                }

                var clone = Templates.CloneInactive(source, "ModNook_Prompt");

                // Read the widget's own references before the component that owns them is removed.
                var icon = IconField?.GetValue(clone) as Component;
                var text = TextField?.GetValue(clone) as Component;

                var host = clone.gameObject;

                // The driver goes: left alive it would re-setup from Data it no longer has, and
                // clicking it would simulate an input action instead of calling us.
                UnityEngine.Object.DestroyImmediate(clone);
                Templates.StripLocalization(host, destroy: true);

                if (icon != null)
                {
                    icon.gameObject.SetActive(false);
                }

                var caption = text != null
                    ? text.GetComponent<TextMeshProUGUI>()
                    : host.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();

                if (caption != null)
                {
                    caption.text = label;
                }

                var button = host.GetComponent<AnimatedButton>();
                if (button != null)
                {
                    button.OnClick.AddListener(() =>
                    {
                        try
                        {
                            onClick();
                        }
                        catch (Exception e)
                        {
                            ModNookPlugin.Log.LogError($"'{label}' failed: {e}");
                        }
                    });
                }
                else
                {
                    ModNookPlugin.Log.LogWarning(
                        $"The '{label}' prompt has no button component, so it will not respond.");
                }

                host.transform.SetParent(parent, false);

                var rect = (RectTransform)host.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(Margin, Margin);

                host.SetActive(true);

                if (caption != null)
                {
                    // Again after activation, in case anything on the clone rewrites text on Awake.
                    caption.text = label;
                }

                return host;
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning($"Could not build the '{label}' prompt: {e.Message}");
                return null;
            }
        }

        /// <summary>Roughly where the game's own corner prompt sits.</summary>
        private const float Margin = 60f;
    }
}
