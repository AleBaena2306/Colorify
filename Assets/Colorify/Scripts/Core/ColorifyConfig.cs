using System;
using System.Collections.Generic;
using UnityEngine;

namespace Colorify
{
    // Todos los números del diseño en un solo sitio. El asset vive en Assets/Colorify/Resources/ColorifyConfig.asset
    // (lo crea COLORIFY > Setup). Si no existe, el juego usa estos mismos valores por defecto.
    // Equilibrado: valores iniciales razonables, pendientes de ajustar jugando (día 2 del plan).
    [CreateAssetMenu(fileName = "ColorifyConfig", menuName = "Colorify/Config")]
    public class ColorifyConfig : ScriptableObject
    {
        [Serializable]
        public class ColorFamily
        {
            public string id;            // red, orange... (analítica y guardado)
            public string displayName;   // texto en las cartas
            [Range(0, 360)] public float hue;
            [Range(0, 1)] public float lightness;
            [Range(0, 0.37f)] public float chroma;
            public int basePoints;       // más cálido = más puntos
            public float spawnWeight;    // más cálido = menos probable

            public ColorFamily(string id, string name, float hue, float l, float c, int points, float weight)
            {
                this.id = id; displayName = name; this.hue = hue; lightness = l; chroma = c;
                basePoints = points; spawnWeight = weight;
            }

            public Color SwatchColor => OkColor.FromLch(lightness, chroma, hue).ToColor();
        }

        // Ajuste por plazo (Herramienta de dificultad). 1 = el valor base de las fórmulas de abajo.
        [Serializable]
        public class StageTuning
        {
            [Tooltip("Multiplica el objetivo de puntos de las rondas de este plazo (1 = base, 2 = el doble…)")]
            public float difficulty = 1f;
            [Tooltip("Cuánto se parecen los distractores al color objetivo (1 = base, 2 = el doble de parecidos…)")]
            public float similarity = 1f;
            public StageTuning() { }
            public StageTuning(float d, float s) { difficulty = d; similarity = s; }
        }

        [Header("Ajuste por plazo (COLORIFY > Herramienta de dificultad)")]
        [Tooltip("Elemento 0 = plazo 1. Los plazos posteriores al último usan el último valor")]
        public List<StageTuning> stageTuning = new List<StageTuning>();

        [Header("Colores (de frío a cálido). Paleta mate: croma bajo-medio")]
        public ColorFamily[] families =
        {
            new ColorFamily("blue",   "Blue",   255f, 0.70f, 0.10f, 10, 30f),
            new ColorFamily("teal",   "Teal",   195f, 0.76f, 0.09f, 15, 24f),
            new ColorFamily("green",  "Green",  145f, 0.76f, 0.10f, 20, 18f),
            new ColorFamily("yellow", "Yellow",  98f, 0.86f, 0.12f, 30, 13f),
            new ColorFamily("orange", "Orange",  58f, 0.76f, 0.12f, 45,  9f),
            new ColorFamily("red",    "Red",     25f, 0.66f, 0.13f, 70,  6f),
        };

        [Tooltip("Variación de cada tono dentro de su familia, para que el objetivo no sea siempre igual")]
        public float shadeLightnessJitter = 0.05f;
        public float shadeChromaJitter = 0.02f;
        public float shadeHueJitter = 10f;

        [Header("Estructura (como las antes de Balatro)")]
        [Tooltip("Rondas por plazo. La última es la «jefe»: no se puede saltar")]
        public int roundsPerStage = 3;
        [Tooltip("Lado de la cuadrícula en el plazo 1. Sube 1 por plazo hasta el máximo")]
        public int startGridSize = 2;
        public int maxGridSize = 8;

        [Header("Objetivo de puntos")]
        public float baseTarget = 250f;
        [Tooltip("Multiplicador del objetivo por cada plazo superado (crecimiento exponencial)")]
        public float stageTargetGrowth = 1.45f;
        [Tooltip("Objetivo relativo de cada ronda dentro del plazo (pequeña, grande, jefe)")]
        public float[] roundTargetScale = { 1f, 1.4f, 1.8f };

        [Header("Tiempo por ronda")]
        public float roundTimeStart = 30f;
        [Tooltip("Cada ronda (jugada o saltada) dura este factor de la anterior")]
        public float roundTimeDecay = 0.97f;
        public float roundTimeMin = 15f;
        [Tooltip("Fracción del tiempo total de la ronda que resta un toque erróneo (0,07 = 7 %: 2,1 s en una ronda de 30 s). " +
                 "Al ser proporcional, sigue cuadrando si el tiempo de la ronda cambia (cartas de tiempo, continuar…)")]
        [Range(0, 0.5f)] public float wrongTapPenaltyFraction = 0.07f;
        public int countdownSeconds = 3;

        [Header("Puntuación por toque = puntos del color × velocidad × multiplicador")]
        [Tooltip("Bonus máximo por velocidad: un toque instantáneo vale ×(1 + esto)")]
        public float speedBonusMax = 1f;
        [Tooltip("Ventana para el bonus de velocidad: base + segundos por casilla")]
        public float speedWindowBase = 0.8f;
        public float speedWindowPerTile = 0.04f;
        [Tooltip("Ronda «rápida» si queda al menos esta fracción del tiempo. Rápida = multiplicador +1; lenta = la racha vuelve a 0")]
        [Range(0, 1)] public float fastClearFraction = 0.35f;

