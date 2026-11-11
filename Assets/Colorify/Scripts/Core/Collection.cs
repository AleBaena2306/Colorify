using System.Collections.Generic;
using UnityEngine;

namespace Colorify
{
    // Metaprogresión (11/11/2026): los puntos de todas las partidas de Normal se acumulan y van desbloqueando colores
    // nuevos, más raros (salen en los tableros de Normal en solitario; nunca en duelos ni carreras, que tienen que ser
    // iguales para todos) y temas de la interfaz, siempre mate: combinaciones de fondo, panel, tinta y acentos.
    // Los colores no dan más puntos: la puntuación del modo Normal no depende del color.
    public static class Collection
    {
        public class Theme
        {
            public string id, name;
            public Color paper, paperDark, ink, inkSoft, danger;
            public Color[] title;   // 6 acentos, de frío a cálido (botones y títulos arcoíris)

            public Theme(string id, string name, string paper, string paperDark, string ink, string inkSoft, string danger, params string[] title)
            {
                this.id = id; this.name = name;
                this.paper = Hex(paper); this.paperDark = Hex(paperDark); this.ink = Hex(ink); this.inkSoft = Hex(inkSoft); this.danger = Hex(danger);
                this.title = new Color[title.Length];
                for (int i = 0; i < title.Length; i++) this.title[i] = Hex(title[i]);
            }
        }

        public enum Kind { Color, Theme, Emblem, NameColor }

        public class Item
        {
            public string id, name;
            public Kind kind;
            public long cost;   // puntos acumulados para desbloquearlo
            public ColorifyConfig.ColorFamily family;   // si es un color
            public Theme theme;                         // si es un tema
            public Emblems.Emblem emblem;               // si es un emblema
            public Emblems.NameColor nameColor;         // si es un color de nombre
        }

        public static readonly Theme DefaultTheme = new Theme("paper", "Paper", "F3EEE6", "E6DFD3", "3B3836", "8A847D", "D96C5F",
            "6F9BD8", "5FB8AE", "86B86C", "E6C15A", "E8955B", "D96C5F");

        public static readonly Theme[] Themes =
        {
            DefaultTheme,
            new Theme("sage", "Sage", "EEF1E8", "DCE3D3", "34403A", "7E8A80", "C9705F", "7FA38A", "5FA8A0", "A3B86C", "D9C36A", "D99A6C", "C9705F"),
            new Theme("blush", "Blush", "F6ECE9", "EBDAD5", "4A3838", "948480", "C8605A", "D98C9A", "C79BC4", "8FA6D6", "E6B36A", "E38D6B", "C8605A"),
            new Theme("mist", "Mist", "EAF0F4", "D6E1E9", "2F3A45", "7D8894", "D0675E", "6E95C8", "6DB3C6", "8FB39A", "D7C277", "DB9672", "D0675E"),
            new Theme("lavender", "Lavender", "F0ECF4", "DFD8EA", "3A3448", "8A8396", "CD6A6A", "9A8BD0", "C08BC4", "7FA3D6", "E3BE73", "E39A7A", "CD6A6A"),
            new Theme("sand", "Sand", "F3EADB", "E5D6BE", "4A3F30", "958A79", "C7684F", "7C9DB8", "74AE9E", "9AB16E", "DDB24F", "D98A4F", "C7684F"),
            new Theme("clay", "Clay", "F1E6E0", "E2CFC5", "45342E", "927D74", "BB5B4A", "8A9FB5", "7DA89A", "A2A866", "D9A95A", "C97E55", "BB5B4A"),
        };

        private static ColorifyConfig.ColorFamily Rare(string id, string name, float hue, float l, float c) =>
            new ColorifyConfig.ColorFamily(id, name, hue, l, c, 10, 5f);   // poco frecuentes: peso 5 (azul 30, rojo 6)

