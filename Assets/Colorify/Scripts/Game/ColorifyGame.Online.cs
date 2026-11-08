using System.Collections;
using System.Collections.Generic;
using Colorify.Online;
using UnityEngine;
using UnityEngine.UI;

namespace Colorify
{
    // Modo Online (duelos 1 contra 1) y ranking global. El servidor está en el portfolio:
    // worker/colorify/lobby.js (duelos) y worker/colorify/scores.js (ranking y fantasmas).
    //
    // Un duelo usa las reglas del modo Normal con los mismos tableros para los dos (semilla del servidor).
    // Si el rival se queda sin tiempo, ganas en cuanto le superas; si te quedas tú antes, pierdes.
    // Sin rival en 10 s, el servidor propone un fantasma (partida real grabada), marcado como GHOST.
    public partial class ColorifyGame
    {
        private enum OnlineStage { None, Lobby, Searching, Playing, Result }

        private OnlineStage ostage = OnlineStage.None;
        private DuelConnection duel;
        private readonly List<NetMsg> inbox = new List<NetMsg>();
        private bool queueSent;
        private int duelSeed;
        private NetOpp oppInfo;
        private int oppHits;
        private float oppTimeLeft = -1f;
        private bool oppOver;
        private NetMsg pendingResult;
        private bool waitingResult;
        private Button pauseButton;

        // Tiempos de cada acierto y fallos por tamaño de tablero (modo Normal): el bot del modo Online aprende de ellos
        private readonly List<int> hitSamples = new List<int>();     // [tamaño, ms, tamaño, ms, …]
        private readonly Dictionary<int, int> missBySize = new Dictionary<int, int>();
        private List<int> missSamples
        {
            get { var l = new List<int>(); foreach (var kv in missBySize) { l.Add(kv.Key); l.Add(kv.Value); } return l; }
        }
        private float normalPlayMs, lastHitMs;

        // UI
        private CanvasGroup onlineMenu, duelMenu, searching, onlineResult, leaderboard;
        private TMPro.TextMeshProUGUI duelStatus, onlineTrophies;
        private string trophyNote = "";   // «+30 TROPHIES …» de la última partida, para las pantallas de resultado
        private string duelFirst = "{\"t\":\"queue\"}", duelRoomCode = "";
        private bool duelPrivate;
        private TMPro.TextMeshProUGUI onlineName, onlineStatus, searchTitle, searchSub, resTitle, resScore, resInfo, resSub;
        private TMPro.TextMeshProUGUI oppNameText, oppHitsText, oppTagText, lbMe, lbStatus;
        private RectTransform oppPanel, lbList;
        private Image oppTimerBack, oppTimerFill;
        private readonly Dictionary<string, Image> lbTabs = new Dictionary<string, Image>();
        private string lbMode = "points";
        private Button resAgain;

        // =====================================================================
        // Pantallas
        // =====================================================================

