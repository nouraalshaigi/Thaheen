using System.Collections.Generic;
using UnityEngine;

namespace BuildingInteractionSystem
{
    // Generates a reusable, 9-sliced rounded-rectangle sprite at runtime so the popup's
    // "rounded playful design" doesn't depend on any external sprite asset being imported.
    public static class RoundedRectSpriteFactory
    {
        private static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();

        public static Sprite GetRoundedSprite(int radius, int textureSize = 128)
        {
            radius = Mathf.Clamp(radius, 2, textureSize / 2 - 1);
            int key = radius * 100000 + textureSize;

            if (cache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = $"RoundedRect_{radius}"
            };

            Color32[] pixels = new Color32[textureSize * textureSize];
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    byte alpha = (byte)Mathf.RoundToInt(SampleCoverage(x, y, textureSize, radius) * 255f);
                    pixels[y * textureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0, 0, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = tex.name;

            cache[key] = sprite;
            return sprite;
        }

        private static float SampleCoverage(int x, int y, int size, float radius)
        {
            float fx = x + 0.5f;
            float fy = y + 0.5f;

            float nearestCornerX = Mathf.Clamp(fx, radius, size - radius);
            float nearestCornerY = Mathf.Clamp(fy, radius, size - radius);

            float dx = fx - nearestCornerX;
            float dy = fy - nearestCornerY;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            return Mathf.Clamp01(radius - dist + 0.5f);
        }
    }
}
