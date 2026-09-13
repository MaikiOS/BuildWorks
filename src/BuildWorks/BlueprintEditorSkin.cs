using System;
using UnityEngine;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
    // Small original frames; sliced borders keep their bevels at any panel size.
    // The illustrated concept is a reference, never a stretched UI background.
    internal sealed class BlueprintEditorSkin : IDisposable
    {
        internal enum Surface { Panel, Header, Button, Hover, Active, Disabled, Input, Focus, Tooltip }
        private const int Size = 32;
        private readonly Sprite[] sprites = new Sprite[9];
        private bool disposed;

        internal Sprite Get(Surface surface)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintEditorSkin));
            int index = (int)surface;
            if (sprites[index]) return sprites[index];
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "BuildWorks_Skin_" + surface,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            try
            {
                texture.SetPixels32(BuildPixels(surface));
                texture.Apply(false, true);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                    new Vector4(8f, 8f, 8f, 8f));
                sprite.name = texture.name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                sprites[index] = sprite;
                return sprite;
            }
            catch { UnityEngine.Object.Destroy(texture); throw; }
        }

        internal void Apply(Image image, Surface surface)
        {
            image.sprite = Get(surface);
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }

        internal void Apply(Selectable control, bool active = false, bool input = false)
        {
            Image image = control.GetComponent<Image>();
            Apply(image, input ? Surface.Input : active ? Surface.Active : Surface.Button);
            control.targetGraphic = image;
            control.transition = Selectable.Transition.SpriteSwap;
            control.spriteState = new SpriteState
            {
                highlightedSprite = Get(active ? Surface.Focus : Surface.Hover),
                pressedSprite = Get(input ? Surface.Focus : Surface.Active),
                selectedSprite = Get(Surface.Focus),
                disabledSprite = Get(Surface.Disabled)
            };
        }

        // Pure pixel generation also permits an offline check without a Unity player.
        internal static Color32[] BuildPixels(Surface surface)
        {
            var pixels = new Color32[Size * Size];
            bool active = surface == Surface.Active;
            bool focus = surface == Surface.Focus;
            bool disabled = surface == Surface.Disabled;
            bool inset = surface == Surface.Input;
            Color32 fill = active ? new Color32(64, 40, 20, 255)
                : focus ? new Color32(39, 33, 24, 255)
                : surface == Surface.Hover ? new Color32(43, 40, 34, 255)
                : surface == Surface.Header ? new Color32(34, 34, 32, 255)
                : surface == Surface.Tooltip ? new Color32(19, 20, 20, 255)
                : inset ? new Color32(17, 18, 18, 255)
                : disabled ? new Color32(25, 26, 26, 255)
                : surface == Surface.Panel ? new Color32(25, 27, 27, 255)
                : new Color32(30, 31, 30, 255);
            Color32 bronze = disabled ? new Color32(66, 65, 60, 255)
                : focus ? new Color32(224, 176, 94, 255)
                : active ? new Color32(184, 128, 55, 255)
                : surface == Surface.Hover ? new Color32(161, 141, 103, 255)
                : new Color32(113, 100, 77, 255);
            for (int y = 0; y < Size; ++y)
            for (int x = 0; x < Size; ++x)
            {
                int edgeX = Math.Min(x, Size - 1 - x);
                int edgeY = Math.Min(y, Size - 1 - y);
                int edge = Math.Min(edgeX, edgeY);
                if (edgeX + edgeY < 3) continue; // chamfered outer corners
                Color32 color = fill;
                if (edge == 0) color = new Color32(9, 10, 10, 220);
                else if (edge == 1) color = bronze;
                else if (edge == 2)
                {
                    bool raisedEdge = y > Size / 2 || x < Size / 2 && edgeX < edgeY;
                    color = Tint(bronze, raisedEdge != inset ? 1.32f : 0.45f);
                }
                else if (edge == 3) color = new Color32(9, 10, 10, 255);
                else if (edgeX <= 7 && edgeY <= 7 && edgeX + edgeY == 11)
                    color = Tint(bronze, 0.82f); // small corner metal inlays
                else if (y >= Size - 6) color = Tint(fill, inset ? 0.78f : 1.12f);
                pixels[y * Size + x] = color;
            }
            return pixels;
        }

        private static Color32 Tint(Color32 color, float factor) => new Color32(
            (byte)Math.Min(255, color.r * factor), (byte)Math.Min(255, color.g * factor),
            (byte)Math.Min(255, color.b * factor), color.a);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int index = 0; index < sprites.Length; ++index)
            {
                Sprite sprite = sprites[index];
                if (!sprite) continue;
                Texture2D texture = sprite.texture;
                UnityEngine.Object.Destroy(sprite);
                UnityEngine.Object.Destroy(texture);
                sprites[index] = null;
            }
        }
    }
}
