using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colorify
{
    // Dibuja emblemas (Emblems) con las formas del juego, sin sprites, y el nombre con su color
    public static class EmblemView
    {
        private static readonly string[] RainbowHex = { "6F9BD8", "86B86C", "E6C15A", "D96C5F" };

        public static RectTransform Build(Transform parent, string emblemId, float size) => Build(parent, Emblems.Get(emblemId), size);

        public static RectTransform Build(Transform parent, Emblems.Emblem e, float size)
        {
            var root = UIKit.Rect("Emblem", parent);
            root.sizeDelta = Vector2.one * size;

            var outer = Layer(root, "Outer", e.outer, e.outerHex, size);
            if (e.hasInner) Layer(root, "Inner", e.inner, e.innerHex, size * e.innerScale);
            if (e.rainbow)
            {
                float d = size * 0.2f, off = size * 0.13f;
                for (int i = 0; i < 4; i++)
                {
                    var dot = Layer(root, "Dot" + i, Shape.Circle, RainbowHex[i], d);
                    dot.rectTransform.anchoredPosition = new Vector2(i % 2 == 0 ? -off : off, i < 2 ? off : -off);
                }
            }
            if (!string.IsNullOrEmpty(e.glyph))
            {
                var t = UIKit.Text("Glyph", root, e.glyph, size * (e.glyph.Length > 2 ? 0.22f : 0.3f), Collection.Hex(e.glyphHex ?? Emblems.Paper), black: true);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                t.rectTransform.sizeDelta = Vector2.one * size;
                t.rectTransform.anchoredPosition = Vector2.zero;
                t.raycastTarget = false;
            }
            outer.raycastTarget = false;
            return root;
        }

        private static Image Layer(RectTransform root, string name, Shape shape, string hex, float size)
        {
            var rt = UIKit.Rect(name, root);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Collection.Hex(hex);
            img.raycastTarget = false;
            ColorifyView.SetShape(img, shape, 2f, size);
            return img;
        }

        // Nombre con su color (texto enriquecido)
        public static string Name(string name, string nameColorId)
        {
            var c = Emblems.GetNameColor(nameColorId);
            if (c.rainbow) return ColorifyView.RainbowTitle(name);
            return $"<color=#{c.hex}>{name}</color>";
        }
    }
}