        private void BuildOnlineOverlays()
        {
            // --- Online (antes de buscar) ---
            onlineMenu = v.Overlay("Online", 0.9f);
            var t = UIKit.Text("Title", onlineMenu.transform, ColorifyView.RainbowTitle("ONLINE"), 120, UIKit.Ink, black: true);
            UIKit.Place(t.rectTransform, 0.5f, 0.5f, new Vector2(1000, 160), new Vector2(0, 560));
            var rules = UIKit.Text("Rules", onlineMenu.transform,
                "DUEL: 1 vs 1 on the same boards.\nRACE: up to 8 players. First tap wins.", 38, UIKit.InkSoft);
            rules.enableWordWrapping = true;   // por si una línea no cabe en pantallas estrechas
            UIKit.Place(rules.rectTransform, 0.5f, 0.5f, new Vector2(960, 200), new Vector2(0, 380));
            var youLabel = UIKit.Text("YouLabel", onlineMenu.transform, "YOU PLAY AS", 32, UIKit.InkSoft, black: true);
            UIKit.Place(youLabel.rectTransform, 0.5f, 0.5f, new Vector2(900, 50), new Vector2(0, 230));
            onlineName = UIKit.Text("Name", onlineMenu.transform, "", 64, UIKit.Ink, black: true);
            UIKit.Place(onlineName.rectTransform, 0.5f, 0.5f, new Vector2(1000, 90), new Vector2(0, 175));
            onlineTrophies = UIKit.Text("Trophies", onlineMenu.transform, "", 36, UIKit.Ink, black: true);
            UIKit.Place(onlineTrophies.rectTransform, 0.5f, 0.5f, new Vector2(1000, 56), new Vector2(0, 118));
            var rename = UIKit.Button("Rename", onlineMenu.transform, "CHANGE NAME", UIKit.PaperDark, new Vector2(340, 74), ShowNameEditor, 34);
            UIKit.Place((RectTransform)rename.transform, 0.5f, 0.5f, new Vector2(340, 74), new Vector2(0, 50));
            // Guardar un nombre propio exige cuenta de Google, que los portales no permiten: allí se juega con
            // el nombre generado (o, más adelante, con el de la cuenta del portal)
            rename.gameObject.SetActive(KovusPride.Publishing.StorePlatform.AllowsExternalLogin);
            var find = UIKit.Button("Duel", onlineMenu.transform, "DUEL  <size=60%>1 VS 1</size>", ColorifyView.TitleColors[2], new Vector2(680, 150), ShowDuelMenu, 66);
            UIKit.Place((RectTransform)find.transform, 0.5f, 0.5f, new Vector2(680, 150), new Vector2(0, -70));
            var race = UIKit.Button("Race", onlineMenu.transform, "RACE  <size=60%>UP TO 8</size>", ColorifyView.TitleColors[4], new Vector2(680, 150), ShowRace, 66);
            UIKit.Place((RectTransform)race.transform, 0.5f, 0.5f, new Vector2(680, 150), new Vector2(0, -240));
            var lb = UIKit.Button("Leaderboard", onlineMenu.transform, "LEADERBOARD", ColorifyView.TitleColors[0], new Vector2(560, 110), () => ShowLeaderboard(lbMode), 48);
            UIKit.Place((RectTransform)lb.transform, 0.5f, 0.5f, new Vector2(560, 110), new Vector2(0, -395));
            onlineStatus = UIKit.Text("Status", onlineMenu.transform, "", 36, UIKit.Danger);
            UIKit.Place(onlineStatus.rectTransform, 0.5f, 0.5f, new Vector2(960, 100), new Vector2(0, -515));
            var back = UIKit.Button("Back", onlineMenu.transform, "BACK", UIKit.PaperDark, new Vector2(420, 120), () => { sfx.Play(Sfx.Id.Click); ShowMenu(); }, 50);
            UIKit.Place((RectTransform)back.transform, 0.5f, 0f, new Vector2(420, 120), new Vector2(0, 110));

            // --- Duelo: emparejamiento, sala privada o código ---
            duelMenu = v.Overlay("DuelMenu", 0.9f);
            var dt = UIKit.Text("Title", duelMenu.transform, ColorifyView.RainbowTitle("DUEL"), 130, UIKit.Ink, black: true);
            UIKit.Place(dt.rectTransform, 0.5f, 0.5f, new Vector2(1000, 170), new Vector2(0, 560));
            var drules = UIKit.Text("Rules", duelMenu.transform,
                "1 vs 1 in real time, on the same boards.\nFind a match to win or lose trophies.\nRooms with a code are just for fun.", 38, UIKit.InkSoft);
            drules.enableWordWrapping = true;
            UIKit.Place(drules.rectTransform, 0.5f, 0.5f, new Vector2(960, 200), new Vector2(0, 370));
            var dfind = UIKit.Button("Find", duelMenu.transform, "FIND A MATCH", ColorifyView.TitleColors[2], new Vector2(680, 160), FindMatch, 70);
            UIKit.Place((RectTransform)dfind.transform, 0.5f, 0.5f, new Vector2(680, 160), new Vector2(0, 160));
            var dcreate = UIKit.Button("Create", duelMenu.transform, "CREATE ROOM", ColorifyView.TitleColors[4], new Vector2(560, 130), DuelCreate, 54);
            UIKit.Place((RectTransform)dcreate.transform, 0.5f, 0.5f, new Vector2(560, 130), new Vector2(0, -10));
            var djoin = UIKit.Button("Join", duelMenu.transform, "JOIN WITH CODE", ColorifyView.TitleColors[0], new Vector2(560, 130), () => ShowJoinFor(true), 50);
            UIKit.Place((RectTransform)djoin.transform, 0.5f, 0.5f, new Vector2(560, 130), new Vector2(0, -160));
            duelStatus = UIKit.Text("Status", duelMenu.transform, "", 38, UIKit.Danger);
            UIKit.Place(duelStatus.rectTransform, 0.5f, 0.5f, new Vector2(960, 120), new Vector2(0, -320));
            var dback = UIKit.Button("Back", duelMenu.transform, "BACK", UIKit.PaperDark, new Vector2(420, 120), () => { sfx.Play(Sfx.Id.Click); Hide(duelMenu); Show(onlineMenu); }, 50);
            UIKit.Place((RectTransform)dback.transform, 0.5f, 0f, new Vector2(420, 120), new Vector2(0, 110));

            // --- Buscando rival ---
            searching = v.Overlay("Searching", 0.9f);
            searchTitle = UIKit.Text("Title", searching.transform, "", 80, UIKit.Ink, black: true);
            UIKit.Place(searchTitle.rectTransform, 0.5f, 0.5f, new Vector2(1000, 120), new Vector2(0, 260));
            searchSub = UIKit.Text("Sub", searching.transform, "", 42, UIKit.InkSoft);
            UIKit.Place(searchSub.rectTransform, 0.5f, 0.5f, new Vector2(960, 220), new Vector2(0, 90));
            var cancel = UIKit.Button("Cancel", searching.transform, "CANCEL", UIKit.PaperDark, new Vector2(420, 120), CancelSearch, 50);
            UIKit.Place((RectTransform)cancel.transform, 0.5f, 0.5f, new Vector2(420, 120), new Vector2(0, -150));

            // --- Resultado del duelo ---
            onlineResult = v.Overlay("DuelResult", 0.92f);
            resTitle = UIKit.Text("Title", onlineResult.transform, "", 140, UIKit.Ink, black: true);
            UIKit.Place(resTitle.rectTransform, 0.5f, 0.5f, new Vector2(1000, 180), new Vector2(0, 560));
            resScore = UIKit.Text("Score", onlineResult.transform, "", 130, UIKit.Ink, black: true);
            UIKit.Place(resScore.rectTransform, 0.5f, 0.5f, new Vector2(1000, 170), new Vector2(0, 360));
            resSub = UIKit.Text("Names", onlineResult.transform, "", 38, UIKit.InkSoft, black: true);
            UIKit.Place(resSub.rectTransform, 0.5f, 0.5f, new Vector2(1000, 60), new Vector2(0, 260));
            resInfo = UIKit.Text("Info", onlineResult.transform, "", 40, UIKit.InkSoft);
            UIKit.Place(resInfo.rectTransform, 0.5f, 0.5f, new Vector2(960, 160), new Vector2(0, 140));
            resAgain = UIKit.Button("Again", onlineResult.transform, "PLAY AGAIN", ColorifyView.TitleColors[2], new Vector2(600, 160), PlayAgainDuel, 68);
            UIKit.Place((RectTransform)resAgain.transform, 0.5f, 0.5f, new Vector2(600, 160), new Vector2(0, -80));
            // Tras un duelo: otra partida, volver al menú Online (lo más habitual) o al menú principal
            var resOnline = UIKit.Button("Online", onlineResult.transform, "ONLINE MENU", ColorifyView.TitleColors[0], new Vector2(420, 120), BackToOnline, 44);
            UIKit.Place((RectTransform)resOnline.transform, 0.5f, 0.5f, new Vector2(420, 120), new Vector2(-220, -260));
            var resMenu = UIKit.Button("Menu", onlineResult.transform, "MAIN MENU", UIKit.PaperDark, new Vector2(420, 120), () => { sfx.Play(Sfx.Id.Click); ShowMenu(); }, 44);
            UIKit.Place((RectTransform)resMenu.transform, 0.5f, 0.5f, new Vector2(420, 120), new Vector2(220, -260));

            // --- Ranking global ---
            leaderboard = v.Overlay("Leaderboard", 0.95f);
            var lt = UIKit.Text("Title", leaderboard.transform, "LEADERBOARD", 96, UIKit.Ink, black: true);
            UIKit.Place(lt.rectTransform, 0.5f, 1f, new Vector2(1000, 130), new Vector2(0, -120));
            // Normal (puntos) y trofeos. Supervivencia ya no está en el menú, así que tampoco su ranking.
            string[] modes = { "points", "trophies" };
            string[] labels = { "NORMAL", "TROPHIES" };
            for (int i = 0; i < modes.Length; i++)
            {
                string m = modes[i];
                var tab = UIKit.Button("Tab" + m, leaderboard.transform, labels[i], UIKit.PaperDark, new Vector2(300, 100), () => ShowLeaderboard(m), 40);
                UIKit.Place((RectTransform)tab.transform, 0.5f, 1f, new Vector2(300, 100), new Vector2(-160 + i * 320, -260));
                lbTabs[m] = tab.GetComponent<Image>();
            }
            lbList = UIKit.Rect("List", leaderboard.transform);
            UIKit.Place(lbList, 0.5f, 1f, new Vector2(1000, 1200), new Vector2(0, -930));
            lbStatus = UIKit.Text("Status", leaderboard.transform, "", 42, UIKit.InkSoft);
            UIKit.Place(lbStatus.rectTransform, 0.5f, 1f, new Vector2(960, 100), new Vector2(0, -500));
            lbMe = UIKit.Text("Me", leaderboard.transform, "", 40, UIKit.Ink, black: true);
            UIKit.Place(lbMe.rectTransform, 0.5f, 0f, new Vector2(1000, 70), new Vector2(0, 240));
            var lbBack = UIKit.Button("Back", leaderboard.transform, "BACK", UIKit.PaperDark, new Vector2(420, 120), () => { sfx.Play(Sfx.Id.Click); Hide(leaderboard); }, 50);
            UIKit.Place((RectTransform)lbBack.transform, 0.5f, 0f, new Vector2(420, 120), new Vector2(0, 110));

            // --- Panel del rival (a la derecha de la muestra) ---
            var box = UIKit.Box("Opponent", v.game, UIKit.WithAlpha(UIKit.PaperDark, 0.85f), 0.5f);
            oppPanel = box.rectTransform;
            UIKit.Place(oppPanel, 0.5f, 1f, new Vector2(300, 330), new Vector2(370, ColorifyView.Layout.SwatchY));
            var vs = UIKit.Text("VS", oppPanel, "VS", 30, UIKit.InkSoft, black: true);
            UIKit.Place(vs.rectTransform, 0.5f, 1f, new Vector2(280, 40), new Vector2(0, -32));
            oppNameText = UIKit.Text("Name", oppPanel, "", 32, UIKit.Ink, black: true);
            UIKit.Place(oppNameText.rectTransform, 0.5f, 1f, new Vector2(280, 80), new Vector2(0, -90));
            oppHitsText = UIKit.Text("Hits", oppPanel, "0", 110, UIKit.Ink, black: true);
            UIKit.Place(oppHitsText.rectTransform, 0.5f, 0.5f, new Vector2(280, 120), new Vector2(0, -20));
            oppTagText = UIKit.Text("Tag", oppPanel, "HITS", 28, UIKit.InkSoft, black: true);
            UIKit.Place(oppTagText.rectTransform, 0.5f, 0f, new Vector2(280, 40), new Vector2(0, 62));
            oppTimerBack = UIKit.Box("Timer", oppPanel, UIKit.Paper, 2f);
            UIKit.Place(oppTimerBack.rectTransform, 0.5f, 0f, new Vector2(240, 14), new Vector2(0, 28));
            oppTimerFill = UIKit.Box("Fill", oppTimerBack.transform, UIKit.InkSoft, 2f);
            var fr = oppTimerFill.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.pivot = new Vector2(0f, 0.5f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            oppPanel.gameObject.SetActive(false);

            BuildRaceOverlays();
            BuildAccountOverlays();
        }

        private void HideOnlineOverlays(bool instant)
        {
            foreach (var cg in new[] { onlineMenu, duelMenu, searching, onlineResult, leaderboard, nameEditor, signInWait }) if (cg != null) Hide(cg, instant);
            if (oppPanel != null) oppPanel.gameObject.SetActive(false);
            HideRaceOverlays(instant);
        }

        // Sale del modo Online: cierra la conexión y las pantallas
        private void LeaveOnline(bool instant)
        {
            HideOnlineOverlays(instant);
            CloseDuel();
            if (pauseButton != null) pauseButton.gameObject.SetActive(true);
            ostage = OnlineStage.None;
            if (mode == Mode.Online || mode == Mode.Race) mode = Mode.Normal;
        }

        private void CloseDuel()
        {
            if (duel != null) duel.Close();
            duel = null;
            inbox.Clear();
            queueSent = false;
            waitingResult = false;
        }

        private void EnsureIdentity()
        {
            bool changed = false;
            if (string.IsNullOrEmpty(save.playerId) || save.playerId.Length != 32) { save.playerId = PlayerNames.NewPlayerId(); changed = true; }
            if (!PlayerNames.IsValid(save.playerName)) { save.playerName = PlayerNames.Random(rng); changed = true; }
            if (changed) save.Save();
        }

        // Vuelve al menú Online desde el final de un duelo o de una carrera
        private void BackToOnline()
        {
            if (mode == Mode.Race) LeaveRace();
            ShowMenu();
            ShowOnline();
        }

        private void ShowOnline()
        {
            sfx.Play(Sfx.Id.Click);
            EnsureIdentity();
            Hide(v.menu);
            onlineName.text = CurrentName;
            RefreshTrophies();
            onlineStatus.text = save.onlineWins + save.onlineLosses > 0 ? $"<color=#{ColorUtility.ToHtmlStringRGB(UIKit.InkSoft)}>Your duels: {save.onlineWins} won · {save.onlineLosses} lost</color>" : "";
            ostage = OnlineStage.Lobby;
            Show(onlineMenu);
        }

        private void FindMatch() => StartDuelSearch("{\"t\":\"queue\",\"rules\":" + RulesJson() + "}", false);

        // Reglas del modo Normal de este juego, para que el bot juegue con los mismos números
        private string RulesJson()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return "{\"s\":" + cfg.normalStartTime.ToString(ci) + ",\"m\":" + cfg.normalMaxTime.ToString(ci) + ",\"b\":" + cfg.normalHitBonus.ToString(ci) +
                   ",\"d\":" + cfg.normalBonusDecayPerHit.ToString(ci) + ",\"n\":" + cfg.normalHitBonusMin.ToString(ci) + ",\"p\":" + cfg.normalMissPenalty.ToString(ci) +
                   ",\"g0\":" + cfg.startGridSize + ",\"gs\":" + cfg.normalHitsPerGridStep + ",\"gm\":" + cfg.maxGridSize +
                   ",\"fl\":" + cfg.normalHitFloorFraction.ToString(ci) + ",\"ve\":" + (cfg.normalVariants ? 1 : 0) +
                   ",\"vf\":" + cfg.normalVariantRoundsFirst + ",\"vp\":" + cfg.normalVariantRoundsPerGrid + "}";
        }

