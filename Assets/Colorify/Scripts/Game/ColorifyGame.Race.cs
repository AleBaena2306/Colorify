using System.Collections;
using System.Collections.Generic;
using Colorify.Online;
using UnityEngine;
using UnityEngine.UI;

namespace Colorify
{
    // Modo Carrera: salas de 2 a 8 jugadores (código de 4 cifras o partida rápida en una sala pública).
    // Cada ronda, todos ven el mismo tablero y gana más puntos quien antes toca el color correcto:
    // puesto (10, 8, 6, 5, 4, 3, 2, 1) + rapidez (hasta 5). Un fallo bloquea 1,5 s. Servidor: worker/colorify/race.js.
    public partial class ColorifyGame
    {
        private const float RaceLockSeconds = 1.5f;
        private const int RaceMinPlayers = 2, RaceMaxPlayers = 8, RaceMinRounds = 3, RaceMaxRounds = 30;

        private string pendingRaceMsg;
        private bool raceActive;           // dentro de una sala (en espera o jugando)
        private NetMsg lastRoom;
        private int mySlot;
        private int raceRound, raceTotal;
        private float raceLimit = 8f, raceShownAt, raceLockedUntil;
        private bool raceGot, raceRevealed;
        private readonly Dictionary<int, int> racePlaces = new Dictionary<int, int>();   // slot → puesto en la ronda
        private List<NetPlayer> raceStandings = new List<NetPlayer>();
        private string joinCode = "";

        // UI
        private CanvasGroup raceMenu, raceJoin, raceRoom, raceFinal;
        private TMPro.TextMeshProUGUI raceStatus, joinDisplay, joinStatus;
        private TMPro.TextMeshProUGUI roomTitle, roomSub, roomPlayersValue, roomRoundsValue, roomInfo, roomWait;
        private Button roomAddBot, roomRemoveBot;
        private Button roomStart, roomPub, roomPlayersMinus, roomPlayersPlus, roomRoundsMinus, roomRoundsPlus;
        private RectTransform roomList, finalList, racePanel;
        private TMPro.TextMeshProUGUI finalTitle, finalSub, racePanelText;
        private CanvasGroup boardGroup;

        // =====================================================================
        // Pantallas
        // =====================================================================

