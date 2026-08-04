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
    /// Edits a comma-separated setting as a list of entries rather than one long line of text.
    ///
    /// <para>
    /// This is the case that is genuinely unreadable in a text field. BiggerUI's canvas allowlist
    /// and its per-screen overrides are lists, and asking someone to spot a missing comma inside
    /// "GameCanvas,SharedCanvas,MenuCanvas" - in a box narrower than the value - is not editing,
    /// it is proofreading. Here each entry is its own row with its own remove button, and the
    /// comma-separated line is rebuilt on save.
    /// </para>
    /// <para>
    /// The stored format never changes. What lands in the config file is the same string the mod
    /// would have parsed anyway, so hand-editing still works and nothing breaks when this is not
    /// installed.
    /// </para>
    /// </summary>
    internal sealed class ListEditor : MonoBehaviour
    {
        private const char Separator = ',';

        private static ListEditor open;

        /// <summary>True while the dialog is up, so the panel leaves Escape to it.</summary>
        internal static bool IsOpen => open != null;

        private ConfigEntryBase entry;
        private AnimatedButton buttonTemplate;
        private Action<string> onSave;

        private GameObject root;
        private CanvasGroup group;
        private Transform itemsHost;
        private TextMeshProUGUI emptyNote;

        private readonly List<string> items = new List<string>();

        /// <summary>True when this setting reads as a list worth editing entry by entry.</summary>
        internal static bool Suits(ConfigEntryBase entry)
        {
            if (entry.SettingType != typeof(string))
            {
                return false;
            }

            if (Tags.Has(entry, "List"))
            {
                return true;
            }

            // A mod that describes its setting as comma-separated has told us what it is, without
            // needing to know this panel exists.
            var description = entry.Description?.Description;
            return description != null &&
                   description.IndexOf("comma", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static void Open(
            RectTransform parent, ConfigEntryBase entry, AnimatedButton buttonTemplate,
            Action<string> onSave)
        {
            CloseAny();

            var host = new GameObject("ModNook_ListEditor", typeof(RectTransform));
            host.transform.SetParent(parent, false);
            host.transform.SetAsLastSibling();

            var editor = host.AddComponent<ListEditor>();
            editor.entry = entry;
            editor.buttonTemplate = buttonTemplate;
            editor.onSave = onSave;
            editor.Build(host);

            open = editor;
        }

        /// <summary>Tears down any open editor, so one cannot outlive the panel that owns it.</summary>
        internal static void CloseAny()
        {
            if (open != null)
            {
                open.Close();
            }

            open = null;
        }

        private void Build(GameObject host)
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

            group = host.AddComponent<CanvasGroup>();

            var panel = new GameObject(
                "Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(host.transform, false);

            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(880f, 0f);

            var plate = panel.GetComponent<Image>();
            plate.sprite = PanelSprite.Get();
            plate.type = Image.Type.Sliced;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 32, 32);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text(panel.transform, Rows.LabelOf(entry), 34f, Palette.Label, TextAlignmentOptions.Center);

            var description = entry.Description?.Description;
            if (!string.IsNullOrEmpty(description))
            {
                Text(
                    panel.transform, description.Split('\n')[0], 22f, Palette.Muted,
                    TextAlignmentOptions.Center);
            }

            var list = new GameObject("Items", typeof(RectTransform), typeof(VerticalLayoutGroup));
            list.transform.SetParent(panel.transform, false);

            var listLayout = list.GetComponent<VerticalLayoutGroup>();
            listLayout.spacing = 8f;
            listLayout.childControlWidth = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandHeight = false;

            itemsHost = list.transform;

            emptyNote = Text(
                panel.transform, "Nothing in this list yet.", 24f, Palette.Muted,
                TextAlignmentOptions.Center);

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
                Templates.CloneButton(buttonTemplate, buttons.transform, "Add", "Add", Add);
                Templates.CloneButton(buttonTemplate, buttons.transform, "Save", "Save", Save);
                Templates.CloneButton(buttonTemplate, buttons.transform, "Cancel", "Cancel", Close);
            }

            // The mod's own description tells the player to write a comma-separated line, and this
            // dialog shows rows instead. Without saying why, that reads as the panel ignoring the
            // instructions right above it.
            Text(
                panel.transform,
                "This mod stores the setting as one comma-separated line. Add each entry on its " +
                "own row and Mod Nook joins them when you save.",
                20f, Palette.Muted, TextAlignmentOptions.Center);

            Load();
            Refresh();
        }

        private void Load()
        {
            items.Clear();

            var raw = entry.BoxedValue as string ?? string.Empty;
            items.AddRange(
                raw.Split(Separator)
                    .Select(part => part.Trim())
                    .Where(part => part.Length > 0));
        }

        private void Refresh()
        {
            for (var i = itemsHost.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(itemsHost.GetChild(i).gameObject);
            }

            emptyNote.gameObject.SetActive(items.Count == 0);

            foreach (var item in items.ToArray())
            {
                AddRow(item);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
        }

        private void AddRow(string item)
        {
            var row = new GameObject(
                "Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(HorizontalLayoutGroup));
            row.transform.SetParent(itemsHost, false);

            var plate = row.GetComponent<Image>();
            plate.sprite = PanelSprite.Get();
            plate.type = Image.Type.Sliced;
            plate.color = new Color(1f, 1f, 1f, 0.1f);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 12, 0, 0);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;

            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 56f;
            element.minHeight = 56f;

            var text = Text(row.transform, item, 26f, Palette.Label, TextAlignmentOptions.MidlineLeft);
            var textElement = text.gameObject.AddComponent<LayoutElement>();
            textElement.flexibleWidth = 1f;

            if (buttonTemplate == null)
            {
                return;
            }

            var remove = Templates.CloneButton(
                buttonTemplate, row.transform, "Remove", "Remove",
                () =>
                {
                    items.Remove(item);
                    Refresh();
                },
                48f);

            var removeElement = remove.gameObject.GetComponent<LayoutElement>();
            if (removeElement != null)
            {
                removeElement.preferredWidth = 150f;
                removeElement.flexibleWidth = 0f;
            }
        }

        private void Add()
        {
            Rows.Prompt(
                $"Add to {Rows.LabelOf(entry)}", "One entry. Commas are added for you.",
                string.Empty, group,
                typed =>
                {
                    var cleaned = typed?.Trim();
                    if (string.IsNullOrEmpty(cleaned))
                    {
                        return;
                    }

                    // A separator typed into a single entry would silently become two on save, so
                    // it is split here where the result is visible instead.
                    foreach (var part in cleaned.Split(Separator))
                    {
                        var piece = part.Trim();
                        if (piece.Length > 0 && !items.Contains(piece))
                        {
                            items.Add(piece);
                        }
                    }

                    Refresh();
                });
        }

        private void Save()
        {
            onSave?.Invoke(string.Join(",", items.ToArray()));
            Close();
        }

        private void Close()
        {
            if (open == this)
            {
                open = null;
            }

            DestroyImmediate(root);
        }

        private static TextMeshProUGUI Text(
            Transform parent, string content, float size, Color colour,
            TextAlignmentOptions alignment)
        {
            var host = new GameObject("Text", typeof(RectTransform));
            host.transform.SetParent(parent, false);

            var text = host.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.alignment = alignment;
            text.color = colour;
            text.fontSize = size;
            GameFonts.Apply(text, preferOutline: false);

            return text;
        }
    }
}
