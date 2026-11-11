using System.Collections.Generic;

namespace Colorify
{
    // Personalización del jugador (11/11/2026): emblema y color del nombre, visibles en el ranking.
    //  · Emblemas y colores de nombre: se desbloquean con puntos acumulados (escalera de Collection).
    //  · Emblemas de temporada: oro, plata y bronce para los 3 primeros del ranking mensual (modo Normal). Solo los
    //    concede el servidor («s1g» = temporada I, oro). Cada temporada tiene su motivo (forma) y su color.
    // Los colores son fijos (no dependen del tema), para que todos vean el mismo emblema en el ranking.
    public static class Emblems
    {
        public class Emblem
        {
            public string id, name;
            public Shape outer;
            public string outerHex;
            public bool hasInner;
            public Shape inner;
            public string innerHex;
            public float innerScale = 0.44f;
            public bool rainbow;        // cuatro puntos de colores en el centro
            public string glyph;        // texto encima (numeral de temporada)
            public string glyphHex;
        }

        public class NameColor { public string id, name, hex; public bool rainbow; }

        public const string DefaultEmblem = "dot", DefaultNameColor = "ink";
        public const string Paper = "F3EEE6", Ink = "3B3836";

        private static Emblem E(string id, string name, Shape outer, string outerHex, Shape? inner = null, string innerHex = Paper, float innerScale = 0.44f, bool rainbow = false) =>
            new Emblem { id = id, name = name, outer = outer, outerHex = outerHex, hasInner = inner.HasValue, inner = inner ?? Shape.Circle, innerHex = innerHex, innerScale = innerScale, rainbow = rainbow };

        public static readonly Emblem[] All =
        {
            E("dot", "Dot", Shape.Circle, "A9A39B"),
            E("drop", "Drop", Shape.Circle, "5FB8AE", Shape.Circle),
            E("gem", "Gem", Shape.Diamond, "6F9BD8", Shape.Diamond),
            E("peak", "Peak", Shape.Triangle, "E8955B", Shape.Triangle, Paper, 0.42f),
            E("eye", "Eye", Shape.Circle, "9A8BD0", Shape.Circle, Ink, 0.40f),
            E("prism", "Prism", Shape.Triangle, "86B86C", Shape.Diamond, Paper, 0.36f),
            E("block", "Block", Shape.Square, "D96C5F", Shape.Circle, Paper, 0.46f),
            E("orbit", "Orbit", Shape.Circle, "E6C15A", Shape.Diamond, Ink, 0.40f),
            E("spectrum", "Spectrum", Shape.Circle, Ink, null, Paper, 0.44f, rainbow: true),
            E("crown", "Crown", Shape.Diamond, Ink, null, Paper, 0.44f, rainbow: true),
        };

        public static readonly NameColor[] NameColors =
        {
            new NameColor { id = "ink", name = "Ink", hex = Ink },
            new NameColor { id = "teal", name = "Teal", hex = "3F9C93" },
            new NameColor { id = "coral", name = "Coral", hex = "D9774A" },
            new NameColor { id = "amber", name = "Amber", hex = "C99A2E" },
            new NameColor { id = "rose", name = "Rose", hex = "C9667A" },
            new NameColor { id = "lavender", name = "Lavender", hex = "8573C2" },
            new NameColor { id = "sky", name = "Sky", hex = "4F86C6" },
            new NameColor { id = "sage", name = "Sage", hex = "5E8F4E" },
            new NameColor { id = "rainbow", name = "Rainbow", hex = Ink, rainbow = true },
        };

        // ---------------- Temporadas ----------------

        // Temporada = mes natural (UTC); la I es noviembre de 2026 (igual que el servidor)
        public static int SeasonOf(System.DateTime utc) => (utc.Year - 2026) * 12 + (utc.Month - 11) + 1;
        public static int CurrentSeason => SeasonOf(System.DateTime.UtcNow);

        private static readonly Shape[] SeasonMotif = { Shape.Diamond, Shape.Triangle, Shape.Circle, Shape.Square };
        private static readonly string[] SeasonAccent = { "E8955B", "6F9BD8", "86B86C", "D96C5F", "9A8BD0", "5FB8AE" };
        private static readonly string[] MetalHex = { "D4AF4A", "A9B0B8", "C08457" };   // oro, plata, bronce
        private static readonly string[] PlaceName = { "Gold", "Silver", "Bronze" };

        public static string SeasonId(int season, int place) => "s" + season + (place == 1 ? "g" : place == 2 ? "s" : "b");

        public static bool TryParseSeason(string id, out int season, out int place)
        {
            season = place = 0;
            if (string.IsNullOrEmpty(id) || id.Length < 3 || id[0] != 's') return false;
            char p = id[id.Length - 1];
            place = p == 'g' ? 1 : p == 's' ? 2 : p == 'b' ? 3 : 0;
            return place > 0 && int.TryParse(id.Substring(1, id.Length - 2), out season) && season > 0;
        }

        public static Emblem SeasonEmblem(int season, int place)
        {
            int i = (season - 1) % SeasonMotif.Length;
            return new Emblem
            {
                id = SeasonId(season, place),
                name = $"Season {Roman(season)} · {PlaceName[place - 1]}",
                outer = Shape.Circle, outerHex = MetalHex[place - 1],
                hasInner = true, inner = SeasonMotif[i], innerHex = SeasonAccent[(season - 1) % SeasonAccent.Length], innerScale = 0.62f,
                glyph = Roman(season), glyphHex = Paper,
            };
        }

        public static Emblem Get(string id)
        {
            int season, place;
            if (TryParseSeason(id, out season, out place)) return SeasonEmblem(season, place);
            foreach (var e in All) if (e.id == id) return e;
            return All[0];
        }

        public static NameColor GetNameColor(string id)
        {
            foreach (var c in NameColors) if (c.id == id) return c;
            return NameColors[0];
        }

        public static string Roman(int n)
        {
            if (n <= 0) return "";
            int[] v = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] r = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < v.Length; i++) while (n >= v[i]) { sb.Append(r[i]); n -= v[i]; }
            return sb.ToString();
        }

        // ---------------- Aspecto («look») ----------------

        public static string Look(string emblem, string nameColor) => $"e:{emblem};c:{nameColor}";

        public static void ParseLook(string look, out string emblem, out string nameColor)
        {
            emblem = DefaultEmblem; nameColor = DefaultNameColor;
            if (string.IsNullOrEmpty(look)) return;
            foreach (var part in look.Split(';'))
            {
                int k = part.IndexOf(':');
                if (k <= 0) continue;
                string key = part.Substring(0, k), val = part.Substring(k + 1);
                if (key == "e" && val.Length > 0) emblem = val;
                if (key == "c" && val.Length > 0) nameColor = val;
            }
        }
    }
}
