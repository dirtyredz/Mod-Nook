using System;
using System.Collections.Generic;
using System.Linq;
using Chicken.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModNook
{
    /// <summary>
    /// Sources the game's own settings widgets so this panel is built out of them rather than
    /// redrawn in their likeness.
    ///
    /// This is the whole reason the panel reads correctly at any font size: a cloned
    /// <see cref="CycleButton"/> is the same object the game's Settings screen uses, with the same
    /// anchors, the same font and the same controller navigation. Nothing here invents a look.
    /// See 10-visual-integration.md.
    /// </summary>
    internal static class Templates
    {
        private static CycleButton cycle;
        private static SliderButton slider;
        private static AnimatedToggle toggle;
        private static bool searched;

        /// <summary>The game's checkbox. Falls back to a two-item cycle when none is loaded.</summary>
        internal static AnimatedToggle Toggle
        {
            get { Search(); return toggle; }
        }

        /// <summary>The row the game uses for every enum and boolean in its own Settings.</summary>
        internal static CycleButton Cycle
        {
            get { Search(); return cycle; }
        }

        /// <summary>The row the game uses for render scale and framerate.</summary>
        internal static SliderButton Slider
        {
            get { Search(); return slider; }
        }

        /// <summary>
        /// Templates are found once and held. Re-running the search on every page build would walk
        /// every loaded object each time, and the answer does not change.
        /// </summary>
        private static void Search()
        {
            if (searched)
            {
                return;
            }
            searched = true;

            cycle = Find<CycleButton>("CycleButton");
            slider = Find<SliderButton>("SliderButton");
            toggle = Find<AnimatedToggle>("AnimatedToggle");
        }

        /// <summary>
        /// Resources.FindObjectsOfTypeAll reaches inactive objects and loaded prefabs, not just
        /// what is on screen - so a widget stays sourceable while its own screen is hidden, which
        /// is exactly the situation the pause menu puts us in.
        /// </summary>
        private static T Find<T>(string label) where T : Component
        {
            try
            {
                var found = Resources.FindObjectsOfTypeAll<T>().Where(x => x != null).ToArray();

                // A laid-out scene instance beats a bare prefab: its rect already carries the size
                // the game gives that row, which is what the panel's layout needs.
                var template =
                    found.FirstOrDefault(x => x.gameObject.scene.IsValid() && HasSize(x)) ??
                    found.FirstOrDefault(x => x.gameObject.scene.IsValid()) ??
                    found.FirstOrDefault();

                if (template == null)
                {
                    ModNookPlugin.Log.LogWarning(
                        $"No {label} found. Settings needing it will show as read-only.");
                }
                else
                {
                    ModNookPlugin.Log.LogInfo($"{label} template: {PathOf(template.transform)}");
                }

                return template;
            }
            catch (Exception e)
            {
                ModNookPlugin.Log.LogWarning($"{label} lookup failed: {e.Message}");
                return null;
            }
        }

        private static bool HasSize(Component component)
        {
            var rect = component.transform as RectTransform;
            return rect != null && rect.rect.width > 1f && rect.rect.height > 1f;
        }

        internal static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                parts.Add(current.name);
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ------------------------------------------------------------------ cloning

        private static Transform staging;

        /// <summary>
        /// An inactive holder that clones are born into, so their <c>Awake</c> does not run until
        /// we have finished stripping them.
        ///
        /// This is not tidiness. <see cref="AnimatedBatWing.Awake"/> subscribes to an
        /// <c>AnimatedWidget</c> that lives outside the cloned subtree, so a clone registers
        /// against the pause screen's own shared signal. Chicken's <c>Signal.Dispatch</c> is a
        /// multicast delegate invoked in one call, so the moment a clone's handler throws, every
        /// later listener on that signal is skipped - and the wings stop animating for the whole
        /// menu, not just for us. Disabling the component afterwards is too late; the subscription
        /// already happened.
        /// </summary>
        private static Transform Staging
        {
            get
            {
                if (staging == null)
                {
                    var host = new GameObject("ModNook_Staging");
                    host.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(host);
                    staging = host.transform;
                }

                return staging;
            }
        }

        /// <summary>
        /// Clones a native widget without waking it. The caller strips what it does not want, then
        /// calls <see cref="Place{T}"/> to put it on screen.
        /// </summary>
        internal static T CloneInactive<T>(T template, string name) where T : Component
        {
            var clone = UnityEngine.Object.Instantiate(template, Staging, false);
            clone.name = name;
            return clone;
        }

        /// <summary>
        /// Moves a staged clone into the live hierarchy, carrying over the height it has as part of
        /// a real settings screen.
        ///
        /// The height matters: a layout group has no way to measure one of these rows on its own
        /// and will collapse it to nothing, stacking every row on the same line.
        /// </summary>
        internal static void Place<T>(T clone, T template, Transform parent, float fallbackHeight = 64f)
            where T : Component
        {
            var templateRect = template.transform as RectTransform;
            if (templateRect != null)
            {
                var height = templateRect.rect.height;
                var element = clone.gameObject.GetComponent<LayoutElement>() ??
                              clone.gameObject.AddComponent<LayoutElement>();

                element.preferredHeight = height > 1f ? height : fallbackHeight;
                element.minHeight = element.preferredHeight;
            }

            clone.transform.SetParent(parent, false);
            clone.gameObject.SetActive(true);
        }

        /// <summary>
        /// The whole dance for a labelled button: stage it, strip it, label it, place it, then
        /// label it again.
        ///
        /// The second labelling is deliberate belt-and-braces. Anything that rewrites text from
        /// <c>Awake</c> gets its chance the moment the clone is activated, and a button that
        /// silently reverts to the template's own caption is a bug that reads as "every mod is
        /// called Settings".
        /// </summary>
        internal static AnimatedButton CloneButton(
            AnimatedButton template, Transform parent, string name, string label, Action onClick,
            float height = 64f, bool keepWings = false)
        {
            var button = CloneInactive(template, name);

            // Destroyed rather than disabled: a plain button has no value text depending on it.
            StripLocalization(button.gameObject, destroy: true);
            RemoveBatWings(button.gameObject);

            // Kept only for the pause-menu entry, which sits among the game's own buttons and
            // should behave like one. Panel rows decline it - they are rebuilt constantly, and the
            // marker is shared state that does not survive having its target destroyed.
            if (!keepWings)
            {
                DisableSelectionMarker(button.gameObject);
            }

            SetLabel(button.gameObject, label);

            // Guarded, because OnClick is a Chicken Signal: one throwing handler aborts the single
            // multicast invocation and silently kills every listener registered after it.
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

            Place(button, template, parent, height);
            SetLabel(button.gameObject, label);

            if (keepWings)
            {
                FitBatWings(button.gameObject);
            }

            return button;
        }

        /// <summary>
        /// Clone, strip and place in one go, for the common case.
        /// </summary>
        internal static T Clone<T>(T template, Transform parent, string name) where T : Component
        {
            var clone = CloneInactive(template, name);
            StripDecorations(clone.gameObject);

            // Value widgets only. The game drives cycles and sliders from the navigation axes, and
            // the wheel is one of them - so with select-on-hover left on, scrolling a settings page
            // quietly edits whichever row the pointer crosses.
            DisableHoverSelect(clone.gameObject);

            Place(clone, template, parent);
            return clone;
        }

        /// <summary>
        /// Cuts a cloned bat wing's tie to the pause screen's shared widget, so it animates on its
        /// own instead of subscribing to a signal it does not own.
        ///
        /// <para>
        /// <c>AnimatedBatWing.Awake</c> listens to a <c>waitForWidget</c> that lives outside the
        /// cloned subtree, so every clone piles handlers onto the original screen's signal - and
        /// because Chicken's <c>Signal.Dispatch</c> is one multicast invocation, a single throwing
        /// clone silently stops every wing registered after it. Clearing the reference makes
        /// <c>Awake</c> subscribe to nothing and <c>OnEnable</c> start the idle animation directly,
        /// which is both safe and what a menu button is supposed to look like. Removing the wings
        /// instead - as 0.1.0 did - fixed the crash by deleting the feature.
        /// </para>
        /// <para>Only effective before the clone is activated, while <c>Awake</c> is still pending.</para>
        /// </summary>
        internal static void DetachBatWings(GameObject root)
        {
            foreach (var wing in root.GetComponentsInChildren<AnimatedBatWing>(true))
            {
                WaitForWidgetField?.SetValue(wing, null);
            }
        }

        private static readonly System.Reflection.FieldInfo WaitForWidgetField =
            typeof(AnimatedBatWing).GetField(
                "waitForWidget",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>
        /// Schedules a cloned button's bat wings to be moved back against the label it now carries.
        ///
        /// The wings sit at fixed offsets authored for the caption the template shipped with -
        /// "Settings", eight characters. Relabel it "Plant Peek  (17)" and they stay put, stranded
        /// out in the margin.
        /// </summary>
        internal static void FitBatWings(GameObject root)
        {
            if (root.GetComponentsInChildren<AnimatedBatWing>(true).Length > 0)
            {
                root.AddComponent<BatWingFitter>();
            }
        }

        /// <summary>
        /// Stops a row from selecting itself when the pointer passes over it.
        ///
        /// The game binds value changes to the navigation axes, and the mouse wheel drives those.
        /// With select-on-hover left on, scrolling a list of settings edits whichever row the
        /// pointer happens to be over - which is a very easy way to change six settings by
        /// accident while looking for the seventh.
        /// </summary>
        internal static void DisableHoverSelect(GameObject root)
        {
            foreach (var button in root.GetComponentsInChildren<AnimatedButton>(true))
            {
                HoverSelectField?.SetValue(button, false);
            }
        }

        private static readonly System.Reflection.FieldInfo HoverSelectField =
            typeof(AnimatedButton).GetField(
                "selectOnHover",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>
        /// Stops the clone's localization components from reasserting the game's own string over
        /// whatever label we set.
        ///
        /// <para>
        /// They are disabled, never destroyed. <c>CycleButton</c> and <c>SliderButton</c> hold their
        /// value text <em>as</em> a <c>LocalizedTextField</c> and write through it on every change,
        /// so removing it makes <c>Setup</c> throw into a destroyed object - which takes out the
        /// rest of the page with it. Disabling only stops Unity's own callbacks; a direct
        /// <c>Text</c> assignment still lands, because the setter writes straight to the TMP field.
        /// </para>
        /// <para>
        /// What disabling cannot prevent is <c>Awake</c>, which Unity runs on activation regardless.
        /// That is why labels are applied again after the clone goes live - see
        /// <see cref="CloneButton"/>.
        /// </para>
        /// </summary>
        internal static void StripLocalization(GameObject root, bool destroy = false)
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var name = behaviour == null ? null : behaviour.GetType().FullName;
                if (name == null || name.IndexOf("Localiz", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (destroy)
                {
                    // Safe on a plain button, which has no value text to write through. Disabling
                    // alone was not enough: something re-runs these on a later show, and the mod
                    // list came back reading "Settings" for every entry.
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
                else
                {
                    behaviour.enabled = false;
                }
            }
        }

        /// <summary>
        /// Removes a clone's bat wings outright, subtree and all.
        ///
        /// Used everywhere inside the panel. Detaching them from the screen's shared widget was
        /// meant to keep the animation without the crosstalk, and it still leaked: opening the
        /// panel once was enough to stop the wings working on the game's own menu. Inside a
        /// settings list they were never worth that - they are sized for a menu entry and read as
        /// specks against a full-width row. They stay only on the pause-menu button.
        /// </summary>
        internal static void RemoveBatWings(GameObject root)
        {
            foreach (var wing in root.GetComponentsInChildren<AnimatedBatWing>(true))
            {
                if (wing == null || wing.gameObject == root)
                {
                    continue;
                }

                // Immediate, so nothing wakes when the clone is activated.
                UnityEngine.Object.DestroyImmediate(wing.gameObject);
            }

        }

        /// <summary>
        /// Declines the game's shared selection marker - the bat wings that sweep around whatever
        /// is currently selected.
        ///
        /// <para>
        /// The wings are not part of a button. A pause-menu button is an image and a label and
        /// nothing else; the marker is drawn by a shared screen that <see cref="SelectableWidget"/>
        /// opts into. Declining it leaves the button's own highlight and audio intact.
        /// </para>
        /// <para>
        /// Used on everything inside the panel, where rows are destroyed and rebuilt on every page
        /// change - the marker is shared state, and pointing it at objects that are about to be
        /// torn down is what stopped the wings working on the game's own menu.
        /// </para>
        /// </summary>
        internal static void DisableSelectionMarker(GameObject root)
        {
            foreach (var widget in root.GetComponentsInChildren<SelectableWidget>(true))
            {
                SelectionScreenField?.SetValue(widget, false);
            }
        }

        private static readonly System.Reflection.FieldInfo SelectionScreenField =
            typeof(SelectableWidget).GetField(
                "useSelectionScreen",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>
        /// Everything a staged clone needs before it goes live: the game's own strings turned off,
        /// and the bat wings untied from the screen they came from.
        ///
        /// Hover-select is deliberately left alone here. On a button it is what makes the wings
        /// sweep out as the pointer passes, which is most of why the menu reads as part of the
        /// game - it is only a problem on a widget whose value the wheel can change.
        /// </summary>
        internal static void StripDecorations(GameObject root)
        {
            StripLocalization(root);
            RemoveBatWings(root);
            DisableSelectionMarker(root);
        }

        /// <summary>
        /// Sets a cloned row's label. <see cref="CycleButton"/> and <see cref="SliderButton"/> carry
        /// a second text for their current value, which must be left alone for the widget to drive.
        /// </summary>
        internal static void SetLabel(GameObject root, string label, bool labelOnly = false)
        {
            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length == 0)
            {
                return;
            }

            if (labelOnly)
            {
                texts[0].text = label;
                return;
            }

            foreach (var text in texts)
            {
                text.text = label;
            }
        }
    }
}