        private void DuelCreate() => StartDuelSearch("{\"t\":\"dcreate\"}", true);

        private void ShowDuelMenu()
        {
            sfx.Play(Sfx.Id.Click);
            EnsureIdentity();
            duelStatus.text = "";
            Hide(onlineMenu);
            Show(duelMenu);
        }

        private void PlayAgainDuel()
        {
            if (!duelPrivate) { FindMatch(); return; }
            sfx.Play(Sfx.Id.Click);
            Hide(onlineResult);
            ostage = OnlineStage.Lobby;
            mode = Mode.Normal;
            duelStatus.text = "";
            Show(duelMenu);
        }

        // first: mensaje que se envía al estar conectado (queue, dcreate o djoin)
        private void StartDuelSearch(string first, bool isPrivate)
        {
            sfx.Play(Sfx.Id.Click);
            duelFirst = first;
            duelPrivate = isPrivate;
            duelRoomCode = "";
            EnsureIdentity();
            if (attract != null) { StopCoroutine(attract); attract = null; }
            foreach (var cg in new[] { v.menu, v.gameOver, onlineMenu, onlineResult, duelMenu, raceJoin }) Hide(cg);
            if (oppPanel != null) oppPanel.gameObject.SetActive(false);
            normal = null;
            mode = Mode.Online;
            state = State.Menu;
            if (duel == null || duel.Closed) { CloseDuel(); duel = new DuelConnection(cfg, save.playerId, CurrentName, save.bestNormalHits); }
            queueSent = false;
            if (duel.Welcomed) { duel.SendRaw(duelFirst); queueSent = true; }
            ostage = OnlineStage.Searching;
            searchTitle.text = "CONNECTING";
            searchSub.text = "";
            Show(searching);
            if (attract == null) attract = StartCoroutine(SearchingBoard());
        }

