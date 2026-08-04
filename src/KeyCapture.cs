using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Chicken.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModNook
{
    /// <summary>
    /// A dialog for rebinding a key, replacing the text field a <see cref="KeyboardShortcut"/>
    /// otherwise falls back to.
    ///
    /// <para>
    /// Typing "LeftAlt" correctly, from memory, into a free-form box is not a way to bind a key.
    /// This shows what is bound, listens for a press, and only writes when the player says so - so
    /// hitting the wrong key costs a second press rather than a trip to the config file.
    /// </para>
    /// </summary>
    internal sealed class KeyCapture : MonoBehaviour
    {
        /// <summary>Held keys that qualify a binding rather than being the binding.</summary>
        private static readonly KeyCode[] Modifiers =
        {
            KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        private ConfigEntryBase entry;
        private Action<KeyboardShortcut> onSave;
        private Action onClosed;

        private GameObject root;
        private TextMeshProUGUI currentText;
        private TextMeshProUGUI promptText;

        private KeyboardShortcut captured;
        private bool listening;
        private bool hasCapture;

        /// <summary>A modifier pressed but not yet released, so not yet committed to.</summary>
        private KeyCode pending = KeyCode.None;

        /// <summary>
        /// Opens the dialog over <paramref name="parent"/>, which should be the panel overlay so it
        /// covers the settings list.
        /// </summary>
        private static KeyCapture open;

        /// <summary>True while the dialog is up, so the panel leaves Escape to it.</summary>
        internal static bool IsOpen => open != null;

        internal static void Open(
            RectTransform parent, ConfigEntryBase entry, AnimatedButton buttonTemplate,
            Action<KeyboardShortcut> onSave, Action onClosed = null)
        {
            CloseAny();

            var host = new GameObject("ModNook_KeyCapture", typeof(RectTransform));
            host.transform.SetParent(parent, false);
            host.transform.SetAsLastSibling();

            var capture = host.AddComponent<KeyCapture>();
            capture.entry = entry;
            capture.onSave = onSave;
            capture.onClosed = onClosed;
            capture.Build(host, buttonTemplate);

            open = capture;
        }

        /// <summary>
        /// Tears down any open dialog. The panel calls this when it closes: the dialog is a child of
        /// the overlay, so without it a dialog left open when the pause menu is dismissed simply
        /// reappears - on top of everything, blocking every button - the next time the panel opens.
        /// </summary>
        internal static void CloseAny()
        {
            if (open != null)
            {
                open.Close();
            }

            open = null;
        }

        private void Build(GameObject host, AnimatedButton buttonTemplate)
        {
            root = host;

            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.offsetMin = Vector2.zero;
            hostRect.offsetMax = Vector2.zero;

            var dim = host.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.01f, 0.04f, 0.8f);
            dim.raycastTarget = true;

            var panel = new GameObject(
                "Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(host.transform, false);

            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 0f);

            var plate = panel.GetComponent<Image>();
            plate.sprite = PanelSprite.Get();
            plate.type = Image.Type.Sliced;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 36, 36);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text(panel.transform, Describe(entry.Definition.Key), 34f, Palette.Label);

            currentText = Text(
                panel.transform, $"Current: {entry.BoxedValue}", 28f, Palette.Muted);

            promptText = Text(panel.transform, "Press any key...", 30f, Palette.Label);

            var buttons = new GameObject(
                "Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            buttons.transform.SetParent(panel.transform, false);

            var buttonLayout = buttons.GetComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 20f;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonLayout.childControlWidth = true;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandHeight = false;

            var buttonsElement = buttons.AddComponent<LayoutElement>();
            buttonsElement.preferredHeight = 72f;
            buttonsElement.minHeight = 72f;

            if (buttonTemplate != null)
            {
                Templates.CloneButton(buttonTemplate, buttons.transform, "Save", "Save", Save);
                Templates.CloneButton(
                    buttonTemplate, buttons.transform, "Retry", "Try again", Listen);
                Templates.CloneButton(buttonTemplate, buttons.transform, "Cancel", "Cancel", Close);
            }

            // What the mod's description asks for is a key name typed into a field. Saying what
            // this does instead - and what it can and cannot store - saves a player wondering why
            // their Ctrl went missing.
            Text(
                panel.transform,
                entry.SettingType == typeof(KeyCode)
                    ? "This mod stores a single key, so Ctrl, Shift and Alt are not saved with it. " +
                      "Nothing is written until you press Save."
                    : "Hold Ctrl, Shift or Alt while pressing a key to include them. Press one on " +
                      "its own to bind just that key. Nothing is written until you press Save.",
                20f, Palette.Muted);

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            Listen();
        }

        /// <summary>Starts (or restarts) waiting for a key.</summary>
        private void Listen()
        {
            hasCapture = false;
            pending = KeyCode.None;
            promptText.text = "Press any key...";
            promptText.color = Palette.Label;

            // A frame's grace, so the click that armed this is not itself read as the new binding.
            listening = false;
            Invoke(nameof(BeginListening), 0.15f);
        }

        private void BeginListening()
        {
            listening = true;
        }

        private void Update()
        {
            if (!listening)
            {
                return;
            }

            // Escape leaves without binding. Binding Escape itself would trap the player in a menu
            // whose only exit is the key they just took away.
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            // An ordinary key ends it straight away, taking whatever modifiers are held with it.
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None || Modifiers.Contains(key) || IsMouse(key) ||
                    IsIgnored(key) || !UnityEngine.Input.GetKeyDown(key))
                {
                    continue;
                }

                Commit(key);
                return;
            }

            // A modifier is only provisional. Pressing Ctrl on the way to Ctrl+S must not bind Ctrl,
            // so it is held back until released - if nothing else follows, the player meant the
            // modifier itself, which is a perfectly ordinary binding. PlantPeek defaults to LeftAlt.
            foreach (var modifier in Modifiers)
            {
                if (UnityEngine.Input.GetKeyDown(modifier))
                {
                    pending = modifier;
                }
            }

            if (pending != KeyCode.None && UnityEngine.Input.GetKeyUp(pending))
            {
                Commit(pending);
            }
        }

        private void Commit(KeyCode main)
        {
            // Never list the main key among its own modifiers - that is what turned Right Alt into
            // "AltGr + RightAlt".
            var held = Modifiers
                .Where(m => m != main && UnityEngine.Input.GetKey(m))
                .ToArray();

            captured = new KeyboardShortcut(main, held);
            hasCapture = true;
            listening = false;
            pending = KeyCode.None;

            promptText.text = captured.ToString();
            promptText.color = Palette.Label;
        }

        /// <summary>
        /// Mouse buttons are not bindable here. The dialog's own Save, Try again and Cancel are
        /// clicked with the mouse, so accepting a click as the binding means every press of a
        /// button is also an attempt to bind that button.
        /// </summary>
        private static bool IsMouse(KeyCode key)
        {
            return key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6;
        }

        /// <summary>
        /// Windows reports the right Alt key as AltGr <em>and</em> RightAlt in the same frame. Left
        /// in, AltGr wins the ordinary-key loop and the binding comes out as "AltGr + RightAlt".
        /// </summary>
        private static bool IsIgnored(KeyCode key)
        {
            return key == KeyCode.AltGr;
        }

        private void Save()
        {
            if (!hasCapture)
            {
                promptText.text = "Press a key first";
                promptText.color = Palette.Muted;
                return;
            }

            onSave?.Invoke(captured);
            currentText.text = $"Current: {captured}";
            Close();
        }

        private void Close()
        {
            if (open == this)
            {
                open = null;
            }

            onClosed?.Invoke();

            // Immediate, so a dialog dismissed as the panel closes is gone before the overlay is
            // hidden - a deferred Destroy would leave it to reappear with the overlay next time.
            DestroyImmediate(root);
        }

        private static TextMeshProUGUI Text(
            Transform parent, string content, float size, Color colour)
        {
            var host = new GameObject("Text", typeof(RectTransform));
            host.transform.SetParent(parent, false);

            var text = host.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.alignment = TextAlignmentOptions.Center;
            text.color = colour;
            text.fontSize = size;
            text.enableWordWrapping = true;
            GameFonts.Apply(text, preferOutline: false);

            return text;
        }

        /// <summary>Splits a config key on its camel case, matching the rest of the panel.</summary>
        private static string Describe(string key)
        {
            var builder = new System.Text.StringBuilder(key.Length + 8);
            for (var i = 0; i < key.Length; i++)
            {
                var c = key[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(key[i - 1]))
                {
                    builder.Append(' ');
                    builder.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }
}
