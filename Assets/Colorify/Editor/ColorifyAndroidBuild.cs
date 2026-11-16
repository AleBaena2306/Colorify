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
    // COLORIFY > Build > Android publisher > ★ Todas las builds de publishers
    // Botón maestro: compila una tras otra las builds de todos los publishers, cada una en su carpeta
    // (Builds/Android/Publishers/<Publisher>/), sin preguntar nada entre medias. Si una falla, sigue con las demás y
    // al final muestra un resumen y abre la carpeta. No necesita internet si ya se ha compilado antes para Android y
    // los SDK no han cambiado (Gradle tira de su caché); la primera build tras importar un SDK nuevo sí lo necesita.
    // Requisitos: módulo Android Build Support instalado en Unity y el móvil con la depuración USB activada.
    public static class ColorifyAndroidBuild
    {
        private static string OutputPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Android", "Colorify.apk"));

        private static string PublishersRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Android", "Publishers"));

        // Cada publisher en su carpeta: Builds/Android/Publishers/<Publisher>/Colorify-<Publisher>-v…-fecha.apk
        // (el APK genérico, sin SDK, en Builds/Android/Publishers/Generico/).
        private static string PublishersPath(string publisher) =>
            Path.Combine(PublishersRoot, publisher ?? "Generico",
                $"Colorify{(publisher != null ? "-" + publisher : "")}-v{PlayerSettings.bundleVersion}-{System.DateTime.Now:yyyy-MM-dd}.apk");

        // ---------------- Builds con el SDK de un publisher ----------------
        private const string Pub = "COLORIFY/Build/Android publisher/";

        [MenuItem(Pub + "Prueba: anuncios simulados (instalar por USB)", priority = 50)]
        public static void BuildPublisherTest() => Build(run: true, define: "KP_PUB_TEST");

        // Lista única de publishers: la usan los botones sueltos y el botón maestro (añadir aquí los nuevos).
        private static readonly (string name, string define)[] Publishers =
        {
            ("CrazyLabs", "KP_PUB_CRAZYLABS"),
            ("Homa", "KP_PUB_HOMA"),
            ("Voodoo", "KP_PUB_VOODOO"),
            ("Kwalee", "KP_PUB_KWALEE"),
            ("SayGames", "KP_PUB_SAYGAMES"),
            ("Supersonic", "KP_PUB_SUPERSONIC"),
            ("Tapnation", "KP_PUB_TAPNATION"),
            ("MondayOFF", "KP_PUB_MONDAYOFF"),
            ("Supercent", "KP_PUB_SUPERCENT"),
            ("BoomBit", "KP_PUB_BOOMBIT"),
            ("Rollic", "KP_PUB_ROLLIC"),
            ("Azur", "KP_PUB_AZUR"),
        };

        [MenuItem(Pub + "CrazyLabs", priority = 61)] public static void BuildCrazyLabs() => BuildPublisher("CrazyLabs");
        [MenuItem(Pub + "Homa", priority = 62)] public static void BuildHoma() => BuildPublisher("Homa");
        [MenuItem(Pub + "Voodoo", priority = 63)] public static void BuildVoodoo() => BuildPublisher("Voodoo");
        [MenuItem(Pub + "Kwalee", priority = 64)] public static void BuildKwalee() => BuildPublisher("Kwalee");
        [MenuItem(Pub + "SayGames", priority = 65)] public static void BuildSayGames() => BuildPublisher("SayGames");
        [MenuItem(Pub + "Supersonic", priority = 66)] public static void BuildSupersonic() => BuildPublisher("Supersonic");
        [MenuItem(Pub + "Tapnation", priority = 67)] public static void BuildTapnation() => BuildPublisher("Tapnation");
        [MenuItem(Pub + "MondayOFF", priority = 68)] public static void BuildMondayOff() => BuildPublisher("MondayOFF");
        [MenuItem(Pub + "Supercent", priority = 69)] public static void BuildSupercent() => BuildPublisher("Supercent");
        [MenuItem(Pub + "BoomBit", priority = 70)] public static void BuildBoomBit() => BuildPublisher("BoomBit");
        [MenuItem(Pub + "Rollic", priority = 71)] public static void BuildRollic() => BuildPublisher("Rollic");
        [MenuItem(Pub + "Azur Games", priority = 72)] public static void BuildAzur() => BuildPublisher("Azur");

        private static void BuildPublisher(string name) =>
            Build(run: false, forPublishers: true, define: Publishers.First(p => p.name == name).define, publisher: name);

        // ---------------- Botón maestro ----------------
        [MenuItem(Pub + "★ Todas las builds de publishers", priority = 45)]
        public static void BuildAllPublishers()
        {
            if (!CheckAndroidModule()) return;
            bool needsSwitch = EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android;
            if (!EditorUtility.DisplayDialog("COLORIFY · Todas las builds",
                    $"Se van a compilar {Publishers.Length} APK, uno por publisher, cada uno en su carpeta:\n" +
                    "Builds/Android/Publishers/<Publisher>/\n\n" +
                    "Tarda bastante (la primera varios minutos; las demás algo menos). Unity no se podrá usar mientras tanto " +
                    "y no hace falta que esté delante: si una falla, sigue con las demás y al final verás un resumen." +
                    (needsSwitch ? "\n\nAntes se cambia la plataforma a Android (reimporta los assets)." : ""),
                    "Compilar todas", "Cancelar"))
                return;
            if (!SwitchToAndroid()) return;

            var start = System.DateTime.Now;
            var ok = new System.Collections.Generic.List<string>();
            var failed = new System.Collections.Generic.List<string>();
            for (int i = 0; i < Publishers.Length; i++)
            {
                var (name, define) = Publishers[i];
                Debug.Log($"[AndroidBuild] ===== {i + 1}/{Publishers.Length}: {name} =====");
                string path = PublishersPath(name);
                var (succeeded, size) = RunBuild(path, define, run: false);
                if (succeeded) ok.Add($"• {name} ({size})");
                else failed.Add($"• {name}");
            }
            EditorUtility.ClearProgressBar();

            var minutes = (System.DateTime.Now - start).TotalMinutes;
            string msg = $"Terminado en {minutes:0} min.\n\n" +
                         $"Listas ({ok.Count}/{Publishers.Length}):\n{string.Join("\n", ok)}" +
                         (failed.Count > 0 ? $"\n\nHan fallado:\n{string.Join("\n", failed)}\n(El motivo está en la consola.)" : "") +
                         "\n\nSe abre la carpeta Publishers. Unity se queda en Android.";
            Debug.Log("[AndroidBuild] " + msg.Replace("\n", " "));
            EditorUtility.DisplayDialog("COLORIFY · Todas las builds", msg, "Vale");
            EditorUtility.RevealInFinder(PublishersRoot);
        }

        [MenuItem("COLORIFY/Build/Android: APK para publishers", priority = 39)]
        public static void BuildForPublishers() => Build(run: false, forPublishers: true);

        [MenuItem("COLORIFY/Build/Android: instalar en el móvil (USB)", priority = 40)]
        public static void BuildAndRun() => Build(run: true);

        [MenuItem("COLORIFY/Build/Android: solo crear el APK", priority = 41)]
        public static void BuildApkOnly() => Build(run: false);

        private static bool CheckAndroidModule()
        {
            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android)) return true;
            EditorUtility.DisplayDialog("COLORIFY · Android",
                "Falta el módulo Android Build Support en esta versión de Unity.\n\n" +
                "Unity Hub > Installs > 6000.2.6f2 > Add modules > Android Build Support (con OpenJDK y Android SDK & NDK).", "Vale");
            return false;
        }

        private static bool SwitchToAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return true;
            if (EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android)) return true;
            Debug.LogError("[AndroidBuild] No se pudo cambiar la plataforma a Android.");
            return false;
        }

        private static void Build(bool run, bool forPublishers = false, string define = null, string publisher = null)
        {
            string outputPath = forPublishers ? PublishersPath(publisher) : OutputPath;
            if (!CheckAndroidModule()) return;

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                bool ok = EditorUtility.DisplayDialog("COLORIFY · Android",
                    "Hay que cambiar la plataforma a Android. Unity reimportará los assets (puede tardar unos minutos).\n\n" +
                    "Al terminar se queda en Android; para la build web, COLORIFY > Build > Web vuelve a cambiarla.",
                    "Continuar", "Cancelar");
                if (!ok || !SwitchToAndroid()) return;
            }
            else if (run && !EditorUtility.DisplayDialog("COLORIFY · Android",
                         "Conecta el móvil por USB con la depuración USB activada (Ajustes > Opciones de desarrollador) " +
                         "y acepta el aviso «¿Permitir depuración USB?» en el móvil.\n\nLa primera compilación tarda varios minutos.",
                         "Compilar e instalar", "Cancelar"))
                return;

            if (EditorBuildSettings.scenes.All(sc => !sc.enabled))
            {
                EditorUtility.DisplayDialog("COLORIFY · Android", "No hay escenas en Build Settings. Ejecuta COLORIFY > Setup > Configurar proyecto.", "Vale");
                return;
            }

            var (succeeded, size) = RunBuild(outputPath, define, run);
            if (succeeded)
            {
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

            EditorUtility.DisplayDialog("COLORIFY · Android",
                "La build ha fallado. Causas habituales:\n" +
                "• No hay móvil conectado o no se aceptó la depuración USB (prueba «solo crear el APK»).\n" +
                "• Falta el SDK/NDK de Android (Unity Hub > Add modules).\n" +
                "• Keystore personalizado sin contraseña (Player Settings > Publishing Settings).\n\n" +
                "El detalle está en la consola.", "Vale");
        }

        // Compila sin diálogos (lo usan los botones sueltos y el maestro). Devuelve si salió bien y el tamaño.
        private static (bool succeeded, string size) RunBuild(string outputPath, string define, bool run)
        {
            // Ajustes de la build: IL2CPP + ARM64 (funciona en todos los Android actuales), APK (no AAB)
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
                Debug.LogError("[AndroidBuild] No hay escenas en Build Settings.");
                return (false, null);
            }

            Debug.Log($"[AndroidBuild] Compilando {PlayerSettings.productName} {PlayerSettings.bundleVersion}" +
                      (define != null ? $" con {define}" : "") + $" en {outputPath}…");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                string size = $"{summary.totalSize / (1024f * 1024f):0.0} MB";
                Debug.Log($"[AndroidBuild] APK listo ({size}): {outputPath}");
                return (true, size);
            }
            Debug.LogError($"[AndroidBuild] La build ha fallado ({summary.result}, {summary.totalErrors} errores): {outputPath}");
            return (false, null);
        }
    }
}
