using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Colorify.EditorTools
{
    // COLORIFY > Build > Android: instalar en el móvil (USB)
    // Compila un APK de prueba y lo instala y abre en el Android conectado por USB (como «Build and Run»).
    // Sin marca «Development Build» en pantalla, para poder sacar capturas limpias.
    // COLORIFY > Build > Android: APK para publishers
    // El mismo APK, guardado aparte con la versión y la fecha en el nombre (Builds/Android/Publishers), listo para
    // subirlo a Drive y mandar el enlace. Firmado con la clave de depuración de Unity: se instala en cualquier Android
    // con «Instalar apps desconocidas»; para Google Play haría falta un keystore propio.
    // Requisitos: módulo Android Build Support instalado en Unity y el móvil con la depuración USB activada.
    public static class ColorifyAndroidBuild
    {
        private static string OutputPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Android", "Colorify.apk"));

        private static string PublishersPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Android", "Publishers",
                $"Colorify-v{PlayerSettings.bundleVersion}-{System.DateTime.Now:yyyy-MM-dd}.apk"));

        [MenuItem("COLORIFY/Build/Android: APK para publishers", priority = 39)]
        public static void BuildForPublishers() => Build(run: false, forPublishers: true);

        [MenuItem("COLORIFY/Build/Android: instalar en el móvil (USB)", priority = 40)]
        public static void BuildAndRun() => Build(run: true);

        [MenuItem("COLORIFY/Build/Android: solo crear el APK", priority = 41)]
        public static void BuildApkOnly() => Build(run: false);

        private static void Build(bool run, bool forPublishers = false)
        {
            string outputPath = forPublishers ? PublishersPath : OutputPath;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("COLORIFY · Android",
                    "Falta el módulo Android Build Support en esta versión de Unity.\n\n" +
                    "Unity Hub > Installs > 6000.2.6f2 > Add modules > Android Build Support (con OpenJDK y Android SDK & NDK).", "Vale");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                bool ok = EditorUtility.DisplayDialog("COLORIFY · Android",
                    "Hay que cambiar la plataforma a Android. Unity reimportará los assets (puede tardar unos minutos).\n\n" +
                    "Al terminar se queda en Android; para la build web, COLORIFY > Build > Web vuelve a cambiarla.",
                    "Continuar", "Cancelar");
                if (!ok) return;
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogError("[AndroidBuild] No se pudo cambiar la plataforma a Android.");
                    return;
                }
            }
            else if (run && !EditorUtility.DisplayDialog("COLORIFY · Android",
                         "Conecta el móvil por USB con la depuración USB activada (Ajustes > Opciones de desarrollador) " +
                         "y acepta el aviso «¿Permitir depuración USB?» en el móvil.\n\nLa primera compilación tarda varios minutos.",
                         "Compilar e instalar", "Cancelar"))
                return;

            // Ajustes de la build de prueba: IL2CPP + ARM64 (funciona en todos los Android actuales), APK (no AAB)
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;   // sin marca «Development Build» en las capturas

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = run ? BuildOptions.AutoRunPlayer : BuildOptions.None,
            };
            if (options.scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("COLORIFY · Android", "No hay escenas en Build Settings. Ejecuta COLORIFY > Setup > Configurar proyecto.", "Vale");
                return;
            }

            Debug.Log($"[AndroidBuild] Compilando {PlayerSettings.productName} {PlayerSettings.bundleVersion} en {outputPath}…");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string size = $"{summary.totalSize / (1024f * 1024f):0.0} MB";
                Debug.Log($"[AndroidBuild] APK listo ({size}): {outputPath}");
                if (forPublishers)
                {
                    EditorUtility.DisplayDialog("COLORIFY · Android",
                        $"APK listo ({size}):\n{Path.GetFileName(outputPath)}\n\n" +
                        "Se abre la carpeta. Súbelo a Google Drive, compártelo con «Cualquier persona con el enlace» " +
                        "y pega ese enlace en los correos a los publishers.\n\n" +
                        "Unity se queda en Android: COLORIFY > Build > Web la devuelve a Web cuando la necesites.", "Vale");
                    EditorUtility.RevealInFinder(outputPath);
                }
                else if (run)
                    EditorUtility.DisplayDialog("COLORIFY · Android",
                        $"Instalado y abierto en el móvil ({size}).\n\n" +
                        "Si no se abre: comprueba el cable, la depuración USB y el aviso de permiso en el móvil, " +
                        "o usa «Android: solo crear el APK» y pásalo al móvil a mano.", "Vale");
                else
                    EditorUtility.RevealInFinder(outputPath);
                return;
            }

            Debug.LogError($"[AndroidBuild] La build ha fallado ({summary.result}, {summary.totalErrors} errores). Revisa la consola.");
            EditorUtility.DisplayDialog("COLORIFY · Android",
                "La build ha fallado. Causas habituales:\n" +
                "• No hay móvil conectado o no se aceptó la depuración USB (prueba «solo crear el APK»).\n" +
                "• Falta el SDK/NDK de Android (Unity Hub > Add modules).\n" +
                "• Keystore personalizado sin contraseña (Player Settings > Publishing Settings).\n\n" +
                "El detalle está en la consola.", "Vale");
        }
    }
}
