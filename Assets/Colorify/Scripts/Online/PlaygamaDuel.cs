using System;
using System.Collections.Generic;
using KovusPride.Publishing;
using UnityEngine;

namespace Colorify.Online
{
    // Duelos 1 contra 1 sobre las salas de multijugador de Playgama (sus servidores; sin servidor propio).
    // Imita el protocolo del servidor del portfolio (worker/colorify/lobby.js) para que el resto del juego no cambie:
    //   queue  → partida rápida (sala visible con filtro {mode, v}); si a los 6 s no hay nadie, juega un bot local
    //   dcreate → sala visible con un código corto de 4 cifras en el filtro (el teclado del juego es de 4 cifras)
    //   djoin  → busca la sala con ese código y entra
    //   hit / miss / out → mensaje «h» al rival
    // El anfitrión (quien crea la sala) da la salida con la semilla de los tableros y decide el resultado con las
    // mismas reglas que el servidor: si el rival está fuera, ganas en cuanto le superas; los dos fuera, más aciertos.
    // Sin trofeos ni fantasmas (eso vive en el servidor propio).
    public class PlaygamaDuel : IDuelLink
    {
        private const int LeadMs = 3000;
        private const float BotAfter = 6f;
        private const float HiTimeout = 4f;

        private readonly ColorifyConfig cfg;
        private readonly string myName;
        private readonly int myTrophies;
        private readonly Queue<NetMsg> outbox = new Queue<NetMsg>();
        private readonly System.Random rng = new System.Random();

        private enum Stage { Idle, Searching, WaitingGuest, Playing, Done }
        private Stage stage = Stage.Idle;
        private bool isHost, inRoom, priv, closed;
        private float searchSince, guestJoinedAt = -1f;
        private string oppId = "", oppName = "";
        private int oppTrophies;

        // Estado del duelo (lo lleva el anfitrión para decidir; el invitado solo lo enseña)
        private int myHits, oppHits;
        private bool myOut, oppOut;

        // Bot local
        private LocalBot bot;
        private float duelStart;

        public bool Welcomed => true;
        public bool Closed => closed;
        public bool TimedOut => false;

        public PlaygamaDuel(ColorifyConfig cfg, string name, int trophies)
        {
            this.cfg = cfg;
            myName = string.IsNullOrEmpty(name) ? "Player" : name;
            myTrophies = trophies;
            StorePlatform.PlatformEvent += OnPlatformEvent;
            outbox.Enqueue(new NetMsg { t = "welcome", trophies = trophies });
        }

        // ---------------- Juego → «servidor» ----------------

        [Serializable] private class Raw { public string t, room; }

        public void SendRaw(string json)
        {
            Raw r = null;
            try { r = JsonUtility.FromJson<Raw>(json); } catch (Exception) { }
            if (r == null) return;
            switch (r.t)
            {
                case "queue": Quick(); break;
                case "dcreate": CreatePrivate(); break;
                case "djoin": JoinPrivate(r.room); break;
                case "cancel": case "leave": LeaveRoom(); stage = Stage.Idle; break;
            }
        }

        public void Hit(int n, float timeLeft, int g)
        {
            if (stage != Stage.Playing || myOut) return;
            myHits = n;
            Tell("h", n, timeLeft, false);
            Check();
        }

        public void Miss(int n, float timeLeft, int g)
        {
            if (stage != Stage.Playing || myOut) return;
            Tell("h", n, timeLeft, false);
        }

        public void Out(int n)
        {
            if (stage != Stage.Playing || myOut) return;
            myOut = true;
            myHits = n;
            Tell("h", n, 0f, true);
            Check();
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            StorePlatform.PlatformEvent -= OnPlatformEvent;
            LeaveRoom();
        }

        // ---------------- Salas ----------------

        private string DuelFilter(string code = null) =>
            "{\"mode\":\"duel\",\"v\":" + OnlineApi.Protocol + (code != null ? ",\"code\":\"" + code + "\"" : "") + "}";

        private void Quick()
        {
            priv = false;
            stage = Stage.Searching;
            searchSince = Time.realtimeSinceStartup;
            outbox.Enqueue(new NetMsg { t = "queued", online = 1 });
            StorePlatform.Call("mp.quick", "{\"max\":2,\"filter\":" + DuelFilter() + "}", (ok, json) =>
            {
                if (closed || stage != Stage.Searching) { if (ok) LeaveRoom(); return; }
                if (!ok) { StartBot(); return; }   // sin salas (caída, beta): juega el bot en vez de dejarle esperando
                OnEnteredRoom(json);
            });
        }

