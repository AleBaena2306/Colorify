using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Colorify.Online;

namespace Colorify
{
    // Metaprogresión (ver Collection): pantalla COLLECTION con los temas y colores, desbloqueos al acabar la partida
    // y cambio de tema en caliente (se recolorea la interfaz ya construida, sin recargar la escena).
    public partial class ColorifyGame
    {
        private CanvasGroup collection;
        private RectTransform collectionList;
        private TextMeshProUGUI collectionSub, overUnlock;
        private Button menuCollection, menuNext;
        private TextMeshProUGUI menuNextText, menuNextValue;
        private Image menuNextSwatch, menuNextFill;
        private RectTransform menuNewBadge;
        private ColorifyConfig soloColors;
        private int soloColorsCount = -1;
        private Collection.Theme previewing;   // tema bloqueado que se está probando unos segundos
        private Coroutine previewRoutine;
        private string collectionTab = "profile";   // profile · themes · colors
        private readonly Dictionary<string, Image> collectionTabs = new Dictionary<string, Image>();
        private readonly Dictionary<string, RectTransform> collectionTabBadges = new Dictionary<string, RectTransform>();

        // Puntos acumulados a mostrar: en la pantalla de fin de partida ya cuentan los de la partida que acaba de
        // terminar (se guardan al salir de esa pantalla, por si el jugador continúa con anuncio)
        private long LifetimeShown => save.lifetimePoints + (state == State.GameOver && normal != null && mode == Mode.Normal ? normal.points : 0);

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
            var p = new List<Color> { UIKit.Paper, UIKit.PaperDark, UIKit.PaperLight, UIKit.Ink, UIKit.InkSoft, UIKit.Danger };
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
            collection = v.Overlay("Collection", 1f);   // opaca: que no se transparente el menú ni el fin de partida
            var title = UIKit.Text("Title", collection.transform, ColorifyView.RainbowTitle("COLLECTION"), 104, UIKit.Ink, black: true);
            UIKit.Place(title.rectTransform, 0.5f, 1f, new Vector2(1000, 150), new Vector2(0, -150));
            collectionSub = UIKit.Text("Sub", collection.transform, "", 38, UIKit.InkSoft);
            UIKit.Place(collectionSub.rectTransform, 0.5f, 1f, new Vector2(1000, 110), new Vector2(0, -265));
            // Pestañas: PROFILE (nombre, emblema y color del nombre), THEMES y COLORS
            string[] tabs = { "profile", "themes", "colors" };
            string[] labels = { "PROFILE", "THEMES", "COLORS" };
            for (int i = 0; i < tabs.Length; i++)
            {
                string tab = tabs[i];
                var b = UIKit.Button("Tab" + tab, collection.transform, labels[i], UIKit.PaperDark, new Vector2(300, 90), () => { sfx.Play(Sfx.Id.Click); EndPreview(); collectionTab = tab; RefreshCollection(); }, 40);
                UIKit.Place((RectTransform)b.transform, 0.5f, 1f, new Vector2(300, 90), new Vector2(-320 + i * 320, -380));
                collectionTabs[tab] = b.GetComponent<Image>();
                var dot = UIKit.Box("New", b.transform, ColorifyView.TitleColors[5], 2f);
                UIKit.Place(dot.rectTransform, 1f, 1f, new Vector2(26, 26), new Vector2(-6, -6));
                collectionTabBadges[tab] = dot.rectTransform;
            }
            collectionList = UIKit.Rect("List", collection.transform);
            UIKit.Place(collectionList, 0.5f, 0.5f, new Vector2(1000, 1100), new Vector2(0, -110));
            var back = UIKit.Button("Back", collection.transform, "BACK", UIKit.PaperDark, new Vector2(420, 120), HideCollection, 50);
            UIKit.Place((RectTransform)back.transform, 0.5f, 0f, new Vector2(420, 120), new Vector2(0, 110));

