using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModNook
{
    /// <summary>
    /// A single floating panel that shows a setting's description while the pointer is over its
    /// info icon.
    ///
    /// <para>
    /// Descriptions used to be printed under every row. They are the most useful text on the page
    /// and they were also, at a paragraph each, most of the page - a mod with nineteen settings
    /// became a wall to scroll past rather than a list to read. Putting them behind an icon keeps
    /// the list scannable and the explanation one hover away.
    /// </para>
    /// </summary>
    internal sealed class Tooltip : MonoBehaviour
    {
        private const float MaxWidth = 620f;
        private const float CursorGap = 20f;

        private static Tooltip instance;

        private RectTransform bounds;
        private RectTransform plate;
        private TextMeshProUGUI label;

        /// <summary>Null for an overlay canvas, which is what the conversion expects there.</summary>
        private Camera camera;

        /// <summary>Builds the shared tooltip under <paramref name="root"/>, once per panel.</summary>
        internal static void Ensure(RectTransform root)
        {
            if (instance != null && instance.bounds == root)
            {
                return;
            }

            var host = new GameObject("ModNook_Tooltip", typeof(RectTransform));
            host.transform.SetParent(root, false);

            instance = host.AddComponent<Tooltip>();
            instance.Build(root);
        }

        internal static void Show(string text, Vector2 screenPosition)
        {
            instance?.ShowAt(text, screenPosition);
        }

        internal static void Hide()
        {
            if (instance != null)
            {
                instance.plate.gameObject.SetActive(false);
            }
        }

        private void Build(RectTransform root)
        {
            bounds = root;

            // The plate is positioned in this object's space but clamped against the overlay's, so
            // the two have to be the same rect. Left at its default zero size, every tooltip would
            // be placed relative to a point in the middle of the screen.
            var self = (RectTransform)transform;
            self.anchorMin = Vector2.zero;
            self.anchorMax = Vector2.one;
            self.offsetMin = Vector2.zero;
            self.offsetMax = Vector2.zero;

            var canvas = root.GetComponentInParent<Canvas>();
            camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            var plateObject = new GameObject(
                "Plate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(CanvasGroup));
            plateObject.transform.SetParent(transform, false);

            plate = (RectTransform)plateObject.transform;
            plate.anchorMin = new Vector2(0f, 0f);
            plate.anchorMax = new Vector2(0f, 0f);
            plate.pivot = new Vector2(0f, 0f);

            var image = plateObject.GetComponent<Image>();
            image.sprite = PanelSprite.Get();
            image.type = Image.Type.Sliced;

            // The tooltip must never eat a click meant for the row underneath it.
            var group = plateObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var layout = plateObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 14, 16);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = plateObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(plate, false);

            label = textObject.AddComponent<TextMeshProUGUI>();
            label.color = Palette.Label;
            label.fontSize = 24f;
            label.enableWordWrapping = true;
            label.alignment = TextAlignmentOptions.TopLeft;
            GameFonts.Apply(label, preferOutline: false);

            var element = textObject.AddComponent<LayoutElement>();
            element.preferredWidth = MaxWidth;

            plate.gameObject.SetActive(false);
        }

        private void ShowAt(string text, Vector2 screenPosition)
        {
            label.text = text;
            plate.gameObject.SetActive(true);
            plate.SetAsLastSibling();

            LayoutRebuilder.ForceRebuildLayoutImmediate(plate);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    bounds, screenPosition, camera, out var local))
            {
                return;
            }

            // Local space is measured from the rect's centre; the plate is anchored bottom-left.
            var origin = local + bounds.rect.size * 0.5f + new Vector2(CursorGap, CursorGap);
            var size = plate.rect.size;

            // Keep the whole tooltip on screen: flip to the other side of the cursor rather than
            // letting it run off the edge, which is what the game's own text popup does wrong.
            if (origin.x + size.x > bounds.rect.width)
            {
                origin.x = Mathf.Max(0f, local.x + bounds.rect.width * 0.5f - CursorGap - size.x);
            }

            if (origin.y + size.y > bounds.rect.height)
            {
                origin.y = Mathf.Max(0f, bounds.rect.height - size.y);
            }

            plate.anchoredPosition = origin;
        }
    }

    /// <summary>
    /// The hover target. Kept separate from the tooltip so the icon owns nothing but its own text.
    /// </summary>
    internal sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal string Text;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(Text))
            {
                Tooltip.Show(Text, eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Tooltip.Hide();
        }

        private void OnDisable()
        {
            Tooltip.Hide();
        }
    }
}