        // Mientras se busca rival, el tablero gris sigue moviéndose detrás
        private IEnumerator SearchingBoard()
        {
            while (ostage == OnlineStage.Searching)
            {
                ShowBoard(ColorBoardGenerator.Grey(cfg.startGridSize, rng));
                yield return new WaitForSecondsRealtime(1.4f);
            }
            attract = null;
        }

        private void CancelSearch()
        {
            sfx.Play(Sfx.Id.Click);
            CloseDuel();   // al cerrar la conexión, el servidor nos saca de la cola (y nunca empieza un duelo a medias)
            Hide(searching);
            ostage = OnlineStage.Lobby;
            mode = Mode.Normal;
            duelStatus.text = "";
            Show(duelMenu);
        }

        // =====================================================================
        // Red
        // =====================================================================

        private void UpdateOnline()
        {
            if (ostage == OnlineStage.Searching && searchTitle != null)
            {
                int dots = (int)(Time.unscaledTime * 2.5f) % 4;
                string baseTitle = duel != null && duel.Welcomed ? (duelPrivate ? "WAITING" : "SEARCHING") : "CONNECTING";
                searchTitle.text = !string.IsNullOrEmpty(duelRoomCode) ? "ROOM " + duelRoomCode : baseTitle + new string('.', dots) + new string(' ', 3 - dots);
            }
            if (oppPanel != null && oppPanel.gameObject.activeSelf && state == State.Playing && !oppOver && oppTimeLeft > 0f)
                oppTimeLeft = Mathf.Max(0f, oppTimeLeft - Time.deltaTime);
            RefreshOppPanel();
            if (waitingResult && normal != null) RefreshWaiting();

            if (duel == null) return;
            inbox.Clear();
            duel.Poll(inbox);
            foreach (var m in inbox) HandleNet(m);
            if (duel != null && (duel.TimedOut || duel.Closed)) OnConnectionLost();
        }

