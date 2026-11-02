using System.Collections.Generic;
using UnityEngine;

namespace Colorify
{
    public enum CardType { MoreChance, MorePoints, DoubleMult }

    // Una carta de mejora. family = índice en config.families (-1 en Double mult).
    [System.Serializable]
    public struct Card
    {
        public CardType type;
        public int family;

        public Card(CardType type, int family) { this.type = type; this.family = family; }

        public string Id(ColorifyConfig cfg)
        {
            if (type == CardType.MoreChance) return "chance_" + cfg.families[family].id;
            if (type == CardType.MorePoints) return "points_" + cfg.families[family].id;
            return "double_mult";
        }

        public string Title(ColorifyConfig cfg)
        {
            if (type == CardType.MoreChance) return $"More {cfg.families[family].displayName}";
            if (type == CardType.MorePoints) return $"Rich {cfg.families[family].displayName}";
            return "Double Mult";
        }

        public string Description(ColorifyConfig cfg)
        {
            if (type == CardType.MoreChance) return $"{cfg.families[family].displayName} shows up more often";
            if (type == CardType.MorePoints) return $"{cfg.families[family].displayName} gives +{Mathf.RoundToInt((cfg.pointsCardFactor - 1f) * 100f)}% points";
            return "Your multiplier is doubled for the rest of the run";
        }
    }

    // Estado de una partida (run). Sin dependencias de UI: se puede probar en tests de Edit Mode.
    public class RunState
    {
        public readonly ColorifyConfig cfg;

        public int stage = 1;              // plazo (empieza en 1)
        public int roundInStage;           // 0..roundsPerStage-1 (la última es la jefe)
        public int globalRound;            // rondas jugadas o saltadas desde el inicio
        public long totalScore;
        public float roundScore;
        public int streak;                 // rondas rápidas seguidas
        public float multFactor = 1f;      // se dobla con la carta Double mult
        public bool victoryReached;
        public bool continueUsed;
        public int pendingExtraPicks;
        private int rerollStageMark = -1, rerollsUsed;      // elecciones de carta extra ganadas al saltar rondas (se cobran tras la siguiente ronda superada)
        public int hits, wrongTaps;
        public float bestReaction = float.MaxValue;
        public readonly float[] familyWeightMul;
        public readonly float[] familyPointsMul;
        public readonly List<Card> cards = new List<Card>();

        public RunState(ColorifyConfig cfg)
        {
            this.cfg = cfg;
            familyWeightMul = new float[cfg.families.Length];
            familyPointsMul = new float[cfg.families.Length];
            for (int i = 0; i < cfg.families.Length; i++) { familyWeightMul[i] = 1f; familyPointsMul[i] = 1f; }
        }

        public float Multiplier => (1 + streak) * multFactor;
        public int GridSize => cfg.GridSizeForStage(stage);
        public float Target => cfg.TargetFor(stage, roundInStage);
        public float RoundTime => cfg.RoundTime(globalRound);
        public float ColorDifficulty => cfg.ColorDifficulty(stage, roundInStage);
        public bool IsBossRound => roundInStage >= cfg.roundsPerStage - 1;
        public bool CanSkip => !IsBossRound;
        public string RoundLabel => $"{stage}-{roundInStage + 1}";

        public float FamilyWeight(int f) => cfg.families[f].spawnWeight * familyWeightMul[f];

        // Puntos de un acierto. reaction = segundos desde que apareció la cuadrícula.
        public int PointsFor(int family, float reaction)
        {
            float window = cfg.SpeedWindow(GridSize);
            float speed = 1f + cfg.speedBonusMax * Mathf.Clamp01(1f - reaction / window);
            float pts = cfg.families[family].basePoints * familyPointsMul[family] * speed * Multiplier;
            return Mathf.Max(1, Mathf.RoundToInt(pts));
        }

        public void ApplyCard(Card c)
        {
            cards.Add(c);
            switch (c.type)
            {
                case CardType.MoreChance: familyWeightMul[c.family] *= cfg.weightCardFactor; break;
                case CardType.MorePoints: familyPointsMul[c.family] *= cfg.pointsCardFactor; break;
                case CardType.DoubleMult: multFactor *= 2f; break;
            }
        }

