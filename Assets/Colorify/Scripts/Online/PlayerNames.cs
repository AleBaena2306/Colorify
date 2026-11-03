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

        // Id anónimo del jugador (32 caracteres hexadecimales). No identifica a la persona: solo agrupa sus puntuaciones.
        public static string NewPlayerId() => Guid.NewGuid().ToString("N");
    }
}