        private void HandleNet(NetMsg m)
        {
            switch (m.t)
            {
                case "trophies":
                    OnTrophies(m.delta, m.trophies);
                    break;
                case "welcome":
                    if (m.trophies != save.trophies) { save.trophies = m.trophies; save.Save(); RefreshTrophies(); }
                    if (ostage == OnlineStage.Searching && !queueSent) { duel.SendRaw(duelFirst); queueSent = true; }
                    if (pendingRaceMsg != null) { duel.SendRaw(pendingRaceMsg); pendingRaceMsg = null; }
                    break;
                case "room": case "rstart": case "rhit": case "rend": case "rover":
                    HandleRace(m);
                    break;
                case "queued":
                    searchSub.text = PlayersOnline(m.online) + "\nLooking for an opponent with similar trophies.\nIf nobody's around, our bot will play you.";
                    break;
                case "droom":
                    duelRoomCode = m.room;
                    searchSub.text = "Share this code with your friend.\nThe duel starts as soon as they join.";
                    break;
                case "noghost":
                    searchSub.text = "Nobody else is around right now.\nKeep waiting, or try again later.";
                    break;
                case "match":
                    if (ostage == OnlineStage.Searching) BeginDuel(m);
                    break;
                case "opp":
                    if (m.n > oppHits) juice.Punch(oppHitsText.transform, 0.18f, 0.18f);
                    oppHits = m.n;
                    if (m.tl >= 0f) oppTimeLeft = m.tl;
                    if (m.over && !oppOver) { oppOver = true; juice.Punch(oppPanel, 0.1f, 0.25f); }
                    break;
                case "result":
                    OnDuelResult(m);
                    break;
                case "error":
                    if (NameError(m.code)) break;
                    if (RaceError(m.code)) break;
                    if (m.code == "update") ShowOnlineError("A new version of Colorify is out.\nReload the page or update the game to play online.");
                    else ShowOnlineError("The server didn't accept the connection. Try again later.");
                    break;
            }
        }

        private static string PlayersOnline(int n) => n <= 1 ? "You're the only one online." : $"{n} players online.";

        private void OnConnectionLost()
        {
            if (mode == Mode.Race || raceActive || pendingRaceMsg != null || (raceMenu != null && raceMenu.gameObject.activeSelf && raceMenu.alpha > 0.5f))
            { CloseDuel(); RaceConnectionLost(); return; }
            bool searchingNow = ostage == OnlineStage.Searching;
            bool inMatch = ostage == OnlineStage.Playing && pendingResult == null;
            CloseDuel();
            if (searchingNow) { ShowOnlineError("Couldn't reach the Colorify server.\nCheck your connection and try again."); return; }
            if (inMatch)
            {
                StopAllCoroutines();
                attract = null;
                v.countdownText.text = v.stampText.text = v.stampSub.text = "";
                state = State.GameOver;
                ShowDuelScreen("CONNECTION LOST", normal != null ? normal.hits : 0, oppHits, "The connection dropped and the duel couldn't be finished.");
                FinishRun("disconnect");
            }
        }

