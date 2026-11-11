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
        public int continuesUsed;   // normalmente 1 como mucho; 2 si el jugador acepta casi siempre (RunDirector)
        // Reglas nuevas (modo Normal): calentamiento, cuadrícula hasta 5×5, formas y efectos especiales y puntos.
        // El duelo 1v1 las tiene desactivadas y sigue con las de siempre (aciertos y rayas/memoria por tamaño).
        public readonly bool specials;
        public long points;
        public int chain = 1, bestChain = 1;
        public BoardKind kind = BoardKind.Plain;

        public NormalRun(ColorifyConfig cfg, bool specials = false, float paceScale = 1f)
        {
            this.cfg = cfg;
            this.specials = specials;
            pace = (specials ? Mathf.Clamp(Tuning.Current.pace, 0.5f, 4f) : 1f) * Mathf.Clamp(paceScale, 0.2f, 2f);
        }

        // Variante del tablero: normal, muestra con franjas blancas o muestra que solo se ve unos segundos
        public enum Variant { Plain = 0, Stripes = 1, Flash = 2 }

        public int GridSize { get { if (specials) return Progression.GridSize(cfg, hits); int size; Variant v; Phase(cfg, hits, out size, out v); return size; } }
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
        // Con las reglas nuevas, el servidor ajusta el ritmo (Tuning.pace) para que una partida dure unos 5 minutos.
        // Multiplica todo el tiempo de la partida: el inicial, el máximo (y con él el mínimo que deja cada acierto)
        // y el que da cada acierto. Se fija al empezar, para que no cambie a mitad de partida.
        private readonly float pace = 1f;
        public float Pace => pace;
        public float HitBonus => Mathf.Max(cfg.normalHitBonusMin, cfg.normalHitBonus - cfg.normalBonusDecayPerHit * hits) * pace;
        public float MissPenalty => cfg.normalMissPenalty;
        public float MaxTime => cfg.normalMaxTime > 0 ? cfg.normalMaxTime * pace : float.MaxValue;
        public float StartTime => cfg.normalStartTime * pace;
        // Separación mínima de color según los aciertos (cuanto más avanzas, más se parecen los matices)
        public float MinSeparation => Mathf.Lerp(cfg.normalSeparationEasy, cfg.normalSeparationHard, ColorDifficulty) / Mathf.Max(0.05f, cfg.normalSimilarity);
    }
}
