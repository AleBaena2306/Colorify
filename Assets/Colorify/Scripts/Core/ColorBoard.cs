using System.Collections.Generic;
using UnityEngine;

namespace Colorify
{
    // Una cuadrícula: el color objetivo, la casilla correcta (copia exacta del objetivo) y los distractores.
    public class ColorBoard
    {
        public int size;
        public int correctIndex;
        public int targetFamily;
        public OkColor target;
        public readonly List<OkColor> tiles = new List<OkColor>();

        public Color TargetColor => target.ToColor();
    }

    public static class ColorBoardGenerator
    {
        public static ColorBoard Generate(RunState run, System.Random rng) =>
            Generate(run.cfg, run.GridSize, run.ColorDifficulty, run.FamilyWeight, rng, run.cfg.Similarity(run.stage));

        // Tablero gris (antes de que empiece la ronda): muestra y casillas en distintos tonos de gris
        public static ColorBoard Grey(int size, System.Random rng)
        {
            var b = new ColorBoard { size = size, correctIndex = -1, targetFamily = -1 };
            b.target = new OkColor(0.62f + (float)rng.NextDouble() * 0.2f, 0f, 0f);
            for (int i = 0; i < size * size; i++) b.tiles.Add(new OkColor(0.55f + (float)rng.NextDouble() * 0.37f, 0f, 0f));
            return b;
        }

        // difficulty 0..1. familyWeight(f) = peso de aparición de cada familia (con las cartas aplicadas).
        // similarity: ajuste del plazo (1 = base; 2 = distractores el doble de parecidos)
        public static ColorBoard Generate(ColorifyConfig cfg, int size, float difficulty,
            System.Func<int, float> familyWeight, System.Random rng, float similarity = 1f)
        {
            similarity = Mathf.Max(0.05f, similarity);
            float minSep = Mathf.Max(ColorifyConfig.MinSeparationFloor, Mathf.Lerp(cfg.minSeparationEasy, cfg.minSeparationHard, difficulty) / similarity);
            // Más parecido también significa más distractores del mismo tono que el objetivo
            float sameFamily = Mathf.Clamp01(1f - (1f - Mathf.Lerp(cfg.sameFamilyEasy, cfg.sameFamilyHard, difficulty)) / similarity);
            return GenerateWith(cfg, size, minSep, sameFamily, familyWeight, rng);
        }

        // minSep: distancia OKLab mínima entre el objetivo y cualquier distractor.
        // sameFamily: probabilidad de que un distractor sea un tono parecido al objetivo (1 = todos lo son:
        // el tablero entero es de la misma familia de color y hay que diferenciar matices, no buscar uno distinto)
        public static ColorBoard GenerateWith(ColorifyConfig cfg, int size, float minSep, float sameFamily,
            System.Func<int, float> familyWeight, System.Random rng)
        {
            var board = new ColorBoard { size = size };
            int count = size * size;
            minSep = Mathf.Max(ColorifyConfig.MinSeparationFloor, minSep);

            board.targetFamily = PickFamily(cfg, familyWeight, rng);
            board.target = Shade(cfg, board.targetFamily, rng);
            board.correctIndex = rng.Next(count);

            var placed = new List<OkColor> { board.target };
            for (int i = 0; i < count; i++)
            {
                if (i == board.correctIndex) { board.tiles.Add(board.target); continue; }

                OkColor c = default;
                bool ok = false;
                for (int attempt = 0; attempt < 40 && !ok; attempt++)
                {
                    // Entre distractores basta con que se distingan (no marcan la dificultad); si cuesta, se relaja
                    float pairMin = attempt < 20 ? Mathf.Min(minSep * 0.8f, 0.03f) : Mathf.Min(minSep * 0.5f, 0.015f);
                    c = rng.NextDouble() < sameFamily
                        ? Near(cfg, board.target, minSep, rng)
                        : Shade(cfg, OtherFamily(cfg, board.targetFamily, rng), rng);
                    ok = OkColor.Distance(c, board.target) >= minSep && FarFromAll(c, placed, pairMin);
                }
                if (!ok)
                {
                    // Sin hueco: si el tablero es de una sola familia, se acepta un matiz parecido aunque se acerque
                    // a otro distractor (nunca al objetivo); si no, un color de otra familia
                    if (sameFamily >= 0.99f)
                        for (int k = 0; k < 20 && OkColor.Distance(c = Near(cfg, board.target, minSep, rng), board.target) < minSep; k++) { }
                    else c = Shade(cfg, OtherFamily(cfg, board.targetFamily, rng), rng);
                }
                placed.Add(c);
                board.tiles.Add(c);
            }
            return board;
        }

