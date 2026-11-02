using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Colorify.EditorTools
{
    // COLORIFY > Setup > Configurar proyecto: deja el proyecto listo para jugar y compilar para web.
    // Se puede repetir sin riesgo (no duplica nada).
    public static class ColorifySetup
    {
        private const string ScenePath = "Assets/Colorify/Scenes/Main.unity";
        private const string ConfigPath = "Assets/Colorify/Resources/ColorifyConfig.asset";

        [MenuItem("COLORIFY/Setup/Configurar proyecto", priority = 0)]
        public static void SetupProject()
        {
            // 1. Identidad y orientación
            PlayerSettings.companyName = "Kovu's Pride";
            PlayerSettings.productName = "Colorify";
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "1.0" || PlayerSettings.bundleVersion == "0.1")
                PlayerSettings.bundleVersion = "0.01";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.KovusPride.Colorify");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.runInBackground = false;

            // 2. TextMeshPro: recursos esenciales (shaders y ajustes). Sin ellos los textos salen rosas
            ImportTmpEssentials();

            // 2b. Fuentes SDF de TextMeshPro (Nunito). Si fallara, el juego las genera al arrancar desde el TTF
            CreateFontAsset("Nunito-ExtraBold");
            CreateFontAsset("Nunito-Black");

            // 3. Configuración del diseño (ScriptableObject con todos los números)
            if (AssetDatabase.LoadAssetAtPath<ColorifyConfig>(ConfigPath) == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ColorifyConfig>(), ConfigPath);
                Debug.Log($"[Colorify] Creado {ConfigPath}: aquí se ajustan tiempos, objetivos, colores y cartas.");
            }

            // 4. Escena principal (solo cámara: el juego se monta solo al arrancar)
            if (!File.Exists(ScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                var cam = camGo.GetComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = UIKit.Paper;
                camGo.transform.position = new Vector3(0, 0, -10);
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            else EditorSceneManager.OpenScene(ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log("[Colorify] Proyecto configurado. Pulsa Play para jugar. Trampas en el Editor: N = superar ronda, K = perder. " +
                      "Build web: COLORIFY > Build > Web: prueba rápida (solo PC, sin publicar).");
            EditorUtility.DisplayDialog("COLORIFY · Setup",
                "Proyecto listo.\n\n• Pulsa Play para jugar (vista Game en 1080×1920 o Simulator).\n" +
                "• Ajustes de diseño: Assets/Colorify/Resources/ColorifyConfig.\n" +
                "• En el Editor: N = superar ronda, K = perder.\n" +
                "• Build web: COLORIFY > Build.", "Vale");
        }

        private static void CreateFontAsset(string name)
        {
            string ttfPath = $"Assets/Colorify/Resources/Fonts/{name}.ttf";
            string assetPath = $"Assets/Colorify/Resources/Fonts/{name} SDF.asset";
            if (AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(assetPath) != null) return;
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null) { Debug.LogWarning($"[Colorify] No está {ttfPath}"); return; }
            try
            {
                var fa = TMPro.TMP_FontAsset.CreateFontAsset(font);
                fa.name = name + " SDF";
                AssetDatabase.CreateAsset(fa, assetPath);
                fa.material.name = name + " SDF Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                fa.atlasTextures[0].name = name + " SDF Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Colorify] Creada la fuente {assetPath}");
            }
            catch (Exception e) { Debug.LogWarning($"[Colorify] No se pudo crear {assetPath} (se generará al arrancar): {e.Message}"); }
        }

        // La API del importador de TMP no es pública en todas las versiones: se busca por reflexión
        private static void ImportTmpEssentials()
        {
            if (Resources.Load("TMP Settings") != null) return;
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("TMPro.TMP_PackageResourceImporter"))
                .FirstOrDefault(t => t != null);
            var method = type?.GetMethod("ImportResources", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method != null && method.GetParameters().Length == 3)
            {
                method.Invoke(null, new object[] { true, false, false });
                Debug.Log("[Colorify] Importados los recursos esenciales de TextMeshPro.");
                return;
            }
            if (!EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources"))
                Debug.LogWarning("[Colorify] Importa a mano: Window > TextMeshPro > Import TMP Essential Resources.");
        }

        [MenuItem("COLORIFY/Debug/Borrar guardado local", priority = 100)]
        public static void ClearSave()
        {
            PlayerPrefs.DeleteKey("colorify.save.v1");
            PlayerPrefs.Save();
            Debug.Log("[Colorify] Guardado local borrado (récord, historial y ajustes).");
        }

        // Comprobación rápida del generador de colores: imprime 5 tableros de cada plazo con su separación mínima real
        [MenuItem("COLORIFY/Debug/Probar dificultad de color", priority = 101)]
        public static void ProbeColors()
        {
            var cfg = ColorifyConfig.Load();
            var rng = new System.Random(1);
            for (int stage = 1; stage <= cfg.hardestColorStage + 1; stage++)
            {
                float d = cfg.ColorDifficulty(stage, 0);
                int n = cfg.GridSizeForStage(stage);
                float worst = float.MaxValue;
                for (int k = 0; k < 5; k++)
                {
                    var b = ColorBoardGenerator.Generate(cfg, n, d, f => cfg.families[f].spawnWeight, rng, cfg.Similarity(stage));
                    for (int i = 0; i < b.tiles.Count; i++)
                        if (i != b.correctIndex) worst = Mathf.Min(worst, OkColor.Distance(b.tiles[i], b.target));
                }
                Debug.Log($"[Colorify] Plazo {stage}: {n}×{n}, dificultad {d:0.00}, objetivo ronda 1 = {cfg.TargetFor(stage, 0)}, " +
                          $"tiempo ≈ {cfg.RoundTime((stage - 1) * cfg.roundsPerStage):0.0}s, distractor más parecido ΔE = {worst:0.000}");
            }
        }
    }
}