        private void ShowOnlineError(string text)
        {
            CloseDuel();
            Hide(searching);
            ostage = OnlineStage.Lobby;
            mode = Mode.Normal;
            onlineStatus.text = text;
            Show(onlineMenu);
        }

        // =====================================================================
        // Duelo
        // =====================================================================

        private void BeginDuel(NetMsg m)
        {
            Hide(searching);
            if (attract != null) { StopCoroutine(attract); attract = null; }
            ostage = OnlineStage.Playing;
            mode = Mode.Online;
            run = null;
            normal = new NormalRun(cfg);
            duelSeed = m.seed;
            oppInfo = m.opp ?? new NetOpp { name = "?" };
            oppHits = 0;
            oppTimeLeft = Mathf.Min(cfg.normalStartTime, normal.MaxTime);
            oppOver = false;
            pendingResult = null;
            waitingResult = false;
            trophyNote = "";
            roundTime = normal.MaxTime < float.MaxValue ? normal.MaxTime : cfg.normalStartTime;
            timeLeft = Mathf.Min(cfg.normalStartTime, normal.MaxTime);
            v.multChip.gameObject.SetActive(false);
            pauseButton.gameObject.SetActive(false);
            ResetHitRecording();
            continueOffered = false;
            runStartedAt = Time.realtimeSinceStartup;
            pausedTotal = 0f;
            oppNameText.text = oppInfo.name + (oppInfo.ghost ? "" : $"\n<size=75%><color=#{ColorUtility.ToHtmlStringRGB(UIKit.InkSoft)}>{UIKit.Thousands(oppInfo.trophies)} TROPHIES{(oppInfo.bot ? " · BOT" : "")}</color></size>");
            oppPanel.gameObject.SetActive(true);
            juice.PopIn(oppPanel, 0f, 0.3f, 0.6f);
            Analytics.Log("game_start", ("mode", "online"), ("level_id", "duel"), ("ghost", oppInfo.ghost));
            StartCoroutine(DuelCountdown(m.lead));
        }

        private IEnumerator DuelCountdown(int leadMs)
        {
            // El servidor da la salida dentro de leadMs: la cuenta atrás termina a la vez para los dos
            float wait = leadMs / 1000f - cfg.countdownSeconds * 0.6f;
            v.stampSub.text = "VS " + oppInfo.name.ToUpperInvariant() + (oppInfo.ghost ? " · GHOST" : oppInfo.bot ? " · BOT" : "");
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            v.stampSub.text = "";
            yield return CountdownThen(BeginNormal);
        }

        private System.Random DuelBoardRng() => new System.Random(unchecked(duelSeed + normal.hits * 7919));

        private string DuelHudLine()
        {
            if (!oppOver) return "";
            int need = oppHits + 1 - normal.hits;
            return need > 0 ? $"{oppInfo.name.ToUpperInvariant()} IS OUT · {need} MORE TO WIN" : "";
        }

        private void RefreshOppPanel()
        {
            if (oppPanel == null || !oppPanel.gameObject.activeSelf || oppInfo == null) return;
            oppHitsText.text = oppHits.ToString();
            bool ghost = oppInfo.ghost;
            oppTagText.text = oppOver ? "OUT OF TIME" : ghost ? "HITS · GHOST" : oppInfo.bot ? "HITS · BOT" : "HITS";
            oppTagText.color = oppOver ? UIKit.Danger : UIKit.InkSoft;
            bool showTimer = !ghost && !oppOver && oppTimeLeft >= 0f;
            oppTimerBack.gameObject.SetActive(showTimer);
            if (showTimer)
            {
                float max = roundTime > 0 ? roundTime : cfg.normalMaxTime;
                var fr = oppTimerFill.rectTransform;
                fr.anchorMax = new Vector2(Mathf.Clamp(oppTimeLeft / max, 0.02f, 1f), 1f);
                fr.offsetMin = fr.offsetMax = Vector2.zero;
            }
            oppHitsText.color = oppOver ? UIKit.InkSoft : UIKit.Ink;
        }

        // Me he quedado sin tiempo (tras «TIME!» y la ficha correcta): resultado o espera al rival
        private void DuelAfterOut()
        {
            if (pendingResult != null) { ShowDuelResult(pendingResult); return; }
            waitingResult = true;
            state = State.GameOver;
            ShowDuelScreen("TIME'S UP", normal.hits, oppHits, "", showAgain: false);
            RefreshWaiting();
        }

        private void RefreshWaiting()
        {
            int need = normal.hits - oppHits + 1;
            resScore.text = $"{normal.hits} <color=#{ColorUtility.ToHtmlStringRGB(UIKit.InkSoft)}>–</color> {oppHits}";
            resInfo.text = $"Waiting for {oppInfo.name}…\nThey need {need} more {(need == 1 ? "hit" : "hits")} to win.";
        }

        private void OnDuelResult(NetMsg m)
        {
            pendingResult = m;
            if (waitingResult) { ShowDuelResult(m); return; }
            if (state == State.Playing || state == State.Countdown)
            {
                // Sigo jugando: el rival se ha quedado fuera y le he superado (o se ha ido)
                StopAllCoroutines();
                attract = null;
                StartCoroutine(DuelEndWhilePlaying(m));
            }
            // Si está en «TIME!» (Resolving), DuelAfterOut lo enseña al terminar
        }

