using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Colorify.EditorTools
{
    // COLORIFY > Build > Android: AAB para Google Play
    // Compila el Android App Bundle (.aab) que se sube a Play Console (prueba cerrada, abierta o producción):
    //   · Firmado con la clave de subida del estudio (la misma de MASSBOUND vale: Play App Signing crea una clave
    //     de firma distinta para cada app). Los datos de la clave se leen, por este orden, de:
    //       1. Variables de entorno KP_ANDROID_KEYSTORE, KP_ANDROID_KEYSTORE_PASS, KP_ANDROID_KEYALIAS y
    //          KP_ANDROID_KEYALIAS_PASS (PC de builds / GitHub Actions).
    //       2. El archivo %USERPROFILE%\KovusPride\android-signing.json, fuera del proyecto y de Git. Si no existe,
    //          se crea una plantilla, se abre su carpeta y se explica qué rellenar.
    //       3. Lo que ya esté puesto en Player Settings > Publishing Settings en esta sesión de Unity.
    //   · versionCode automático y siempre creciente: minutos desde el 1/1/2026 (UTC). No hay que tocarlo a mano y
    //     dos builds nunca repiten número. El nombre de versión es Player Settings > Version (bundleVersion).
    //   · API mínima 24 (la exige la protección automática de Play) y API objetivo 36 (Android 16), obligatoria en Google Play para apps nuevas y actualizaciones desde el
    //     31/08/2026. IL2CPP con ARMv7 + ARM64 (Play exige 64 bits; ARMv7 cubre móviles antiguos de testers).
    //   · Icono: Assets/Colorify/Art/AppIcon (icono normal y adaptativo con fondo y primer plano).
    //   · Sin SDK de publisher ni anuncios (ningún símbolo KP_PUB_*). El online y el ranking funcionan igual que en
    //     la web; el inicio de sesión con Google abre el navegador.
    // Al terminar deja la firma como estaba, para que las demás builds de Android (APK de prueba o de publishers)
    // sigan funcionando sin contraseñas. Salida: Builds/Android/GooglePlay/Colorify-v<versión>-<versionCode>.aab
    public static class ColorifyGooglePlayBuild
    {
        private const string Title = "COLORIFY · Google Play";
        private const int TargetApi = 36;
        private const int MinApi = 24;
        private const string IconFolder = "Assets/Colorify/Art/AppIcon/";
        private static readonly DateTime VersionEpoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static string OutputRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Android", "GooglePlay"));

        private static string SigningFile =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "KovusPride", "android-signing.json");

        [Serializable]
        private class Signing
        {
            public string keystore = "";
            public string keystorePass = "";
            public string alias = "";
            public string aliasPass = "";
        }

        [MenuItem("COLORIFY/Build/Android: AAB para Google Play", priority = 38)]
        public static void BuildAab()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog(Title,
                    "Falta el módulo Android Build Support en esta versión de Unity.\n\n" +
                    "Unity Hub > Installs > 6000.2.6f2 > Add modules > Android Build Support (con OpenJDK y Android SDK & NDK).", "Vale");
                return;
            }
            if (EditorBuildSettings.scenes.All(s => !s.enabled))
            {
                EditorUtility.DisplayDialog(Title, "No hay escenas en Build Settings. Ejecuta COLORIFY > Setup > Configurar proyecto.", "Vale");
                return;
            }

            var signing = LoadSigning(out string signingSource);
            if (signing == null) return;   // ya se ha explicado qué falta

            int versionCode = Math.Max(PlayerSettings.Android.bundleVersionCode + 1,
                                       (int)(DateTime.UtcNow - VersionEpoch).TotalMinutes);
            string version = PlayerSettings.bundleVersion;
            bool needsSwitch = EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android;
            if (!EditorUtility.DisplayDialog(Title,
                    $"Se va a compilar el AAB para Google Play:\n\n" +
                    $"• Versión {version} (versionCode {versionCode})\n" +
                    $"• API objetivo {TargetApi}, mínima {MinApi}, IL2CPP, ARMv7 + ARM64\n" +
                    $"• Firma: {Path.GetFileName(signing.keystore)} · alias «{signing.alias}» ({signingSource})\n\n" +
                    "Tarda varios minutos." +
                    (needsSwitch ? "\n\nAntes se cambia la plataforma a Android (Unity reimporta los assets). " +
                                   "Al terminar se queda en Android; COLORIFY > Build > Web la devuelve a Web." : ""),
                    "Compilar", "Cancelar"))
                return;

            if (needsSwitch && !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[GooglePlay] No se pudo cambiar la plataforma a Android.");
                return;
            }

            // Guardar lo que se toca para dejarlo igual al terminar (salvo versionCode, API e icono, que se quedan)
            bool prevCustom = PlayerSettings.Android.useCustomKeystore;
            string prevKeystore = PlayerSettings.Android.keystoreName, prevAlias = PlayerSettings.Android.keyaliasName;
            string prevKeystorePass = PlayerSettings.Android.keystorePass, prevAliasPass = PlayerSettings.Android.keyaliasPass;
            var prevArch = PlayerSettings.Android.targetArchitectures;
            bool prevBundle = EditorUserBuildSettings.buildAppBundle;

            string outputPath = Path.Combine(OutputRoot, $"Colorify-v{version}-{versionCode}.aab");
            try
            {
                ApplyAppIcon();
                var android = NamedBuildTarget.Android;
                PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetApi;
                // La protección automática de Play (Play Auto Protect) exige minSdk 24 o más
                if ((int)PlayerSettings.Android.minSdkVersion < MinApi)
                    PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinApi;
                PlayerSettings.Android.bundleVersionCode = versionCode;
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = signing.keystore;
                PlayerSettings.Android.keystorePass = signing.keystorePass;
                PlayerSettings.Android.keyaliasName = signing.alias;
                PlayerSettings.Android.keyaliasPass = signing.aliasPass;
                EditorUserBuildSettings.buildAppBundle = true;
                EditorUserBuildSettings.development = false;

                Directory.CreateDirectory(OutputRoot);
                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                    extraScriptingDefines = new string[0],
                };
                Debug.Log($"[GooglePlay] Compilando Colorify {version} ({versionCode}) en {outputPath}…");
                BuildReport report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                if (summary.result == BuildResult.Succeeded)
                {
                    long bytes = File.Exists(outputPath) ? new FileInfo(outputPath).Length : (long)summary.totalSize;
                    string size = $"{bytes / (1024f * 1024f):0.0} MB";
                    Debug.Log($"[GooglePlay] AAB listo ({size}): {outputPath}");
                    AssetDatabase.SaveAssets();   // guarda el versionCode en ProjectSettings
                    EditorUtility.DisplayDialog(Title,
                        $"AAB listo ({size}):\n{Path.GetFileName(outputPath)}\n\n" +
                        "Play Console > Colorify > Prueba > Prueba cerrada > Crear versión > subir este archivo.\n\n" +
                        "Se abre la carpeta. Unity se queda en Android.", "Vale");
                    EditorUtility.RevealInFinder(outputPath);
                }
                else
                {
                    Debug.LogError($"[GooglePlay] La build ha fallado ({summary.result}, {summary.totalErrors} errores).");
                    EditorUtility.DisplayDialog(Title,
                        "La build ha fallado. Causas habituales:\n" +
                        "• Contraseña o alias de la clave incorrectos (revisa android-signing.json).\n" +
                        $"• Falta la plataforma de Android API {TargetApi} en el SDK de Unity (Unity Hub > Add modules > Android SDK).\n" +
                        "• Falta el SDK/NDK de Android.\n\nEl detalle está en la consola.", "Vale");
                }
            }
            finally
            {
                // Dejar la firma y la arquitectura como estaban: las contraseñas no se quedan en el proyecto
                PlayerSettings.Android.useCustomKeystore = prevCustom;
                PlayerSettings.Android.keystoreName = prevKeystore;
                PlayerSettings.Android.keyaliasName = prevAlias;
                PlayerSettings.Android.keystorePass = prevKeystorePass;
                PlayerSettings.Android.keyaliasPass = prevAliasPass;
                PlayerSettings.Android.targetArchitectures = prevArch;
                EditorUserBuildSettings.buildAppBundle = prevBundle;
            }
        }

        // ---------------- Firma ----------------
        private static Signing LoadSigning(out string source)
        {
            source = null;
            var env = new Signing
            {
                keystore = Environment.GetEnvironmentVariable("KP_ANDROID_KEYSTORE") ?? "",
                keystorePass = Environment.GetEnvironmentVariable("KP_ANDROID_KEYSTORE_PASS") ?? "",
                alias = Environment.GetEnvironmentVariable("KP_ANDROID_KEYALIAS") ?? "",
                aliasPass = Environment.GetEnvironmentVariable("KP_ANDROID_KEYALIAS_PASS") ?? "",
            };
            if (Complete(env)) { source = "variables de entorno"; return Checked(env); }

            if (File.Exists(SigningFile))
            {
                Signing file = null;
                try { file = JsonUtility.FromJson<Signing>(File.ReadAllText(SigningFile)); }
                catch (Exception e) { Debug.LogWarning("[GooglePlay] android-signing.json no se puede leer: " + e.Message); }
                if (file != null && Complete(file)) { source = "android-signing.json"; return Checked(file); }
            }

            var player = new Signing
            {
                keystore = PlayerSettings.Android.keystoreName ?? "",
                keystorePass = PlayerSettings.Android.keystorePass ?? "",
                alias = PlayerSettings.Android.keyaliasName ?? "",
                aliasPass = PlayerSettings.Android.keyaliasPass ?? "",
            };
            if (PlayerSettings.Android.useCustomKeystore && Complete(player)) { source = "Player Settings"; return Checked(player); }

            // Nada configurado: crear la plantilla fuera del proyecto y explicar qué rellenar
            if (!File.Exists(SigningFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SigningFile));
                File.WriteAllText(SigningFile, JsonUtility.ToJson(GuessFromMassbound(), true));
            }
            EditorUtility.DisplayDialog(Title,
                "Falta la clave para firmar el AAB.\n\n" +
                $"Abre {SigningFile} (se abre su carpeta) y rellena:\n" +
                "• keystore y alias: la ruta del .keystore y el nombre de la clave. Si ya aparecen (copiados de " +
                "MASSBOUND), déjalos.\n" +
                "• keystorePass: contraseña del keystore\n" +
                "• aliasPass: contraseña de la clave (la misma que pones en MASSBOUND)\n\n" +
                "El archivo está fuera del proyecto y de Git. Guárdalo y vuelve a pulsar «AAB para Google Play».\n\n" +
                "Alternativa: rellenar Player Settings > Publishing Settings (Custom Keystore) en esta sesión de Unity.", "Vale");
            EditorUtility.RevealInFinder(SigningFile);
            return null;
        }

        // Ruta del keystore y alias de MASSBOUND (carpeta hermana MassboundBU) para rellenar la plantilla;
        // las contraseñas nunca están en el proyecto, así que esas siempre hay que escribirlas.
        private static Signing GuessFromMassbound()
        {
            var guess = new Signing();
            try
            {
                string settings = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "MassboundBU", "ProjectSettings", "ProjectSettings.asset"));
                if (File.Exists(settings))
                    foreach (var raw in File.ReadAllLines(settings))
                    {
                        string line = raw.Trim();
                        if (line.StartsWith("AndroidKeystoreName:")) guess.keystore = line.Substring("AndroidKeystoreName:".Length).Trim();
                        else if (line.StartsWith("AndroidKeyaliasName:")) guess.alias = line.Substring("AndroidKeyaliasName:".Length).Trim();
                    }
            }
            catch (Exception e) { Debug.LogWarning("[GooglePlay] No se pudo leer la configuración de MASSBOUND: " + e.Message); }
            if (string.IsNullOrEmpty(guess.keystore))
            {
                string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "keystore1.keystore");
                if (File.Exists(home)) guess.keystore = home.Replace('\\', '/');
            }
            return guess;
        }

        private static bool Complete(Signing s) =>
            s != null && !string.IsNullOrEmpty(s.keystore) && !string.IsNullOrEmpty(s.keystorePass) &&
            !string.IsNullOrEmpty(s.alias) && !string.IsNullOrEmpty(s.aliasPass);

        private static Signing Checked(Signing s)
        {
            if (File.Exists(s.keystore)) return s;
            EditorUtility.DisplayDialog(Title, $"No se encuentra el keystore:\n{s.keystore}\n\nRevisa la ruta en android-signing.json.", "Vale");
            return null;
        }

        // ---------------- Icono ----------------
        // Icono normal (todas las plataformas) y, en Android, icono adaptativo (fondo + primer plano) y redondo.
        private static void ApplyAppIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconFolder + "AppIcon.png");
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(IconFolder + "AppIcon-Background.png");
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(IconFolder + "AppIcon-Foreground.png");
            if (icon == null)
            {
                Debug.LogWarning("[GooglePlay] No está " + IconFolder + "AppIcon.png: se queda el icono actual.");
                return;
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            var android = NamedBuildTarget.Android;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(android))
            {
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                bool adaptive = kind.ToString().IndexOf("Adaptive", StringComparison.OrdinalIgnoreCase) >= 0;
                foreach (var platformIcon in icons)
                {
                    if (adaptive && background != null && foreground != null && platformIcon.maxLayerCount >= 2)
                        platformIcon.SetTextures(background, foreground);
                    else
                        platformIcon.SetTexture(icon);
                }
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
        }
    }
}