        private void CreatePrivate()
        {
            priv = true;
            stage = Stage.Searching;
            searchSince = Time.realtimeSinceStartup;
            string code = rng.Next(1000, 10000).ToString();
            StorePlatform.Call("mp.create", "{\"max\":2,\"visible\":true,\"filter\":" + DuelFilter(code) + "}", (ok, json) =>
            {
                if (closed) { if (ok) LeaveRoom(); return; }
                if (!ok) { outbox.Enqueue(new NetMsg { t = "error", code = "server" }); stage = Stage.Idle; return; }
                OnEnteredRoom(json);
                outbox.Enqueue(new NetMsg { t = "droom", room = code });
            });
        }

        private void JoinPrivate(string code)
        {
            priv = true;
            stage = Stage.Searching;
            searchSince = Time.realtimeSinceStartup;
            StorePlatform.Call("mp.findJoin", "{\"filter\":" + DuelFilter(code ?? "") + "}", (ok, json) =>
            {
                if (closed) { if (ok) LeaveRoom(); return; }
                if (!ok)
                {
                    string e = json ?? "";
                    outbox.Enqueue(new NetMsg { t = "error", code = e.Contains("FULL") ? "full" : e.Contains("CLOSED") ? "started" : "not_found" });
                    stage = Stage.Idle;
                    return;
                }
                OnEnteredRoom(json);
            });
        }

        [Serializable] private class RoomPlayer { public string id, name; public bool host; }
        [Serializable] private class RoomInfo { public string room, me; public bool host; public List<RoomPlayer> players = new List<RoomPlayer>(); }

        private void OnEnteredRoom(string json)
        {
            RoomInfo info = null;
            try { info = JsonUtility.FromJson<RoomInfo>(json); } catch (Exception) { }
            inRoom = true;
            isHost = info != null && info.host;
            if (!isHost)
            {
                // Invitado: se presenta; el anfitrión da la salida
                stage = Stage.WaitingGuest;
                Send("hi", "{\"name\":\"" + OnlineApi.Escape(myName) + "\",\"tr\":" + myTrophies + "}");
            }
            else stage = Stage.WaitingGuest;
        }

        private void LeaveRoom()
        {
            if (!inRoom) return;
            inRoom = false;
            StorePlatform.Call("mp.leave", "{}", null);
        }

        // ---------------- Mensajes de la plataforma ----------------

        [Serializable] private class Who { public string id, name; public bool host; }
        [Serializable] private class Msg { public string type, from, data; }
        [Serializable] private class Hi { public string name; public int tr; }
        [Serializable] private class Start { public int seed, lead, tr; public string name; }
        [Serializable] private class HitMsg { public int n; public float tl = -1f; public bool over; }
        [Serializable] private class Res { public string r, reason; public int you, them; }
        [Serializable] private class Left { public string reason; }

        private void OnPlatformEvent(string name, string json)
        {
            if (closed) return;
            try
            {
                switch (name)
                {
                    case "mp.joined":
                    {
                        var w = JsonUtility.FromJson<Who>(json);
                        if (isHost && stage == Stage.WaitingGuest && w != null)
                        {
                            oppId = w.id;
                            oppName = string.IsNullOrEmpty(w.name) ? "Player" : w.name;
                            guestJoinedAt = Time.realtimeSinceStartup;
                        }
                        break;
                    }
                    case "mp.left":
                    {
                        var w = JsonUtility.FromJson<Who>(json);
                        if (w != null && !string.IsNullOrEmpty(oppId) && w.id != oppId) break;
                        OpponentLeft();
                        break;
                    }
                    case "mp.roomLeft":
                    {
                        var l = JsonUtility.FromJson<Left>(json);
                        inRoom = false;
                        if (l != null && l.reason == "left") break;   // la hemos dejado nosotros
                        if (stage == Stage.Playing || stage == Stage.WaitingGuest || stage == Stage.Searching) OpponentLeft(lost: true);
                        break;
                    }
                    case "mp.message":
                        OnMessage(JsonUtility.FromJson<Msg>(json));
                        break;
                }
            }
            catch (Exception e) { Debug.LogWarning("[PlaygamaDuel] " + e.Message); }
        }