        private IEnumerator DuelEndWhilePlaying(NetMsg m)
        {
            state = State.Resolving;
            v.countdownText.text = "";
            v.stampText.text = m.r == "win" ? "WIN!" : m.r == "draw" ? "DRAW" : "LOST";
            v.stampSub.text = m.reason == "left" ? oppInfo.name + " left" : "";
            juice.PopIn(v.stampText.transform, 0f, 0.35f, 1.8f);
            sfx.Play(m.r == "win" ? Sfx.Id.Victory : Sfx.Id.Lose);
            Haptics.Pulse(m.r == "win" ? 40 : 80);
            yield return new WaitForSecondsRealtime(1.2f);
            v.stampText.text = v.stampSub.text = "";
            ShowDuelResult(m);
        }

        private void ShowDuelResult(NetMsg m)
        {
            waitingResult = false;
            state = State.GameOver;
            ostage = OnlineStage.Result;
            string title = m.r == "win" ? ColorifyView.RainbowTitle("YOU WIN!") : m.r == "draw" ? "DRAW" : "YOU LOSE";
            string info;
            if (m.reason == "left") info = $"{oppInfo.name} left the duel.";
            else if (m.reason == "time") info = "The duel hit the time limit.";
            else if (oppInfo.ghost) info = $"You played against a ghost: a recorded run by {oppInfo.name}.";
            else if (oppInfo.bot) info = (m.r == "win" ? "You beat the bot! " : "") + "Nobody else was around, so you played our bot. It learns from real games.";
            else info = m.r == "win" ? "Nice eyes!" : m.r == "draw" ? "Same hits. Again?" : "So close. Again?";
            if (!duelPrivate && !oppInfo.ghost && m.r == "draw") info += "\nNo trophies change hands on a draw.";
            ShowDuelScreen(title, m.you, m.them, info + (trophyNote != "" ? "\n" + trophyNote : ""));
            if (m.r == "win") { save.onlineWins++; sfx.Play(Sfx.Id.Victory); juice.Punch(resTitle.transform, 0.25f, 0.4f); }
            else if (m.r == "lose") save.onlineLosses++;
            save.Save();
            FinishRun(m.r);
        }

        private void ShowDuelScreen(string title, int you, int them, string info, bool showAgain = true)
        {
            resTitle.text = title;
            resScore.text = $"{you} <color=#{ColorUtility.ToHtmlStringRGB(UIKit.InkSoft)}>–</color> {them}";
            resSub.text = $"YOU  ·  {(oppInfo != null ? oppInfo.name.ToUpperInvariant() : "")}";
            resInfo.text = info;
            resAgain.gameObject.SetActive(showAgain);
            if (oppPanel != null) oppPanel.gameObject.SetActive(!showAgain && ostage == OnlineStage.Playing);
            Show(onlineResult);
        }

        // =====================================================================
        // Ranking global
        // =====================================================================

        private void ShowLeaderboard(string m)
        {
            sfx.Play(Sfx.Id.Click);
            EnsureIdentity();
            lbMode = m;
            foreach (var kv in lbTabs) kv.Value.color = kv.Key == m ? ColorifyView.TitleColors[0] : UIKit.PaperDark;
            for (int i = lbList.childCount - 1; i >= 0; i--) Destroy(lbList.GetChild(i).gameObject);
            lbStatus.text = "Loading…";
            lbMe.text = "";
            Show(leaderboard);
            StartCoroutine(OnlineApi.GetScores(cfg, m, save.playerId, r => { if (lbMode == m) FillLeaderboard(r); }));
        }

        private void FillLeaderboard(ScoresResponse r)
        {
            for (int i = lbList.childCount - 1; i >= 0; i--) Destroy(lbList.GetChild(i).gameObject);
            if (r == null || !r.ok) { lbStatus.text = "Couldn't load the leaderboard.\nCheck your connection."; return; }
            lbStatus.text = r.top.Count == 0 ? (r.mode == "trophies" ? "No trophies yet. Win a public duel or race!" : "No scores yet. Be the first!") : "";
            string unit = r.mode == "points" ? "pts" : r.mode == "normal" ? "hits" : r.mode == "duel" ? "wins" : r.mode == "trophies" ? "trophies" : "";
            int rows = Mathf.Min(r.top.Count, 20);
            for (int i = 0; i < rows; i++)
            {
                var e = r.top[i];
                var row = UIKit.Box("Row" + i, lbList, e.me ? ColorifyView.WithAlphaTitle(0, 0.35f) : UIKit.WithAlpha(UIKit.PaperDark, i % 2 == 0 ? 0.8f : 0.4f), 0.6f);
                UIKit.Place(row.rectTransform, 0.5f, 1f, new Vector2(980, 56), new Vector2(0, -30 - i * 60));
                var rank = UIKit.Text("Rank", row.transform, "#" + e.rank, 34, UIKit.InkSoft, black: true);
                rank.alignment = TMPro.TextAlignmentOptions.Left;
                UIKit.Place(rank.rectTransform, 0f, 0.5f, new Vector2(110, 56), new Vector2(80, 0));
                var name = UIKit.Text("Name", row.transform, e.name + (string.IsNullOrEmpty(e.info) ? "" : $"  <size=70%><color=#{ColorUtility.ToHtmlStringRGB(UIKit.InkSoft)}>{e.info}</color></size>"), 34, UIKit.Ink, black: true);
                name.alignment = TMPro.TextAlignmentOptions.Left;
                UIKit.Place(name.rectTransform, 0f, 0.5f, new Vector2(560, 56), new Vector2(420, 0));
                var score = UIKit.Text("Score", row.transform, UIKit.Thousands(e.score) + (unit != "" ? $" <size=70%>{unit}</size>" : ""), 36, UIKit.Ink, black: true);
                score.alignment = TMPro.TextAlignmentOptions.Right;
                UIKit.Place(score.rectTransform, 1f, 0.5f, new Vector2(280, 56), new Vector2(-170, 0));
            }
            lbMe.text = r.me != null && r.me.rank > 0
                ? $"{CurrentName}: #{r.me.rank} of {r.total} · {UIKit.Thousands(r.me.best)} {unit}"
                : $"{CurrentName}: no {(r.mode == "trophies" ? "trophies" : "score")} yet";
        }

