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
    // COLORIFY > Build > Android publisher > <Publisher>
    // APK con el SDK de ese publisher (símbolo KP_PUB_<PUBLISHER>, solo para esa build; no cambia los ajustes del
    // proyecto). El código de cada SDK está en Assets/KovusPride/Publishing/Mobile/Adapters. «Prueba: anuncios
    // simulados» instala por USB una build con anuncios falsos de 1 s para comprobar el flujo antes de tener ningún SDK.
    // Requisitos: módulo Android Build Support instalado en Unity y el móvil con la depuración USB activada.
    public static class ColorifyAndroidBuild
    {
        private static string OutputPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Android", "Colorify.apk"));

        private static string PublishersPath(string publisher) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Android", "Publishers",
                $"Colorify{(publisher != null ? "-" + publisher : "")}-v{PlayerSettings.bundleVersion}-{System.DateTime.Now:yyyy-MM-dd}.apk"));

        // ---------------- Builds con el SDK de un publisher ----------------
        private const string Pub = "COLORIFY/Build/Android publisher/";

        [MenuItem(Pub + "Prueba: anuncios simulados (instalar por USB)", priority = 50)]
        public static void BuildPublisherTest() => Build(run: true, define: "KP_PUB_TEST");

        [MenuItem(Pub + "CrazyLabs", priority = 61)] public static void BuildCrazyLabs() => BuildPublisher("CrazyLabs", "KP_PUB_CRAZYLABS");
        [MenuItem(Pub + "Homa", priority = 62)] public static void BuildHoma() => BuildPublisher("Homa", "KP_PUB_HOMA");
        [MenuItem(Pub + "Voodoo", priority = 63)] public static void BuildVoodoo() => BuildPublisher("Voodoo", "KP_PUB_VOODOO");
        [MenuItem(Pub + "Kwalee", priority = 64)] public static void BuildKwalee() => BuildPublisher("Kwalee", "KP_PUB_KWALEE");
        [MenuItem(Pub + "SayGames", priority = 65)] public static void BuildSayGames() => BuildPublisher("SayGames", "KP_PUB_SAYGAMES");
        [MenuItem(Pub + "Supersonic", priority = 66)] public static void BuildSupersonic() => BuildPublisher("Supersonic", "KP_PUB_SUPERSONIC");
        [MenuItem(Pub + "Tapnation", priority = 67)] public static void BuildTapnation() => BuildPublisher("Tapnation", "KP_PUB_TAPNATION");
        [MenuItem(Pub + "MondayOFF", priority = 68)] public static void BuildMondayOff() => BuildPublisher("MondayOFF", "KP_PUB_MONDAYOFF");
        [MenuItem(Pub + "Supercent", priority = 69)] public static void BuildSupercent() => BuildPublisher("Supercent", "KP_PUB_SUPERCENT");
        [MenuItem(Pub + "BoomBit", priority = 70)] public static void BuildBoomBit() => BuildPublisher("BoomBit", "KP_PUB_BOOMBIT");
        [MenuItem(Pub + "Rollic", priority = 71)] public static void BuildRollic() => BuildPublisher("Rollic", "KP_PUB_ROLLIC");
        [MenuItem(Pub + "Azur Games", priority = 72)] public static void BuildAzur() => BuildPublisher("Azur", "KP_PUB_AZUR");

        private static void BuildPublisher(string name, string define) =>
            Build(run: false, forPublishers: true, define: define, publisher: name);

        [MenuItem("COLORIFY/Build/Android: APK para publishers", priority = 39)]
        public static void BuildForPublishers() => Build(run: false, forPublishers: true);

        [MenuItem("COLORIFY/Build/Android: instalar en el móvil (USB)", priority = 40)]
        public static void BuildAndRun() => Build(run: true);

        [MenuItem("COLORIFY/Build/Android: solo crear el APK", priority = 41)]
        public static void BuildApkOnly() => Build(run: false);

        private static void Build(bool run, bool forPublishers = false, string define = null, string publisher = null)
        {
            string outputPath = forPublishers ? PublishersPath(publisher) : OutputPath;
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
                extraScriptingDefines = define != null ? new[] { define } : new string[0],
            };
            if (options.scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("COLORIFY · Android", "No hay escenas en Build Settings. Ejecuta COLORIFY > Setup > Configurar proyecto.", "Vale");
                return;
            }

            Debug.Log($"[AndroidBuild] Compilando {PlayerSettings.productName} {PlayerSettings.bundleVersion}" +
                      (define != null ? $" con {define}" : "") + $" en {outputPath}…");
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
