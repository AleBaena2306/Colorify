using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Colorify
{
    // Bucle principal: menú (con el tablero jugando solo detrás) → cuenta atrás → ronda → cartas →
    // siguiente ronda (jugar o saltar) → … → fin de partida. Se crea solo al arrancar la escena.
    public partial class ColorifyGame : MonoBehaviour
    {
        private enum State { Menu, Countdown, Playing, Resolving, Cards, NextRound, Paused, GameOver, Victory }

        private ColorifyConfig cfg;
        private ColorifyView v;
        private Juice juice;
        private Sfx sfx;
        private Ads ads;
        private SaveData save;
        private readonly System.Random rng = new System.Random();

        private enum Mode { Normal, Survival, Online, Race }

        private State state = State.Menu;
        private Mode mode = Mode.Normal;
        private NormalRun normal;   // partida del modo Normal (null si no se juega ese modo)
        private State stateBeforePause;
        private RunState run;
        private ColorBoard current;
        private float timeLeft, roundTime, boardShownAt, displayedRoundScore, lastTickSecond;
        private int hitsThisRound, wrongThisRound;
        private float runStartedAt, pausedAt, pausedTotal;
        private bool continueOffered;
        private Coroutine attract;
        // Tipo del tablero actual: forma de las fichas y efecto de la muestra (Normal, carreras y, con rayas y
        // memoria, el duelo)
        private BoardKind boardKind = BoardKind.Plain;
        private float flashLeft, flashRevealLeft, fxTime;
        private int lastBoardSize;
        private Coroutine bannerRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<ColorifyGame>() != null) return;
            new GameObject("Colorify").AddComponent<ColorifyGame>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            cfg = ColorifyConfig.Load();
            save = SaveData.Load();
            Sfx.Enabled = save.soundOn;
            Haptics.Enabled = save.vibrationOn;

            if (Camera.main != null) Camera.main.backgroundColor = UIKit.Paper;
            v = new ColorifyView(transform);
            juice = gameObject.AddComponent<Juice>();
            juice.Init(v.fx);
            sfx = Sfx.Create(transform);
            ads = Ads.Create(transform);
            foreach (var t in v.tiles) t.onPress = OnTilePressed;

            BuildOverlays();
            ShowMenu(instant: true);
            StartCoroutine(LoadTuning());
        }

        // Lo que el servidor ha aprendido de las partidas (ritmo y dificultad de cada especial). Sin conexión,
        // el juego usa valores de referencia.
        private IEnumerator LoadTuning()
        {
            if (!cfg.submitScoresOnline) yield break;
            yield return Online.OnlineApi.GetTuning(cfg, r =>
            {
                if (r == null || !r.ok) return;
                var t = new Tuning { pace = r.pace > 0f ? r.pace : 1f };
                if (r.codes != null) foreach (var c in r.codes) t.Set(c.c, c.ms, c.miss, c.n);
                Tuning.Current = t;
            });
        }

        // =====================================================================
        // Pantallas
        // =====================================================================

        private void BuildOverlays()
        {
            // --- Menú ---
            v.menu = v.Overlay("Menu", 0.78f);
            var title = UIKit.Text("Title", v.menu.transform, ColorifyView.RainbowTitle("COLORIFY"), 96, UIKit.Ink, black: true);
            UIKit.Place(title.rectTransform, 0.5f, 0.5f, new Vector2(1000, 130), new Vector2(0, 400));
            var tag = UIKit.Text("Tagline", v.menu.transform, "Tap the matching color. Fast.", 46, UIKit.InkSoft);
            UIKit.Place(tag.rectTransform, 0.5f, 0.5f, new Vector2(1000, 70), new Vector2(0, 300));
            v.menuBest = UIKit.Text("Best", v.menu.transform, "", 40, UIKit.Ink, black: true);
            UIKit.Place(v.menuBest.rectTransform, 0.5f, 0.5f, new Vector2(1000, 70), new Vector2(0, 205));
            // Modos: Normal (puntos y tiempo) y Online. El modo Supervivencia (rondas y cartas) ya no sale en el
            // menú: su código sigue en el proyecto por si se recupera.
            var normalBtn = UIKit.Button("Normal", v.menu.transform, "PLAY", ColorifyView.TitleColors[3], new Vector2(660, 180), () => StartRun(Mode.Normal), 84);
            UIKit.Place((RectTransform)normalBtn.transform, 0.5f, 0.5f, new Vector2(660, 180), new Vector2(0, 40));
            var onlineBtn = UIKit.Button("Online", v.menu.transform, "ONLINE", ColorifyView.TitleColors[2], new Vector2(620, 140), ShowOnline, 66);
            UIKit.Place((RectTransform)onlineBtn.transform, 0.5f, 0.5f, new Vector2(620, 140), new Vector2(0, -170));
            var best = UIKit.Button("Ranking", v.menu.transform, "RANKING", ColorifyView.TitleColors[1], new Vector2(400, 110), () => ShowLeaderboard("points"), 46);
            UIKit.Place((RectTransform)best.transform, 0.5f, 0.5f, new Vector2(400, 110), new Vector2(-215, -430));
            var sound = UIKit.Button("Sound", v.menu.transform, "", ColorifyView.TitleColors[0], new Vector2(400, 110), ToggleSound, 46);
            UIKit.Place((RectTransform)sound.transform, 0.5f, 0.5f, new Vector2(400, 110), new Vector2(215, -430));
            v.menuSoundLabel = sound.GetComponentInChildren<TMPro.TextMeshProUGUI>();

            // --- Pausa ---
            v.pause = v.Overlay("Pause");
            var pt = UIKit.Text("Title", v.pause.transform, "PAUSED", 120, UIKit.Ink, black: true);
            UIKit.Place(pt.rectTransform, 0.5f, 0.5f, new Vector2(900, 160), new Vector2(0, 360));
            var resume = UIKit.Button("Resume", v.pause.transform, "RESUME", ColorifyView.TitleColors[2], new Vector2(560, 160), Resume, 70);
            UIKit.Place((RectTransform)resume.transform, 0.5f, 0.5f, new Vector2(560, 160), new Vector2(0, 150));
            var ps = UIKit.Button("Sound", v.pause.transform, "", ColorifyView.TitleColors[0], new Vector2(560, 120), ToggleSound, 50);
            UIKit.Place((RectTransform)ps.transform, 0.5f, 0.5f, new Vector2(560, 120), new Vector2(0, -10));
            v.pauseSoundLabel = ps.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            var pv = UIKit.Button("Vibration", v.pause.transform, "", ColorifyView.TitleColors[1], new Vector2(560, 120), ToggleVibration, 50);
            UIKit.Place((RectTransform)pv.transform, 0.5f, 0.5f, new Vector2(560, 120), new Vector2(0, -150));
            v.pauseVibrationLabel = pv.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            var quit = UIKit.Button("Quit", v.pause.transform, "QUIT RUN", ColorifyView.TitleColors[5], new Vector2(560, 120), QuitRun, 50);
            UIKit.Place((RectTransform)quit.transform, 0.5f, 0.5f, new Vector2(560, 120), new Vector2(0, -330));

            pauseButton = UIKit.Button("PauseButton", v.game, "II", UIKit.PaperDark, new Vector2(120, 110), Pause, 54);
            UIKit.Place((RectTransform)pauseButton.transform, 0f, 1f, new Vector2(120, 110), new Vector2(105, -110));

            // --- Cartas ---
            v.cards = v.Overlay("Cards");
            v.cardsTitle = UIKit.Text("Title", v.cards.transform, "ROUND CLEAR", 84, UIKit.Ink, black: true);
            UIKit.Place(v.cardsTitle.rectTransform, 0.5f, 0.5f, new Vector2(1000, 110), new Vector2(0, 780));
            v.cardsSub = UIKit.Text("Sub", v.cards.transform, "", 42, UIKit.InkSoft);
            UIKit.Place(v.cardsSub.rectTransform, 0.5f, 0.5f, new Vector2(1000, 110), new Vector2(0, 680));
            v.cardsRow = UIKit.Rect("Row", v.cards.transform);
            UIKit.Place(v.cardsRow, 0.5f, 0.5f, new Vector2(960, 900), new Vector2(0, 160));
            var oddsLabel = UIKit.Text("OddsLabel", v.cards.transform, "YOUR COLORS  ·  chance to be the target  ·  points per hit", 30, UIKit.InkSoft, black: true);
            UIKit.Place(oddsLabel.rectTransform, 0.5f, 0.5f, new Vector2(1000, 50), new Vector2(0, -345));
            v.cardsOdds = UIKit.Rect("Odds", v.cards.transform);
            UIKit.Place(v.cardsOdds, 0.5f, 0.5f, new Vector2(960, 130), new Vector2(0, -440));
            v.cardsReroll = UIKit.Button("Reroll", v.cards.transform, "REROLL", ColorifyView.TitleColors[1], new Vector2(860, 120), RerollCards, 42);
            UIKit.Place((RectTransform)v.cardsReroll.transform, 0.5f, 0.5f, new Vector2(860, 120), new Vector2(0, -610));

            // --- Siguiente ronda ---
            v.nextRound = v.Overlay("NextRound");
            v.nextBoss = UIKit.Text("Boss", v.nextRound.transform, "BOSS ROUND", 52, ColorifyView.TitleColors[5], black: true);
            UIKit.Place(v.nextBoss.rectTransform, 0.5f, 0.5f, new Vector2(900, 70), new Vector2(0, 470));
            v.nextTitle = UIKit.Text("Title", v.nextRound.transform, "", 130, UIKit.Ink, black: true);
            UIKit.Place(v.nextTitle.rectTransform, 0.5f, 0.5f, new Vector2(1000, 160), new Vector2(0, 360));
            v.nextInfo = UIKit.Text("Info", v.nextRound.transform, "", 52, UIKit.InkSoft);
            UIKit.Place(v.nextInfo.rectTransform, 0.5f, 0.5f, new Vector2(1000, 240), new Vector2(0, 160));
            v.nextPlay = UIKit.Button("Play", v.nextRound.transform, "PLAY", ColorifyView.TitleColors[3], new Vector2(560, 170), PlayNextRound, 80);
            UIKit.Place((RectTransform)v.nextPlay.transform, 0.5f, 0.5f, new Vector2(560, 170), new Vector2(0, -80));
            v.nextSkip = UIKit.Button("Skip", v.nextRound.transform, "SKIP · +1 CARD PICK LATER", UIKit.PaperDark, new Vector2(760, 120), SkipRound, 44);
            UIKit.Place((RectTransform)v.nextSkip.transform, 0.5f, 0.5f, new Vector2(760, 120), new Vector2(0, -260));

            // --- Fin de partida ---
            v.gameOver = v.Overlay("GameOver");
            var got = UIKit.Text("Title", v.gameOver.transform, "RUN OVER", 120, UIKit.Ink, black: true);
            UIKit.Place(got.rectTransform, 0.5f, 0.5f, new Vector2(1000, 150), new Vector2(0, 640));
            v.overScore = UIKit.Text("Score", v.gameOver.transform, "", 150, UIKit.Ink, black: true);
            UIKit.Place(v.overScore.rectTransform, 0.5f, 0.5f, new Vector2(1000, 180), new Vector2(0, 470));
            v.overBest = UIKit.Text("Best", v.gameOver.transform, "", 48, UIKit.InkSoft, black: true);
            UIKit.Place(v.overBest.rectTransform, 0.5f, 0.5f, new Vector2(1000, 70), new Vector2(0, 360));
            v.overInfo = UIKit.Text("Info", v.gameOver.transform, "", 44, UIKit.InkSoft);
            UIKit.Place(v.overInfo.rectTransform, 0.5f, 0.5f, new Vector2(1000, 70), new Vector2(0, 280));
            v.overCardsRow = UIKit.Rect("Cards", v.gameOver.transform);
            UIKit.Place(v.overCardsRow, 0.5f, 0.5f, new Vector2(1000, 80), new Vector2(0, 180));
            v.overContinue = UIKit.Button("Continue", v.gameOver.transform, "WATCH AD · ONE MORE TRY", ColorifyView.TitleColors[2], new Vector2(820, 150), ContinueWithAd, 50);
            UIKit.Place((RectTransform)v.overContinue.transform, 0.5f, 0.5f, new Vector2(820, 150), new Vector2(0, 10));
            var again = UIKit.Button("Again", v.gameOver.transform, "PLAY AGAIN", ColorifyView.TitleColors[3], new Vector2(560, 160), () => { FinishRun("lose"); StartRun(mode); }, 70);
            UIKit.Place((RectTransform)again.transform, 0.5f, 0.5f, new Vector2(560, 160), new Vector2(0, -180));
            var toMenu = UIKit.Button("Menu", v.gameOver.transform, "MENU", UIKit.PaperDark, new Vector2(420, 120), () => { FinishRun("lose"); ShowMenu(); }, 50);
            UIKit.Place((RectTransform)toMenu.transform, 0.5f, 0.5f, new Vector2(420, 120), new Vector2(0, -350));

            // --- Victoria (primera ronda 8×8 superada) ---
            v.victory = v.Overlay("Victory");
            var vt = UIKit.Text("Title", v.victory.transform, ColorifyView.RainbowTitle("YOU WIN!"), 150, UIKit.Ink, black: true);
            UIKit.Place(vt.rectTransform, 0.5f, 0.5f, new Vector2(1000, 200), new Vector2(0, 360));
            var vs = UIKit.Text("Sub", v.victory.transform, "You cleared your first 8×8 round.\nThe run goes on: how far can you get?", 50, UIKit.InkSoft);
            UIKit.Place(vs.rectTransform, 0.5f, 0.5f, new Vector2(960, 200), new Vector2(0, 170));
            var keep = UIKit.Button("Keep", v.victory.transform, "KEEP GOING", ColorifyView.TitleColors[3], new Vector2(600, 170), AfterVictory, 70);
            UIKit.Place((RectTransform)keep.transform, 0.5f, 0.5f, new Vector2(600, 170), new Vector2(0, -60));

            // --- Mejores partidas (local) ---
            v.history = v.Overlay("History");
            var ht = UIKit.Text("Title", v.history.transform, "BEST RUNS", 110, UIKit.Ink, black: true);
            UIKit.Place(ht.rectTransform, 0.5f, 1f, new Vector2(1000, 150), new Vector2(0, -150));
            v.historyList = UIKit.Rect("List", v.history.transform);
            UIKit.Place(v.historyList, 0.5f, 0.5f, new Vector2(1000, 1300), new Vector2(0, -20));
            var back = UIKit.Button("Back", v.history.transform, "BACK", UIKit.PaperDark, new Vector2(420, 120), HideHistory, 50);
            UIKit.Place((RectTransform)back.transform, 0.5f, 0f, new Vector2(420, 120), new Vector2(0, 110));

            BuildOnlineOverlays();
            RefreshToggles();
        }

        private void Show(CanvasGroup cg) { cg.transform.SetAsLastSibling(); juice.Fade(cg, 1f, 0.18f); }
        private void Hide(CanvasGroup cg, bool instant = false)
        {
            if (instant) { cg.alpha = 0f; cg.interactable = cg.blocksRaycasts = false; cg.gameObject.SetActive(false); }
            else if (cg.gameObject.activeSelf) juice.Fade(cg, 0f, 0.15f);
        }

        // =====================================================================
        // Menú (el tablero juega solo detrás)
        // =====================================================================

        private void ShowMenu(bool instant = false)
        {
            state = State.Menu;
            run = null;
            normal = null;
            foreach (var cg in new[] { v.pause, v.cards, v.nextRound, v.gameOver, v.victory, v.history }) Hide(cg, instant);
            LeaveOnline(instant);
            var bests = new List<string>();
            if (save.bestNormalPoints > 0) bests.Add($"{UIKit.Thousands(save.bestNormalPoints)} PTS");
            v.menuBest.text = bests.Count > 0 ? "BEST  ·  " + string.Join("   ·   ", bests) : "";
            v.multChip.gameObject.SetActive(true);
            v.roundLabel.text = "";
            v.scoreText.text = "";
            v.totalText.text = "";
            v.multText.text = "x1";
            SetTimer(1f);
            if (instant) { v.menu.gameObject.SetActive(true); v.menu.alpha = 1f; v.menu.interactable = v.menu.blocksRaycasts = true; }
            else Show(v.menu);
            if (attract != null) StopCoroutine(attract);
            attract = StartCoroutine(AttractLoop());
        }

        private IEnumerator AttractLoop()
        {
            // Antes de jugar, el tablero solo muestra grises: los colores aparecen al empezar la ronda
            while (state == State.Menu)
            {
                ShowBoard(ColorBoardGenerator.Grey(cfg.startGridSize, rng));
                yield return new WaitForSecondsRealtime(1.4f);
            }
        }

        private void ShowHistory()
        {
            sfx.Play(Sfx.Id.Click);
            for (int i = v.historyList.childCount - 1; i >= 0; i--) Destroy(v.historyList.GetChild(i).gameObject);
            var normalRow = UIKit.Box("Normal", v.historyList, ColorifyView.WithAlphaTitle(3, 0.35f), 0.8f);
            UIKit.Place(normalRow.rectTransform, 0.5f, 1f, new Vector2(980, 110), new Vector2(0, -55));
            var normalLabel = UIKit.Text("Label", normalRow.transform,
                save.bestNormalHits > 0 ? $"NORMAL  ·  BEST {save.bestNormalHits} HITS" : "NORMAL  ·  NO RUNS YET", 42, UIKit.Ink, black: true);
            UIKit.Stretch(normalLabel.rectTransform);
            if (save.topRuns.Count == 0)
            {
                var empty = UIKit.Text("Empty", v.historyList, "No survival runs yet. Go play!", 46, UIKit.InkSoft);
                UIKit.Place(empty.rectTransform, 0.5f, 1f, new Vector2(900, 80), new Vector2(0, -185));
            }
            for (int i = 0; i < Mathf.Min(save.topRuns.Count, 8); i++)
            {
                var r = save.topRuns[i];
                var row = UIKit.Box("Run" + i, v.historyList, i == 0 ? UIKit.PaperDark : UIKit.WithAlpha(UIKit.PaperDark, 0.5f), 0.8f);
                UIKit.Place(row.rectTransform, 0.5f, 1f, new Vector2(980, 120), new Vector2(0, -185 - i * 130));
                var label = UIKit.Text("Label", row.transform, $"#{i + 1}  {UIKit.Thousands(r.score)}", 46, UIKit.Ink, black: true);
                if (i == 0) label.text = "SURVIVAL  " + label.text;
                label.alignment = TMPro.TextAlignmentOptions.Left;
                UIKit.Place(label.rectTransform, 0f, 0.5f, new Vector2(420, 100), new Vector2(240, 14));
                var info = UIKit.Text("Info", row.transform, $"Round {r.stage}-{r.round}{(r.victory ? " · WIN" : "")}", 30, UIKit.InkSoft);
                info.alignment = TMPro.TextAlignmentOptions.Left;
                UIKit.Place(info.rectTransform, 0f, 0.5f, new Vector2(420, 50), new Vector2(240, -30));
                var cardsRow = UIKit.Rect("Cards", row.transform);
                UIKit.Place(cardsRow, 1f, 0.5f, new Vector2(520, 60), new Vector2(-280, 0));
                ColorifyView.MiniCards(cardsRow, r.cards.Count > 7 ? r.cards.GetRange(r.cards.Count - 7, 7) : r.cards, cfg, 52f);
            }
            Show(v.history);
        }

        private void HideHistory() { sfx.Play(Sfx.Id.Click); Hide(v.history); }

        // =====================================================================
        // Partida
        // =====================================================================

        private void StartRun(Mode m)
        {
            sfx.Play(Sfx.Id.Click);
            if (attract != null) { StopCoroutine(attract); attract = null; }
            foreach (var cg in new[] { v.menu, v.gameOver, v.history }) Hide(cg);
            mode = m;
            run = m == Mode.Survival ? new RunState(cfg) : null;
            normal = m == Mode.Normal ? new NormalRun(cfg, specials: true) : null;
            if (normal != null)
            {
                roundTime = normal.MaxTime < float.MaxValue ? normal.MaxTime : cfg.normalStartTime;
                timeLeft = Mathf.Min(cfg.normalStartTime, normal.MaxTime);
            }
            v.multChip.gameObject.SetActive(m == Mode.Survival || m == Mode.Normal);
            v.multText.text = "x1";
            lastBoardSize = 0;
            pauseButton.gameObject.SetActive(true);
            ResetHitRecording();
            continueOffered = false;
            runStartedAt = Time.realtimeSinceStartup;
            pausedTotal = 0f;
            Analytics.Log("game_start", ("mode", ModeId), ("level_id", "run"));
            StartCoroutine(CountdownThen(m == Mode.Survival ? (System.Action)BeginRound : BeginNormal));
        }

        private string ModeId => mode == Mode.Normal ? "normal" : mode == Mode.Online ? "online" : mode == Mode.Race ? "race" : "survival";
        private bool InRun => run != null || normal != null;
        private int ActiveGridSize => normal != null ? normal.GridSize : run != null ? run.GridSize : cfg.startGridSize;

        // ---------------- Modo Normal ----------------

        private void BeginNormal()
        {
            roundTime = normal.MaxTime < float.MaxValue ? normal.MaxTime : cfg.normalStartTime;
            timeLeft = Mathf.Min(cfg.normalStartTime, normal.MaxTime);
            lastTickSecond = -1;
            NewBoard();
            state = State.Playing;
            RefreshHud(instant: true);
        }

        private void NormalHit(Tile tile)
        {
            int code = boardKind.Code(current.size);
            int pts = 0;
            bool chainUp = false;
            if (normal.specials)
            {
                float reactionMs = (Time.time - boardShownAt) * 1000f;
                bool fast = Points.IsFast(cfg, Tuning.Current, current.size, boardKind, reactionMs);
                int chainBefore = normal.chain;
                normal.chain = fast ? Mathf.Min(Mathf.Max(1, cfg.chainMax), normal.chain + 1) : 1;
                chainUp = normal.chain > chainBefore;
                if (normal.chain > normal.bestChain) normal.bestChain = normal.chain;
                pts = Points.ForHit(cfg, Tuning.Current, current.size, boardKind, reactionMs, normal.chain, normal.hits);
                normal.points += pts;
            }
            normal.hits++;
            normal.streak++;
            if (normal.streak > normal.bestStreak) normal.bestStreak = normal.streak;
            float bonus = normal.HitBonus;
            // Un acierto siempre te deja como mínimo en el 25 % del tiempo (normalHitFloorFraction)
            float floor = (normal.MaxTime < float.MaxValue ? normal.MaxTime : cfg.normalStartTime) * cfg.normalHitFloorFraction;
            float before = timeLeft;
            timeLeft = Mathf.Min(Mathf.Max(timeLeft + bonus, floor), normal.MaxTime);
            bonus = timeLeft - before;
            if (normal.MaxTime == float.MaxValue) roundTime = Mathf.Max(roundTime, timeLeft);
            RecordHit(code);
            if (mode == Mode.Online) duel?.Hit(normal.hits, timeLeft, code);

            Color col = current.TargetColor;
            juice.Punch(tile.transform, 0.2f);
            juice.Burst(tile.transform.position, col, 8, 800f);
            juice.FloatText(v.timerBack.transform.position, "+" + bonus.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s", UIKit.Ink, 56f, 80f);
            juice.Punch(v.scoreText.transform, 0.12f, 0.18f);
            sfx.Play(Sfx.Id.Hit, 1f + 0.02f * Mathf.Min(normal.streak, 25) + (normal.specials ? 0.06f * (normal.chain - 1) : 0f));
            Haptics.Pulse(10);
            if (normal.specials)
            {
                // Puntos sobre la ficha y, si el acierto ha sido rápido, el multiplicador bien grande
                float big = Mathf.Lerp(64f, 96f, Mathf.Clamp01(pts / 600f));
                juice.FloatText(tile.transform.position + Vector3.up * 30f, "+" + UIKit.Thousands(pts), UIKit.Ink, big, 150f);
                v.multText.text = "x" + normal.chain;
                if (chainUp)
                {
                    Color c = ColorifyView.TitleColors[(normal.chain + 1) % ColorifyView.TitleColors.Length];
                    juice.FloatText(tile.transform.position - Vector3.up * 40f, $"X{normal.chain}!", c, 120f + 14f * normal.chain, 220f);
                    juice.Punch(v.multChip.transform, 0.35f, 0.3f);
                    sfx.Play(Sfx.Id.HitWarm, 1f + 0.08f * normal.chain, 0.7f);
                    if (normal.chain >= 3) juice.Shake(v.board, 6f + 3f * normal.chain, 0.15f);
                    Haptics.Pulse(20);
                }
            }
            else
            {
                // Duelo: aviso cuando crece la cuadrícula o cambia la variante de la muestra (reglas de siempre)
                var nextVariant = normal.BoardVariant;
                string notice = normal.GridSize != current.size ? $"{normal.GridSize}×{normal.GridSize}"
                    : (int)nextVariant != (int)boardKind.fx ? (nextVariant == NormalRun.Variant.Stripes ? "STRIPES!" : nextVariant == NormalRun.Variant.Flash ? "REMEMBER IT!" : "") : "";
                if (notice != "") ShowBanner(notice);
            }
            NewBoard();
        }

        // Aviso grande entre la muestra y el tablero: entra con rebote y se va hacia arriba desvaneciéndose
        private void ShowBanner(string text, bool rainbow = true)
        {
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(text, rainbow));
        }

        private IEnumerator BannerRoutine(string text, bool rainbow)
        {
            var t = v.banner;
            var rt = t.rectTransform;
            t.text = rainbow ? ColorifyView.RainbowTitle(text) : text;
            t.fontSize = text.Length > 16 ? 76 : text.Length > 11 ? 92 : 112;
            Vector2 home = new Vector2(0, ColorifyView.Layout.BoardBottom + ColorifyView.Layout.BoardSize + 40);
            sfx.Play(Sfx.Id.Card, 1.1f, 0.8f);
            Haptics.Pulse(15);
            const float pop = 0.32f, hold = 0.75f, leave = 0.3f;
            for (float e = 0f; e < pop + hold + leave; e += Time.unscaledDeltaTime)
            {
                float scale, alpha = 1f, rise = 0f, tilt;
                if (e < pop)
                {
                    float k = e / pop;
                    // Rebote: pasa de 1,25 y vuelve a 1
                    scale = k < 0.6f ? Mathf.Lerp(0.2f, 1.25f, k / 0.6f) : Mathf.Lerp(1.25f, 1f, (k - 0.6f) / 0.4f);
                    tilt = Mathf.Lerp(-12f, -3f, k);
                }
                else if (e < pop + hold)
                {
                    float k = (e - pop) / hold;
                    scale = 1f + 0.04f * Mathf.Sin(k * Mathf.PI * 3f);
                    tilt = -3f + 2f * Mathf.Sin(k * Mathf.PI * 2f);
                }
                else
                {
                    float k = (e - pop - hold) / leave;
                    scale = 1f + 0.25f * k;
                    alpha = 1f - k;
                    rise = 80f * k;
                    tilt = -1f;
                }
                rt.localScale = Vector3.one * scale;
                rt.localRotation = Quaternion.Euler(0f, 0f, tilt);
                rt.anchoredPosition = home + Vector2.up * rise;
                t.alpha = alpha;
                yield return null;
            }
            t.text = "";
            t.alpha = 1f;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = home;
            bannerRoutine = null;
        }

        private IEnumerator ClearStampSub(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (state == State.Playing) v.stampSub.text = "";
        }

        private IEnumerator NormalOver()
        {
            state = State.Resolving;
            RevealSwatch();   // al perder se ve el color (memoria y encoger)
            sfx.Play(Sfx.Id.Lose);
            Haptics.Pulse(80);
            juice.Shake(v.board, 26f, 0.4f);
            v.stampText.text = "TIME!";
            v.stampSub.text = "";
            juice.PopIn(v.stampText.transform, 0f, 0.35f, 1.8f);
            yield return new WaitForSecondsRealtime(0.9f);
            v.stampText.text = "";
            yield return RevealCorrectTile(cfg.normalRevealSeconds);
            ShowGameOver();
        }

        // Al perder en Normal: apaga las demás fichas y hace botar la correcta, con un aro, para que se vea cuál era
        private IEnumerator RevealCorrectTile(float seconds)
        {
            if (current == null || seconds <= 0f) yield break;
            int count = current.size * current.size;
            if (current.correctIndex < 0 || current.correctIndex >= count) yield break;
            var tile = v.tiles[current.correctIndex];
            var rt = tile.image.rectTransform;
            Vector2 basePos = rt.anchoredPosition;
            int sibling = rt.GetSiblingIndex();
            rt.SetAsLastSibling();

            var others = new List<(Image img, Color col)>();
            for (int i = 0; i < count; i++)
                if (i != current.correctIndex) others.Add((v.tiles[i].image, v.tiles[i].image.color));

            // Aro detrás de la ficha (hermano justo debajo de ella, que la sigue al botar)
            var ring = new GameObject("RevealRing", typeof(RectTransform), typeof(Image));
            var ringRt = (RectTransform)ring.transform;
            ringRt.SetParent(rt.parent, false);
            ringRt.SetSiblingIndex(rt.GetSiblingIndex());
            ringRt.anchorMin = rt.anchorMin; ringRt.anchorMax = rt.anchorMax; ringRt.pivot = rt.pivot;
            float pad = Mathf.Max(8f, rt.sizeDelta.x * 0.09f);
            ringRt.sizeDelta = rt.sizeDelta + Vector2.one * pad * 2f;
            var ringImg = ring.GetComponent<Image>();
            ringImg.sprite = tile.image.sprite;
            ringImg.type = tile.image.type;
            ringImg.pixelsPerUnitMultiplier = tile.image.pixelsPerUnitMultiplier;
            ringImg.raycastTarget = false;
            Color ink = UIKit.Ink;

            float bounceHeight = Mathf.Clamp(rt.sizeDelta.y * 0.22f, 14f, 60f);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.25f);                       // entrada suave
                foreach (var o in others) o.img.color = Color.Lerp(o.col, new Color(o.col.r, o.col.g, o.col.b, 0.22f), k);
                float phase = (t * 2.6f) % 1f;                             // 2,6 botes por segundo
                float hop = 4f * phase * (1f - phase);                     // parábola 0→1→0
                float squash = phase < 0.08f || phase > 0.92f ? 0.1f : 0f;  // aplastado al tocar suelo
                rt.anchoredPosition = basePos + Vector2.up * hop * bounceHeight * k;
                float grow = Mathf.Lerp(1f, 1.12f, k);
                rt.localScale = new Vector3(grow + squash * k, grow - squash * k, 1f);
                ringRt.anchoredPosition = rt.anchoredPosition;
                ringRt.localScale = rt.localScale;
                ringImg.color = new Color(ink.r, ink.g, ink.b, k * (0.65f + 0.35f * Mathf.Sin(t * 10f)));
                yield return null;
            }

            Destroy(ring);
            rt.anchoredPosition = basePos;
            rt.localScale = Vector3.one;
            rt.SetSiblingIndex(sibling);
            foreach (var o in others) o.img.color = o.col;
        }

        private IEnumerator CountdownThen(System.Action next)
        {
            state = State.Countdown;
            RefreshHud(instant: true);
            v.board.gameObject.SetActive(true);
            if (InRun) ShowBoard(ColorBoardGenerator.Grey(ActiveGridSize, rng));
            for (int i = cfg.countdownSeconds; i > 0; i--)
            {
                v.countdownText.text = i.ToString();
                juice.PopIn(v.countdownText.transform, 0f, 0.3f, 1.6f);
                sfx.Play(Sfx.Id.Tick, 1f, 0.8f);
                for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime) { while (state == State.Paused) yield return null; yield return null; }
            }
            v.countdownText.text = "";
            sfx.Play(Sfx.Id.Go);
            next();
        }

        private void BeginRound()
        {
            roundTime = run.RoundTime;
            timeLeft = roundTime;
            hitsThisRound = wrongThisRound = 0;
            displayedRoundScore = run.roundScore;
            lastTickSecond = -1;
            NewBoard();
            state = State.Playing;
            RefreshHud(instant: true);
        }

        private void NewBoard()
        {
            if (normal != null && normal.specials)
            {
                var previous = normal.kind;
                normal.kind = Progression.Pick(cfg, Tuning.Current, normal.hits, previous, rng);
                current = ColorBoardGenerator.GenerateWith(cfg, normal.GridSize, normal.MinSeparation, cfg.normalSameFamily, f => cfg.families[f].spawnWeight, rng);
                ShowBoard(current);
                ApplyKind(normal.kind);
                // Aviso grande al entrar en un especial nuevo o al crecer la cuadrícula
                if (!normal.kind.IsPlain && !normal.kind.Same(previous)) ShowBanner(normal.kind.Banner);
                else if (lastBoardSize > 0 && current.size != lastBoardSize) ShowBanner($"{current.size}×{current.size}");
                lastBoardSize = current.size;
            }
            else if (normal != null)
            {
                current = ColorBoardGenerator.GenerateWith(cfg, normal.GridSize, normal.MinSeparation, cfg.normalSameFamily, f => cfg.families[f].spawnWeight,
                    mode == Mode.Online ? DuelBoardRng() : rng);
                ShowBoard(current);
                var variant = normal.BoardVariant;
                ApplyKind(new BoardKind(Shape.Square, variant == NormalRun.Variant.Stripes ? Effect.Stripes : variant == NormalRun.Variant.Flash ? Effect.Flash : Effect.None));
            }
            else
            {
                current = ColorBoardGenerator.Generate(run, rng);
                ShowBoard(current);
            }
            boardShownAt = Time.time;
        }

        // Forma de las fichas y de la muestra, y efecto de la muestra:
        //  · Rayas blancas u oscuras por encima de la muestra.
        //  · Memoria: el color se ve unos segundos (con un anillo que se vacía) y luego se oculta.
        //  · Encoger: la muestra se hace pequeña hasta desaparecer.
        //  · Tambaleo y giro: la muestra se balancea o gira.
        // En memoria y encoger, al fallar se vuelve a ver el color un momento.
        private void ApplyKind(BoardKind kind)
        {
            boardKind = kind;
            fxTime = 0f;
            int count = current != null ? current.size * current.size : 0;
            float tileRadius = v.tiles.Count > 0 ? v.tiles[0].image.pixelsPerUnitMultiplier : 0.5f;
            for (int i = 0; i < count && i < v.tiles.Count; i++) ColorifyView.SetShape(v.tiles[i].image, kind.shape, tileRadius);
            ColorifyView.SetShape(v.swatch, kind.shape, 0.35f);
            ColorifyView.SetShape(v.swatchShadow, kind.shape, 0.35f);

            bool stripes = kind.fx == Effect.Stripes || kind.fx == Effect.DarkStripes;
            v.swatchStripes.gameObject.SetActive(stripes);
            if (stripes)
            {
                v.swatchStripes.sprite = ColorifyView.StripesSprite(cfg.normalStripeCoverage, kind.shape);
                v.swatchStripes.color = kind.fx == Effect.DarkStripes ? new Color(0.09f, 0.08f, 0.08f, 1f) : Color.white;
                v.swatchStripes.preserveAspect = true;
            }
            flashLeft = kind.fx == Effect.Flash ? cfg.normalFlashSeconds : kind.fx == Effect.Shrink ? cfg.normalShrinkSeconds : 0f;
            flashRevealLeft = 0f;
            v.swatchRing.gameObject.SetActive(kind.fx == Effect.Flash);
            v.swatchRing.fillAmount = 1f;
            SetSwatchTransform(0f, 1f, Vector2.zero);
            SetSwatchHidden(false);
        }

        private void SetSwatchTransform(float degrees, float scale, Vector2 offset)
        {
            var rot = Quaternion.Euler(0f, 0f, degrees);
            var s = new Vector3(scale, scale, 1f);
            v.swatch.rectTransform.localRotation = rot;
            v.swatch.rectTransform.localScale = s;
            v.swatch.rectTransform.anchoredPosition = v.swatchBase + offset;
            v.swatchShadow.rectTransform.localRotation = rot;
            v.swatchShadow.rectTransform.localScale = s;
            v.swatchShadow.rectTransform.anchoredPosition = v.shadowBase + offset;
        }

        // La muestra vuelve a verse entera (al perder o al acabar la ronda)
        private void RevealSwatch()
        {
            if (boardKind.fx != Effect.Flash && boardKind.fx != Effect.Shrink) return;
            flashLeft = 0f;
            flashRevealLeft = 0f;
            v.swatchRing.gameObject.SetActive(false);
            SetSwatchTransform(0f, 1f, Vector2.zero);
            SetSwatchHidden(false);
        }

        // Tras un fallo en memoria o encoger, si la muestra ya no se ve, se enseña un momento
        private void PeekSwatchAfterMiss()
        {
            if ((boardKind.fx != Effect.Flash && boardKind.fx != Effect.Shrink) || flashLeft > 0f) return;
            flashRevealLeft = cfg.normalFlashOnMiss;
            SetSwatchTransform(0f, 1f, Vector2.zero);
            SetSwatchHidden(false);
            juice.Punch(v.swatch.transform, 0.12f, 0.2f);
        }

        private void SetSwatchHidden(bool hidden)
        {
            if (current == null) return;
            v.swatch.color = hidden ? UIKit.PaperDark : current.TargetColor;
            v.swatchHidden.gameObject.SetActive(hidden);
        }

        private void UpdateBoardFx(float dt)
        {
            fxTime += dt;
            switch (boardKind.fx)
            {
                case Effect.Flash:
                case Effect.Shrink:
                {
                    bool shrink = boardKind.fx == Effect.Shrink;
                    if (flashLeft > 0f)
                    {
                        flashLeft -= dt;
                        float k = Mathf.Clamp01(flashLeft / Mathf.Max(0.01f, shrink ? cfg.normalShrinkSeconds : cfg.normalFlashSeconds));
                        if (shrink) SetSwatchTransform(0f, Mathf.Max(0.001f, k * (0.85f + 0.15f * k)), Vector2.zero);   // se acelera al final
                        else v.swatchRing.fillAmount = k;
                        if (flashLeft <= 0f)
                        {
                            v.swatchRing.gameObject.SetActive(false);
                            SetSwatchTransform(0f, 1f, Vector2.zero);
                            SetSwatchHidden(true);
                            if (shrink) juice.PopIn(v.swatch.transform, 0f, 0.18f, 0.7f);
                        }
                    }
                    else if (flashRevealLeft > 0f)
                    {
                        flashRevealLeft -= dt;
                        if (flashRevealLeft <= 0f) SetSwatchHidden(true);
                    }
                    break;
                }
                case Effect.Wobble:
                {
                    float deg = cfg.normalWobbleDegrees * Mathf.Sin(fxTime * 7.5f) * (0.8f + 0.2f * Mathf.Sin(fxTime * 2.1f));
                    var off = new Vector2(16f * Mathf.Sin(fxTime * 4.3f), 6f * Mathf.Sin(fxTime * 9.1f));
                    SetSwatchTransform(deg, 1f + 0.03f * Mathf.Sin(fxTime * 11f), off);
                    break;
                }
                case Effect.Spin:
                    SetSwatchTransform(-cfg.normalSpinSpeed * fxTime, 1f, Vector2.zero);
                    break;
            }
        }

        private void ShowBoard(ColorBoard b)
        {
            boardKind = BoardKind.Plain;
            flashLeft = flashRevealLeft = 0f;
            v.swatchStripes.gameObject.SetActive(false);
            v.swatchRing.gameObject.SetActive(false);
            v.swatchHidden.gameObject.SetActive(false);
            SetSwatchTransform(0f, 1f, Vector2.zero);
            ColorifyView.SetShape(v.swatch, Shape.Square, 0.35f);
            ColorifyView.SetShape(v.swatchShadow, Shape.Square, 0.35f);
            v.LayoutTiles(b.size);
            for (int i = 0; i < b.size * b.size && i < v.tiles.Count; i++) ColorifyView.SetShape(v.tiles[i].image, Shape.Square, v.tiles[i].image.pixelsPerUnitMultiplier);
            for (int i = 0; i < b.tiles.Count; i++)
            {
                v.tiles[i].image.color = b.tiles[i].ToColor();
                juice.PopIn(v.tiles[i].transform, 0f, 0.12f, 0.88f);
            }
            v.swatch.color = b.TargetColor;
            juice.Punch(v.swatch.transform, 0.08f, 0.18f);
        }

        private void OnTilePressed(Tile tile)
        {
            if (state != State.Playing || current == null) return;
            if (mode == Mode.Race) { RaceTap(tile); return; }
            if (normal != null) { if (tile.index == current.correctIndex) NormalHit(tile); else Miss(tile); return; }
            if (tile.index == current.correctIndex) Hit(tile);
            else Miss(tile);
        }

        private void Hit(Tile tile)
        {
            float reaction = Time.time - boardShownAt;
            int fam = current.targetFamily;
            int pts = run.PointsFor(fam, reaction);
            run.roundScore += pts;
            run.totalScore += pts;
            run.hits++;
            hitsThisRound++;
            if (reaction < run.bestReaction) run.bestReaction = reaction;

            bool warm = cfg.families[fam].basePoints >= 45;
            Color col = current.TargetColor;
            juice.Punch(tile.transform, 0.2f);
            juice.Burst(tile.transform.position, col, warm ? 16 : 8, warm ? 1200f : 800f);
            juice.FloatText(tile.transform.position, "+" + UIKit.Thousands(pts), UIKit.Ink, warm ? 90f : 70f);
            if (warm) juice.Shake(v.board, 10f, 0.15f);
            sfx.Play(warm ? Sfx.Id.HitWarm : Sfx.Id.Hit, 1f + 0.025f * Mathf.Min(hitsThisRound, 20));
            Haptics.Pulse(warm ? 25 : 10);

            if (run.roundScore >= run.Target) { StartCoroutine(RoundCleared()); return; }
            NewBoard();
        }

        private void Miss(Tile tile)
        {
            float penalty;
            if (normal != null)
            {
                normal.misses++; normal.streak = 0; penalty = normal.MissPenalty;
                if (normal.specials && normal.chain > 1) { normal.chain = 1; v.multText.text = "x1"; juice.Shake((RectTransform)v.multChip.transform, 12f, 0.25f); }
            }
            else { run.wrongTaps++; penalty = roundTime * cfg.wrongTapPenaltyFraction; }
            wrongThisRound++;
            timeLeft -= penalty;
            if (normal != null)
            {
                int code = boardKind.Code(current.size);
                RecordMiss(code);
                if (mode == Mode.Online) duel?.Miss(normal.hits, timeLeft, code);
                // Memoria y encoger: al fallar, la muestra se vuelve a ver un momento
                PeekSwatchAfterMiss();
            }
            juice.Shake((RectTransform)tile.transform, 22f, 0.25f);
            juice.Shake(v.board, 8f, 0.2f);
            juice.ColorFlash(v.timerFill, UIKit.Danger, 0.35f);
            juice.FloatText(v.timerBack.transform.position, "-" + penalty.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s", UIKit.Danger, 64f, 90f);
            sfx.Play(Sfx.Id.Wrong);
            Haptics.Pulse(50);
        }

        private void Update()
        {
            UpdateOnline();
            if (state == State.Playing && normal != null) normalPlayMs += Mathf.Min(Time.deltaTime, 0.1f) * 1000f;
            if (PausePressed())
            {
                if (state == State.Paused) Resume();
                else if (state == State.Playing || state == State.Countdown) Pause();
            }

            if (mode == Mode.Race) UpdateRace();
            else if (state == State.Playing)
            {
                timeLeft -= Mathf.Min(Time.deltaTime, 0.1f);
                if (normal != null) UpdateBoardFx(Mathf.Min(Time.deltaTime, 0.1f));
#if UNITY_EDITOR
                if (run != null && KeyPressed("n")) { run.roundScore = run.Target; StartCoroutine(RoundCleared()); return; }   // trampa: superar ronda
                if (KeyPressed("k")) timeLeft = 0f;                                                                 // trampa: perder
#endif
                int sec = Mathf.CeilToInt(timeLeft);
                if (timeLeft > 0 && timeLeft < 5f && sec != (int)lastTickSecond)
                {
                    lastTickSecond = sec;
                    sfx.Play(Sfx.Id.Tick, 1.2f, 0.7f);
                    juice.Punch(v.timerBack.transform, 0.06f, 0.15f);
                }
                if (timeLeft <= 0f)
                {
                    timeLeft = 0f;
                    if (mode == Mode.Online && normal != null) duel?.Out(normal.hits);
                    StartCoroutine(normal != null ? NormalOver() : RoundFailed());
                }
            }

            if (InRun && state != State.Menu) RefreshHud(instant: false);
        }

        private void RefreshHud(bool instant)
        {
            if (normal != null)
            {
                v.roundLabel.text = mode == Mode.Online ? "ONLINE DUEL" : "NORMAL";
                if (normal.specials)
                {
                    v.scoreText.text = $"{UIKit.Thousands(normal.points)} <size=60%><color=#{ColorUtility.ToHtmlStringRGB(UIKit.InkSoft)}>PTS</color></size>";
                    v.totalText.text = save.bestNormalPoints > 0 ? "BEST " + UIKit.Thousands(save.bestNormalPoints) : "";
                }
                else
                {
                    v.scoreText.text = $"{normal.hits} <size=60%><color=#{ColorUtility.ToHtmlStringRGB(UIKit.InkSoft)}>HITS</color></size>";
                    v.totalText.text = mode == Mode.Online ? DuelHudLine() : save.bestNormalHits > 0 ? "BEST " + save.bestNormalHits : "";
                }
                SetTimer(roundTime > 0 ? Mathf.Clamp01(timeLeft / roundTime) : 1f);
                return;
            }
            if (run == null) return;
            displayedRoundScore = instant ? run.roundScore : Mathf.Lerp(displayedRoundScore, run.roundScore, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            if (Mathf.Abs(displayedRoundScore - run.roundScore) < 1f) displayedRoundScore = run.roundScore;
            v.scoreText.text = $"{UIKit.Thousands((long)displayedRoundScore)} <size=60%><color=#{ColorUtility.ToHtmlStringRGB(UIKit.InkSoft)}>/ {UIKit.Thousands((long)run.Target)}</color></size>";
            v.totalText.text = "TOTAL " + UIKit.Thousands(run.totalScore);
            v.roundLabel.text = "ROUND " + run.RoundLabel + (run.IsBossRound ? " · BOSS" : "");
            v.multText.text = "x" + run.Multiplier.ToString("0.#");
            float frac = roundTime > 0 ? Mathf.Clamp01(timeLeft / roundTime) : 1f;
            if (state != State.Playing && state != State.Paused) frac = 1f;
            SetTimer(frac);
        }

        private void SetTimer(float frac)
        {
            var fr = v.timerFill.rectTransform;
            fr.anchorMax = new Vector2(Mathf.Max(0.02f, frac), 1f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            bool low = frac < 0.25f && state == State.Playing;
            if (low) v.timerFill.color = Color.Lerp(UIKit.Danger, UIKit.Ink, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f));
            else v.timerFill.color = UIKit.Ink;
        }

        // =====================================================================
        // Fin de ronda
        // =====================================================================

        private IEnumerator RoundCleared()
        {
            state = State.Resolving;
            float left = timeLeft;
            bool fast = left / roundTime >= cfg.fastClearFraction;
            run.streak = fast ? run.streak + 1 : 0;

            Analytics.Log("round_end", ("stage", run.stage), ("round", run.roundInStage + 1), ("grid", run.GridSize),
                ("result", "clear"), ("fast", fast), ("time_left", left), ("round_score", (long)run.roundScore),
                ("target", (long)run.Target), ("hits", hitsThisRound), ("wrong_taps", wrongThisRound), ("mult", run.Multiplier));

            sfx.Play(Sfx.Id.Clear);
            Haptics.Pulse(40);
            v.stampText.text = "CLEAR!";
            v.stampSub.text = fast ? $"FAST!  MULT x{run.Multiplier:0.#}" : "Too slow: mult streak reset";
            juice.PopIn(v.stampText.transform, 0f, 0.35f, 1.8f);
            juice.PopIn(v.stampSub.transform, 0.15f, 0.3f, 0.5f);
            juice.Punch(v.multChip.transform, fast ? 0.35f : 0.1f, 0.35f);
            for (int i = 0; i < current.tiles.Count; i++)
                juice.Burst(v.tiles[i].transform.position, v.tiles[i].image.color, 3, 700f, 22f);
            yield return new WaitForSecondsRealtime(1.1f);
            v.stampText.text = v.stampSub.text = "";

            if (!run.victoryReached && run.GridSize >= cfg.maxGridSize)
            {
                run.victoryReached = true;
                Analytics.Log("run_victory", ("stage", run.stage), ("total_score", run.totalScore), ("duration_sec", RunDuration()));
                state = State.Victory;
                sfx.Play(Sfx.Id.Victory);
                Show(v.victory);
                yield break;
            }
            ShowCards();
        }

        private void AfterVictory() { sfx.Play(Sfx.Id.Click); Hide(v.victory); ShowCards(); }

        // Tras superar una ronda: 1 elección normal + 1 extra por cada ronda saltada desde la última pantalla de cartas.
        // Cada elección es una oferta nueva de 3 cartas, de la que se coge una.
        private int picksTotal, picksDone;

        private void ShowCards()
        {
            picksTotal = 1 + run.pendingExtraPicks;
            picksDone = 0;
            run.pendingExtraPicks = 0;
            OfferNextPick();
        }

        private void OfferNextPick()
        {
            state = State.Cards;
            v.cardsTitle.text = "ROUND " + run.RoundLabel + " CLEAR";
            v.cardsSub.text = picksTotal > 1
                ? $"Pick one upgrade  ·  {picksDone + 1} of {picksTotal}" + (picksDone > 0 ? "\n<size=80%>Extra pick from a skipped round</size>" : "")
                : "Pick one upgrade";
            DealOffer();
            if (picksDone == 0) Show(v.cards);
            else juice.Punch(v.cardsSub.transform, 0.15f);
        }

        private void DealOffer()
        {
            for (int i = v.cardsRow.childCount - 1; i >= 0; i--) Destroy(v.cardsRow.GetChild(i).gameObject);
            var offer = run.OfferCards(rng);
            string source = picksDone == 0 ? "choice" : "skip_extra";
            var size = new Vector2(960, 260);
            for (int i = 0; i < offer.Count; i++)
            {
                var card = offer[i];
                var b = ColorifyView.CardButton(v.cardsRow, card, run, size, () => PickCard(card, source));
                UIKit.Place((RectTransform)b.transform, 0.5f, 1f, size, new Vector2(0, -size.y * 0.5f - i * (size.y + 30)));
                juice.PopIn(b.transform, 0.08f * i, 0.3f, 0.7f);
            }
            ColorifyView.ColorOdds(v.cardsOdds, run);
            RefreshReroll();
        }

        private void RefreshReroll()
        {
            int left = run.RerollsLeft;
            var label = v.cardsReroll.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            label.text = left > 0 ? $"REROLL  ·  {left} LEFT THIS STAGE" : "NO REROLLS LEFT THIS STAGE";
            v.cardsReroll.interactable = left > 0;
            v.cardsReroll.GetComponent<Image>().color = left > 0 ? ColorifyView.TitleColors[1] : UIKit.PaperDark;
        }

        // Cambia las 3 cartas de esta elección por otras 3 (uno por plazo)
        private void RerollCards()
        {
            if (state != State.Cards || !run.TryUseReroll()) return;
            sfx.Play(Sfx.Id.Card, 0.85f);
            Analytics.Log("card_reroll", ("stage", run.stage), ("round", run.roundInStage + 1));
            DealOffer();
        }

        private void PickCard(Card card, string source)
        {
            if (state != State.Cards) return;
            sfx.Play(Sfx.Id.Card);
            run.ApplyCard(card);
            Analytics.Log("card_pick", ("card_id", card.Id(cfg)), ("card_type", card.type.ToString()),
                ("stage", run.stage), ("source", source));
            picksDone++;
            if (picksDone < picksTotal) { OfferNextPick(); return; }
            run.AdvanceRound();
            Hide(v.cards);
            ShowNextRound();
        }

        private void ShowNextRound(string notice = null)
        {
            state = State.NextRound;
            v.nextTitle.text = "ROUND " + run.RoundLabel;
            v.nextBoss.text = run.IsBossRound ? "BOSS ROUND" : "";
            int n = run.GridSize;
            string info = $"Target  <b>{UIKit.Thousands((long)run.Target)}</b>\n{n}×{n} grid · {Mathf.RoundToInt(run.RoundTime)}s · mult x{run.Multiplier:0.#}";
            if (run.pendingExtraPicks > 0)
                info += $"\n<b>+{run.pendingExtraPicks} extra card pick{(run.pendingExtraPicks > 1 ? "s" : "")}</b> after your next clear";
            if (!string.IsNullOrEmpty(notice)) info = notice + "\n" + info;
            v.nextInfo.text = info;
            v.nextSkip.gameObject.SetActive(run.CanSkip);
            Show(v.nextRound);
        }

        private void PlayNextRound()
        {
            if (state != State.NextRound) return;
            sfx.Play(Sfx.Id.Click);
            Hide(v.nextRound);
            StartCoroutine(CountdownThen(BeginRound));
        }

        // Saltar: no juegas la ronda y no hay cartas ahora; a cambio, tras la siguiente ronda superada eliges una carta
        // extra (de una oferta nueva de 3) por cada ronda saltada. Si pierdes antes, las elecciones guardadas se pierden.
        // El multiplicador se conserva.
        private void SkipRound()
        {
            if (state != State.NextRound || !run.CanSkip) return;
            Analytics.Log("round_end", ("stage", run.stage), ("round", run.roundInStage + 1), ("grid", run.GridSize),
                ("result", "skip"), ("mult", run.Multiplier));
            run.pendingExtraPicks++;
            run.AdvanceRound();
            sfx.Play(Sfx.Id.Card);
            ShowNextRound("Round skipped");
            juice.Punch(v.nextTitle.transform, 0.2f);
        }

        private IEnumerator RoundFailed()
        {
            state = State.Resolving;
            Analytics.Log("round_end", ("stage", run.stage), ("round", run.roundInStage + 1), ("grid", run.GridSize),
                ("result", "fail"), ("round_score", (long)run.roundScore), ("target", (long)run.Target),
                ("hits", hitsThisRound), ("wrong_taps", wrongThisRound), ("mult", run.Multiplier));
            sfx.Play(Sfx.Id.Lose);
            Haptics.Pulse(80);
            juice.Shake(v.board, 26f, 0.4f);
            v.stampText.text = "TIME!";
            juice.PopIn(v.stampText.transform, 0f, 0.35f, 1.8f);
            yield return new WaitForSecondsRealtime(0.9f);
            v.stampText.text = "";
            ShowGameOver();
        }

        private void ShowGameOver()
        {
            state = State.GameOver;
            if (mode == Mode.Online && normal != null) { DuelAfterOut(); return; }
            if (normal != null) { ShowNormalGameOver(); return; }
            bool isRecord = run.totalScore > save.bestScore;
            v.overScore.text = UIKit.Thousands(run.totalScore);
            v.overBest.text = isRecord ? "NEW BEST!" : "BEST " + UIKit.Thousands(save.bestScore);
            v.overInfo.text = $"Reached round {run.RoundLabel}" + (run.victoryReached ? " · WIN" : "");
            var ids = new List<string>();
            foreach (var c in run.cards) ids.Add(c.Id(cfg));
            ColorifyView.MiniCards(v.overCardsRow, ids, cfg);

            bool nearRecord = save.bestScore > 0 && run.totalScore >= save.bestScore * cfg.continueNearRecordFraction;
            bool everyFew = save.runsSinceContinueOffer >= cfg.continueEveryRuns - 1;
            bool offer = !run.continueUsed && ads.IsReady && (nearRecord || everyFew);
            v.overContinue.gameObject.SetActive(offer);
            if (offer) continueOffered = true;
            Show(v.gameOver);
            if (isRecord) juice.Punch(v.overBest.transform, 0.3f, 0.4f);
            // Sin oferta de continuar, la partida se guarda ya (por si el jugador cierra la pestaña aquí)
            if (!offer) FinishRun("lose");
        }

        private void ShowNormalGameOver()
        {
            bool isRecord = normal.points > save.bestNormalPoints;
            v.overScore.text = UIKit.Thousands(normal.points) + " <size=40%>PTS</size>";
            v.overBest.text = isRecord ? "NEW BEST!" : "BEST " + UIKit.Thousands(save.bestNormalPoints);
            v.overInfo.text = $"{normal.hits} hits  ·  best combo x{normal.bestChain}  ·  {normal.misses} misses";
            ColorifyView.MiniCards(v.overCardsRow, null, cfg);
            bool nearRecord = save.bestNormalPoints > 0 && normal.points >= save.bestNormalPoints * cfg.continueNearRecordFraction;
            bool everyFew = save.runsSinceContinueOffer >= cfg.continueEveryRuns - 1;
            bool offer = !normal.continueUsed && ads.IsReady && (nearRecord || everyFew);
            v.overContinue.gameObject.SetActive(offer);
            if (offer) continueOffered = true;
            Show(v.gameOver);
            if (isRecord) juice.Punch(v.overBest.transform, 0.3f, 0.4f);
            if (!offer) FinishRun("lose");
        }

        private void ContinueWithAd()
        {
            if (state == State.GameOver && normal != null && !normal.continueUsed) { ContinueNormal(); return; }
            if (state != State.GameOver || run == null || run.continueUsed) return;
            v.overContinue.interactable = false;
            ads.ShowRewarded("continue", rewarded =>
            {
                v.overContinue.interactable = true;
                Analytics.Log("continue_offer", ("accepted", true), ("rewarded", rewarded), ("stage", run.stage));
                if (!rewarded) { v.overContinue.gameObject.SetActive(false); return; }
                run.continueUsed = true;
                Hide(v.gameOver);
                // Se repite la ronda con el tiempo completo y conservando los puntos ya hechos en ella
                StartCoroutine(CountdownThen(() =>
                {
                    roundTime = run.RoundTime;
                    timeLeft = roundTime;
                    lastTickSecond = -1;
                    NewBoard();
                    state = State.Playing;
                }));
            });
        }

        // Modo Normal: el anuncio devuelve el tiempo inicial una vez por partida
        private void ContinueNormal()
        {
            v.overContinue.interactable = false;
            ads.ShowRewarded("continue", rewarded =>
            {
                v.overContinue.interactable = true;
                Analytics.Log("continue_offer", ("accepted", true), ("rewarded", rewarded), ("mode", "normal"), ("hits", normal.hits));
                if (!rewarded) { v.overContinue.gameObject.SetActive(false); return; }
                normal.continueUsed = true;
                Hide(v.gameOver);
                timeLeft = Mathf.Min(cfg.normalStartTime, normal.MaxTime);
                StartCoroutine(CountdownThen(() =>
                {
                    lastTickSecond = -1;
                    NewBoard();
                    state = State.Playing;
                }));
            });
        }

        // Cierra la partida una sola vez: guardado local y evento game_end
        private void FinishRun(string result)
        {
            if (mode == Mode.Online && normal != null)
            {
                Analytics.Log("game_end", ("mode", "online"), ("level_id", "duel"), ("duration_sec", RunDuration()), ("result", result),
                    ("hits", normal.hits), ("misses", normal.misses), ("opp_hits", oppHits), ("ghost", oppInfo != null && oppInfo.ghost));
                normal = null;
                return;
            }
            if (normal != null)
            {
                SubmitOnline("points", normal.points, $"{normal.hits} hits", hitSamples, missSamples,
                    Mathf.RoundToInt(normalPlayMs), result == "quit" ? "quit" : "time");
                save.normalRunsPlayed++;
                if (normal.hits > save.bestNormalHits) save.bestNormalHits = normal.hits;
                if (normal.points > save.bestNormalPoints) save.bestNormalPoints = normal.points;
                save.runsSinceContinueOffer = continueOffered ? 0 : save.runsSinceContinueOffer + 1;
                save.Save();
                Analytics.Log("game_end", ("mode", "normal"), ("level_id", "run"), ("duration_sec", RunDuration()), ("result", result),
                    ("hits", normal.hits), ("points", normal.points), ("best_combo", normal.bestChain), ("misses", normal.misses), ("best_streak", normal.bestStreak), ("grid", normal.GridSize),
                    ("continue_used", normal.continueUsed));
                normal = null;
                return;
            }
            if (run == null) return;
            if (run.victoryReached && result != "quit") result = "win";
            var rec = new RunRecord
            {
                score = run.totalScore, stage = run.stage, round = run.roundInStage + 1, victory = run.victoryReached,
                date = System.DateTime.UtcNow.ToString("yyyy-MM-dd"),
            };
            foreach (var c in run.cards) rec.cards.Add(c.Id(cfg));
            SubmitOnline("survival", run.totalScore, $"Round {run.RoundLabel}" + (run.victoryReached ? " WIN" : ""), null, null);
            save.runsSinceContinueOffer = continueOffered ? 0 : save.runsSinceContinueOffer + 1;
            save.AddRun(rec);
            save.Save();

            Analytics.Log("game_end", ("mode", "survival"), ("level_id", "run"), ("duration_sec", RunDuration()), ("result", result),
                ("total_score", run.totalScore), ("stage_reached", run.stage), ("cards", run.cards.Count),
                ("continue_used", run.continueUsed));
            run = null;
        }

        private int RunDuration() => Mathf.RoundToInt(Time.realtimeSinceStartup - runStartedAt - pausedTotal);

        // =====================================================================
        // Pausa y ajustes
        // =====================================================================

        private void Pause()
        {
            if (state != State.Playing && state != State.Countdown) return;
            if ((mode == Mode.Online && normal != null) || mode == Mode.Race) return;   // en línea no se puede pausar
            stateBeforePause = state;
            state = State.Paused;
            pausedAt = Time.realtimeSinceStartup;
            RefreshToggles();
            Show(v.pause);
        }

        private void Resume()
        {
            if (state != State.Paused) return;
            sfx.Play(Sfx.Id.Click);
            pausedTotal += Time.realtimeSinceStartup - pausedAt;
            Hide(v.pause);
            state = stateBeforePause;
            if (state == State.Playing) boardShownAt = Time.time;   // no penaliza la velocidad por la pausa
        }

        private void QuitRun()
        {
            pausedTotal += Time.realtimeSinceStartup - pausedAt;
            StopAllCoroutines();
            v.countdownText.text = v.stampText.text = v.stampSub.text = "";
            FinishRun("quit");
            ShowMenu();
        }

        private void ToggleSound()
        {
            save.soundOn = !save.soundOn;
            Sfx.Enabled = save.soundOn;
            save.Save();
            sfx.Play(Sfx.Id.Click);
            RefreshToggles();
        }

        private void ToggleVibration()
        {
            save.vibrationOn = !save.vibrationOn;
            Haptics.Enabled = save.vibrationOn;
            save.Save();
            Haptics.Pulse(30);
            RefreshToggles();
        }

        private void RefreshToggles()
        {
            string s = save.soundOn ? "SOUND ON" : "SOUND OFF";
            if (v.menuSoundLabel != null) v.menuSoundLabel.text = s;
            if (v.pauseSoundLabel != null) v.pauseSoundLabel.text = s;
            if (v.pauseVibrationLabel != null) v.pauseVibrationLabel.text = save.vibrationOn ? "VIBRATION ON" : "VIBRATION OFF";
        }

        // Al cambiar de pestaña o bloquear el móvil, se pausa
        private void OnApplicationFocus(bool focus) { if (!focus) Pause(); }
        private void OnApplicationPause(bool paused) { if (paused) Pause(); }

        // =====================================================================
        // Teclado (ordenador): Esc o P para pausar
        // =====================================================================

        private static bool PausePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
#endif
        }

#if UNITY_EDITOR
        private static bool KeyPressed(string key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            return key == "n" ? k.nKey.wasPressedThisFrame : key == "k" && k.kKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }
#endif
    }
}
