using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colorify
{
    // Metaprogresión (ver Collection): pantalla COLLECTION con los temas y colores, desbloqueos al acabar la partida
    // y cambio de tema en caliente (se recolorea la interfaz ya construida, sin recargar la escena).
    public partial class ColorifyGame
    {
        private CanvasGroup collection;
        private RectTransform collectionList;
        private TextMeshProUGUI collectionSub, overUnlock;
        private Button menuCollection;
        private ColorifyConfig soloColors;
        private int soloColorsCount = -1;

        // Colores de los tableros de Normal en solitario: los de siempre más los raros ya desbloqueados
        private ColorifyConfig SoloColors
        {
            get
            {
                var extra = Collection.UnlockedColors(save.lifetimePoints);
                if (extra.Count == 0) return cfg;
                if (soloColors == null || soloColorsCount != extra.Count)
                {
                    if (soloColors != null) Destroy(soloColors);
                    soloColors = Instantiate(cfg);
                    var all = new List<ColorifyConfig.ColorFamily>(cfg.families);
                    all.AddRange(extra);
                    soloColors.families = all.ToArray();
                    soloColorsCount = extra.Count;
                }
                return soloColors;
            }
        }

        // ---------------- Temas ----------------

        private static Color[] Palette()
        {
            var p = new List<Color> { UIKit.Paper, UIKit.PaperDark, UIKit.Ink, UIKit.InkSoft, UIKit.Danger };
            p.AddRange(ColorifyView.TitleColors);
            return p.ToArray();
        }

        // recolor = false al arrancar (aún no hay interfaz); true al cambiar de tema con el juego abierto
        private void ApplyTheme(Collection.Theme t, bool recolor)
        {
            var from = Palette();
            UIKit.Paper = t.paper; UIKit.PaperDark = t.paperDark; UIKit.Ink = t.ink; UIKit.InkSoft = t.inkSoft; UIKit.Danger = t.danger;
            for (int i = 0; i < ColorifyView.TitleColors.Length && i < t.title.Length; i++) ColorifyView.TitleColors[i] = t.title[i];
            if (Camera.main != null) Camera.main.backgroundColor = UIKit.Paper;
            if (!recolor) return;
            var to = Palette();
            var fromHex = new string[from.Length];
            var toHex = new string[to.Length];
            for (int i = 0; i < from.Length; i++) { fromHex[i] = ColorUtility.ToHtmlStringRGB(from[i]); toHex[i] = ColorUtility.ToHtmlStringRGB(to[i]); }
            foreach (var g in GetComponentsInChildren<Graphic>(true))
            {
                g.color = MapColor(g.color, from, to);
                var tmp = g as TMP_Text;
                if (tmp == null || string.IsNullOrEmpty(tmp.text) || tmp.text.IndexOf('#') < 0) continue;
                // Colores dentro del texto (títulos arcoíris, «PTS»…): en dos pasos para no encadenar sustituciones
                string s = tmp.text;
                for (int i = 0; i < fromHex.Length; i++) s = s.Replace("#" + fromHex[i], "#§" + i + "§");
                for (int i = 0; i < toHex.Length; i++) s = s.Replace("#§" + i + "§", "#" + toHex[i]);
                tmp.text = s;
            }
        }

        private static Color MapColor(Color c, Color[] from, Color[] to)
        {
            for (int i = 0; i < from.Length; i++)
                if (Mathf.Abs(c.r - from[i].r) < 0.004f && Mathf.Abs(c.g - from[i].g) < 0.004f && Mathf.Abs(c.b - from[i].b) < 0.004f)
                    return new Color(to[i].r, to[i].g, to[i].b, c.a);
            return c;
        }

        // ---------------- Pantalla COLLECTION ----------------

        private void BuildCollectionOverlay()
        {
            collection = v.Overlay("Collection");
            var title = UIKit.Text("Title", collection.transform, ColorifyView.RainbowTitle("COLLECTION"), 104, UIKit.Ink, black: true);
            UIKit.Place(title.rectTransform, 0.5f, 1f, new Vector2(1000, 150), new Vector2(0, -150));
            collectionSub = UIKit.Text("Sub", collection.transform, "", 38, UIKit.InkSoft);
            UIKit.Place(collectionSub.rectTransform, 0.5f, 1f, new Vector2(1000, 110), new Vector2(0, -265));
            collectionList = UIKit.Rect("List", collection.transform);
            UIKit.Place(collectionList, 0.5f, 0.5f, new Vector2(1000, 1180), new Vector2(0, -60));
            var back = UIKit.Button("Back", collection.transform, "BACK", UIKit.PaperDark, new Vector2(420, 120), HideCollection, 50);
            UIKit.Place((RectTransform)back.transform, 0.5f, 0f, new Vector2(420, 120), new Vector2(0, 110));
        }

        private void ShowCollection()
        {
            sfx.Play(Sfx.Id.Click);
            RefreshCollection();
            Show(collection);
        }

        private void HideCollection() { sfx.Play(Sfx.Id.Click); Hide(collection); }

        private void RefreshCollection()
        {
            long life = save.lifetimePoints;
            var next = Collection.Next(life);
            collectionSub.text = $"{UIKit.Thousands(life)} PTS EARNED IN NORMAL\n" +
                (next != null ? $"Next: {next.name.ToUpperInvariant()} {Collection.KindLabel(next)} at {UIKit.Thousands(next.cost)}" : "Everything unlocked!");
            for (int i = collectionList.childCount - 1; i >= 0; i--) Destroy(collectionList.GetChild(i).gameObject);

            // Dos columnas: temas a la izquierda, colores a la derecha
            var themes = new List<Collection.Item>();
            var colors = new List<Collection.Item>();
            themes.Add(new Collection.Item { id = "theme.paper", name = Collection.DefaultTheme.name, kind = Collection.Kind.Theme, cost = 0, theme = Collection.DefaultTheme });
            foreach (var it in Collection.Ladder) (it.kind == Collection.Kind.Theme ? themes : colors).Add(it);
            Column("THEMES", themes, -250f, life);
            Column("COLORS", colors, 250f, life);
        }

        private void Column(string header, List<Collection.Item> items, float x, long life)
        {
            var h = UIKit.Text("Header", collectionList, header, 40, UIKit.InkSoft, black: true);
            UIKit.Place(h.rectTransform, 0.5f, 1f, new Vector2(480, 60), new Vector2(x, -30));
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                bool unlocked = Collection.IsUnlocked(it, life);
                bool inUse = it.kind == Collection.Kind.Theme && it.theme.id == CurrentThemeId;
                var row = UIKit.Box("Item" + i, collectionList, inUse ? UIKit.WithAlpha(ColorifyView.TitleColors[3], 0.45f) : UIKit.WithAlpha(UIKit.PaperDark, unlocked ? 0.95f : 0.45f), 0.7f, raycast: true);
                UIKit.Place(row.rectTransform, 0.5f, 1f, new Vector2(480, 92), new Vector2(x, -110 - i * 104));

                // Muestra: el color, o los acentos del tema
                if (it.kind == Collection.Kind.Color)
                {
                    var sw = UIKit.Box("Swatch", row.transform, unlocked ? it.family.SwatchColor : UIKit.WithAlpha(UIKit.InkSoft, 0.25f), 2f);
                    UIKit.Place(sw.rectTransform, 0f, 0.5f, new Vector2(62, 62), new Vector2(50, 0));
                }
                else
                {
                    var bg = UIKit.Box("Swatch", row.transform, it.theme.paper, 0.5f);
                    UIKit.Place(bg.rectTransform, 0f, 0.5f, new Vector2(110, 62), new Vector2(74, 0));
                    for (int k = 0; k < 4; k++)
                    {
                        var dot = UIKit.Box("Dot" + k, bg.transform, unlocked ? it.theme.title[(k * 2) % it.theme.title.Length] : UIKit.WithAlpha(UIKit.InkSoft, 0.25f), 2f);
                        UIKit.Place(dot.rectTransform, 0f, 0.5f, new Vector2(20, 20), new Vector2(16 + k * 26, 0));
                    }
                }
                float textX = it.kind == Collection.Kind.Color ? 100f : 146f;
                var name = UIKit.Text("Name", row.transform, it.name.ToUpperInvariant(), 36, unlocked ? UIKit.Ink : UIKit.InkSoft, black: true);
                name.alignment = TextAlignmentOptions.Left;
                UIKit.Place(name.rectTransform, 0f, 0.5f, new Vector2(330, 46), new Vector2(textX + 165, 14));
                string state = !unlocked ? $"{UIKit.Thousands(it.cost)} PTS" : it.kind == Collection.Kind.Color ? "IN YOUR BOARDS" : inUse ? "IN USE" : "TAP TO USE";
                var info = UIKit.Text("State", row.transform, state, 26, UIKit.InkSoft);
                info.alignment = TextAlignmentOptions.Left;
                UIKit.Place(info.rectTransform, 0f, 0.5f, new Vector2(330, 36), new Vector2(textX + 165, -22));

                if (it.kind == Collection.Kind.Theme && unlocked && !inUse)
                {
                    var btn = row.gameObject.AddComponent<Button>();
                    var theme = it.theme;
                    btn.onClick.AddListener(() => SelectTheme(theme));
                }
            }
        }

        private string CurrentThemeId => string.IsNullOrEmpty(save.theme) ? Collection.DefaultTheme.id : save.theme;

        private void SelectTheme(Collection.Theme t)
        {
            if (!Collection.ThemeUnlocked(t.id, save.lifetimePoints)) return;
            sfx.Play(Sfx.Id.Card);
            save.theme = t.id;
            save.Save();
            ApplyTheme(t, recolor: true);
            RefreshCollection();
            Analytics.Log("theme_select", ("theme", t.id));
        }

        // Fin de partida de Normal: lo que se desbloquea con estos puntos o lo que falta para lo siguiente
        private void ShowUnlockLine(long runPoints)
        {
            if (overUnlock == null) return;
            long before = save.lifetimePoints, after = before + runPoints;
            var news = Collection.UnlockedBetween(before, after);
            if (news.Count > 0)
            {
                var names = new List<string>();
                foreach (var it in news) names.Add($"{it.name.ToUpperInvariant()} {Collection.KindLabel(it)}");
                overUnlock.text = ColorifyView.RainbowTitle("UNLOCKED: ") + string.Join(" · ", names) + "!";
                juice.PopIn(overUnlock.transform, 0.2f, 0.35f, 1.3f);
                sfx.Play(Sfx.Id.Card);
                return;
            }
            var next = Collection.Next(after);
            overUnlock.text = next == null ? "" :
                $"NEXT: {next.name.ToUpperInvariant()} {Collection.KindLabel(next)}  ·  {UIKit.Thousands(after)} / {UIKit.Thousands(next.cost)}";
        }
    }
}