        // Tras superar o saltar una ronda
        public void AdvanceRound()
        {
            globalRound++;
            roundInStage++;
            if (roundInStage >= cfg.roundsPerStage) { roundInStage = 0; stage++; }
            roundScore = 0;
        }

        // ---------------- Reroll (uno por plazo, configurable) ----------------

        public int RerollsLeft => Mathf.Max(0, cfg.rerollsPerStage - (rerollStageMark == stage ? rerollsUsed : 0));

        public bool TryUseReroll()
        {
            if (rerollStageMark != stage) { rerollStageMark = stage; rerollsUsed = 0; }
            if (rerollsUsed >= cfg.rerollsPerStage) return false;
            rerollsUsed++;
            return true;
        }

        // ---------------- Probabilidades y efecto de las cartas (para el panel de cartas) ----------------

        // Probabilidad de que el color objetivo sea de la familia f (0..1)
        public float FamilyChance(int f) => FamilyChance(f, familyWeightMul);

        private float FamilyChance(int f, float[] weightMul)
        {
            float total = 0f;
            for (int i = 0; i < cfg.families.Length; i++) total += cfg.families[i].spawnWeight * weightMul[i];
            return total <= 0f ? 0f : cfg.families[f].spawnWeight * weightMul[f] / total;
        }

        public float FamilyPoints(int f) => cfg.families[f].basePoints * familyPointsMul[f];

        // Puntos medios por acierto (sin contar la velocidad), con el multiplicador actual
        public float AveragePointsPerHit() => AveragePoints(familyWeightMul, familyPointsMul, Multiplier);

        private float AveragePoints(float[] weightMul, float[] pointsMul, float mult)
        {
            float avg = 0f;
            for (int f = 0; f < cfg.families.Length; f++)
                avg += FamilyChance(f, weightMul) * cfg.families[f].basePoints * pointsMul[f];
            return avg * mult;
        }

        // Puntos medios por acierto si se cogiera esta carta
        public float AveragePointsWith(Card c)
        {
            var w = (float[])familyWeightMul.Clone();
            var p = (float[])familyPointsMul.Clone();
            float mult = Multiplier;
            if (c.type == CardType.MoreChance) w[c.family] *= cfg.weightCardFactor;
            else if (c.type == CardType.MorePoints) p[c.family] *= cfg.pointsCardFactor;
            else mult *= 2f;
            return AveragePoints(w, p, mult);
        }

        // Probabilidad de que una carta concreta aparezca en una oferta (aprox.: cada hueco es independiente)
        public float OfferChance(Card c)
        {
            float slot = c.type == CardType.DoubleMult
                ? cfg.doubleMultChance
                : (1f - cfg.doubleMultChance) * 0.5f / Mathf.Max(1, cfg.families.Length);
            return 1f - Mathf.Pow(1f - slot, Mathf.Max(1, cfg.cardsOffered));
        }

        // Texto del efecto concreto de la carta, con los valores de la partida actual
        public string EffectText(Card c)
        {
            if (c.type == CardType.MoreChance)
            {
                var w = (float[])familyWeightMul.Clone();
                w[c.family] *= cfg.weightCardFactor;
                return $"{cfg.families[c.family].displayName}: {FamilyChance(c.family) * 100f:0}% → {FamilyChance(c.family, w) * 100f:0}% of targets";
            }
            if (c.type == CardType.MorePoints)
                return $"{cfg.families[c.family].displayName}: {FamilyPoints(c.family):0} → {FamilyPoints(c.family) * cfg.pointsCardFactor:0} pts per hit";
            return $"Mult x{Multiplier:0.#} → x{Multiplier * 2f:0.#}";
        }

        public List<Card> OfferCards(System.Random rng)
        {
            var offer = new List<Card>();
            int guard = 0;
            while (offer.Count < cfg.cardsOffered && guard++ < 100)
            {
                Card c;
                if (rng.NextDouble() < cfg.doubleMultChance) c = new Card(CardType.DoubleMult, -1);
                else
                {
                    var type = rng.NextDouble() < 0.5 ? CardType.MoreChance : CardType.MorePoints;
                    c = new Card(type, rng.Next(cfg.families.Length));
                }
                if (!offer.Exists(o => o.type == c.type && o.family == c.family)) offer.Add(c);
            }
            return offer;
        }
    }
}
