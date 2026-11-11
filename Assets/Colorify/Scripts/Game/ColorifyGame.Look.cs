using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colorify
{
    // Piezas del diseño «mate premium» (lienzo aprobado el 11/11/2026; medidas del lienzo × 2,5 en la UI de 1080 × 1920):
    // logotipo de fichas, decoración del menú, datos del fin de partida y franja de desbloqueo.
    public partial class ColorifyGame
    {
        private Image overBestPill, overUnlockStrip;
        private readonly Image[] overStat = new Image[3];
        private readonly TextMeshProUGUI[] overStatValue = new TextMeshProUGUI[3], overStatLabel = new TextMeshProUGUI[3];

        // Logotipo: cada letra en una ficha con relieve de los colores del tema, un poco girada
        private void BuildLogo(Transform parent, Vector2 center)
        {
            const string word = "COLORIFY";
            const float w = 100f, h = 120f, gap = 12f;
            float total = word.Length * w + (word.Length - 1) * gap;
            float[] tilt = { -4f, 3f, -2f, 4f, -3f, 2f, -3f, 4f };
            float[] lift = { 0f, 7f, 0f, 5f, 0f, 7f, 0f, 5f };
            const float radius = 0.75f;
            for (int i = 0; i < word.Length; i++)
            {
                var tile = UIKit.Card("Logo" + i, parent, ColorifyView.TitleColors[i % ColorifyView.TitleColors.Length], radius);
                UIKit.Place(tile.rectTransform, 0.5f, 0.5f, new Vector2(w, h + UIKit.DepthUnits(radius)),
                    center + new Vector2(-total * 0.5f + w * 0.5f + i * (w + gap), lift[i]));
                tile.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -tilt[i]);
                var t = UIKit.Text("Letter", tile.transform, word[i].ToString(), 76, UIKit.PaperLight, black: true);
                UIKit.Stretch(t.rectTransform);
                t.rectTransform.offsetMin = new Vector2(0f, UIKit.DepthUnits(radius));
            }
        }

        // Fichas grandes y muy tenues en los bordes del menú (no reciben toques)
        private static void BuildMenuDecor(Transform parent)
        {
            // posición (centro), lado, giro (grados, sentido del lienzo), color del tema y opacidad
            var decor = new (Vector2 pos, float size, float tilt, int color, float alpha)[]
            {
                (new Vector2(-512f, 377f), 185f, -14f, 0, 0.16f),
                (new Vector2(512f, 137f), 145f, 12f, 4, 0.18f),
                (new Vector2(-430f, -530f), 110f, 9f, 2, 0.18f),
                (new Vector2(422f, -392f), 85f, -8f, 5, 0.16f),
                (new Vector2(292f, 697f), 65f, 18f, 3, 0.22f),
            };
            foreach (var d in decor)
            {
                var img = UIKit.Box("Decor", parent, UIKit.WithAlpha(ColorifyView.TitleColors[d.color], d.alpha), 22f / (d.size * 0.27f));
                UIKit.Place(img.rectTransform, 0.5f, 0.5f, Vector2.one * d.size, d.pos);
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -d.tilt);
            }
        }

        // Fin de partida: tres datos (Normal) o la línea de texto de antes (Supervivencia, sin datos)
        private void SetOverStats(params string[] pairs)
        {
            bool any = pairs != null && pairs.Length >= 2;
            for (int i = 0; i < overStat.Length; i++)
            {
                if (overStat[i] == null) continue;
                bool on = any && i * 2 + 1 < pairs.Length;
                overStat[i].gameObject.SetActive(on);
                if (!on) continue;
                overStatValue[i].text = pairs[i * 2];
                overStatLabel[i].text = pairs[i * 2 + 1];
            }
            if (v.overInfo != null) v.overInfo.gameObject.SetActive(!any);
        }

        // «NEW BEST» en una pastilla amarilla; el resto de mensajes del récord, como texto
        private void SetOverBest(string text, bool record)
        {
            v.overBest.text = text;
            v.overBest.color = record ? UIKit.Ink : UIKit.InkSoft;
            if (overBestPill != null) overBestPill.gameObject.SetActive(record);
        }

        // La franja de desbloqueo solo se ve cuando tiene texto
        private void SyncUnlockStrip()
        {
            if (overUnlockStrip != null && overUnlock != null) overUnlockStrip.gameObject.SetActive(!string.IsNullOrEmpty(overUnlock.text));
        }

        // Color que representa un desbloqueo (muestra de la tarjeta NEXT del menú)
        private static Color ItemColor(Collection.Item it)
        {
            if (it == null) return ColorifyView.TitleColors[4];
            switch (it.kind)
            {
                case Collection.Kind.Color: return it.family != null ? it.family.SwatchColor : ColorifyView.TitleColors[4];
                case Collection.Kind.Theme: return it.theme != null && it.theme.title.Length > 0 ? it.theme.title[0] : ColorifyView.TitleColors[4];
                case Collection.Kind.Emblem: return it.emblem != null ? Collection.Hex(it.emblem.outerHex) : ColorifyView.TitleColors[4];
                case Collection.Kind.NameColor:
                    return it.nameColor == null || it.nameColor.rainbow ? ColorifyView.TitleColors[0] : Collection.Hex(it.nameColor.hex);
            }
            return ColorifyView.TitleColors[4];
        }
    }
}
