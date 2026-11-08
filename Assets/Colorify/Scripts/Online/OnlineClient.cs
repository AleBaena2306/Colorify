using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Colorify.Online
{
    // Mensajes del servidor (worker/colorify/lobby.js del portfolio). Un solo tipo con todos los campos
    // porque JsonUtility no admite tipos variables; «t» dice cuál es.
    [Serializable]
    public class NetOpp { public string name; public bool ghost, bot; public int best, trophies; }

    [Serializable]
    public class NetPlayer { public int slot; public string name; public int score, pts, place; public bool host, me, bot; }

    [Serializable]
    public class NetMsg
    {
        public string t;          // welcome, queued, noghost, match, opp, result, pong, error
        public int online, waiting;
        public int seed, lead;    // match
        public NetOpp opp;        // match
        public int n;             // opp: aciertos del rival
        public float tl = -1f;    // opp: tiempo que le queda (−1 si no se sabe, p. ej. fantasma)
        public bool over;         // opp: se ha quedado sin tiempo
        public string r, reason;  // result: win/lose/draw · score/left/time
        public int you, them;     // result
        public string code;       // error
        // Modo Carrera (race.js)
        public string room, state;          // room: código y estado (lobby/playing)
        public int max, rounds;             // room
        public bool pub, host;              // room: pública · soy el anfitrión
        public List<NetPlayer> players = new List<NetPlayer>();
        public int round, total, size, limit, slot, place, pts;   // rstart / rhit / rend
        public float diff;                  // rstart: dificultad de color 0→1
        public int shape, fx;               // rstart: forma de las fichas y efecto de la muestra (Specials.cs)
        public int trophies, delta;         // welcome / trophies: total y cambio
    }

    // GET /api/colorify/tuning: lo aprendido de todas las partidas
    [Serializable]
    public class TuningCode { public int c; public float ms, miss, n; }

    [Serializable]
    public class TuningResponse { public bool ok; public float pace = 1f; public List<TuningCode> codes = new List<TuningCode>(); }

    [Serializable]
    public class ScoreRow { public int rank; public string name; public long score; public string info; public bool me; }

    [Serializable]
    public class MyRank { public int rank; public long best; }

    [Serializable]
    public class ScoresResponse { public bool ok; public string mode; public int total; public List<ScoreRow> top = new List<ScoreRow>(); public MyRank me; public string error; }

    [Serializable]
    public class SubmitResponse { public bool ok; public int rank; public long best; public string error; }

    // Conexión con el servidor de Kovu's Pride en el portfolio: ranking global (HTTP) y duelos (WebSocket).
    public static class OnlineApi
    {
        public const int Protocol = 1;

        // En la web se usa el mismo dominio desde el que se juega (así también vale en vistas previas);
        // en el editor, en Android o en una prueba local, el del portfolio.
        public static string BaseUrl(ColorifyConfig cfg)
        {
            string fallback = string.IsNullOrEmpty(cfg.onlineBaseUrl) ? "https://alejandrobaenaportfolio.com" : cfg.onlineBaseUrl.TrimEnd('/');
#if UNITY_WEBGL && !UNITY_EDITOR
            // En los portales (CrazyGames, Playgama…) el juego está en otro dominio: el servidor es siempre el del
            // portfolio. En el portfolio se usa la misma web (vale para www, sin www y las vistas previas)
            if (KovusPride.Publishing.StorePlatform.IsPortal) return fallback;
            try
            {
                var u = new Uri(Application.absoluteURL);
                if ((u.Scheme == "https" || u.Scheme == "http") && u.Host != "localhost" && u.Host != "127.0.0.1")
                    return u.GetLeftPart(UriPartial.Authority);
            }
            catch (Exception) { }
#endif
            return fallback;
        }

        public static string SocketUrl(ColorifyConfig cfg)
        {
            string b = BaseUrl(cfg);
            return (b.StartsWith("https") ? "wss" + b.Substring(5) : "ws" + b.Substring(4)) + "/api/colorify/play";
        }

        public static IEnumerator GetScores(ColorifyConfig cfg, string mode, string player, Action<ScoresResponse> done)
        {
            string url = BaseUrl(cfg) + "/api/colorify/scores?mode=" + mode + "&player=" + UnityWebRequest.EscapeURL(player ?? "");
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                ScoresResponse r = null;
                if (req.result == UnityWebRequest.Result.Success)
                    try { r = JsonUtility.FromJson<ScoresResponse>(req.downloadHandler.text); } catch (Exception) { }
                done?.Invoke(r);
            }
        }

        public static IEnumerator GetTuning(ColorifyConfig cfg, Action<TuningResponse> done)
        {
            using (var req = UnityWebRequest.Get(BaseUrl(cfg) + "/api/colorify/tuning"))
            {
                req.timeout = 8;
                yield return req.SendWebRequest();
                TuningResponse r = null;
                if (req.result == UnityWebRequest.Result.Success)
                    try { r = JsonUtility.FromJson<TuningResponse>(req.downloadHandler.text); } catch (Exception) { }
                done?.Invoke(r);
            }
        }

        // samples: [tamaño, ms, tamaño, ms, …] de cada acierto y misses: [tamaño, fallos, …], para que el bot aprenda
        public static IEnumerator SubmitScore(ColorifyConfig cfg, string player, string name, string mode, long score, string info,
            List<int> samples, List<int> misses, Action<SubmitResponse> done, int playMs = 0, string end = null)
        {
            var sb = new StringBuilder("{");
            sb.Append("\"player\":\"").Append(player).Append("\",\"name\":\"").Append(Escape(name)).Append("\",\"mode\":\"").Append(mode)
              .Append("\",\"score\":").Append(score).Append(",\"info\":\"").Append(Escape(info)).Append('"');
            // Duración jugada y cómo acabó (time / quit): el servidor ajusta el ritmo para partidas de ~5 minutos
            if (playMs > 0) sb.Append(",\"ms\":").Append(playMs);
            if (!string.IsNullOrEmpty(end)) sb.Append(",\"end\":\"").Append(end).Append('"');
            if (samples != null && samples.Count >= 2) AppendArray(sb, "samples", samples);
            if (misses != null && misses.Count >= 2) AppendArray(sb, "misses", misses);
            sb.Append('}');
            SubmitResponse r = null;
            yield return PostJson(cfg, "scores", sb.ToString(), text =>
            {
                if (!string.IsNullOrEmpty(text)) try { r = JsonUtility.FromJson<SubmitResponse>(text); } catch (Exception) { }
            });
            if (r != null && !r.ok) Debug.LogWarning("[Online] Puntuación rechazada: " + r.error);
            done?.Invoke(r);
        }

        private static void AppendArray(StringBuilder sb, string key, List<int> values)
        {
            sb.Append(",\"").Append(key).Append("\":[");
            for (int i = 0; i < values.Count; i++) { if (i > 0) sb.Append(','); sb.Append(values[i]); }
            sb.Append(']');
        }

        // POST a /api/colorify/<path>; done recibe el texto de la respuesta (o null sin conexión)
        public static IEnumerator PostJson(ColorifyConfig cfg, string path, string json, Action<string> done)
        {
            using (var req = new UnityWebRequest(BaseUrl(cfg) + "/api/colorify/" + path, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 15;
                yield return req.SendWebRequest();
                string text = req.downloadHandler != null ? req.downloadHandler.text : null;
                done?.Invoke(req.result == UnityWebRequest.Result.ConnectionError ? null : text);
            }
        }

        public static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }

    // Sesión de duelo: abre el WebSocket, se presenta, mantiene la conexión viva y entrega los mensajes en Update.
    public class DuelConnection
    {
        private readonly KpSocket socket;
        private readonly string hello;
        private bool helloSent;
        private float nextPing;
        public bool Welcomed { get; private set; }
        public bool Closed => socket.State == KpSocket.Status.Closed;
        public bool EverOpened { get; private set; }
        private float openedAt;

        public DuelConnection(ColorifyConfig cfg, string player, string name, int best)
        {
            hello = "{\"t\":\"hello\",\"v\":" + OnlineApi.Protocol + ",\"player\":\"" + player + "\",\"name\":\"" + OnlineApi.Escape(name) + "\",\"best\":" + best + "}";
            socket = new KpSocket(OnlineApi.SocketUrl(cfg));
            openedAt = Time.realtimeSinceStartup;
        }

        // Sin conexión a los 10 s: se da por fallida
        public bool TimedOut => !EverOpened && Time.realtimeSinceStartup - openedAt > 10f;

        public void Poll(List<NetMsg> into)
        {
            string raw;
            while (socket.TryReceive(out raw))
            {
                NetMsg m = null;
                try { m = JsonUtility.FromJson<NetMsg>(raw); } catch (Exception) { }
                if (m == null || string.IsNullOrEmpty(m.t)) continue;
                if (m.t == "welcome") Welcomed = true;
                into.Add(m);
            }
            if (socket.State == KpSocket.Status.Open)
            {
                EverOpened = true;
                if (!helloSent) { socket.Send(hello); helloSent = true; }
                if (Time.realtimeSinceStartup >= nextPing) { nextPing = Time.realtimeSinceStartup + 5f; if (Welcomed) socket.Send("{\"t\":\"ping\"}"); }
            }
        }

        public void Queue() => socket.Send("{\"t\":\"queue\"}");
        public void SendRaw(string json) => socket.Send(json);
        // g: tamaño del tablero (el servidor lo usa para que el bot aprenda)
        public void Hit(int n, float timeLeft, int g) => socket.Send("{\"t\":\"hit\",\"n\":" + n + ",\"tl\":" + F(timeLeft) + ",\"g\":" + g + "}");
        public void Miss(int n, float timeLeft, int g) => socket.Send("{\"t\":\"miss\",\"n\":" + n + ",\"tl\":" + F(timeLeft) + ",\"g\":" + g + "}");
        public void Out(int n) => socket.Send("{\"t\":\"out\",\"n\":" + n + "}");
        public void Close() => socket.Dispose();

        private static string F(float v) => Mathf.Max(0f, v).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