        // Envía la puntuación al ranking global (Normal y Supervivencia). Sin conexión, simplemente no se envía.
        private void SubmitOnline(string m, long score, string info, List<int> samples, List<int> misses, int playMs = 0, string end = null)
        {
            if (!cfg.submitScoresOnline || score <= 0) return;
            EnsureIdentity();
            var s = samples != null && samples.Count >= 6 ? new List<int>(samples) : null;
            StartCoroutine(OnlineApi.SubmitScore(cfg, save.playerId, CurrentName, m, score, info, s, s != null ? misses : null, r =>
            {
                if (r != null && !r.ok && r.error == "bad_name") sessionName = null;   // nombre reservado por otra cuenta
                if (m == "points") StartCoroutine(LoadTuning());   // ritmo ya corregido para la siguiente partida
            }, playMs, end));
        }

        // =====================================================================
        // Trofeos y ligas
        // =====================================================================

        private static readonly string[] LeagueNames = { "GREY", "TEAL", "SAGE", "AMBER", "CORAL", "SKY", "RAINBOW" };
        private static readonly int[] LeagueFloors = { 0, 200, 500, 900, 1400, 2000, 3000 };

        private static int LeagueIndex(int trophies)
        {
            int i = 0;
            while (i + 1 < LeagueFloors.Length && trophies >= LeagueFloors[i + 1]) i++;
            return i;
        }

        private static string LeagueLabel(int trophies)
        {
            int i = LeagueIndex(trophies);
            string name = LeagueNames[i] + " LEAGUE";
            if (i == LeagueNames.Length - 1) return ColorifyView.RainbowTitle(name);
            Color c = i == 0 ? UIKit.InkSoft : i == 1 ? ColorifyView.TitleColors[1] : i == 2 ? ColorifyView.TitleColors[2]
                : i == 3 ? ColorifyView.TitleColors[3] : i == 4 ? ColorifyView.TitleColors[5] : ColorifyView.TitleColors[0];
            return $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{name}</color>";
        }

        private void RefreshTrophies()
        {
            if (onlineTrophies == null) return;
            int i = LeagueIndex(save.trophies);
            string next = i + 1 < LeagueFloors.Length ? $"  <size=75%><color=#{ColorUtility.ToHtmlStringRGB(UIKit.InkSoft)}>· next at {UIKit.Thousands(LeagueFloors[i + 1])}</color></size>" : "";
            onlineTrophies.text = $"{UIKit.Thousands(save.trophies)} TROPHIES  ·  {LeagueLabel(save.trophies)}{next}";
        }

        private void OnTrophies(int delta, int total)
        {
            int before = save.trophies;
            save.trophies = total;
            save.Save();
            RefreshTrophies();
            string sign = delta > 0 ? "+" : delta < 0 ? "–" : "±";
            trophyNote = $"<b>{sign}{Mathf.Abs(delta)} TROPHIES</b> · {UIKit.Thousands(total)} · {LeagueLabel(total)}";
            if (LeagueIndex(total) > LeagueIndex(before)) trophyNote += "\nNEW LEAGUE!";
            // Si la pantalla de resultado ya está a la vista, se añade ahí
            if (onlineResult != null && onlineResult.gameObject.activeSelf && !waitingResult && resInfo.text.IndexOf("TROPHIES") < 0)
                resInfo.text += "\n" + trophyNote;
            if (raceFinal != null && raceFinal.gameObject.activeSelf && finalSub.text.IndexOf("TROPHIES") < 0)
                finalSub.text += "\n" + trophyNote;
            if (delta > 0) sfx.Play(Sfx.Id.Card);
        }

        private void ResetHitRecording()
        {
            hitSamples.Clear();
            missBySize.Clear();
            normalPlayMs = lastHitMs = 0f;
        }

        private void RecordHit(int size)
        {
            if (hitSamples.Count < 4000) { hitSamples.Add(size); hitSamples.Add(Mathf.RoundToInt(normalPlayMs - lastHitMs)); }
            lastHitMs = normalPlayMs;
        }

        private void RecordMiss(int size)
        {
            int n;
            missBySize.TryGetValue(size, out n);
            missBySize[size] = n + 1;
        }

        private void OnDestroy() => CloseDuel();
    }
}