        // Escalera de desbloqueos, colores y temas intercalados (puntos acumulados en Normal)
        // Escalera de desbloqueos (puntos acumulados en Normal): temas, colores, emblemas y colores de nombre
        // intercalados para que siempre haya algo cerca. El primero llega en la primera partida (que es corta).
        public static readonly Item[] Ladder =
        {
            ThemeItem(600, "sage"),
            EmblemItem(1500, "drop"),
            ColorItem(3000, Rare("lavender", "Lavender", 300f, 0.72f, 0.10f)),
            NameItem(4500, "teal"),
            ColorItem(7000, Rare("rose", "Rose", 355f, 0.74f, 0.10f)),
            EmblemItem(9000, "gem"),
            ThemeItem(12000, "blush"),
            NameItem(16000, "coral"),
            ColorItem(20000, Rare("mint", "Mint", 170f, 0.85f, 0.08f)),
            EmblemItem(25000, "peak"),
            ColorItem(30000, Rare("sky", "Sky", 230f, 0.80f, 0.08f)),
            NameItem(37000, "amber"),
            ThemeItem(45000, "mist"),
            EmblemItem(52000, "eye"),
            ColorItem(60000, Rare("sand", "Sand", 80f, 0.82f, 0.06f)),
            NameItem(70000, "rose"),
            ColorItem(80000, Rare("lime", "Lime", 122f, 0.84f, 0.12f)),
            EmblemItem(90000, "prism"),
            ThemeItem(100000, "lavender"),
            NameItem(112000, "lavender"),
            ColorItem(125000, Rare("terracotta", "Terracotta", 40f, 0.60f, 0.10f)),
            EmblemItem(137000, "block"),
            ColorItem(150000, Rare("plum", "Plum", 325f, 0.60f, 0.11f)),
            NameItem(165000, "sky"),
            ThemeItem(180000, "sand"),
            EmblemItem(200000, "orbit"),
            ColorItem(215000, Rare("indigo", "Indigo", 275f, 0.58f, 0.11f)),
            NameItem(235000, "sage"),
            ColorItem(255000, Rare("slate", "Slate", 250f, 0.62f, 0.04f)),
            EmblemItem(280000, "spectrum"),
            ThemeItem(300000, "clay"),
            NameItem(350000, "rainbow"),
            EmblemItem(400000, "crown"),
        };

        private static Item EmblemItem(long cost, string id)
        {
            var e = Emblems.Get(id);
            return new Item { id = "emblem." + id, name = e.name, kind = Kind.Emblem, cost = cost, emblem = e };
        }

        private static Item NameItem(long cost, string id)
        {
            var c = Emblems.GetNameColor(id);
            return new Item { id = "name." + id, name = c.name, kind = Kind.NameColor, cost = cost, nameColor = c };
        }

        // ¿Puede usarlo? Los de por defecto siempre; los de temporada, si el servidor los ha concedido
        public static bool EmblemUnlocked(string id, long lifetime, ICollection<string> awards)
        {
            if (id == Emblems.DefaultEmblem) return true;
            int season, place;
            if (Emblems.TryParseSeason(id, out season, out place)) return awards != null && awards.Contains(id);
            foreach (var it in Ladder) if (it.kind == Kind.Emblem && it.emblem.id == id) return lifetime >= it.cost;
            return false;
        }

        public static bool NameColorUnlocked(string id, long lifetime)
        {
            if (id == Emblems.DefaultNameColor) return true;
            foreach (var it in Ladder) if (it.kind == Kind.NameColor && it.nameColor.id == id) return lifetime >= it.cost;
            return false;
        }

        public static long CostOf(Kind kind, string id)
        {
            foreach (var it in Ladder)
                if (it.kind == kind && ((kind == Kind.Emblem && it.emblem.id == id) || (kind == Kind.NameColor && it.nameColor.id == id))) return it.cost;
            return 0;
        }

        private static Item ColorItem(long cost, ColorifyConfig.ColorFamily f) =>
            new Item { id = "color." + f.id, name = f.displayName, kind = Kind.Color, cost = cost, family = f };

        private static Item ThemeItem(long cost, string themeId)
        {
            var t = ThemeById(themeId);
            return new Item { id = "theme." + t.id, name = t.name, kind = Kind.Theme, cost = cost, theme = t };
        }

        public static Theme ThemeById(string id)
        {
            foreach (var t in Themes) if (t.id == id) return t;
            return DefaultTheme;
        }

        public static bool IsUnlocked(Item item, long lifetime) => lifetime >= item.cost;

        public static bool ThemeUnlocked(string themeId, long lifetime)
        {
            if (themeId == DefaultTheme.id) return true;
            foreach (var it in Ladder) if (it.kind == Kind.Theme && it.theme.id == themeId) return lifetime >= it.cost;
            return false;
        }

        public static int UnlockedCount(long lifetime)
        {
            int n = 0;
            foreach (var it in Ladder) if (lifetime >= it.cost) n++;
            return n;
        }

        public static List<ColorifyConfig.ColorFamily> UnlockedColors(long lifetime)
        {
            var list = new List<ColorifyConfig.ColorFamily>();
            foreach (var it in Ladder) if (it.kind == Kind.Color && lifetime >= it.cost) list.Add(it.family);
            return list;
        }

        // Siguiente desbloqueo (null si ya están todos)
        public static Item Next(long lifetime)
        {
            foreach (var it in Ladder) if (lifetime < it.cost) return it;
            return null;
        }

        // Lo desbloqueado entre dos cantidades de puntos acumulados (para celebrarlo al acabar la partida)
        public static List<Item> UnlockedBetween(long before, long after)
        {
            var list = new List<Item>();
            foreach (var it in Ladder) if (before < it.cost && after >= it.cost) list.Add(it);
            return list;
        }

        public static string KindLabel(Item it) =>
            it.kind == Kind.Theme ? "THEME" : it.kind == Kind.Emblem ? "EMBLEM" : it.kind == Kind.NameColor ? "NAME COLOR" : "COLOR";

        public static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString("#" + hex, out c) ? c : Color.magenta;
        }
    }
}