        private void OnMessage(Msg m)
        {
            if (m == null || string.IsNullOrEmpty(m.type)) return;
            switch (m.type)
            {
                case "hi":
                    if (isHost && stage == Stage.WaitingGuest)
                    {
                        var hi = JsonUtility.FromJson<Hi>(m.data ?? "{}") ?? new Hi();
                        oppId = m.from;
                        if (!string.IsNullOrEmpty(hi.name)) oppName = hi.name;
                        oppTrophies = hi.tr;
                        HostStart();
                    }
                    break;
                case "start":
                    if (!isHost && stage == Stage.WaitingGuest)
                    {
                        var st = JsonUtility.FromJson<Start>(m.data ?? "{}") ?? new Start();
                        oppId = m.from;
                        BeginMatch(st.seed, st.lead > 0 ? st.lead : LeadMs, st.name, st.tr, false);
                    }
                    break;
                case "h":
                    if (stage != Stage.Playing) break;
                    var h = JsonUtility.FromJson<HitMsg>(m.data ?? "{}") ?? new HitMsg();
                    oppHits = Mathf.Max(oppHits, h.n);
                    if (h.over) oppOut = true;
                    outbox.Enqueue(new NetMsg { t = "opp", n = oppHits, tl = h.over ? 0f : h.tl, over = oppOut });
                    Check();
                    break;
                case "res":
                    if (!isHost && stage == Stage.Playing)
                    {
                        var r = JsonUtility.FromJson<Res>(m.data ?? "{}") ?? new Res();
                        Finish(r.r, r.you, r.them, r.reason, tellGuest: false);
                    }
                    break;
            }
        }

        // ---------------- Duelo ----------------

        private void HostStart()
        {
            int seed = 1 + rng.Next(2000000000);
            StorePlatform.Call("mp.close", "{}", null);   // nadie más entra en la sala
            Send("start", "{\"seed\":" + seed + ",\"lead\":" + LeadMs + ",\"name\":\"" + OnlineApi.Escape(myName) + "\",\"tr\":" + myTrophies + "}");
            BeginMatch(seed, LeadMs, oppName, oppTrophies, false);
        }

        private void BeginMatch(int seed, int lead, string opp, int trophies, bool isBot)
        {
            stage = Stage.Playing;
            myHits = oppHits = 0;
            myOut = oppOut = false;
            oppName = string.IsNullOrEmpty(opp) ? "Player" : opp;
            duelStart = Time.realtimeSinceStartup + lead / 1000f;
            outbox.Enqueue(new NetMsg { t = "match", seed = seed, lead = lead, opp = new NetOpp { name = oppName, bot = isBot, trophies = trophies } });
        }

        private void StartBot()
        {
            LeaveRoom();
            isHost = true;   // el resultado lo decide este dispositivo
            bot = new LocalBot(cfg, myTrophies, rng);
            BeginMatch(1 + rng.Next(2000000000), LeadMs, bot.name, bot.trophies, true);
        }

        private void Check()
        {
            if (!isHost || stage != Stage.Playing) return;
            if (myOut && oppOut) Decide(myHits == oppHits ? "draw" : myHits > oppHits ? "win" : "lose", "score");
            else if (myOut && oppHits > myHits) Decide("lose", "score");
            else if (oppOut && myHits > oppHits) Decide("win", "score");
        }

        private void Decide(string r, string reason)
        {
            Finish(r, myHits, oppHits, reason, tellGuest: bot == null);
        }

        private void Finish(string r, int you, int them, string reason, bool tellGuest)
        {
            if (stage != Stage.Playing) return;
            stage = Stage.Done;
            if (tellGuest)
            {
                string g = r == "win" ? "lose" : r == "lose" ? "win" : "draw";
                Send("res", "{\"r\":\"" + g + "\",\"you\":" + them + ",\"them\":" + you + ",\"reason\":\"" + reason + "\"}");
            }
            outbox.Enqueue(new NetMsg { t = "result", r = r, you = you, them = them, reason = reason });
            LeaveRoom();
        }

        // El rival se ha ido (o se ha perdido la sala)
        private void OpponentLeft(bool lost = false)
        {
            if (stage == Stage.WaitingGuest && isHost && !lost) { oppId = ""; guestJoinedAt = -1f; return; }   // sigue esperando
            if (stage == Stage.Playing && bot == null)
            {
                if (oppOut) { isHost = true; Check(); return; }   // su resultado vale: decido yo
                isHost = true;
                Decide("win", "left");
                return;
            }
            if (stage == Stage.Searching || stage == Stage.WaitingGuest)
            {
                stage = Stage.Idle;
                outbox.Enqueue(new NetMsg { t = "error", code = "server" });
            }
        }