        [Header("Dificultad de color (distancia OKLab entre el objetivo y los distractores)")]
        [Tooltip("Separación mínima al principio (muy fácil) y al final (muy difícil). 0,02 ≈ diferencia apenas visible")]
        public float minSeparationEasy = 0.14f;
        public float minSeparationHard = 0.05f;
        [Tooltip("Probabilidad de que un distractor sea de la misma familia que el objetivo, al principio y al final")]
        [Range(0, 1)] public float sameFamilyEasy = 0f;
        [Range(0, 1)] public float sameFamilyHard = 0.75f;
        [Tooltip("Parte de la diferencia que va en luminosidad (ayuda a jugadores daltónicos)")]
        [Range(0, 1)] public float lightnessShare = 0.5f;
        [Tooltip("Plazo en el que la dificultad de color llega al máximo")]
        public int hardestColorStage = 7;

        [Header("Cartas")]
        public float weightCardFactor = 1.6f;
        public float pointsCardFactor = 1.5f;
        [Tooltip("Probabilidad de que cada carta ofrecida sea «Double mult»")]
        [Range(0, 1)] public float doubleMultChance = 0.12f;
        public int cardsOffered = 3;
        [Tooltip("Rerolls de la oferta de cartas por plazo")]
        public int rerollsPerStage = 1;

        [Header("Modo Normal (sin cartas: los aciertos suman tiempo y los fallos lo restan)")]
        [Tooltip("Segundos al empezar")]
        public float normalStartTime = 10f;
        [Tooltip("Tope de tiempo acumulado (0 = sin tope). También es el lleno de la barra")]
        public float normalMaxTime = 15f;
        [Tooltip("Segundos que suma cada acierto al principio…")]
        public float normalHitBonus = 1.2f;
        [Tooltip("…lo que baja por cada acierto…")]
        public float normalBonusDecayPerHit = 0.01f;
        [Tooltip("…y el mínimo al que puede bajar")]
        public float normalHitBonusMin = 0.4f;
        [Tooltip("Segundos que resta cada fallo")]
        public float normalMissPenalty = 2f;
        [Tooltip("Aciertos para que la cuadrícula crezca una fila y una columna")]
        public int normalHitsPerGridStep = 10;
        [Tooltip("Aciertos para llegar a la máxima dificultad de color")]
        public int normalHitsToMaxColorDifficulty = 90;
        [Tooltip("Probabilidad de que cada distractor sea un matiz del mismo color que el objetivo. 1 = todo el tablero es de " +
                 "la misma familia: hay que diferenciar tonos, no buscar el color distinto")]
        [Range(0, 1)] public float normalSameFamily = 1f;
        [Tooltip("Distancia OKLab entre el objetivo y el distractor más parecido al empezar (0,02 ≈ apenas visible)")]
        public float normalSeparationEasy = 0.09f;
        [Tooltip("…y al llegar a la máxima dificultad")]
        public float normalSeparationHard = 0.03f;
        [Tooltip("Multiplicador extra de parecido en este modo (1 = sin cambio, 2 = el doble de parecidos)")]
        public float normalSimilarity = 1f;
        [Tooltip("Modo Normal: segundos que, al perder, se señala y bota la ficha correcta antes de la pantalla de fin.")]
        public float normalRevealSeconds = 2.2f;
        [Tooltip("Modo Normal: un acierto siempre te deja como mínimo en esta fracción del tiempo máximo (0,10 = 10 %). Era 0,25 hasta el 11/11/2026: con eso la partida era casi infinita")]
        [Range(0, 1)] public float normalHitFloorFraction = 0.1f;

        [Header("Modo Normal: variantes de la muestra entre tamaños de cuadrícula")]
        [Tooltip("Tras los aciertos normales de cada tamaño llegan rondas con la muestra rayada y rondas de memoria")]
        public bool normalVariants = true;
        [Tooltip("Rondas de cada variante en el primer tamaño (2×2); se suma «Por tamaño» en cada tamaño siguiente")]
        public int normalVariantRoundsFirst = 1;
        public int normalVariantRoundsPerGrid = 1;
        [Tooltip("Memoria: segundos que se ve el color de la muestra")]
        public float normalFlashSeconds = 2f;
        [Tooltip("Memoria: segundos que vuelve a verse el color tras un fallo")]
        public float normalFlashOnMiss = 0.6f;
        [Tooltip("Rayas: parte de la muestra tapada por franjas blancas (0-1)")]
        [Range(0.1f, 0.8f)] public float normalStripeCoverage = 0.45f;

