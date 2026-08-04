// Copied from mods/PlantPeek/src/PanelSprite.cs, namespace aside. See 10-visual-integration.md.
// Fix bugs in both copies.
using UnityEngine;

namespace ModNook
{
    /// <summary>
    /// Generates the rounded, gold-edged plate the mods in this repo use for their own panels.
    /// 9-sliced, so the corners hold their radius at any size.
    /// </summary>
    internal static class PanelSprite
    {
        private const int Size = 48;
        private const int Corner = 14;
        private const float EdgeWidth = 2.0f;

        // The game's window palette: deep plum fill, muted gold rim. See 10-visual-integration.md.
        private static readonly Color Fill = new Color32(0x1B, 0x0F, 0x2E, 0xFF);
        private static readonly Color Edge = new Color32(0xC7, 0xA2, 0x5B, 0xFF);

        private static Sprite cached;

        internal static Sprite Get()
        {
            if (cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ModNook_Panel"
            };

            var pixels = new Color[Size * Size];

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var distance = RoundedRectDistance(x + 0.5f, y + 0.5f);

                    var coverage = Mathf.Clamp01(0.5f - distance);
                    var rim = Mathf.Clamp01(distance + EdgeWidth) * Mathf.Clamp01(0.5f - distance);

                    var colour = Color.Lerp(Fill, Edge, Mathf.Clamp01(rim));
                    colour.a = coverage;

                    pixels[y * Size + x] = colour;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var border = new Vector4(Corner, Corner, Corner, Corner);
            cached = Sprite.Create(
                texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            cached.name = "ModNook_Panel";
            return cached;
        }

        private static Sprite circle;

        /// <summary>
        /// A plain disc, for the info icon. The 9-sliced plate above cannot do this job: stretched
        /// to a non-square rect its corners keep their radius and the result reads as a lozenge.
        /// </summary>
        internal static Sprite Circle()
        {
            if (circle != null)
            {
                return circle;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ModNook_Circle"
            };

            var pixels = new Color[size * size];
            var centre = size * 0.5f;
            var radius = centre - 1f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - centre;
                    var dy = y + 0.5f - centre;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // One pixel of falloff at the rim, so the edge is smooth without supersampling.
                    var colour = Color.white;
                    colour.a = Mathf.Clamp01(radius - distance);

                    pixels[y * size + x] = colour;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            circle = Sprite.Create(
                texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            circle.name = "ModNook_Circle";
            return circle;
        }

        private static float RoundedRectDistance(float x, float y)
        {
            var halfSize = Size * 0.5f;
            var innerHalf = halfSize - Corner;

            var dx = Mathf.Abs(x - halfSize) - innerHalf;
            var dy = Mathf.Abs(y - halfSize) - innerHalf;

            var outsideX = Mathf.Max(dx, 0f);
            var outsideY = Mathf.Max(dy, 0f);
            var outside = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY);

            var inside = Mathf.Min(Mathf.Max(dx, dy), 0f);

            return outside + inside - Corner;
        }
    }
}