        public static int PickFamily(ColorifyConfig cfg, System.Func<int, float> weight, System.Random rng)
        {
            float total = 0f;
            for (int f = 0; f < cfg.families.Length; f++) total += Mathf.Max(0f, weight(f));
            double r = rng.NextDouble() * total;
            for (int f = 0; f < cfg.families.Length; f++)
            {
                r -= Mathf.Max(0f, weight(f));
                if (r <= 0) return f;
            }
            return cfg.families.Length - 1;
        }

        public static OkColor Shade(ColorifyConfig cfg, int family, System.Random rng)
        {
            var fam = cfg.families[family];
            float l = fam.lightness + Range(rng, -cfg.shadeLightnessJitter, cfg.shadeLightnessJitter);
            float c = Mathf.Max(0.02f, fam.chroma + Range(rng, -cfg.shadeChromaJitter, cfg.shadeChromaJitter));
            float h = fam.hue + Range(rng, -cfg.shadeHueJitter, cfg.shadeHueJitter);
            // Pasar por sRGB y volver deja el color dentro de la gama que se puede mostrar
            return OkColor.FromColor(OkColor.FromLch(Mathf.Clamp(l, 0.35f, 0.95f), c, h).ToColor());
        }

        // Un matiz del mismo color que el objetivo, a una distancia OKLab entre minSep y 1,7·minSep.
        // La diferencia va sobre todo en luminosidad (más claro o más oscuro) y algo en croma y tono, sin salir
        // de la paleta mate: así el tablero se lee como tonos de un mismo color, no como colores distintos.
        // La parte en luminosidad (cfg.lightnessShare como mínimo) también ayuda a jugadores daltónicos.
        private static OkColor Near(ColorifyConfig cfg, OkColor target, float minSep, System.Random rng)
        {
            float L = target.L, C = target.Chroma, hDeg = target.HueDeg;
            OkColor c = target;
            for (int tries = 0; tries < 12; tries++)
            {
                float dist = minSep * Range(rng, 1.0f, 1.5f);
                // Reparto: casi todo en luminosidad, un poco en croma y como mucho ±6° de tono.
                float share = Range(rng, Mathf.Max(0.7f, cfg.lightnessShare), 0.95f);
                float dL = (rng.NextDouble() < 0.5 ? -1f : 1f) * dist * share;
                if (L + dL < 0.34f || L + dL > 0.93f) dL = -dL;
                float dC = (rng.NextDouble() < 0.5 ? -1f : 1f) * dist * Mathf.Sqrt(Mathf.Max(0f, 1f - share * share));
                float l2 = Mathf.Clamp(L + dL, 0.32f, 0.94f);
                float c2 = Mathf.Clamp(C + dC, Mathf.Min(C, 0.04f), C + 0.03f);
                float h2 = hDeg + Range(rng, -6f, 6f);
                c = OkColor.FromColor(OkColor.FromLch(l2, c2, h2).ToColor());
                if (OkColor.Distance(c, target) >= minSep) return c;
            }
            return c;
        }

        private static int OtherFamily(ColorifyConfig cfg, int exclude, System.Random rng)
        {
            if (cfg.families.Length < 2) return exclude;
            int f = rng.Next(cfg.families.Length - 1);
            return f >= exclude ? f + 1 : f;
        }

        private static bool FarFromAll(OkColor c, List<OkColor> others, float minDist)
        {
            foreach (var o in others) if (OkColor.Distance(c, o) < minDist) return false;
            return true;
        }

        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