            // Menú: lo siguiente que se desbloquea, con barra de progreso, y aviso NEW en el botón COLLECTION
            // Tarjeta NEXT (diseño del 11/11/2026): muestra del desbloqueo, nombre, puntos y barra de progreso
            menuNext = UIKit.Button("Next", v.menu.transform, "", UIKit.PaperLight, new Vector2(850, 145), ShowCollection, 30);
            UIKit.Place((RectTransform)menuNext.transform, 0.5f, 0.5f, new Vector2(850, 145), new Vector2(0, -412));
            float nd = UIKit.DepthUnits(0.6f) * 0.5f;
            menuNextSwatch = UIKit.Card("Swatch", menuNext.transform, ColorifyView.TitleColors[4], 1.4f);
            UIKit.Place(menuNextSwatch.rectTransform, 0f, 0.5f, new Vector2(85, 85 + UIKit.DepthUnits(1.4f)), new Vector2(35f + 42.5f, nd));
            menuNextText = menuNext.GetComponentInChildren<TextMeshProUGUI>();
            var nrt = menuNextText.rectTransform;
            nrt.anchorMin = new Vector2(0f, 0.5f); nrt.anchorMax = new Vector2(1f, 0.5f);
            nrt.offsetMin = new Vector2(150f, nd + 2f); nrt.offsetMax = new Vector2(-260f, nd + 46f);
            menuNextText.alignment = TextAlignmentOptions.Left;
            menuNextText.fontSize = 30f; menuNextText.enableAutoSizing = true; menuNextText.fontSizeMin = 18f; menuNextText.fontSizeMax = 30f;
            menuNextText.enableWordWrapping = false;
            menuNextValue = UIKit.Text("Value", menuNext.transform, "", 30, UIKit.InkSoft, black: true);
            var vrt = menuNextValue.rectTransform;
            vrt.anchorMin = new Vector2(0f, 0.5f); vrt.anchorMax = new Vector2(1f, 0.5f);
            vrt.offsetMin = new Vector2(150f, nd + 2f); vrt.offsetMax = new Vector2(-35f, nd + 46f);
            menuNextValue.alignment = TextAlignmentOptions.Right;
            menuNextValue.enableAutoSizing = true; menuNextValue.fontSizeMin = 18f; menuNextValue.fontSizeMax = 30f;
            var bar = UIKit.Box("Bar", menuNext.transform, UIKit.PaperDark, 2.5f);
            var brt = bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 0.5f); brt.anchorMax = new Vector2(1f, 0.5f);
            brt.offsetMin = new Vector2(150f, nd - 36f); brt.offsetMax = new Vector2(-35f, nd - 11f);
            menuNextFill = UIKit.Box("Fill", bar.transform, ColorifyView.TitleColors[4], 2.5f);
            UIKit.Stretch(menuNextFill.rectTransform);
            if (menuCollection != null)
            {
                var badge = UIKit.Box("New", menuCollection.transform, ColorifyView.TitleColors[5], 2f);
                UIKit.Place(badge.rectTransform, 1f, 1f, new Vector2(96, 46), new Vector2(-14, -2));
                var bt = UIKit.Text("Label", badge.transform, "NEW", 28, UIKit.Paper, black: true);
                UIKit.Stretch(bt.rectTransform);
                menuNewBadge = badge.rectTransform;
            }
            // Fin de partida: la línea de desbloqueos lleva a COLLECTION
            if (overUnlock != null)
            {
                overUnlock.raycastTarget = true;
                overUnlock.gameObject.AddComponent<Button>().onClick.AddListener(ShowCollection);
            }
            RefreshMenuCollection();
        }

        private void RefreshMenuCollection()
        {
            if (menuNext == null || save == null) return;
            long life = save.lifetimePoints;
            var next = Collection.Next(life);
            menuNext.gameObject.SetActive(next != null && save.normalRunsPlayed > 0);
            if (next != null)
            {
                menuNextText.text = $"NEXT · {next.name.ToUpperInvariant()} {Collection.KindLabel(next)}";
                menuNextValue.text = $"{UIKit.Thousands(life)} / {UIKit.Thousands(next.cost)}";
                menuNextSwatch.color = ItemColor(next);
                var frt = menuNextFill.rectTransform;
                frt.anchorMax = new Vector2(Mathf.Clamp01(next.cost > 0 ? (float)life / next.cost : 1f), 1f);
                frt.offsetMin = frt.offsetMax = Vector2.zero;
            }
            bool fresh = AnyNew(life, Collection.Kind.Theme, Collection.Kind.Color, Collection.Kind.Emblem, Collection.Kind.NameColor) || HasUnseenAwards;
            if (menuNewBadge != null)
            {
                bool was = menuNewBadge.gameObject.activeSelf;
                menuNewBadge.gameObject.SetActive(fresh);
                if (fresh && !was) juice.PopIn(menuNewBadge, 0.3f, 0.35f, 0.4f);
            }
            RefreshMenuProfile();
        }

        private void ShowCollection()
        {
            sfx.Play(Sfx.Id.Click);
            CompleteCollectionCoach();
            // Se abre en la pestaña de lo último nuevo (o en PROFILE)
            long life = LifetimeShown;
            collectionTab = HasUnseenAwards ? "profile" : collectionTab;
            for (int i = Collection.Ladder.Length - 1; i >= 0; i--)
                if (IsNew(Collection.Ladder[i], life)) { collectionTab = TabOf(Collection.Ladder[i].kind); break; }
            if (forceCollectionTab != null) { collectionTab = forceCollectionTab; forceCollectionTab = null; }
            RefreshCollection();
            Show(collection);
        }

        private void HideCollection()
        {
            sfx.Play(Sfx.Id.Click);
            EndPreview();
            Hide(collection);
            RefreshMenuCollection();
        }

        private void RefreshCollection()
        {
            long life = LifetimeShown;
            var next = Collection.Next(life);
            collectionSub.text = $"{UIKit.Thousands(life)} PTS EARNED IN NORMAL\n" +
                (next != null ? $"Next: {next.name.ToUpperInvariant()} {Collection.KindLabel(next)} at {UIKit.Thousands(next.cost)}" : "Everything unlocked!");
            for (int i = collectionList.childCount - 1; i >= 0; i--) Destroy(collectionList.GetChild(i).gameObject);
            foreach (var kv in collectionTabs) kv.Value.color = kv.Key == collectionTab ? ColorifyView.TitleColors[3] : UIKit.PaperDark;

            var themes = new List<Collection.Item>();
            var colors = new List<Collection.Item>();
            themes.Add(new Collection.Item { id = "theme.paper", name = Collection.DefaultTheme.name, kind = Collection.Kind.Theme, cost = 0, theme = Collection.DefaultTheme });
            foreach (var it in Collection.Ladder)
            {
                if (it.kind == Collection.Kind.Theme) themes.Add(it);
                else if (it.kind == Collection.Kind.Color) colors.Add(it);
            }
            // Puntos de cada pestaña con algo nuevo
            collectionTabBadges["profile"].gameObject.SetActive(collectionTab != "profile" && (HasUnseenAwards || AnyNew(life, Collection.Kind.Emblem, Collection.Kind.NameColor)));
            collectionTabBadges["themes"].gameObject.SetActive(collectionTab != "themes" && AnyNew(life, Collection.Kind.Theme));
            collectionTabBadges["colors"].gameObject.SetActive(collectionTab != "colors" && AnyNew(life, Collection.Kind.Color));

            if (collectionTab == "themes") Column("THEMES", themes, 0f, life);
            else if (collectionTab == "colors") Column("COLORS", colors, 0f, life);
            else BuildProfileTab(life);
            // Lo de la pestaña abierta ya se ha visto; lo del resto sigue marcado hasta abrirla
            MarkSeen(life, collectionTab);
            if (collectionTab == "profile") { foreach (var a in save.awards) if (!save.seenAwards.Contains(a)) save.seenAwards.Add(a); }
            save.Save();
        }

        // Lo visto se recuerda por objeto (save.seenItems): al abrir una pestaña, todo lo desbloqueado de ella queda visto
        private void MarkSeen(long life, string tab)
        {
            foreach (var it in Collection.Ladder)
                if (Collection.IsUnlocked(it, life) && TabOf(it.kind) == tab && !save.seenItems.Contains(it.id)) save.seenItems.Add(it.id);
        }

        private static string TabOf(Collection.Kind k) => k == Collection.Kind.Theme ? "themes" : k == Collection.Kind.Color ? "colors" : "profile";

        private bool AnyNew(long life, params Collection.Kind[] kinds)
        {
            foreach (var it in Collection.Ladder)
            {
                if (!Collection.IsUnlocked(it, life)) break;
                if (System.Array.IndexOf(kinds, it.kind) >= 0 && !save.seenItems.Contains(it.id)) return true;
            }
            return false;
        }

        private bool IsNew(Collection.Item it, long life) => it.id != null && Collection.IsUnlocked(it, life) && !save.seenItems.Contains(it.id);

        private bool HasUnseenAwards
        {
            get
            {
                if (save == null || save.awards == null) return false;
                foreach (var a in save.awards) if (!save.seenAwards.Contains(a)) return true;
                return false;
            }
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
                bool isNew = IsNew(it, life);
                var row = UIKit.Box("Item" + i, collectionList, inUse ? UIKit.WithAlpha(ColorifyView.TitleColors[3], 0.45f) : UIKit.WithAlpha(UIKit.PaperDark, unlocked ? 0.95f : 0.55f), 0.7f, raycast: true);
                UIKit.Place(row.rectTransform, 0.5f, 1f, new Vector2(480, 92), new Vector2(x, -110 - i * 104));
                if (isNew)
                {
                    var badge = UIKit.Box("New", row.transform, ColorifyView.TitleColors[5], 2f);
                    UIKit.Place(badge.rectTransform, 1f, 1f, new Vector2(84, 40), new Vector2(-10, 4));
                    var bt = UIKit.Text("Label", badge.transform, "NEW", 24, UIKit.Paper, black: true);
                    UIKit.Stretch(bt.rectTransform);
                    juice.PopIn(row.transform, 0.1f + 0.05f * i, 0.4f, 0.7f);
                }

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
                string state = !unlocked ? (it.kind == Collection.Kind.Theme ? $"{UIKit.Thousands(it.cost)} PTS · PREVIEW" : $"{UIKit.Thousands(it.cost)} PTS")
                    : it.kind == Collection.Kind.Color ? "IN YOUR BOARDS" : inUse ? "IN USE" : "TAP TO USE";
                var info = UIKit.Text("State", row.transform, state, 26, UIKit.InkSoft);
                info.alignment = TextAlignmentOptions.Left;
                info.enableAutoSizing = true; info.fontSizeMin = 16f; info.fontSizeMax = 26f;   // siempre en una línea
                name.enableAutoSizing = true; name.fontSizeMin = 22f; name.fontSizeMax = 36f;
                UIKit.Place(info.rectTransform, 0f, 0.5f, new Vector2(330, 36), new Vector2(textX + 165, -22));

                if (it.kind == Collection.Kind.Theme && !inUse)
                {
                    var btn = row.gameObject.AddComponent<Button>();
                    var theme = it.theme;
                    if (unlocked) btn.onClick.AddListener(() => SelectTheme(theme));
                    else btn.onClick.AddListener(() => PreviewTheme(theme));   // probarlo unos segundos: dan ganas de ganarlo
                }
            }
        }

        private string CurrentThemeId => string.IsNullOrEmpty(save.theme) ? Collection.DefaultTheme.id : save.theme;

        private void PreviewTheme(Collection.Theme t)
        {
            EndPreview();
            sfx.Play(Sfx.Id.Click);
            previewing = t;
            ApplyTheme(t, recolor: true);
            collectionSub.text = $"PREVIEW · {t.name.ToUpperInvariant()} THEME\nKeep playing to unlock it!";
            previewRoutine = StartCoroutine(EndPreviewSoon());
        }

        private System.Collections.IEnumerator EndPreviewSoon()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            previewRoutine = null;
            EndPreview();
            RefreshCollection();
        }

        private void EndPreview()
        {
            if (previewRoutine != null) { StopCoroutine(previewRoutine); previewRoutine = null; }
            if (previewing == null) return;
            previewing = null;
            ApplyTheme(Collection.ThemeById(CurrentThemeId), recolor: true);
        }

        private void SelectTheme(Collection.Theme t)
        {
            EndPreview();
            if (!Collection.ThemeUnlocked(t.id, LifetimeShown)) return;
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
                overUnlock.text = ColorifyView.RainbowTitle("UNLOCKED: ") + string.Join(" · ", names) + "!\n<size=75%>TAP TO SEE IT</size>";
                juice.PopIn(overUnlockStrip != null ? overUnlockStrip.transform : overUnlock.transform, 0.2f, 0.35f, 1.3f);
                sfx.Play(Sfx.Id.Card);
                return;
            }
            var next = Collection.Next(after);
            overUnlock.text = next == null ? "" :
                $"NEXT: {next.name.ToUpperInvariant()} {Collection.KindLabel(next)}  ·  {UIKit.Thousands(after)} / {UIKit.Thousands(next.cost)}";
        }

        // ---------------- Perfil: nombre, emblema y color del nombre ----------------

        private string CurrentLook => Emblems.Look(save.emblem, save.nameColor);

        private void BuildProfileTab(long life)
        {
            // Vista previa: así se ve en el ranking
            var preview = UIKit.Box("Preview", collectionList, UIKit.WithAlpha(ColorifyView.TitleColors[0], 0.25f), 0.6f);
            UIKit.Place(preview.rectTransform, 0.5f, 1f, new Vector2(980, 104), new Vector2(0, -56));
            var em = EmblemView.Build(preview.transform, save.emblem, 76f);
            UIKit.Place(em, 0f, 0.5f, new Vector2(76, 76), new Vector2(64, 0));
            var nm = UIKit.Text("Name", preview.transform, EmblemView.Name(CurrentName, save.nameColor), 44, UIKit.Ink, black: true);
            nm.alignment = TextAlignmentOptions.Left;
            nm.enableAutoSizing = true; nm.fontSizeMin = 26f; nm.fontSizeMax = 44f;
            UIKit.Place(nm.rectTransform, 0f, 0.5f, new Vector2(520, 70), new Vector2(380, 0));
            var best = UIKit.Text("Best", preview.transform, save.bestNormalPoints > 0 ? UIKit.Thousands(save.bestNormalPoints) + " <size=70%>pts</size>" : "", 38, UIKit.Ink, black: true);
            best.alignment = TextAlignmentOptions.Right;
            UIKit.Place(best.rectTransform, 1f, 0.5f, new Vector2(300, 70), new Vector2(-180, 0));
            var hint = UIKit.Text("Hint", collectionList, LocalProfile ? "Your name is your Playgama name. Your look is saved on this device." : "This is how everyone sees you on the leaderboard.", 28, UIKit.InkSoft);
            hint.enableAutoSizing = true; hint.fontSizeMin = 18f; hint.fontSizeMax = 28f;
            UIKit.Place(hint.rectTransform, 0.5f, 1f, new Vector2(980, 40), new Vector2(0, -132));
            if (CanRename || CanPlatformLogIn)
            {
                // Portfolio: cambiar el nombre (con Google para conservarlo). CrazyGames y Playgama: iniciar sesión en ellos
                var rn = UIKit.Button("Rename", collectionList, CanRename ? "CHANGE NAME" : "LOG IN TO KEEP YOUR NAME",
                    CanRename ? UIKit.PaperDark : ColorifyView.TitleColors[2], new Vector2(460, 74), ProfileAction, 32);
                UIKit.Place((RectTransform)rn.transform, 0.5f, 1f, new Vector2(460, 74), new Vector2(0, -196));
            }

            // Emblemas por puntos (5 por fila)
            Header("EMBLEMS", -255f);
            for (int i = 0; i < Emblems.All.Length; i++)
            {
                var e = Emblems.All[i];
                bool unlocked = Collection.EmblemUnlocked(e.id, life, save.awards);
                long cost = Collection.CostOf(Collection.Kind.Emblem, e.id);
                var item = FindItem(Collection.Kind.Emblem, e.id);
                EmblemCell(e, unlocked, unlocked ? e.name.ToUpperInvariant() : UIKit.Thousands(cost) + " PTS", i % 5, -360f - (i / 5) * 160f,
                    item != null && IsNew(item, life), () => PickEmblem(e, unlocked, $"{e.name.ToUpperInvariant()} EMBLEM · unlocks at {UIKit.Thousands(cost)} pts"));
            }

            // Temporada en curso: oro, plata y bronce para los 3 primeros del mes; y los ya ganados
            int season = Emblems.CurrentSeason;
            var now = System.DateTime.UtcNow;
            var end = new System.DateTime(now.Year, now.Month, 1, 0, 0, 0, System.DateTimeKind.Utc).AddMonths(1);
            int days = Mathf.Max(0, Mathf.CeilToInt((float)(end - now).TotalDays));
            Header($"SEASON {Emblems.Roman(season)}  ·  TOP 3 OF THE MONTH  ·  {days} DAY{(days == 1 ? "" : "S")} LEFT", -655f);
            var medals = new List<Emblems.Emblem>();
            for (int p = 1; p <= 3; p++) medals.Add(Emblems.SeasonEmblem(season, p));
            foreach (var a in save.awards)
            {
                int sn, pl;
                if (Emblems.TryParseSeason(a, out sn, out pl) && sn != season) medals.Add(Emblems.SeasonEmblem(sn, pl));
            }
            for (int i = 0; i < Mathf.Min(medals.Count, 5); i++)
            {
                var e = medals[i];
                bool won = save.awards.Contains(e.id);
                int sn, pl;
                Emblems.TryParseSeason(e.id, out sn, out pl);
                string label = won ? $"S{Emblems.Roman(sn)} {(pl == 1 ? "GOLD" : pl == 2 ? "SILVER" : "BRONZE")}" : $"TOP {pl}";
                EmblemCell(e, won, label, i, -760f, won && !save.seenAwards.Contains(e.id),
                    () => PickEmblem(e, won, $"Finish Season {Emblems.Roman(sn)} in the top {pl} of the monthly leaderboard to win it"));
            }

            // Color del nombre
            Header("NAME COLOR", -880f);
            for (int i = 0; i < Emblems.NameColors.Length; i++)
            {
                var c = Emblems.NameColors[i];
                bool unlocked = Collection.NameColorUnlocked(c.id, life);
                long cost = Collection.CostOf(Collection.Kind.NameColor, c.id);
                int col = i % 5, row = i / 5;
                bool sel = save.nameColor == c.id;
                var cell = UIKit.Box("Name" + i, collectionList, sel ? UIKit.WithAlpha(ColorifyView.TitleColors[3], 0.5f) : UIKit.WithAlpha(UIKit.PaperDark, unlocked ? 0.95f : 0.5f), 0.6f, raycast: true);
                UIKit.Place(cell.rectTransform, 0.5f, 1f, new Vector2(184, 78), new Vector2(-392f + col * 196f, -950f - row * 88f));
                var t = UIKit.Text("Label", cell.transform, unlocked ? EmblemView.Name(c.name.ToUpperInvariant(), c.id) : UIKit.Thousands(cost) + " PTS", unlocked ? 30 : 24, UIKit.InkSoft, black: true);
                t.enableAutoSizing = true; t.fontSizeMin = 16f; t.fontSizeMax = unlocked ? 30f : 24f;
                UIKit.Stretch(t.rectTransform, 6f);
                var item = FindItem(Collection.Kind.NameColor, c.id);
                if (item != null && IsNew(item, life)) NewBadge(cell.transform);
                var btn = cell.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() => PickNameColor(c, unlocked, $"{c.name.ToUpperInvariant()} NAME · unlocks at {UIKit.Thousands(cost)} pts"));
            }
        }

        private void Header(string text, float y)
        {
            var h = UIKit.Text("Header", collectionList, text, 32, UIKit.InkSoft, black: true);
            h.enableAutoSizing = true; h.fontSizeMin = 20f; h.fontSizeMax = 32f;
            UIKit.Place(h.rectTransform, 0.5f, 1f, new Vector2(980, 50), new Vector2(0, y));
        }

        private void EmblemCell(Emblems.Emblem e, bool unlocked, string label, int col, float y, bool isNew, System.Action onTap)
        {
            bool sel = save.emblem == e.id;
            var cell = UIKit.Box("Emblem" + e.id, collectionList, sel ? UIKit.WithAlpha(ColorifyView.TitleColors[3], 0.5f) : UIKit.WithAlpha(UIKit.PaperDark, unlocked ? 0.95f : 0.5f), 0.6f, raycast: true);
            UIKit.Place(cell.rectTransform, 0.5f, 1f, new Vector2(184, 150), new Vector2(-392f + col * 196f, y));
            var em = EmblemView.Build(cell.transform, e, 78f);
            UIKit.Place(em, 0.5f, 1f, new Vector2(78, 78), new Vector2(0, -52));
            if (!unlocked) foreach (var g in em.GetComponentsInChildren<Graphic>(true)) g.color = new Color(g.color.r, g.color.g, g.color.b, g.color.a * 0.3f);
            var t = UIKit.Text("Label", cell.transform, label, 24, unlocked ? UIKit.Ink : UIKit.InkSoft, black: true);
            t.enableAutoSizing = true; t.fontSizeMin = 14f; t.fontSizeMax = 24f;
            UIKit.Place(t.rectTransform, 0.5f, 0f, new Vector2(176, 40), new Vector2(0, 26));
            if (isNew) NewBadge(cell.transform);
            cell.gameObject.AddComponent<Button>().onClick.AddListener(() => onTap());
        }

        private void NewBadge(Transform parent)
        {
            var badge = UIKit.Box("New", parent, ColorifyView.TitleColors[5], 2f);
            UIKit.Place(badge.rectTransform, 1f, 1f, new Vector2(70, 34), new Vector2(-6, 4));
            var bt = UIKit.Text("Label", badge.transform, "NEW", 20, UIKit.Paper, black: true);
            UIKit.Stretch(bt.rectTransform);
            juice.PopIn(badge.transform, 0.15f, 0.35f, 0.5f);
        }

        private static Collection.Item FindItem(Collection.Kind kind, string id)
        {
            foreach (var it in Collection.Ladder)
                if (it.kind == kind && ((kind == Collection.Kind.Emblem && it.emblem.id == id) || (kind == Collection.Kind.NameColor && it.nameColor.id == id))) return it;
            return null;
        }

        private void PickEmblem(Emblems.Emblem e, bool unlocked, string lockedText)
        {
            if (!unlocked) { sfx.Play(Sfx.Id.Click); collectionSub.text = lockedText; return; }
            sfx.Play(Sfx.Id.Card);
            save.emblem = e.id;
            PushLook();
            RefreshCollection();
            Analytics.Log("emblem_select", ("emblem", e.id));
        }

        private void PickNameColor(Emblems.NameColor c, bool unlocked, string lockedText)
        {
            if (!unlocked) { sfx.Play(Sfx.Id.Click); collectionSub.text = lockedText; return; }
            sfx.Play(Sfx.Id.Card);
            save.nameColor = c.id;
            PushLook();
            RefreshCollection();
            Analytics.Log("name_color_select", ("color", c.id));
        }

        // Guarda el aspecto y, con servidor propio, lo manda para que se vea en el ranking
        private void PushLook()
        {
            save.Save();
            if (!OnlineEnabled) return;
            EnsureIdentity();
            StartCoroutine(OnlineApi.SetLook(cfg, save.playerId, CurrentLook, r => { if (r != null && r.ok) UpdateAwards(r.awards); }));
        }

        // Emblemas de temporada concedidos por el servidor. Si el elegido ya no vale, vuelve al de por defecto.
        private void UpdateAwards(List<string> awards)
        {
            if (awards == null) return;
            bool changed = awards.Count != save.awards.Count;
            foreach (var a in awards) if (!save.awards.Contains(a)) changed = true;
            if (!changed) return;
            save.awards = new List<string>(awards);
            ValidateLook();
            save.Save();
            RefreshMenuCollection();
        }

        private void ValidateLook()
        {
            if (!Collection.EmblemUnlocked(save.emblem, save.lifetimePoints, save.awards)) save.emblem = Emblems.DefaultEmblem;
            if (!Collection.NameColorUnlocked(save.nameColor, save.lifetimePoints)) save.nameColor = Emblems.DefaultNameColor;
        }

        // Nombre del jugador en el ranking de la plataforma (Playgama): el juego lo usa como su nombre
        private void RememberPlatformName(string name)
        {
            if (string.IsNullOrEmpty(name) || name == save.platformName) return;
            save.platformName = name.Length > 24 ? name.Substring(0, 24) : name;
            save.Save();
            if (collection != null && collection.gameObject.activeSelf) RefreshCollection();
            RefreshMenuProfile();
        }

        // Al arrancar: emblemas ganados y aspecto guardado en el servidor. En Playgama, el nombre de su ranking.
        private System.Collections.IEnumerator SyncProfile()
        {
            if (LocalProfile && KovusPride.Publishing.StorePlatform.HasInGameLeaderboard)
            {
                float t = 0f;
                while (!KovusPride.Publishing.StorePlatform.Ready && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
                KovusPride.Publishing.StorePlatform.Call("lb.entries", "{\"board\":\"best\"}", (ok, json) =>
                {
                    if (!ok) return;
                    PlatformLb lb = null;
                    try { lb = JsonUtility.FromJson<PlatformLb>(json); } catch (System.Exception) { }
                    if (lb == null || string.IsNullOrEmpty(lb.me)) return;
                    foreach (var e in lb.entries) if (e.id == lb.me) RememberPlatformName(e.name);
                });
            }
            if (!OnlineEnabled) yield break;
            EnsureIdentity();
            ProfileResponse got = null;
            yield return OnlineApi.GetProfile(cfg, save.playerId, r => got = r);
            if (got == null || !got.ok) yield break;
            UpdateAwards(got.awards);
            if (got.look != CurrentLook && (save.emblem != Emblems.DefaultEmblem || save.nameColor != Emblems.DefaultNameColor || !string.IsNullOrEmpty(got.look)))
                PushLook();
        }
    }
}
