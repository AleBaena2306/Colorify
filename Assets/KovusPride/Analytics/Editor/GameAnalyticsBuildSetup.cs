#if KP_GAMEANALYTICS
using System;
using System.IO;
using GameAnalyticsSDK.Setup;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KovusPride.Analytics.EditorTools
{
    // Pone las claves de GameAnalytics solo mientras se compila para Android y las quita al terminar.
    // El repositorio es público: las claves viven fuera, en %USERPROFILE%\KovusPride\gameanalytics.json:
    //   { "gameKey": "…", "secretKey": "…" }
    // (o en las variables de entorno KP_GA_GAME_KEY y KP_GA_SECRET_KEY, para el PC de builds).
    // Antes de la build se crea Assets/Resources/GameAnalytics/Settings.asset con la plataforma Android y esas
    // claves; después se borra, para que no acabe en Git ni en las builds web. Sin claves, la build sigue sin
    // analítica y lo avisa en la consola.
    public class GameAnalyticsBuildSetup : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => -100;

        private const string Folder = "Assets/Resources/GameAnalytics";
        private const string AssetPath = Folder + "/Settings.asset";
        private static bool createdForBuild, keysWritten;

        [Serializable]
        private class Keys { public string gameKey = ""; public string secretKey = ""; }

        private static string KeysFile =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "KovusPride", "gameanalytics.json");

        public void OnPreprocessBuild(BuildReport report)
        {
            createdForBuild = false;
            if (report.summary.platform != BuildTarget.Android) return;
            var keys = LoadKeys(out string source);
            if (keys == null)
            {
                Debug.LogWarning($"[GameAnalytics] Sin claves ({KeysFile} no existe o está incompleto): esta build de Android va sin analítica.");
                return;
            }
            createdForBuild = !File.Exists(AssetPath);
            WriteSettings(keys);
            keysWritten = true;
            Debug.Log($"[GameAnalytics] Claves de Android puestas para la build ({source}).");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android || !keysWritten) return;
            keysWritten = false;
            if (createdForBuild)
            {
                AssetDatabase.DeleteAsset(AssetPath);
                if (Directory.Exists(Folder) && Directory.GetFileSystemEntries(Folder).Length == 0) AssetDatabase.DeleteAsset(Folder);
            }
            else
            {
                // El asset ya existía (lo puede crear el propio editor de GameAnalytics): se dejan las claves vacías
                var settings = AssetDatabase.LoadAssetAtPath<Settings>(AssetPath);
                int index = settings != null ? settings.Platforms.IndexOf(RuntimePlatform.Android) : -1;
                if (index >= 0)
                {
                    settings.UpdateGameKey(index, "");
                    settings.UpdateSecretKey(index, "");
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
            }
            createdForBuild = false;
            Debug.Log("[GameAnalytics] Claves quitadas del proyecto tras la build.");
        }

        [MenuItem("COLORIFY/Analytics/Comprobar claves de GameAnalytics")]
        private static void Check()
        {
            var keys = LoadKeys(out string source);
            EditorUtility.DisplayDialog("GameAnalytics",
                keys != null
                    ? $"Claves encontradas ({source}).\nGame Key: {keys.gameKey.Substring(0, 6)}…\n\nLas builds de Android llevarán GameAnalytics."
                    : $"No hay claves.\n\nCrea {KeysFile} con:\n{{ \"gameKey\": \"…\", \"secretKey\": \"…\" }}",
                "Vale");
        }

        private static Keys LoadKeys(out string source)
        {
            source = null;
            var env = new Keys
            {
                gameKey = Environment.GetEnvironmentVariable("KP_GA_GAME_KEY") ?? "",
                secretKey = Environment.GetEnvironmentVariable("KP_GA_SECRET_KEY") ?? "",
            };
            if (Valid(env)) { source = "variables de entorno"; return env; }
            try
            {
                if (File.Exists(KeysFile))
                {
                    var file = JsonUtility.FromJson<Keys>(File.ReadAllText(KeysFile));
                    if (Valid(file)) { source = "gameanalytics.json"; return file; }
                }
            }
            catch (Exception e) { Debug.LogWarning("[GameAnalytics] gameanalytics.json no se puede leer: " + e.Message); }
            return null;
        }

        private static bool Valid(Keys k) =>
            k != null && !string.IsNullOrWhiteSpace(k.gameKey) && !string.IsNullOrWhiteSpace(k.secretKey);

        private static void WriteSettings(Keys keys)
        {
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            var settings = AssetDatabase.LoadAssetAtPath<Settings>(AssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<Settings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
            }
            int index = settings.Platforms.IndexOf(RuntimePlatform.Android);
            if (index < 0)
            {
                settings.AddPlatform(RuntimePlatform.Android);
                index = settings.Platforms.Count - 1;
            }
            settings.UpdateGameKey(index, keys.gameKey.Trim());
            settings.UpdateSecretKey(index, keys.secretKey.Trim());
            settings.Build[index] = PlayerSettings.bundleVersion;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
