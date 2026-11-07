using System.Collections.Generic;
using UnityEngine;

namespace Colorify
{
    // Formas de las fichas y de la muestra (cuadrado redondeado de siempre, círculo, triángulo y rombo)
    public enum Shape { Square = 0, Circle = 1, Triangle = 2, Diamond = 3 }

    // Efectos sobre la muestra. Los valores 1 y 2 coinciden con las variantes antiguas (rayas y memoria),
    // así el histórico que ya ha aprendido el bot sigue valiendo.
    public enum Effect { None = 0, Stripes = 1, Flash = 2, DarkStripes = 3, Wobble = 4, Shrink = 5, Spin = 6 }

    // Tipo de tablero: forma + efecto. Código para estadísticas y para el bot: tamaño + 10 × efecto + 100 × forma
    // (el servidor usa la misma cuenta en kinds.js).
    public struct BoardKind
    {
        public Shape shape;
        public Effect fx;

        public BoardKind(Shape s, Effect e) { shape = s; fx = e; }
        public bool IsPlain => shape == Shape.Square && fx == Effect.None;
        public int Code(int size) => size + 10 * (int)fx + 100 * (int)shape;
        public static BoardKind FromCode(int code) => new BoardKind((Shape)(code / 100 % 10), (Effect)(code / 10 % 10));
        public static readonly BoardKind Plain = new BoardKind(Shape.Square, Effect.None);
        public bool Same(BoardKind o) => shape == o.shape && fx == o.fx;

        // Texto del aviso grande al entrar en un efecto especial (las formas no son especiales: no tienen aviso)
        public string Banner =>
            fx == Effect.Stripes ? "STRIPES!" : fx == Effect.DarkStripes ? "DARK STRIPES!" : fx == Effect.Flash ? "REMEMBER IT!"
            : fx == Effect.Wobble ? "WOBBLE!" : fx == Effect.Shrink ? "SHRINKING!" : fx == Effect.Spin ? "SPIN!" : "";
    }

    // Lo que el servidor ha aprendido de las partidas de todos los jugadores (GET /api/colorify/tuning):
    // el ritmo del modo Normal (para que una partida dure unos 5 minutos) y, por código de tablero, el tiempo
    // mediano hasta acertar y la proporción de fallos. Sin conexión se usan valores de referencia.
    public class Tuning
    {
        public static Tuning Current = new Tuning();

        public float pace = 1f;   // multiplica el tiempo que da cada acierto en el modo Normal
        private readonly Dictionary<int, Vector3> codes = new Dictionary<int, Vector3>();   // code → (ms, fallos, partidas)

        public void Set(int code, float ms, float miss, float n) => codes[code] = new Vector3(ms, miss, n);
        public int Known => codes.Count;

        // Referencia antes de tener datos (ms hasta acertar en un tablero normal de cada tamaño)
        private static float PriorMs(int size) => 450f + 220f * Mathf.Clamp(size, 1, 8);

        // Elementos especiales en orden de dificultad por defecto (fácil → difícil)
        public static readonly BoardKind[] Items =
        {
            new BoardKind(Shape.Circle, Effect.None), new BoardKind(Shape.Square, Effect.Stripes), new BoardKind(Shape.Diamond, Effect.None),
            new BoardKind(Shape.Square, Effect.Wobble), new BoardKind(Shape.Square, Effect.DarkStripes), new BoardKind(Shape.Square, Effect.Spin),
            new BoardKind(Shape.Triangle, Effect.None), new BoardKind(Shape.Square, Effect.Flash), new BoardKind(Shape.Square, Effect.Shrink),
        };
        private static float PriorDifficulty(int item) => 1.12f + 0.06f * item;

        // Tiempo esperado (ms) para acertar este tablero: lo aprendido o, con pocos datos, la referencia
        public float ExpectedMs(int size, BoardKind kind)
        {
            float prior = PriorMs(size) * (kind.IsPlain ? 1f : Difficulty(kind));
            Vector3 v;
            if (!codes.TryGetValue(kind.Code(size), out v) || v.x <= 0f) return prior;
            float w = Mathf.Clamp01(v.z / 25f);
            return Mathf.Lerp(prior, v.x, w);
        }

        // Dificultad aprendida de un elemento: cuánto más se tarda (y se falla) que en el tablero normal del
        // mismo tamaño, de media entre los tamaños con datos. 1 = igual que normal.
        public float ItemDifficulty(int item)
        {
            var k = Items[item];
            float sum = 0f, weight = 0f;
            for (int size = 2; size <= 8; size++)
            {
                Vector3 s, p;
                if (!codes.TryGetValue(k.Code(size), out s) || s.z < 15f) continue;
                if (!codes.TryGetValue(size, out p) || p.z < 15f || p.x <= 0f) continue;
                float ratio = s.x / p.x * (1f + 2f * Mathf.Max(0f, s.y - p.y));
                float w = Mathf.Min(s.z, 200f);
                sum += ratio * w; weight += w;
            }
            float prior = PriorDifficulty(item);
            if (weight <= 0f) return prior;
            float learned = Mathf.Clamp(sum / weight, 0.8f, 3f);
            return Mathf.Lerp(prior, learned, Mathf.Clamp01(weight / 60f));
        }

        // Dificultad de un tablero (forma y efecto juntos se multiplican)
        public float Difficulty(BoardKind kind)
        {
            float d = 1f;
            for (int i = 0; i < Items.Length; i++)
            {
                var it = Items[i];
                if (it.shape != Shape.Square && it.shape == kind.shape) d *= ItemDifficulty(i);
                if (it.fx != Effect.None && it.fx == kind.fx) d *= ItemDifficulty(i);
            }
            return Mathf.Clamp(d, 1f, 5f);
        }

