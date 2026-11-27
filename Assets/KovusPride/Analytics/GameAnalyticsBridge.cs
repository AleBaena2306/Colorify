using System;
using System.Collections.Generic;
using UnityEngine;
#if KP_GAMEANALYTICS
using GameAnalyticsSDK;
#endif

namespace KovusPride.Analytics
{
    // GameAnalytics en las builds de Android (pedido de BoomBit, 26/11/2026: D1, D7 y tiempo de juego por jugador).
    // · Sesiones, retención (D1, D7…) y tiempo de juego los calcula GameAnalytics solo, con iniciarlo.
    // · Cada evento del juego (Colorify.Analytics.Log) se reenvía aquí:
    //     game_start / game_end → evento de progresión (Start / Complete / Fail) con el modo como progresión
    //                             y la puntuación de la partida;
    //     el resto → evento de diseño «nombre» o «nombre:valor» con el primer parámetro de texto que lo distinga.
    // · Las claves no están en el repositorio (es público): las pone la build de Android en
    //   Resources/GameAnalytics/Settings.asset a partir de %USERPROFILE%\KovusPride\gameanalytics.json y las borra
    //   al terminar (KovusPride.Analytics.EditorTools.GameAnalyticsBuildSetup).
    // · Si el paquete com.gameanalytics.sdk no está instalado, esta clase compila igual y no hace nada.
    public static class GameAnalyticsBridge
    {
        public static bool Active { get; private set; }

#if KP_GAMEANALYTICS && UNITY_ANDROID && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            try
            {
                var settings = Resources.Load<GameAnalyticsSDK.Setup.Settings>("GameAnalytics/Settings");
                int index = settings != null ? settings.Platforms.IndexOf(RuntimePlatform.Android) : -1;
                if (index < 0 || string.IsNullOrEmpty(settings.GetGameKey(index)))
                {
                    Debug.LogWarning("[GameAnalytics] Esta build no lleva claves de Android: sin analítica.");
                    return;
                }
                if (UnityEngine.Object.FindAnyObjectByType<GameAnalytics>() == null)
                {
                    var go = new GameObject("GameAnalytics");
                    go.AddComponent<GameAnalytics>();
                    UnityEngine.Object.DontDestroyOnLoad(go);
                }
                GameAnalytics.Initialize();
                Active = true;
            }
            catch (Exception e) { Debug.LogWarning("[GameAnalytics] " + e.Message); }
        }
#endif

        // Lo llama Colorify.Analytics.Log con cada evento
        public static void Event(string name, IReadOnlyList<(string key, object value)> parameters)
        {
            if (!Active) return;
#if KP_GAMEANALYTICS
            try
            {
                var p = new Dictionary<string, object>();
                foreach (var kv in parameters) p[kv.key] = kv.value;
                string mode = Text(p, "mode");

                if (name == "game_start" && mode != null)
                {
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, Clean(mode));
                    return;
                }
                if (name == "game_end" && mode != null)
                {
                    string result = Text(p, "result") ?? "";
                    // Partida sin fin (Normal): terminar por tiempo cuenta como completada; abandonar es un fallo.
                    // Online y carreras: ganar = completada, perder = fallo.
                    bool failed = result == "quit" || result == "lose" || result == "loss";
                    int score = Number(p, "points") ?? Number(p, "score") ?? Number(p, "total_score") ?? Number(p, "hits") ?? 0;
                    GameAnalytics.NewProgressionEvent(failed ? GAProgressionStatus.Fail : GAProgressionStatus.Complete,
                                                      Clean(mode), score);
                    return;
                }

                // Eventos de diseño: «nombre» o «nombre:valor» (máximo 5 partes, 64 caracteres cada una)
                string detail = Text(p, "step") ?? Text(p, "theme") ?? Text(p, "emblem") ?? Text(p, "color") ??
                                Text(p, "placement") ?? Text(p, "store") ?? Text(p, "card_type");
                string id = detail != null ? Clean(name) + ":" + Clean(detail) : Clean(name);
                int? value = Number(p, "seconds") ?? Number(p, "duration_sec");
                if (value.HasValue) GameAnalytics.NewDesignEvent(id, value.Value);
                else GameAnalytics.NewDesignEvent(id);
            }
            catch (Exception e) { Debug.LogWarning("[GameAnalytics] " + e.Message); }
#endif
        }

        private static string Text(Dictionary<string, object> p, string key) =>
            p.TryGetValue(key, out var v) && v != null && !(v is bool) && !(v is int) && !(v is long) && !(v is float) && !(v is double)
                ? v.ToString() : null;

        private static int? Number(Dictionary<string, object> p, string key)
        {
            if (!p.TryGetValue(key, out var v) || v == null) return null;
            if (v is int || v is long || v is float || v is double) return (int)Math.Round(Convert.ToDouble(v));
            return null;
        }

        // GameAnalytics solo admite letras, números, guiones y guiones bajos en cada parte del ID
        private static string Clean(string s)
        {
            var chars = new char[Math.Min(s.Length, 64)];
            for (int i = 0; i < chars.Length; i++)
            {
                char c = s[i];
                chars[i] = char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_';
            }
            return new string(chars);
        }
    }
}