        private void BuildRaceOverlays()
        {
            // --- Carrera: elegir cómo entrar ---
            raceMenu = v.Overlay("Race", 0.9f);
            var t = UIKit.Text("Title", raceMenu.transform, ColorifyView.RainbowTitle("RACE"), 130, UIKit.Ink, black: true);
            UIKit.Place(t.rectTransform, 0.5f, 0.5f, new Vector2(1000, 170), new Vector2(0, 560));
            var rules = UIKit.Text("Rules", raceMenu.transform,
                "Everyone gets the same board.\nThe first to tap the color scores the most.\nA wrong tap freezes you for 1.5 s.", 40, UIKit.InkSoft);
            UIKit.Place(rules.rectTransform, 0.5f, 0.5f, new Vector2(960, 200), new Vector2(0, 370));
            var quick = UIKit.Button("Quick", raceMenu.transform, "QUICK PLAY", ColorifyView.TitleColors[4], new Vector2(680, 160), RaceQuick, 70);
            UIKit.Place((RectTransform)quick.transform, 0.5f, 0.5f, new Vector2(680, 160), new Vector2(0, 160));
            var create = UIKit.Button("Create", raceMenu.transform, "CREATE ROOM", ColorifyView.TitleColors[2], new Vector2(560, 130), RaceCreate, 54);
            UIKit.Place((RectTransform)create.transform, 0.5f, 0.5f, new Vector2(560, 130), new Vector2(0, -10));
            var join = UIKit.Button("Join", raceMenu.transform, "JOIN WITH CODE", ColorifyView.TitleColors[0], new Vector2(560, 130), () => ShowJoinFor(false), 50);
            UIKit.Place((RectTransform)join.transform, 0.5f, 0.5f, new Vector2(560, 130), new Vector2(0, -160));
            raceStatus = UIKit.Text("Status", raceMenu.transform, "", 38, UIKit.InkSoft);
            UIKit.Place(raceStatus.rectTransform, 0.5f, 0.5f, new Vector2(960, 120), new Vector2(0, -320));
            var back = UIKit.Button("Back", raceMenu.transform, "BACK", UIKit.PaperDark, new Vector2(420, 120), () => { sfx.Play(Sfx.Id.Click); LeaveRace(); Hide(raceMenu); Show(onlineMenu); }, 50);
            UIKit.Place((RectTransform)back.transform, 0.5f, 0f, new Vector2(420, 120), new Vector2(0, 110));

            // --- Unirse con código (teclado propio: funciona igual en móvil, web y ordenador) ---
            raceJoin = v.Overlay("RaceJoin", 0.95f);
            var jt = UIKit.Text("Title", raceJoin.transform, "ROOM CODE", 90, UIKit.Ink, black: true);
            UIKit.Place(jt.rectTransform, 0.5f, 0.5f, new Vector2(1000, 120), new Vector2(0, 640));
            var dispBox = UIKit.Box("Display", raceJoin.transform, UIKit.PaperDark, 0.5f);
            UIKit.Place(dispBox.rectTransform, 0.5f, 0.5f, new Vector2(560, 160), new Vector2(0, 480));
            joinDisplay = UIKit.Text("Code", dispBox.transform, "", 110, UIKit.Ink, black: true);
            UIKit.Stretch(joinDisplay.rectTransform);
            joinDisplay.characterSpacing = 30;
            joinStatus = UIKit.Text("Status", raceJoin.transform, "", 38, UIKit.Danger);
            UIKit.Place(joinStatus.rectTransform, 0.5f, 0.5f, new Vector2(960, 60), new Vector2(0, 360));
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "DEL", "0", "OK" };
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i];
                Color c = k == "OK" ? ColorifyView.TitleColors[2] : UIKit.PaperDark;
                var key = UIKit.Button("Key" + k, raceJoin.transform, k == "OK" ? "JOIN" : k, c, new Vector2(250, 150), () => JoinKey(k), k.Length > 1 ? 46 : 72);
                UIKit.Place((RectTransform)key.transform, 0.5f, 0.5f, new Vector2(250, 150), new Vector2(-270 + (i % 3) * 270, 220 - (i / 3) * 170));
            }
            var jback = UIKit.Button("Back", raceJoin.transform, "BACK", UIKit.PaperDark, new Vector2(420, 120), () => { sfx.Play(Sfx.Id.Click); Hide(raceJoin); }, 50);
            UIKit.Place((RectTransform)jback.transform, 0.5f, 0f, new Vector2(420, 120), new Vector2(0, 110));

            // --- Sala ---
            raceRoom = v.Overlay("RaceRoom", 0.95f);
            roomTitle = UIKit.Text("Title", raceRoom.transform, "", 100, UIKit.Ink, black: true);
            UIKit.Place(roomTitle.rectTransform, 0.5f, 1f, new Vector2(1000, 130), new Vector2(0, -110));
            roomSub = UIKit.Text("Sub", raceRoom.transform, "", 36, UIKit.InkSoft);
            UIKit.Place(roomSub.rectTransform, 0.5f, 1f, new Vector2(1000, 60), new Vector2(0, -200));
            roomPlayersValue = Stepper(raceRoom.transform, "PLAYERS", -290, () => ChangeRoom(-1, 0), () => ChangeRoom(1, 0), out roomPlayersMinus, out roomPlayersPlus);
            roomRoundsValue = Stepper(raceRoom.transform, "ROUNDS", -400, () => ChangeRoom(0, -1), () => ChangeRoom(0, 1), out roomRoundsMinus, out roomRoundsPlus);
            roomPub = UIKit.Button("Public", raceRoom.transform, "", UIKit.PaperDark, new Vector2(560, 90), TogglePublic, 38);
            UIKit.Place((RectTransform)roomPub.transform, 0.5f, 1f, new Vector2(560, 90), new Vector2(0, -505));
            roomInfo = UIKit.Text("Info", raceRoom.transform, "", 40, UIKit.InkSoft, black: true);
            UIKit.Place(roomInfo.rectTransform, 0.5f, 1f, new Vector2(1000, 120), new Vector2(0, -380));
            roomList = UIKit.Rect("Players", raceRoom.transform);
            UIKit.Place(roomList, 0.5f, 1f, new Vector2(1000, 560), new Vector2(0, -860));
            roomStart = UIKit.Button("Start", raceRoom.transform, "START", ColorifyView.TitleColors[2], new Vector2(600, 150), RaceStart, 66);
            UIKit.Place((RectTransform)roomStart.transform, 0.5f, 0f, new Vector2(600, 150), new Vector2(0, 300));
            roomAddBot = UIKit.Button("AddBot", raceRoom.transform, "+ BOT", UIKit.PaperDark, new Vector2(300, 100), () => RoomBot(true), 44);
            UIKit.Place((RectTransform)roomAddBot.transform, 0.5f, 0f, new Vector2(300, 100), new Vector2(-170, 460));
            roomRemoveBot = UIKit.Button("RemoveBot", raceRoom.transform, "– BOT", UIKit.PaperDark, new Vector2(300, 100), () => RoomBot(false), 44);
            UIKit.Place((RectTransform)roomRemoveBot.transform, 0.5f, 0f, new Vector2(300, 100), new Vector2(170, 460));
            roomWait = UIKit.Text("Wait", raceRoom.transform, "", 40, UIKit.InkSoft);
            UIKit.Place(roomWait.rectTransform, 0.5f, 0f, new Vector2(960, 120), new Vector2(0, 300));
            var leave = UIKit.Button("Leave", raceRoom.transform, "LEAVE", UIKit.PaperDark, new Vector2(420, 120), () => { sfx.Play(Sfx.Id.Click); LeaveRace(); Hide(raceRoom); Show(raceMenu); }, 50);
            UIKit.Place((RectTransform)leave.transform, 0.5f, 0f, new Vector2(420, 120), new Vector2(0, 110));

            // --- Final de la carrera ---
            raceFinal = v.Overlay("RaceFinal", 0.95f);
            finalTitle = UIKit.Text("Title", raceFinal.transform, "", 120, UIKit.Ink, black: true);
            UIKit.Place(finalTitle.rectTransform, 0.5f, 1f, new Vector2(1000, 160), new Vector2(0, -130));
            finalSub = UIKit.Text("Sub", raceFinal.transform, "", 40, UIKit.InkSoft);
            UIKit.Place(finalSub.rectTransform, 0.5f, 1f, new Vector2(1000, 130), new Vector2(0, -260));
            finalList = UIKit.Rect("Standings", raceFinal.transform);
            UIKit.Place(finalList, 0.5f, 1f, new Vector2(1000, 800), new Vector2(0, -740));
            var toRoom = UIKit.Button("Room", raceFinal.transform, "BACK TO ROOM", ColorifyView.TitleColors[2], new Vector2(600, 150), BackToRoom, 56);
            UIKit.Place((RectTransform)toRoom.transform, 0.5f, 0f, new Vector2(600, 150), new Vector2(0, 300));
            var fonline = UIKit.Button("Online", raceFinal.transform, "ONLINE MENU", ColorifyView.TitleColors[0], new Vector2(420, 120), BackToOnline, 44);
            UIKit.Place((RectTransform)fonline.transform, 0.5f, 0f, new Vector2(420, 120), new Vector2(-220, 110));
            var fmenu = UIKit.Button("Menu", raceFinal.transform, "MAIN MENU", UIKit.PaperDark, new Vector2(420, 120), () => { sfx.Play(Sfx.Id.Click); ShowMenu(); }, 44);
            UIKit.Place((RectTransform)fmenu.transform, 0.5f, 0f, new Vector2(420, 120), new Vector2(220, 110));

            // --- Clasificación en partida (a la derecha de la muestra) ---
            var box = UIKit.Box("RaceStandings", v.game, UIKit.WithAlpha(UIKit.PaperDark, 0.85f), 0.5f);
            racePanel = box.rectTransform;
            UIKit.Place(racePanel, 0.5f, 1f, new Vector2(320, 360), new Vector2(370, ColorifyView.Layout.SwatchY));
            racePanelText = UIKit.Text("List", racePanel, "", 26, UIKit.Ink, black: true);
            racePanelText.alignment = TMPro.TextAlignmentOptions.TopLeft;
            UIKit.Stretch(racePanelText.rectTransform, 18f);
            racePanel.gameObject.SetActive(false);
        }

        private TMPro.TextMeshProUGUI Stepper(Transform parent, string label, float y, System.Action minus, System.Action plus, out Button bMinus, out Button bPlus)
        {
            var l = UIKit.Text(label, parent, label, 40, UIKit.InkSoft, black: true);
            l.alignment = TMPro.TextAlignmentOptions.Left;
            UIKit.Place(l.rectTransform, 0.5f, 1f, new Vector2(300, 90), new Vector2(-300, y));
            bMinus = UIKit.Button(label + "Minus", parent, "–", UIKit.PaperDark, new Vector2(110, 90), minus, 60);
            UIKit.Place((RectTransform)bMinus.transform, 0.5f, 1f, new Vector2(110, 90), new Vector2(40, y));
            var value = UIKit.Text(label + "Value", parent, "", 60, UIKit.Ink, black: true);
            UIKit.Place(value.rectTransform, 0.5f, 1f, new Vector2(160, 90), new Vector2(175, y));
            bPlus = UIKit.Button(label + "Plus", parent, "+", UIKit.PaperDark, new Vector2(110, 90), plus, 60);
            UIKit.Place((RectTransform)bPlus.transform, 0.5f, 1f, new Vector2(110, 90), new Vector2(310, y));
            return value;
        }

        private void HideRaceOverlays(bool instant)
        {
            foreach (var cg in new[] { raceMenu, raceJoin, raceRoom, raceFinal }) if (cg != null) Hide(cg, instant);
            if (racePanel != null) racePanel.gameObject.SetActive(false);
            if (boardGroup != null) boardGroup.alpha = 1f;
            raceActive = false;
            pendingRaceMsg = null;
        }

        private void ShowRace()
        {
            sfx.Play(Sfx.Id.Click);
            EnsureIdentity();
            raceStatus.text = "";
            Hide(onlineMenu);
            Show(raceMenu);
        }

        // =====================================================================
        // Entrar y salir
        // =====================================================================

        private void RaceSend(string json)
        {
            EnsureIdentity();
            if (duel == null || duel.Closed) { CloseDuel(); duel = new DuelConnection(cfg, save.playerId, CurrentName, save.bestNormalHits); }
            if (duel.Welcomed) duel.SendRaw(json);
            else pendingRaceMsg = json;
            raceStatus.text = "Connecting…";
        }

        private void RaceQuick() { sfx.Play(Sfx.Id.Click); RaceSend("{\"t\":\"quick\"}"); }

        private void RaceCreate()
        {
            sfx.Play(Sfx.Id.Click);
            RaceSend("{\"t\":\"create\",\"max\":" + Mathf.Clamp(save.raceMaxPlayers, RaceMinPlayers, RaceMaxPlayers) +
                     ",\"rounds\":" + Mathf.Clamp(save.raceRounds, RaceMinRounds, RaceMaxRounds) + ",\"pub\":false}");
        }

        private bool joinForDuel;

        private void ShowJoinFor(bool duelRoom)
        {
            joinForDuel = duelRoom;
            sfx.Play(Sfx.Id.Click);
            joinCode = "";
            joinStatus.text = "";
            RefreshJoin();
            Show(raceJoin);
        }

        private void JoinKey(string k)
        {
            sfx.Play(Sfx.Id.Click);
            joinStatus.text = "";
            if (k == "DEL") { if (joinCode.Length > 0) joinCode = joinCode.Substring(0, joinCode.Length - 1); }
            else if (k == "OK")
            {
                if (joinCode.Length != 4) { joinStatus.text = "The code has 4 digits."; return; }
                if (joinForDuel) { StartDuelSearch("{\"t\":\"djoin\",\"room\":\"" + joinCode + "\"}", true); return; }
                RaceSend("{\"t\":\"join\",\"room\":\"" + joinCode + "\"}");
                joinStatus.text = "<color=#" + ColorUtility.ToHtmlStringRGB(UIKit.InkSoft) + ">Joining…</color>";
            }
            else if (joinCode.Length < 4) joinCode += k;
            RefreshJoin();
        }

        private void RefreshJoin()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 4; i++) sb.Append(i < joinCode.Length ? joinCode[i] : '_');
            joinDisplay.text = sb.ToString();
        }

        private void LeaveRace()
        {
            if (duel != null && raceActive) duel.SendRaw("{\"t\":\"leave\"}");
            raceActive = false;
            lastRoom = null;
            pendingRaceMsg = null;
            if (mode == Mode.Race) mode = Mode.Normal;
        }

        private bool RaceError(string code)
        {
            string msg;
            switch (code)
            {
                case "not_found": msg = "There's no room with that code."; break;
                case "full": msg = "That room is full."; break;
                case "started": msg = "That race has already started."; break;
                case "need_players": msg = "You need at least 2 players to start."; break;
                case "not_host": msg = "Only the host can change that."; break;
                default: return false;
            }
            if (ostage == OnlineStage.Searching && duelPrivate)
            {
                // Código de duelo no válido: de vuelta al teclado con el aviso
                Hide(searching);
                ostage = OnlineStage.Lobby;
                mode = Mode.Normal;
                joinStatus.text = msg;
                Show(raceJoin);
            }
            else if (raceJoin.gameObject.activeSelf && raceJoin.alpha > 0.5f) joinStatus.text = msg;
            else if (raceRoom.gameObject.activeSelf) roomWait.text = msg;
            else raceStatus.text = msg;
            sfx.Play(Sfx.Id.Wrong);
            return true;
        }

        private void RaceConnectionLost()
        {
            bool inGame = mode == Mode.Race;
            StopAllCoroutines();
            attract = null;
            v.countdownText.text = v.stampText.text = v.stampSub.text = "";
            HideRaceOverlays(false);
            mode = Mode.Normal;
            state = State.Menu;
            raceStatus.text = inGame || lastRoom != null ? "The connection dropped. You left the room." : "Couldn't reach the Colorify server.\nCheck your connection and try again.";
            lastRoom = null;
            Hide(onlineMenu);
            Show(raceMenu);
        }

        // =====================================================================
        // Mensajes
        // =====================================================================

        private void HandleRace(NetMsg m)
        {
            switch (m.t)
            {
                case "room":
                    lastRoom = m;
                    raceActive = true;
                    foreach (var p in m.players) if (p.me) mySlot = p.slot;
                    if (m.state == "lobby" && mode != Mode.Race && !(raceFinal.gameObject.activeSelf && raceFinal.alpha > 0.5f)) ShowRoom(m);
                    break;
                case "rstart": StartCoroutine(RaceRound(m)); break;
                case "rhit": RaceHit(m); break;
                case "rend": RaceEnd(m); break;
                case "rover": RaceOver(m); break;
            }
        }

        private void ShowRoom(NetMsg m)
        {
            foreach (var cg in new[] { raceMenu, raceJoin, onlineMenu }) Hide(cg);
            roomTitle.text = "ROOM " + m.room;
            roomSub.text = m.pub ? "Public room · anyone with Quick Play can join · trophies at stake" : "Private room · share the code · just for fun, no trophies";
            bool host = m.host;
            foreach (var b in new[] { roomPlayersMinus, roomPlayersPlus, roomRoundsMinus, roomRoundsPlus }) b.gameObject.SetActive(host);
            roomPlayersValue.gameObject.SetActive(host);
            roomRoundsValue.gameObject.SetActive(host);
            roomPub.gameObject.SetActive(host);
            foreach (var label in new[] { "PLAYERS", "ROUNDS" })
            {
                var tr = raceRoom.transform.Find(label);
                if (tr != null) tr.gameObject.SetActive(host);
            }
            roomPlayersValue.text = m.max.ToString();
            roomRoundsValue.text = m.rounds.ToString();
            roomPub.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = m.pub ? "PUBLIC ROOM" : "PRIVATE ROOM";
            roomInfo.gameObject.SetActive(!host);
            roomInfo.text = $"Up to {m.max} players · {m.rounds} rounds";

            for (int i = roomList.childCount - 1; i >= 0; i--) Destroy(roomList.GetChild(i).gameObject);
            for (int i = 0; i < m.max; i++)
            {
                bool filled = i < m.players.Count;
                var row = UIKit.Box("Row" + i, roomList, filled ? (m.players[i].me ? ColorifyView.WithAlphaTitle(2, 0.35f) : UIKit.WithAlpha(UIKit.PaperDark, 0.85f)) : UIKit.WithAlpha(UIKit.PaperDark, 0.35f), 0.6f);
                UIKit.Place(row.rectTransform, 0.5f, 1f, new Vector2(900, 60), new Vector2(0, -32 - i * 68));
                string text = filled ? m.players[i].name + (m.players[i].bot ? $"  <size=70%><color=#{ColorUtility.ToHtmlStringRGB(UIKit.InkSoft)}>BOT</color></size>" : "") + (m.players[i].host ? $"  <size=70%><color=#{ColorUtility.ToHtmlStringRGB(UIKit.InkSoft)}>HOST</color></size>" : "") + (m.players[i].me ? "  <size=70%>(YOU)</size>" : "") : "<color=#" + ColorUtility.ToHtmlStringRGB(UIKit.InkSoft) + ">waiting…</color>";
                var label = UIKit.Text("Name", row.transform, text, 36, UIKit.Ink, black: filled);
                UIKit.Stretch(label.rectTransform);
            }
            int n = m.players.Count;
            int bots = m.players.FindAll(p => p.bot).Count;
            roomAddBot.gameObject.SetActive(host);
            roomRemoveBot.gameObject.SetActive(host);
            roomAddBot.interactable = n < m.max;
            roomRemoveBot.interactable = bots > 0;
            roomStart.gameObject.SetActive(host);
            roomStart.interactable = n >= RaceMinPlayers;
            roomStart.GetComponent<Image>().color = n >= RaceMinPlayers ? ColorifyView.TitleColors[2] : UIKit.PaperDark;
            roomStart.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = n >= RaceMinPlayers ? $"START  <size=60%>{n}/{m.max}</size>" : "NEED 2 PLAYERS";
            roomWait.gameObject.SetActive(!host);
            roomWait.text = "Waiting for the host to start…";
            if (!raceRoom.gameObject.activeSelf || raceRoom.alpha < 0.5f) Show(raceRoom);
        }

        private void ChangeRoom(int dPlayers, int dRounds)
        {
            if (lastRoom == null || !lastRoom.host) return;
            sfx.Play(Sfx.Id.Click);
            int max = Mathf.Clamp(lastRoom.max + dPlayers, Mathf.Max(RaceMinPlayers, lastRoom.players.Count), RaceMaxPlayers);
            int rounds = Mathf.Clamp(lastRoom.rounds + dRounds, RaceMinRounds, RaceMaxRounds);
            save.raceMaxPlayers = max;
            save.raceRounds = rounds;
            save.Save();
            SendSettings(max, rounds, lastRoom.pub);
        }

        private void TogglePublic()
        {
            if (lastRoom == null || !lastRoom.host) return;
            sfx.Play(Sfx.Id.Click);
            SendSettings(lastRoom.max, lastRoom.rounds, !lastRoom.pub);
        }

        private void SendSettings(int max, int rounds, bool pub)
        {
            duel?.SendRaw("{\"t\":\"settings\",\"max\":" + max + ",\"rounds\":" + rounds + ",\"pub\":" + (pub ? "true" : "false") + "}");
        }

        // El anfitrión puede añadir bots (aprenden de las partidas reales y llevan la etiqueta BOT)
        private void RoomBot(bool add)
        {
            sfx.Play(Sfx.Id.Click);
            duel?.SendRaw(add ? "{\"t\":\"addbot\"}" : "{\"t\":\"removebot\"}");
        }

        private void RaceStart()
        {
            sfx.Play(Sfx.Id.Click);
            duel?.SendRaw("{\"t\":\"start\"}");
        }

        private void BackToRoom()
        {
            sfx.Play(Sfx.Id.Click);
            Hide(raceFinal);
            if (lastRoom != null && duel != null && !duel.Closed) ShowRoom(lastRoom);
            else { Show(raceMenu); }
        }

        // =====================================================================
        // Rondas
        // =====================================================================

        private IEnumerator RaceRound(NetMsg m)
        {
            foreach (var cg in new[] { raceRoom, raceFinal, raceMenu, raceJoin, onlineMenu, v.menu }) Hide(cg);
            if (attract != null) { StopCoroutine(attract); attract = null; }
            if (mode != Mode.Race)
            {
                mode = Mode.Race;
                run = null;
                normal = null;
                raceStandings = new List<NetPlayer>();
                if (lastRoom != null) foreach (var p in lastRoom.players) raceStandings.Add(new NetPlayer { slot = p.slot, name = p.name, me = p.me });
                runStartedAt = Time.realtimeSinceStartup;
                pausedTotal = 0f;
                trophyNote = "";
                Analytics.Log("game_start", ("mode", "race"), ("level_id", "room"), ("players", raceStandings.Count), ("rounds", m.total));
            }
            raceActive = true;
            raceRound = m.round;
            raceTotal = m.total;
            raceLimit = Mathf.Max(1f, m.limit / 1000f);
            raceGot = false;
            raceRevealed = false;
            raceLockedUntil = 0f;
            racePlaces.Clear();
            pauseButton.gameObject.SetActive(false);
            v.multChip.gameObject.SetActive(false);
            racePanel.gameObject.SetActive(true);
            RefreshRaceHud();
            SetBoardAlpha(1f);

            // Aviso de ronda con el tablero en gris y, a la vez para todos, el tablero de verdad
            state = State.Countdown;
            ShowBoard(ColorBoardGenerator.Grey(m.size, rng));
            v.countdownText.text = "";
            v.stampText.text = "ROUND " + m.round;
            v.stampSub.text = m.round == m.total ? "LAST ROUND" : "OF " + m.total;
            juice.PopIn(v.stampText.transform, 0f, 0.3f, 1.5f);
            sfx.Play(Sfx.Id.Tick, 1f, 0.8f);
            SetTimer(1f);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, m.lead / 1000f - 0.05f));
            v.stampText.text = v.stampSub.text = "";

            float minSep = Mathf.Lerp(cfg.normalSeparationEasy, cfg.normalSeparationHard, Mathf.Clamp01(m.diff)) / Mathf.Max(0.05f, cfg.normalSimilarity);
            current = ColorBoardGenerator.GenerateWith(cfg, Mathf.Clamp(m.size, 2, 8), minSep, cfg.normalSameFamily,
                f => cfg.families[f].spawnWeight, new System.Random(m.seed));
            ShowBoard(current);
            // Forma y efecto de la ronda (los elige el servidor, igual para todos)
            var kind = new BoardKind((Shape)Mathf.Clamp(m.shape, 0, 3), (Effect)Mathf.Clamp(m.fx, 0, 8));
            ApplyKind(kind);
            if (kind.fx != Effect.None) ShowBanner(kind.Banner);
            sfx.Play(Sfx.Id.Go);
            raceShownAt = Time.realtimeSinceStartup;
            state = State.Playing;
        }

        private void RaceTap(Tile tile)
        {
            float now = Time.realtimeSinceStartup;
            if (raceGot || now < raceLockedUntil) return;
            if (tile.index == current.correctIndex)
            {
                raceGot = true;
                duel?.SendRaw("{\"t\":\"got\",\"round\":" + raceRound + "}");
                juice.Punch(tile.transform, 0.2f);
                juice.Burst(tile.transform.position, current.TargetColor, 12, 1000f);
                sfx.Play(Sfx.Id.Hit);
                Haptics.Pulse(15);
                SetBoardAlpha(0.55f);
            }
            else
            {
                raceLockedUntil = now + RaceLockSeconds;
                PeekSwatchAfterMiss();
                juice.Shake((RectTransform)tile.transform, 22f, 0.25f);
                juice.Shake(v.board, 8f, 0.2f);
                juice.FloatText(tile.transform.position, "FROZEN", UIKit.Danger, 56f, 90f);
                sfx.Play(Sfx.Id.Wrong);
                Haptics.Pulse(50);
            }
        }

        private void UpdateRace()
        {
            if (state == State.Playing) UpdateBoardFx(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            if (state == State.Playing)
            {
                float left = raceLimit - (Time.realtimeSinceStartup - raceShownAt);
                SetTimer(Mathf.Clamp01(left / raceLimit));
                if (!raceGot) SetBoardAlpha(Time.realtimeSinceStartup < raceLockedUntil ? 0.35f : 1f);
            }
        }

        private void SetBoardAlpha(float a)
        {
            if (boardGroup == null)
            {
                boardGroup = v.board.gameObject.GetComponent<CanvasGroup>();
                if (boardGroup == null) boardGroup = v.board.gameObject.AddComponent<CanvasGroup>();
            }
            boardGroup.alpha = a;
        }

        private void RaceHit(NetMsg m)
        {
            racePlaces[m.slot] = m.place;
            var p = raceStandings.Find(x => x.slot == m.slot);
            if (p != null) p.pts = m.pts;
            if (m.slot == mySlot)
            {
                v.stampText.text = Ordinal(m.place) + "!";
                v.stampSub.text = "+" + m.pts;
                juice.PopIn(v.stampText.transform, 0f, 0.3f, 1.6f);
                if (m.place == 1) sfx.Play(Sfx.Id.Clear);
            }
            RefreshRaceHud();
        }

        private void RaceEnd(NetMsg m)
        {
            raceStandings = m.players;
            if (state == State.Playing || state == State.Countdown)
            {
                state = State.Resolving;
                if (!raceGot)
                {
                    v.stampText.text = "TOO SLOW";
                    v.stampSub.text = "";
                    juice.PopIn(v.stampText.transform, 0f, 0.3f, 1.4f);
                    RevealSwatch();
                    if (current != null && !raceRevealed) { raceRevealed = true; StartCoroutine(RevealCorrectTile(1.6f)); }
                }
            }
            SetBoardAlpha(1f);
            RefreshRaceHud();
            StartCoroutine(ClearRaceStamp(1.8f, m.round));
        }

        private IEnumerator ClearRaceStamp(float delay, int round)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (raceRound == round && state != State.Countdown) v.stampText.text = v.stampSub.text = "";
        }

        private void RefreshRaceHud()
        {
            NetPlayer me = raceStandings.Find(x => x.me);
            int myScore = me != null ? me.score : 0;
            int myPlace = me != null ? raceStandings.IndexOf(me) + 1 : 0;
            v.roundLabel.text = $"RACE · ROUND {raceRound}/{raceTotal}";
            v.scoreText.text = $"{myScore} <size=60%><color=#{ColorUtility.ToHtmlStringRGB(UIKit.InkSoft)}>PTS</color></size>";
            v.totalText.text = myPlace > 0 ? $"{Ordinal(myPlace)} OF {raceStandings.Count}" : "";

            // Una línea por jugador: puesto, nombre y puntos. En verde, quien ya ha acertado esta ronda.
            var sb = new System.Text.StringBuilder("<size=80%><color=#" + ColorUtility.ToHtmlStringRGB(UIKit.InkSoft) + ">STANDINGS</color></size>\n");
            for (int i = 0; i < raceStandings.Count && i < 8; i++)
            {
                var p = raceStandings[i];
                string name = p.name.Length > 13 ? p.name.Substring(0, 13) : p.name;
                if (p.bot) name = (p.name.Length > 9 ? p.name.Substring(0, 9) : p.name) + " <size=70%>BOT</size>";
                bool got = racePlaces.ContainsKey(p.slot) && state != State.Resolving;
                string col = got ? ColorUtility.ToHtmlStringRGB(ColorifyView.TitleColors[2]) : p.me ? ColorUtility.ToHtmlStringRGB(ColorifyView.TitleColors[4]) : null;
                string line = $"{i + 1}. {name}<pos=80%>{p.score}";
                sb.Append(col != null ? $"<color=#{col}>{line}</color>" : line).Append('\n');
            }
            racePanelText.text = sb.ToString();
            racePanelText.fontSize = raceStandings.Count > 6 ? 22 : 26;
        }

        private void RaceOver(NetMsg m)
        {
            StartCoroutine(RaceOverRoutine(m));
        }

        private IEnumerator RaceOverRoutine(NetMsg m)
        {
            yield return new WaitForSecondsRealtime(0.3f);
            state = State.GameOver;
            raceStandings = m.players;
            racePanel.gameObject.SetActive(false);
            SetBoardAlpha(1f);
            v.stampText.text = v.stampSub.text = "";
            NetPlayer me = raceStandings.Find(x => x.me);
            int place = me != null ? raceStandings.IndexOf(me) + 1 : 0;
            finalTitle.text = place == 1 ? ColorifyView.RainbowTitle("YOU WIN!") : place > 0 ? Ordinal(place) + " PLACE" : "RACE OVER";
            finalSub.text = $"{raceTotal} rounds · {raceStandings.Count} players" + (trophyNote != "" ? "\n" + trophyNote : "");
            for (int i = finalList.childCount - 1; i >= 0; i--) Destroy(finalList.GetChild(i).gameObject);
            for (int i = 0; i < raceStandings.Count; i++)
            {
                var p = raceStandings[i];
                var row = UIKit.Box("Row" + i, finalList, p.me ? ColorifyView.WithAlphaTitle(4, 0.35f) : UIKit.WithAlpha(UIKit.PaperDark, i % 2 == 0 ? 0.85f : 0.45f), 0.6f);
                UIKit.Place(row.rectTransform, 0.5f, 1f, new Vector2(940, 84), new Vector2(0, -45 - i * 94));
                var rank = UIKit.Text("Rank", row.transform, Ordinal(i + 1), 40, i == 0 ? ColorifyView.TitleColors[4] : UIKit.InkSoft, black: true);
                rank.alignment = TMPro.TextAlignmentOptions.Left;
                UIKit.Place(rank.rectTransform, 0f, 0.5f, new Vector2(140, 84), new Vector2(100, 0));
                var name = UIKit.Text("Name", row.transform, p.name + (p.bot ? "  <size=70%>BOT</size>" : "") + (p.me ? "  <size=70%>(YOU)</size>" : ""), 40, UIKit.Ink, black: true);
                name.alignment = TMPro.TextAlignmentOptions.Left;
                UIKit.Place(name.rectTransform, 0f, 0.5f, new Vector2(560, 84), new Vector2(460, 0));
                var score = UIKit.Text("Score", row.transform, p.score + " <size=70%>pts</size>", 42, UIKit.Ink, black: true);
                score.alignment = TMPro.TextAlignmentOptions.Right;
                UIKit.Place(score.rectTransform, 1f, 0.5f, new Vector2(220, 84), new Vector2(-140, 0));
            }
            sfx.Play(place == 1 ? Sfx.Id.Victory : Sfx.Id.Clear);
            Show(raceFinal);
            if (place == 1) juice.Punch(finalTitle.transform, 0.25f, 0.4f);
            if (place == 1) save.raceWins++;
            save.racesPlayed++;
            save.Save();
            Analytics.Log("game_end", ("mode", "race"), ("level_id", "room"), ("duration_sec", RunDuration()), ("result", place == 1 ? "win" : "lose"),
                ("place", place), ("players", raceStandings.Count), ("rounds", raceTotal), ("score", me != null ? me.score : 0));
            mode = Mode.Normal;   // de vuelta a la sala (el servidor ya la ha puesto en espera)
            state = State.Menu;
        }

        private static string Ordinal(int n)
        {
            int m100 = n % 100;
            string suf = m100 >= 11 && m100 <= 13 ? "TH" : (n % 10) == 1 ? "ST" : (n % 10) == 2 ? "ND" : (n % 10) == 3 ? "RD" : "TH";
            return n + suf;
        }
    }
}
