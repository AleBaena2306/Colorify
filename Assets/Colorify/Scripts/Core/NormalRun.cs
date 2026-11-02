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

        public int GridSize => Mathf.Clamp(cfg.startGridSize + hits / Mathf.Max(1, cfg.normalHitsPerGridStep), 1, cfg.maxGridSize);
        public float ColorDifficulty => Mathf.Clamp01(hits / (float)Mathf.Max(1, cfg.normalHitsToMaxColorDifficulty));
        public float HitBonus => Mathf.Max(cfg.normalHitBonusMin, cfg.normalHitBonus - cfg.normalBonusDecayPerHit * hits);
        public float MissPenalty => cfg.normalMissPenalty;
        public float MaxTime => cfg.normalMaxTime > 0 ? cfg.normalMaxTime : float.MaxValue;
        // Separación mínima de color según los aciertos (cuanto más avanzas, más se parecen los matices)
        public float MinSeparation => Mathf.Lerp(cfg.normalSeparationEasy, cfg.normalSeparationHard, ColorDifficulty) / Mathf.Max(0.05f, cfg.normalSimilarity);
    }
}
