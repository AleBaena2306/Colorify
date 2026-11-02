using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Colorify.EditorTools
{
    // COLORIFY > Herramienta de dificultad
    // Ajuste por plazo de dos multiplicadores (1 = el valor base de las fórmulas):
    //  - Dificultad: multiplica el objetivo de puntos de las rondas de ese plazo.
    //  - Parecido: cuánto se parecen los distractores al color objetivo (2 = el doble de parecidos).
    // Los plazos posteriores al último de la tabla usan sus valores. Funciona también en Play: los cambios
    // se aplican desde el siguiente tablero, sin reiniciar la partida.
    public class ColorifyTuningWindow : EditorWindow
    {
        private const string ConfigPath = "Assets/Colorify/Resources/ColorifyConfig.asset";

        private ColorifyConfig cfg;
        private Vector2 scroll;
        private int stagesShown = 12;
        private bool showBase;
        private float fillDifficultyStart = 1f, fillDifficultyGrowth = 1.15f;
        private float fillSimilarityStart = 1f, fillSimilarityGrowth = 1.1f;
        private int previewStage = 1, previewRound;
        private int previewSeed = 1;
        private ColorBoard preview;

        [MenuItem("COLORIFY/Herramienta de dificultad", priority = 20)]
        public static void Open()
        {
            var w = GetWindow<ColorifyTuningWindow>("Colorify · Dificultad");
            w.minSize = new Vector2(760, 520);
        }

        private void OnEnable()
        {
            cfg = AssetDatabase.LoadAssetAtPath<ColorifyConfig>(ConfigPath);
            RegeneratePreview();
        }

        private void OnGUI()
        {
            cfg = (ColorifyConfig)EditorGUILayout.ObjectField("Configuración", cfg, typeof(ColorifyConfig), false);
            if (cfg == null)
            {
                EditorGUILayout.HelpBox("No hay ColorifyConfig. Ejecuta COLORIFY > Setup > Configurar proyecto.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "Dificultad ×: multiplica el objetivo de puntos del plazo (1 = base, 2 = el doble, sin límite).\n" +
                "Parecido ×: divide la distancia entre el objetivo y el distractor más parecido (1 = base, 2 = el doble de parecido) " +
                "y aumenta los distractores del mismo tono. Por debajo de ΔE 0,020 los colores son casi indistinguibles.\n" +
                "Los plazos después del último de la tabla repiten sus valores. En Play los cambios se aplican desde el siguiente tablero.",
                MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();

            DrawBaseSettings();
            EditorGUILayout.Space(6);
            DrawFill();
            EditorGUILayout.Space(6);
            DrawTable();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(cfg);
                RegeneratePreview();
            }

            EditorGUILayout.Space(10);
            DrawPreview();
            EditorGUILayout.EndScrollView();
        }

        // ---------------- Valores base ----------------

        private void DrawBaseSettings()
        {
            showBase = EditorGUILayout.Foldout(showBase, "Valores base (fórmulas) y modo Normal", true);
            if (!showBase) return;
            var so = new SerializedObject(cfg);
            so.Update();
            foreach (var name in new[]
                     {
                         "baseTarget", "stageTargetGrowth", "roundTargetScale", "roundTimeStart", "roundTimeDecay", "roundTimeMin",
                         "wrongTapPenaltyFraction", "minSeparationEasy", "minSeparationHard", "sameFamilyEasy", "sameFamilyHard",
                         "hardestColorStage", "fastClearFraction", "rerollsPerStage",
                         "normalStartTime", "normalMaxTime", "normalHitBonus", "normalBonusDecayPerHit", "normalHitBonusMin",
                         "normalMissPenalty", "normalHitsPerGridStep", "normalHitsToMaxColorDifficulty", "normalSameFamily",
                         "normalSeparationEasy", "normalSeparationHard", "normalSimilarity",
                     })
            {
                var prop = so.FindProperty(name);
                if (prop != null) EditorGUILayout.PropertyField(prop, true);
            }
            so.ApplyModifiedProperties();
        }

        // ---------------- Relleno rápido ----------------

        private void DrawFill()
        {
            EditorGUILayout.LabelField("Rellenar todos los plazos mostrados (valor = inicio × crecimiento^(plazo − 1))", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                fillDifficultyStart = EditorGUILayout.FloatField("Dificultad: inicio", fillDifficultyStart);
                fillDifficultyGrowth = EditorGUILayout.FloatField("crecimiento/plazo", fillDifficultyGrowth);
                if (GUILayout.Button("Aplicar", GUILayout.Width(80)))
                    Fill(true, fillDifficultyStart, fillDifficultyGrowth);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                fillSimilarityStart = EditorGUILayout.FloatField("Parecido: inicio", fillSimilarityStart);
                fillSimilarityGrowth = EditorGUILayout.FloatField("crecimiento/plazo", fillSimilarityGrowth);
                if (GUILayout.Button("Aplicar", GUILayout.Width(80)))
                    Fill(false, fillSimilarityStart, fillSimilarityGrowth);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                stagesShown = Mathf.Clamp(EditorGUILayout.IntField("Plazos en la tabla", stagesShown), 1, 99);
                if (GUILayout.Button("Todo a 1", GUILayout.Width(80)))
                {
                    Undo.RecordObject(cfg, "Colorify: restablecer ajuste por plazo");
                    cfg.stageTuning.Clear();
                    EditorUtility.SetDirty(cfg);
                    RegeneratePreview();
                }
            }
        }

        private void Fill(bool difficulty, float start, float growth)
        {
            Undo.RecordObject(cfg, "Colorify: rellenar ajuste por plazo");
            EnsureRows(stagesShown);
            for (int i = 0; i < stagesShown; i++)
            {
                float v = Mathf.Max(0.05f, start * Mathf.Pow(growth, i));
                if (difficulty) cfg.stageTuning[i].difficulty = v;
                else cfg.stageTuning[i].similarity = v;
            }
            EditorUtility.SetDirty(cfg);
            RegeneratePreview();
        }

        private void EnsureRows(int count)
        {
            if (cfg.stageTuning == null) cfg.stageTuning = new List<ColorifyConfig.StageTuning>();
            while (cfg.stageTuning.Count < count)
            {
                var last = cfg.stageTuning.Count > 0 ? cfg.stageTuning[cfg.stageTuning.Count - 1] : null;
                cfg.stageTuning.Add(last != null ? new ColorifyConfig.StageTuning(last.difficulty, last.similarity) : new ColorifyConfig.StageTuning());
            }
        }

        // ---------------- Tabla por plazo ----------------

        private void DrawTable()
        {
            var header = EditorStyles.miniBoldLabel;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Plazo", header, GUILayout.Width(44));
                GUILayout.Label("Cuadr.", header, GUILayout.Width(44));
                GUILayout.Label("Dificultad ×", header, GUILayout.Width(80));
                GUILayout.Label("Parecido ×", header, GUILayout.Width(80));
                GUILayout.Label("Objetivo ronda 1 / 2 / jefe", header, GUILayout.Width(190));
                GUILayout.Label("Tiempo r1", header, GUILayout.Width(66));
                GUILayout.Label("ΔE mínimo", header, GUILayout.Width(70));
                GUILayout.Label("", header);
            }

            for (int stage = 1; stage <= stagesShown; stage++)
            {
                var t = cfg.Tuning(stage);
                bool inherited = cfg.stageTuning == null || stage > cfg.stageTuning.Count;
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(stage.ToString(), GUILayout.Width(44));
                    int n = cfg.GridSizeForStage(stage);
                    GUILayout.Label($"{n}×{n}", GUILayout.Width(44));

                    var prevColor = GUI.color;
                    if (inherited) GUI.color = new Color(1f, 1f, 1f, 0.55f);
                    float d = EditorGUILayout.FloatField(t.difficulty, GUILayout.Width(80));
                    float sim = EditorGUILayout.FloatField(t.similarity, GUILayout.Width(80));
                    GUI.color = prevColor;
                    if (!Mathf.Approximately(d, t.difficulty) || !Mathf.Approximately(sim, t.similarity))
                    {
                        Undo.RecordObject(cfg, "Colorify: ajuste del plazo " + stage);
                        EnsureRows(stage);
                        cfg.stageTuning[stage - 1].difficulty = Mathf.Max(0.05f, d);
                        cfg.stageTuning[stage - 1].similarity = Mathf.Max(0.05f, sim);
                    }

                    string targets = $"{cfg.TargetFor(stage, 0):#,0} / {cfg.TargetFor(stage, 1):#,0} / {cfg.TargetFor(stage, cfg.roundsPerStage - 1):#,0}";
                    GUILayout.Label(targets, GUILayout.Width(190));
                    GUILayout.Label($"{cfg.RoundTime((stage - 1) * cfg.roundsPerStage):0.0} s", GUILayout.Width(66));
                    float sep = cfg.MinSeparation(stage, 0);
                    GUILayout.Label(sep.ToString("0.000"), GUILayout.Width(70));
                    string note = sep < ColorifyConfig.BarelyVisible ? "⚠ casi indistinguible" : "";
                    if (stage == cfg.StageWithMaxGrid) note += (note.Length > 0 ? " · " : "") + "victoria (1.ª ronda 8×8)";
                    if (inherited) note += (note.Length > 0 ? " · " : "") + "hereda del último";
                    GUILayout.Label(note, EditorStyles.miniLabel);
                    if (GUILayout.Button("Ver", GUILayout.Width(40))) { previewStage = stage; previewRound = 0; RegeneratePreview(); }
                }
            }
        }

        // ---------------- Vista previa de un tablero ----------------

        private void RegeneratePreview()
        {
            if (cfg == null) return;
            var rng = new System.Random(previewSeed);
            int n = cfg.GridSizeForStage(previewStage);
            preview = ColorBoardGenerator.Generate(cfg, n, cfg.ColorDifficulty(previewStage, previewRound),
                f => cfg.families[f].spawnWeight, rng, cfg.Similarity(previewStage));
            Repaint();
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Vista previa (tablero de ejemplo con los ajustes actuales)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                int s = EditorGUILayout.IntSlider("Plazo", previewStage, 1, Mathf.Max(stagesShown, 1));
                int r = EditorGUILayout.IntSlider("Ronda", previewRound + 1, 1, cfg.roundsPerStage) - 1;
                if (s != previewStage || r != previewRound) { previewStage = s; previewRound = r; RegeneratePreview(); }
                if (GUILayout.Button("Otro ejemplo", GUILayout.Width(110))) { previewSeed++; RegeneratePreview(); }
            }
            if (preview == null) return;

            float sep = cfg.MinSeparation(previewStage, previewRound);
            EditorGUILayout.LabelField($"ΔE mínimo {sep:0.000} · objetivo {cfg.TargetFor(previewStage, previewRound):#,0} · la casilla correcta lleva un marco");

            const float swatch = 90f, board = 360f, gap = 4f;
            Rect area = GUILayoutUtility.GetRect(swatch + 30 + board, board + 10);
            area.x += 6;
            EditorGUI.DrawRect(new Rect(area.x, area.y, swatch, swatch), preview.TargetColor);

            int n = preview.size;
            float cell = (board - gap * (n - 1)) / n;
            float ox = area.x + swatch + 30;
            for (int i = 0; i < preview.tiles.Count; i++)
            {
                int x = i % n, y = i / n;
                var rect = new Rect(ox + x * (cell + gap), area.y + y * (cell + gap), cell, cell);
                if (i == preview.correctIndex)
                {
                    EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), Color.black);
                }
                EditorGUI.DrawRect(rect, preview.tiles[i].ToColor());
            }
        }
    }
}
