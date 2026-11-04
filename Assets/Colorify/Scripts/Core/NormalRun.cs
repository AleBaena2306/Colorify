using UnityEngine;

namespace Colorify
{
    // Modo Normal: sin rondas ni cartas. Cada acierto suma tiempo y cada fallo lo resta; la partida acaba
    // al quedarse sin tiempo. La puntuación es el número de aciertos. La dificultad sube con los aciertos:
    // la cuadrícula crece, los colores se parecen más y cada acierto da menos tiempo.
    public class NormalRun
    {
        public readonly ColorifyConfig cfg;
        public int hits, misses, streak, bestStreak;
        public bool continueUsed;

        public NormalRun(ColorifyConfig cfg) { this.cfg = cfg; }

        // Variante del tablero: normal, muestra con franjas blancas o muestra que solo se ve unos segundos
        public enum Variant { Plain = 0, Stripes = 1, Flash = 2 }

        public int GridSize { get { int size; Variant v; Phase(cfg, hits, out size, out v); return size; } }
        public Variant BoardVariant { get { int size; Variant v; Phase(cfg, hits, out size, out v); return v; } }

        // Recorrido por tamaños: N aciertos normales (normalHitsPerGridStep), luego k con rayas y k de memoria,
        // y se pasa al tamaño siguiente; k empieza en normalVariantRoundsFirst y sube normalVariantRoundsPerGrid
        // por tamaño. En el tamaño máximo el ciclo se repite. (El bot del servidor usa la misma cuenta: bot.js)
        public static void Phase(ColorifyConfig cfg, int hits, out int size, out Variant variant)
        {
            int step = Mathf.Max(1, cfg.normalHitsPerGridStep);
            int start = Mathf.Max(1, cfg.startGridSize), max = Mathf.Max(start, cfg.maxGridSize);
            int h = Mathf.Max(0, hits);
            for (int s = start; ; s = Mathf.Min(s + 1, max))
            {
                int k = cfg.normalVariants ? Mathf.Max(0, cfg.normalVariantRoundsFirst + cfg.normalVariantRoundsPerGrid * (s - start)) : 0;
                int cycle = step + 2 * k;
                if (s == max) h %= cycle;   // en el tamaño máximo, el ciclo se repite
                if (h < cycle)
                {
                    size = s;
                    variant = h < step ? Variant.Plain : h < step + k ? Variant.Stripes : Variant.Flash;
                    return;
                }
                h -= cycle;
            }
        }

        // Código para las estadísticas del bot: tamaño + 10 × variante
        public int StatsCode => GridSize + 10 * (int)BoardVariant;
        public float ColorDifficulty => Mathf.Clamp01(hits / (float)Mathf.Max(1, cfg.normalHitsToMaxColorDifficulty));
        public float HitBonus => Mathf.Max(cfg.normalHitBonusMin, cfg.normalHitBonus - cfg.normalBonusDecayPerHit * hits);
        public float MissPenalty => cfg.normalMissPenalty;
        public float MaxTime => cfg.normalMaxTime > 0 ? cfg.normalMaxTime : float.MaxValue;
        // Separación mínima de color según los aciertos (cuanto más avanzas, más se parecen los matices)
        public float MinSeparation => Mathf.Lerp(cfg.normalSeparationEasy, cfg.normalSeparationHard, ColorDifficulty) / Mathf.Max(0.05f, cfg.normalSimilarity);
    }
}