        [Header("Modo Normal y carreras: formas y efectos especiales")]
        [Tooltip("Calentamiento: aciertos en 2×2 con colores normales antes de que cambie nada")]
        public int normalWarmupHits = 8;
        [Tooltip("Cuadrícula máxima en Normal y carreras (el duelo sigue con la suya)")]
        public int normalSpecialMaxGrid = 5;
        [Tooltip("Proporción de tableros con efecto especial justo después del calentamiento…")]
        [Range(0, 1)] public float normalSpecialStartShare = 0.5f;
        [Tooltip("…y aciertos tras el calentamiento hasta que todos los tableros llevan efecto")]
        public int normalSpecialRampHits = 40;
        [Tooltip("Cada cuántos aciertos se desbloquea un efecto nuevo (en orden de dificultad aprendida)")]
        public int normalUnlockEveryHits = 4;
        [Tooltip("Encoger: segundos hasta que la muestra desaparece")]
        public float normalShrinkSeconds = 2.6f;
        [Tooltip("Tambaleo: grados de balanceo de la muestra")]
        public float normalWobbleDegrees = 10f;
        [Tooltip("Giro: grados por segundo")]
        public float normalSpinSpeed = 70f;
        [Tooltip("Desplazamiento: unidades de pantalla por segundo (la pantalla mide 1080 de ancho)")]
        public float normalMoveSpeed = 360f;

        [Header("Modo Normal: puntos")]
        public float pointsBase = 37f;
        public float pointsPerGridStep = 14f;
        [Tooltip("Los puntos de cada acierto suben un poco con los aciertos de la partida")]
        public float pointsProgressPerHit = 0.006f;
        [Tooltip("Un acierto es rápido (sube el multiplicador X2, X3…) si tarda menos que esta parte del tiempo esperado")]
        [Range(0.2f, 1.5f)] public float fastHitFraction = 0.8f;
        public int chainMax = 5;

        [Header("Online")]
        [Tooltip("Servidor de Kovu's Pride (el portfolio). En la web se usa el dominio desde el que se juega.")]
        public string onlineBaseUrl = "https://alejandrobaenaportfolio.com";
        [Tooltip("Enviar las puntuaciones de Normal y Supervivencia al ranking global")]
        public bool submitScoresOnline = true;

        [Header("Continuar con anuncio (una vez por partida)")]
        [Tooltip("Se ofrece si la partida llega a esta fracción del récord…")]
        [Range(0, 1)] public float continueNearRecordFraction = 0.75f;
        [Tooltip("…o si han pasado estas partidas desde la última oferta")]
        public int continueEveryRuns = 3;
        [Tooltip("Anuncios de prueba de H5 Games Ads (dejar activado hasta que AdSense apruebe la cuenta)")]
        public bool webTestAds = true;

        // ---------------- Derivados ----------------

        public int GridSizeForStage(int stage) => Mathf.Clamp(startGridSize + stage - 1, 1, maxGridSize);

        public int StageWithMaxGrid => maxGridSize - startGridSize + 1;

        public StageTuning Tuning(int stage)
        {
            if (stageTuning == null || stageTuning.Count == 0) return DefaultTuning;
            return stageTuning[Mathf.Clamp(stage - 1, 0, stageTuning.Count - 1)];
        }
        private static readonly StageTuning DefaultTuning = new StageTuning(1f, 1f);

        public float TargetFor(int stage, int roundInStage)
        {
            float scale = roundTargetScale.Length == 0 ? 1f : roundTargetScale[Mathf.Clamp(roundInStage, 0, roundTargetScale.Length - 1)];
            float d = Mathf.Max(0.01f, Tuning(stage).difficulty);
            return Mathf.Max(10f, Mathf.Round(baseTarget * Mathf.Pow(stageTargetGrowth, stage - 1) * scale * d / 10f) * 10f);
        }

        public float Similarity(int stage) => Mathf.Max(0.05f, Tuning(stage).similarity);

        // Separación mínima real (OKLab) entre el objetivo y el distractor más parecido, con el ajuste del plazo
        public float MinSeparation(int stage, int roundInStage) =>
            Mathf.Max(MinSeparationFloor, Mathf.Lerp(minSeparationEasy, minSeparationHard, ColorDifficulty(stage, roundInStage)) / Similarity(stage));

        // Por debajo de ~0,02 dos colores son casi indistinguibles en pantalla; 0,006 es el límite técnico
        public const float MinSeparationFloor = 0.006f;
        public const float BarelyVisible = 0.02f;

        public float RoundTime(int globalRoundIndex) =>
            Mathf.Max(roundTimeMin, roundTimeStart * Mathf.Pow(roundTimeDecay, globalRoundIndex));

        // 0 = plazo 1, 1 = plazo de máxima dificultad de color
        public float ColorDifficulty(int stage, int roundInStage)
        {
            float progress = (stage - 1) + roundInStage / (float)Mathf.Max(1, roundsPerStage);
            return Mathf.Clamp01(progress / Mathf.Max(1, hardestColorStage - 1));
        }

        public float SpeedWindow(int gridSize) => speedWindowBase + speedWindowPerTile * gridSize * gridSize;

        private static ColorifyConfig cached;

        public static ColorifyConfig Load()
        {
            if (cached != null) return cached;
            cached = Resources.Load<ColorifyConfig>("ColorifyConfig");
            if (cached == null) cached = CreateInstance<ColorifyConfig>();
            return cached;
        }
    }
}