        // Orden en el que se desbloquean los efectos especiales: de lo que a los jugadores les resulta más fácil a
        // lo más difícil según lo aprendido (con la referencia mientras no haya datos). Las formas no entran:
        // son la base de cada tablero y todas salen con la misma probabilidad.
        public List<int> UnlockOrder()
        {
            var diff = new float[Items.Length];
            var effects = new List<int>();
            for (int i = 0; i < Items.Length; i++)
            {
                diff[i] = ItemDifficulty(i);
                if (Items[i].shape == Shape.Square) effects.Add(i);
            }
            effects.Sort((a, b) => diff[a] != diff[b] ? diff[a].CompareTo(diff[b]) : a.CompareTo(b));
            return effects;
        }
    }

    // Progresión del modo Normal (y forma de cada ronda de carrera en el servidor, kinds.js):
    //  · Calentamiento: los primeros aciertos en 2×2, cuadrados y colores normales.
    //  · Luego la cuadrícula crece hasta 5×5. Cada tablero tiene una forma (cuadrado, círculo, triángulo o
    //    rombo, las cuatro igual de probables) y, cada vez más a menudo, un efecto especial sobre la muestra,
    //    hasta que todos los tableros llevan uno.
    public static class Progression
    {
        public static int GridSize(ColorifyConfig cfg, int hits)
        {
            int warm = Mathf.Max(0, cfg.normalWarmupHits);
            int max = Mathf.Clamp(cfg.normalSpecialMaxGrid, 2, 8);
            if (hits < warm) return 2;
            int step = Mathf.Max(1, cfg.normalHitsPerGridStep);
            return Mathf.Min(max, 3 + (hits - warm) / step);
        }

        // Proporción de tableros con efecto especial tras el calentamiento: empieza en normalSpecialStartShare y llega a 1
        public static float SpecialShare(ColorifyConfig cfg, int hits)
        {
            int warm = Mathf.Max(0, cfg.normalWarmupHits);
            if (hits < warm) return 0f;
            float k = Mathf.Clamp01((hits - warm + 1) / (float)Mathf.Max(1, cfg.normalSpecialRampHits));
            return Mathf.Lerp(Mathf.Clamp01(cfg.normalSpecialStartShare), 1f, k);
        }

        // Cada normalUnlockEveryHits aciertos se desbloquea un efecto nuevo y el tablero siguiente lo enseña sí o sí
        public static BoardKind Pick(ColorifyConfig cfg, Tuning tuning, int hits, BoardKind previous, System.Random rng)
        {
            int warm = Mathf.Max(0, cfg.normalWarmupHits);
            if (hits < warm) return BoardKind.Plain;
            var kind = new BoardKind((Shape)rng.Next(4), Effect.None);
            int since = hits - warm, every = Mathf.Max(1, cfg.normalUnlockEveryHits);
            var order = tuning.UnlockOrder();
            int unlocked = Mathf.Clamp(1 + since / every, 1, order.Count);
            int newest = order[unlocked - 1];
            if (since % every == 0 && since / every < order.Count) { kind.fx = Tuning.Items[newest].fx; return kind; }   // novedad
            if (rng.NextDouble() < SpecialShare(cfg, hits)) kind.fx = Tuning.Items[PickOne(order, unlocked, newest, previous, rng)].fx;
            return kind;
        }

        // Lo recién desbloqueado pesa el doble y se evita repetir el efecto del tablero anterior
        private static int PickOne(List<int> order, int unlocked, int newest, BoardKind previous, System.Random rng)
        {
            var w = new float[unlocked];
            float total = 0f;
            for (int i = 0; i < unlocked; i++)
            {
                w[i] = order[i] == newest ? 2f : 1f;
                if (Tuning.Items[order[i]].fx == previous.fx) w[i] *= 0.4f;
                total += w[i];
            }
            double r = rng.NextDouble() * total;
            for (int i = 0; i < unlocked; i++) { r -= w[i]; if (r <= 0) return order[i]; }
            return order[unlocked - 1];
        }
    }

    // Puntos por acierto en el modo Normal. El jugador no ve la fórmula: solo que acertar rápido, en tableros
    // grandes y con especiales difíciles da más, y que encadenar aciertos rápidos multiplica (X2, X3…).
    public static class Points
    {
        public static int ForHit(ColorifyConfig cfg, Tuning tuning, int size, BoardKind kind, float reactionMs, int chain, int hits)
        {
            float expected = tuning.ExpectedMs(size, kind);
            float speed = Mathf.Clamp(expected / Mathf.Max(120f, reactionMs), 0.5f, 2.2f);
            float basePts = cfg.pointsBase + cfg.pointsPerGridStep * (size - 2);
            float diff = kind.IsPlain ? 1f : tuning.Difficulty(kind);
            float progress = 1f + cfg.pointsProgressPerHit * Mathf.Min(hits, 150);
            return Mathf.Max(1, Mathf.RoundToInt(basePts * diff * (0.55f + 0.45f * speed) * progress * Mathf.Max(1, chain)));
        }

        // Un acierto es «rápido» (sube el multiplicador) si se tarda menos que esta parte de lo esperado
        public static bool IsFast(ColorifyConfig cfg, Tuning tuning, int size, BoardKind kind, float reactionMs) =>
            reactionMs <= tuning.ExpectedMs(size, kind) * cfg.fastHitFraction;
    }
}