        private void Send(string type, string data)
        {
            StorePlatform.Call("mp.send", "{\"type\":\"" + type + "\",\"data\":" + data + "}", null);
        }

        private void Tell(string type, int n, float tl, bool over)
        {
            if (bot != null) return;
            Send(type, "{\"n\":" + n + ",\"tl\":" + Mathf.Max(0f, tl).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                       ",\"over\":" + (over ? "true" : "false") + "}");
        }

        // ---------------- Bucle ----------------

        public void Poll(List<NetMsg> into)
        {
            float now = Time.realtimeSinceStartup;
            if (!closed)
            {
                // Partida rápida sin rival: bot (como el servidor propio)
                if (!priv && isHost && stage == Stage.WaitingGuest && string.IsNullOrEmpty(oppId) && now - searchSince > BotAfter) StartBot();
                // Invitado en una sala cuyo anfitrión no da la salida (sala abandonada): bot en la rápida, error en la privada
                if (!isHost && stage == Stage.WaitingGuest && now - searchSince > 10f)
                {
                    if (priv) { LeaveRoom(); stage = Stage.Idle; outbox.Enqueue(new NetMsg { t = "error", code = "not_found" }); }
                    else StartBot();
                }
                // El invitado entró pero no se presentó: se empieza igualmente
                if (isHost && stage == Stage.WaitingGuest && guestJoinedAt > 0f && now - guestJoinedAt > HiTimeout) HostStart();
                // Bot
                if (bot != null && stage == Stage.Playing && now >= duelStart)
                {
                    foreach (var ev in bot.Advance(now - duelStart))
                    {
                        oppHits = ev.n;
                        if (ev.over) oppOut = true;
                        outbox.Enqueue(new NetMsg { t = "opp", n = ev.n, tl = ev.tl, over = ev.over });
                    }
                    Check();
                }
            }
            while (outbox.Count > 0) into.Add(outbox.Dequeue());
        }

        // ---------------- Bot local ----------------
        // Mismo modelo de referencia que el bot del servidor (bot.js) sin lo aprendido: tiempo por tamaño de tablero,
        // un 6 % de fallos y nivel según los trofeos del jugador. El juego lo enseña siempre como BOT.
        private class LocalBot
        {
            private static readonly float[] PriorMs = { 450, 450, 600, 800, 1000, 1250, 1550, 1850, 2200 };
            public readonly string name;
            public readonly int trophies;
            private readonly NormalRun run;
            private readonly System.Random rng;
            private readonly float factor;
            private float timeLeft, lastT, nextAt = -1f;
            private bool outFlag;

            public struct Ev { public int n; public float tl; public bool over; }

            public LocalBot(ColorifyConfig cfg, int opponentTrophies, System.Random rng)
            {
                this.rng = rng;
                run = new NormalRun(cfg);
                name = PlayerNames.Random(rng);
                trophies = Mathf.Max(0, opponentTrophies + rng.Next(-60, 61));
                factor = 1.3f - 0.6f * Mathf.Clamp01(opponentTrophies / 2500f);
                timeLeft = Mathf.Min(run.StartTime, run.MaxTime);
            }

            private float SampleMs()
            {
                int size = Mathf.Clamp(run.GridSize, 1, 8);
                return Mathf.Max(150f, PriorMs[size] * (0.75f + (float)rng.NextDouble() * 0.6f) * factor);
            }

            public List<Ev> Advance(float tSeconds)
            {
                var list = new List<Ev>();
                float t = tSeconds * 1000f;
                if (outFlag) return list;
                if (nextAt < 0f) { lastT = 0f; nextAt = SampleMs(); }
                while (!outFlag)
                {
                    float until = Mathf.Min(t, nextAt);
                    timeLeft -= (until - lastT) / 1000f;
                    lastT = until;
                    if (timeLeft <= 0f) { timeLeft = 0f; outFlag = true; list.Add(new Ev { n = run.hits, tl = 0f, over = true }); break; }
                    if (until < nextAt) break;
                    if (rng.NextDouble() < 0.06 * (0.6 + 0.4 * factor))
                    {
                        timeLeft -= run.MissPenalty;
                        list.Add(new Ev { n = run.hits, tl = Mathf.Max(0f, timeLeft) });
                    }
                    else
                    {
                        timeLeft = Mathf.Min(run.MaxTime, timeLeft + run.HitBonus);
                        run.hits++;
                        list.Add(new Ev { n = run.hits, tl = timeLeft });
                    }
                    nextAt += SampleMs();
                }
                return list;
            }
        }
    }
}
