using System;

namespace Colorify.Online
{
    // Nombres generados («Teal Fox 42»): el juego es 13+ y no hay texto libre. El servidor solo acepta
    // nombres de estas listas; si se cambian, hay que cambiar también worker/colorify/names.js del portfolio.
    public static class PlayerNames
    {
        public static readonly string[] Colors =
        {
            "Amber", "Azure", "Coral", "Cobalt", "Copper", "Cream", "Cyan", "Denim", "Fern", "Ginger",
            "Honey", "Indigo", "Ivory", "Jade", "Khaki", "Lemon", "Lilac", "Lime", "Maple", "Mauve",
            "Mint", "Moss", "Ochre", "Olive", "Peach", "Pearl", "Pine", "Plum", "Rose", "Ruby",
            "Rust", "Saffron", "Sage", "Sand", "Sky", "Slate", "Teal", "Topaz", "Violet", "Wheat",
        };

        public static readonly string[] Animals =
        {
            "Badger", "Beaver", "Bison", "Crane", "Deer", "Dingo", "Falcon", "Ferret", "Finch", "Fox",
            "Frog", "Gazelle", "Gecko", "Hare", "Heron", "Ibis", "Koala", "Kiwi", "Lemur", "Lynx",
            "Marten", "Moose", "Newt", "Ocelot", "Otter", "Owl", "Panda", "Puffin", "Quail", "Raven",
            "Robin", "Seal", "Swan", "Tapir", "Toucan", "Turtle", "Walrus", "Wolf", "Wombat", "Yak",
        };

        public static string Random(System.Random rng)
        {
            return Colors[rng.Next(Colors.Length)] + " " + Animals[rng.Next(Animals.Length)] + " " + rng.Next(10, 100);
        }

        public static bool IsValid(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var parts = name.Split(' ');
            if (parts.Length != 3 || parts[2].Length != 2 || parts[2][0] == '0') return false;
            int n;
            return Array.IndexOf(Colors, parts[0]) >= 0 && Array.IndexOf(Animals, parts[1]) >= 0 && int.TryParse(parts[2], out n);
        }

        // Nombres libres: 3-16 caracteres (letras, números, espacio, _ . -) y sin palabras prohibidas.
        // El servidor (worker/colorify/names.js) aplica las mismas reglas y además comprueba las reservas.
        private static readonly string[] Blocked =
        {
            "fuck", "fuk", "fck", "fack", "shit", "bitch", "cunt", "dick", "pussy", "nigg", "fag", "whore", "slut", "rapist",
            "nazi", "hitler", "porn", "penis", "vagina", "retard", "kkk", "sex", "cock", "rape",
            "puta", "puto", "mierda", "polla", "zorra", "maricon", "gilipolla", "cabron", "follar",
            "joder", "verga", "pendej", "chinga", "hijoputa", "subnormal", "retrasad", "culo",
            "admin", "moderador", "moderator", "kovuspride", "kovu", "official", "oficial", "colorify", "claude", "anthropic",
        };

        public static string Clean(string s)
        {
            s = (s ?? "").Trim();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s;
        }

        // null si el nombre vale; si no, el motivo para enseñarlo al jugador
        public static string Problem(string name)
        {
            if (IsValid(name)) return null;
            if (name.Length < 3) return "At least 3 characters.";
            if (name.Length > 16) return "16 characters at most.";
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '_' && c != '.' && c != '-') return "Only letters, numbers, spaces and _ . -";
            string key = Key(name, true);
            if (Key(name, false).Length < 3) return "Use at least 3 letters or numbers.";
            foreach (var b in Blocked) if (key.Contains(b)) return "That name isn't allowed. Try another one.";
            return null;
        }

        // Minúsculas, sin acentos ni símbolos (l33t → letras si leet)
        private static string Key(string s, bool leet)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char raw in s.Normalize(System.Text.NormalizationForm.FormD).ToLowerInvariant())
            {
                char c = raw;
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (leet)
                    switch (c) { case '0': c = 'o'; break; case '1': c = 'i'; break; case '3': c = 'e'; break; case '4': c = 'a'; break; case '5': c = 's'; break; case '7': c = 't'; break; case '@': c = 'a'; break; case '$': c = 's'; break; }
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
            }
            return sb.ToString();
        }

        // Id anónimo del jugador (32 caracteres hexadecimales). No identifica a la persona: solo agrupa sus puntuaciones.
        public static string NewPlayerId() => Guid.NewGuid().ToString("N");
    }
}
