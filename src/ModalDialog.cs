using System;
using UnityEngine;
using UnityEngine.UI;

namespace ModNook
{
    /// <summary>
    /// The shared shape of Mod Nook's build-your-own dialogs (<see cref="ColorPicker"/>,
    /// <see cref="KeyCapture"/>, <see cref="ListEditor"/>): a single modal at a time, opened over the
    /// panel overlay, dimming everything behind a centered plate, and always dismissible by Escape.
    ///
    /// <para>
    /// Only one is ever up - an open modal's full-screen dim blocks the row clicks that would open a
    /// second - so the "current" modal is one static, not one per type. That single handle is also
    /// what the panel's cancel/close paths steer: <see cref="CloseCurrent"/> shuts whatever is open
    /// without naming its type, so a new dialog kind is closeable the moment it exists.
    /// </para>
    /// <para>
    /// The game's native popups (<see cref="TextPopupDialog"/>, <see cref="Confirm"/>) are a
    /// different shape and deliberately stay outside this base.
    /// </para>
    /// </summary>
    internal abstract class ModalDialog : MonoBehaviour
    {
        private static ModalDialog current;

        /// <summary>True while any modal is up, so the panel leaves cancel/Escape to it.</summary>
        internal static bool IsAnyOpen => current != null;

        /// <summary>
        /// Tears down whatever modal is open. The panel calls this when it closes: a dialog is a
        /// child of the overlay, so one left open when the pause menu is dismissed would reappear -
        /// on top of everything, blocking every button - the next time the panel opens.
        /// </summary>
        internal static void CloseCurrent()
        {
            if (current != null)
            {
                current.Close();
            }

            current = null;
        }

        /// <summary>The full-screen host; owns the dim, the panel and this component.</summary>
        protected GameObject Root { get; private set; }

        /// <summary>
        /// The host's canvas group. Exposed so a modal can stand its own raycasts down while it
        /// borrows the game's text popup on top of itself.
        /// </summary>
        protected CanvasGroup Group { get; private set; }

        /// <summary>
        /// Creates <typeparamref name="T"/> over <paramref name="parent"/> (the panel overlay),
        /// wires it up through <paramref name="configure"/>, registers it as the current modal, then
        /// builds it.
        /// </summary>
        protected static T Show<T>(RectTransform parent, string name, Action<T> configure)
            where T : ModalDialog
        {
            CloseCurrent();

            var host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            host.transform.SetAsLastSibling();

            var dialog = host.AddComponent<T>();
            dialog.Root = host;
            configure(dialog);

            // Registered before Build, never after. A dialog assigned only on success is one nothing
            // can reach if building throws: CloseCurrent cannot find it and IsAnyOpen denies it
            // exists, while the half-built thing sits on screen blocking every click. Getting this
            // right in one place is the point of the base - two of the three dialogs used to get it
            // wrong.
            current = dialog;

            dialog.Build();
            return dialog;
        }

        /// <summary>
        /// Builds the full-screen dim and the centered panel plate every modal shares, and returns
        /// the panel transform for the subclass to fill. Only the width, padding, spacing and (for
        /// one dialog) child alignment differ; everything else is identical so the dialogs dim, size
        /// and frame the same way.
        /// </summary>
        protected Transform BuildShell(
            float width, RectOffset padding, float spacing,
            TextAnchor childAlignment = TextAnchor.UpperLeft)
        {
            var hostRect = (RectTransform)Root.transform;
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.offsetMin = Vector2.zero;
            hostRect.offsetMax = Vector2.zero;

            var dim = Root.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.01f, 0.04f, 0.8f);
            dim.raycastTarget = true;

            Group = Root.AddComponent<CanvasGroup>();

            var panel = new GameObject(
                "Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(Root.transform, false);

            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(width, 0f);

            var plate = panel.GetComponent<Image>();
            plate.sprite = PanelSprite.Get();
            plate.type = Image.Type.Sliced;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = childAlignment;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            panel.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            return panel.transform;
        }

        /// <summary>
        /// A centered, evenly-spaced row to drop buttons into - the action bar every dialog lays out
        /// the same way. Sized to one button-height by default.
        /// </summary>
        protected Transform ButtonRow(Transform parent, float height = 72f)
        {
            var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;

            return row.transform;
        }

        /// <summary>Builds the dialog's own contents. Runs with <see cref="Root"/> already set.</summary>
        protected abstract void Build();

        /// <summary>
        /// Escape always leaves, whatever state the dialog is in - one exit that depends on nothing
        /// having been built correctly. A dialog that listens for input (the key capture) overrides
        /// this to fold Escape into its own loop.
        /// </summary>
        protected virtual void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        /// <summary>Hook for a subclass that must notify something as it closes.</summary>
        protected virtual void OnClosing()
        {
        }

        protected void Close()
        {
            if (current == this)
            {
                current = null;
            }

            OnClosing();

            // Immediate, so a dialog dismissed as the panel closes is gone before the overlay is
            // hidden - a deferred Destroy would leave it to reappear with the overlay next time.
            DestroyImmediate(Root);
        }
    }
}
