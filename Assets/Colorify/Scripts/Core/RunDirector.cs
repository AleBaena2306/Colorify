using System.Collections.Generic;
using UnityEngine;

namespace Colorify
{
    // Director de partida del modo Normal en solitario (nunca en duelos ni carreras, que comparten tablero y ranking
    // en vivo). Decisión de diseño de Alejandro (11/11/2026), inspirada en lo que hace Block Blast: el jugador debe
    // superar su récord por poco y perder poco después, para que vuelva a jugar. El jugador no ve nada de esto.
    //  · Recta final: entre el 85 % y el 100 % del récord, tableros más fáciles (colores más separados, sin efectos
    //    difíciles) para que lo supere.
    //  · Remate: al pasar el récord, unos tableros después llega una racha de tableros «letales» (colores casi
    //    iguales y el efecto más difícil desbloqueado). Igual tras un combo grande, cuando la partida ya dura lo de
    //    siempre. Pasado el récord en un 10 %, cada tablero tiene un 20 % de salir letal. Y si cerca del récord el
    //    jugador va muy rápido (los últimos 6 aciertos en menos del 75 % del tiempo esperado), también.
    //  · Puntos a la baja: desde el 90 % del récord, algunos aciertos valen un 60-85 % de lo normal.
    //  · Tras continuar con anuncio, un bonus de puntos del 4-10 % del récord (hace atractivo el «continuar»).
    //  · Jugadores nuevos (menos de 3 partidas o sin récord): solo la recta final; ni remates ni puntos a la baja.
    public class RunDirector
    {
        public enum Mood { Neutral, Ease, Lethal }

        public struct Board
        {
            public Mood mood;
            public float separationScale;   // multiplica la separación mínima de color (>1 más fácil, <1 más difícil)
            public bool Manipulated => mood != Mood.Neutral;
        }

        public const int NewPlayerRuns = 3;
        public const float EaseFrom = 0.85f;            // fracción del récord donde empieza la recta final
        public const float CutPointsFrom = 0.90f;
        public const float MarginBreak = 1.10f;
        public const float MarginBreakChance = 0.20f;
        public const float FastFrom = 0.93f;
        public const float FastRatio = 0.75f;
        public const int LethalStreak = 4;               // tableros letales seguidos cuando llega el remate
        public const float EaseSeparation = 1.7f;
        public const float LethalSeparation = 0.45f;

        private readonly long best;
        private readonly bool newPlayer;
        private readonly float typicalSeconds;
        private readonly System.Random rng;
        private readonly Queue<float> recent = new Queue<float>();   // tiempo de reacción / tiempo esperado
        private int killIn = -1;          // tableros hasta el remate programado (-1 = ninguno)
        private int lethalLeft;           // tableros letales que quedan de la racha en curso
        private bool passedRecord, bigChainSeen;

        public int EaseBoards, LethalBoards, CutHits;   // para la analítica
        public long CutPoints;

        // typicalSeconds: duración habitual de las partidas de este jugador (mediana de las últimas); 0 = sin datos
        public RunDirector(long best, int runsPlayed, float typicalSeconds, System.Random rng)
        {
            this.best = best;
            this.typicalSeconds = typicalSeconds;
            this.rng = rng;
            newPlayer = best <= 0 || runsPlayed < NewPlayerRuns;
        }

        private float Ratio(long points) => best > 0 ? points / (float)best : 0f;

        // Tras cada acierto: reacción frente a lo esperado y combo actual
        public void OnHit(float reactionMs, float expectedMs, int chain, int chainMax, long points)
        {
            recent.Enqueue(reactionMs / Mathf.Max(1f, expectedMs));
            while (recent.Count > 6) recent.Dequeue();
            if (newPlayer || best <= 0) return;
            if (!passedRecord && points > best)
            {
                passedRecord = true;
                if (killIn < 0 && lethalLeft == 0) killIn = 3 + rng.Next(4);   // deja disfrutar el «NEW BEST» unos tableros
            }
            if (!bigChainSeen && chain >= Mathf.Max(2, chainMax - 1))
            {
                bigChainSeen = true;
                if (killIn < 0 && lethalLeft == 0) killIn = 3 + rng.Next(3);
            }
        }

        public Board Next(long points, float runSeconds)
        {
            float r = Ratio(points);
            if (best > 0 && r >= EaseFrom && r < 1f) return Make(Mood.Ease);   // la recta final manda sobre todo
            if (newPlayer) return Make(Mood.Neutral);

            if (lethalLeft > 0) { lethalLeft--; return Make(Mood.Lethal); }
            if (killIn >= 0)
            {
                if (killIn > 0) killIn--;
                // El remate por combo espera a que la partida dure lo habitual; el de récord, no
                bool longEnough = passedRecord || typicalSeconds <= 0f || runSeconds >= typicalSeconds * 0.8f;
                if (killIn == 0 && longEnough) { killIn = -1; lethalLeft = LethalStreak - 1; return Make(Mood.Lethal); }
            }
            if (r >= MarginBreak && rng.NextDouble() < MarginBreakChance) return Make(Mood.Lethal);
            if (r >= FastFrom && recent.Count >= 6 && Average() < FastRatio) { recent.Clear(); return Make(Mood.Lethal); }
            return Make(Mood.Neutral);
        }

        private Board Make(Mood m)
        {
            if (m == Mood.Ease) EaseBoards++;
            if (m == Mood.Lethal) LethalBoards++;
            return new Board
            {
                mood = m,
                separationScale = m == Mood.Ease ? EaseSeparation : m == Mood.Lethal ? LethalSeparation : 1f,
            };
        }

        private float Average()
        {
            float s = 0f;
            foreach (var v in recent) s += v;
            return recent.Count > 0 ? s / recent.Count : 1f;
        }

        // Puntos de un acierto tras el director (solo modo Normal en solitario)
        public int AdjustPoints(int pts, long points)
        {
            if (newPlayer || best <= 0 || Ratio(points) < CutPointsFrom) return pts;
            if (rng.NextDouble() >= 0.45) return pts;
            int cut = Mathf.Max(1, Mathf.RoundToInt(pts * (0.60f + 0.25f * (float)rng.NextDouble())));
            CutHits++;
            CutPoints += pts - cut;
            return cut;
        }

        // Bonus al volver de un «continuar» con anuncio
        public long ReviveBonus() => best > 0 ? (long)(best * (0.04 + 0.06 * rng.NextDouble())) : 0;

        // Mediana de las duraciones guardadas (segundos); 0 si no hay
        public static float Median(List<int> values)
        {
            if (values == null || values.Count == 0) return 0f;
            var v = new List<int>(values);
            v.Sort();
            int n = v.Count;
            return n % 2 == 1 ? v[n / 2] : (v[n / 2 - 1] + v[n / 2]) * 0.5f;
        }
    }
}
