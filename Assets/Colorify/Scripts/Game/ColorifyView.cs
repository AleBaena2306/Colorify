using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Colorify
{
    // Una casilla de la cuadrícula. Responde al pulsar (no al soltar): en un juego de reflejos se nota.
    public class Tile : MonoBehaviour, IPointerDownHandler
    {
        public int index;
        public Image image;
        public Action<Tile> onPress;
        public void OnPointerDown(PointerEventData e) => onPress?.Invoke(this);
    }

    // Toda la UI del juego, creada por código. Diseño vertical de referencia: 1080 × 1920.
    // Para retocar posiciones y tamaños, cambia las constantes de Layout (o convierte la jerarquía en prefab
    // desde el modo Play si prefieres editarla en la escena).
    public class ColorifyView
    {
        public static class Layout
        {
            public const float RefW = 1080f, RefH = 1920f;
            public const float BoardSize = 960f;
            public const float BoardBottom = 140f;      // margen inferior del tablero
            public const float SwatchSize = 360f;
            public const float SwatchY = -560f;         // desde el borde superior
            public const float TimerY = -270f;
        }

        public Canvas canvas;
        public RectTransform safe, game, board, fx;
        public Image swatch, swatchShadow, timerBack, timerFill;
        public Image swatchStripes, swatchRing;      // variantes del modo Normal: rayas y memoria (anillo de tiempo)
        public TextMeshProUGUI swatchHidden;          // «?» cuando la muestra de memoria se ha ocultado
        public TextMeshProUGUI roundLabel, scoreText, totalText, multText, countdownText, stampText, stampSub;
        public TextMeshProUGUI banner;                // aviso grande de especiales y de multiplicador (X2!, X3!…)
        public Vector2 swatchBase, shadowBase;        // posición de reposo de la muestra (para tambaleo y giro)
        public Image multChip;
        public readonly List<Tile> tiles = new List<Tile>();

        // Pantallas superpuestas
        public CanvasGroup menu, pause, cards, nextRound, gameOver, victory, history;
        public TextMeshProUGUI menuBest, pauseSoundLabel, pauseVibrationLabel, menuSoundLabel;
        public TextMeshProUGUI cardsTitle, cardsSub;
        public RectTransform cardsRow, cardsOdds;
        public Button cardsReroll;
        public TextMeshProUGUI nextTitle, nextInfo, nextBoss;
        public Button nextPlay, nextSkip;
        public TextMeshProUGUI overScore, overInfo, overBest;
        public RectTransform overCardsRow;
        public Button overContinue;
        public RectTransform historyList;

        public static readonly Color[] TitleColors =
        {
            new Color32(0x6F, 0x9B, 0xD8, 0xFF), new Color32(0x5F, 0xB8, 0xAE, 0xFF), new Color32(0x86, 0xB8, 0x6C, 0xFF),
            new Color32(0xE6, 0xC1, 0x5A, 0xFF), new Color32(0xE8, 0x95, 0x5B, 0xFF), new Color32(0xD9, 0x6C, 0x5F, 0xFF),
        };

        public ColorifyView(Transform root)
        {
            BuildCanvas(root);
            BuildHud();
            BuildBoard();
            fx = UIKit.Stretch(UIKit.Rect("FX", safe));
        }

        private void BuildCanvas(Transform root)
        {
            var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(root, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Layout.RefW, Layout.RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cgo.AddComponent<ScalerFit>();

            var bg = UIKit.Rect("Background", cgo.transform);
            UIKit.Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = UIKit.Paper;

            safe = UIKit.Stretch(UIKit.Rect("SafeArea", cgo.transform));
            safe.gameObject.AddComponent<SafeArea>();
            game = UIKit.Stretch(UIKit.Rect("Game", safe));

            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.transform.SetParent(root, false);
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        private void BuildHud()
        {
            roundLabel = UIKit.Text("Round", game, "ROUND 1-1", 40, UIKit.InkSoft, black: true);
            UIKit.Place(roundLabel.rectTransform, 0.5f, 1f, new Vector2(600, 60), new Vector2(0, -70));
            roundLabel.characterSpacing = 8;

            scoreText = UIKit.Text("Score", game, "0 / 250", 92, UIKit.Ink, black: true);
            UIKit.Place(scoreText.rectTransform, 0.5f, 1f, new Vector2(760, 110), new Vector2(0, -150));

            totalText = UIKit.Text("Total", game, "TOTAL 0", 34, UIKit.InkSoft);
            UIKit.Place(totalText.rectTransform, 0.5f, 1f, new Vector2(600, 50), new Vector2(0, -218));

            multChip = UIKit.Box("Mult", game, UIKit.Ink, 0.5f);
            UIKit.Place(multChip.rectTransform, 1f, 1f, new Vector2(190, 110), new Vector2(-125, -110));
            multText = UIKit.Text("Label", multChip.transform, "x1", 64, UIKit.Paper, black: true);
            UIKit.Stretch(multText.rectTransform);

            timerBack = UIKit.Box("Timer", game, UIKit.PaperDark, 2f);
            UIKit.Place(timerBack.rectTransform, 0.5f, 1f, new Vector2(Layout.BoardSize, 28), new Vector2(0, Layout.TimerY));
            timerFill = UIKit.Box("Fill", timerBack.transform, UIKit.Ink, 2f);
            var fr = timerFill.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.pivot = new Vector2(0f, 0.5f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;

            swatchShadow = UIKit.Box("SwatchShadow", game, UIKit.PaperDark, 0.35f);
            UIKit.Place(swatchShadow.rectTransform, 0.5f, 1f, Vector2.one * Layout.SwatchSize, new Vector2(0, Layout.SwatchY - 14));
            swatch = UIKit.Box("Swatch", game, Color.white, 0.35f);
            UIKit.Place(swatch.rectTransform, 0.5f, 1f, Vector2.one * Layout.SwatchSize, new Vector2(0, Layout.SwatchY));
            swatchBase = swatch.rectTransform.anchoredPosition;
            shadowBase = swatchShadow.rectTransform.anchoredPosition;

            // Rayas blancas sobre la muestra (con la misma forma redondeada) y «?» para la muestra oculta
            swatchStripes = new GameObject("Stripes", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            swatchStripes.rectTransform.SetParent(swatch.transform, false);
            UIKit.Stretch(swatchStripes.rectTransform);
            swatchStripes.raycastTarget = false;
            swatchStripes.gameObject.SetActive(false);
            swatchHidden = UIKit.Text("Hidden", swatch.transform, "?", 200, UIKit.InkSoft, black: true);
            UIKit.Stretch(swatchHidden.rectTransform);
            swatchHidden.gameObject.SetActive(false);

            // Anillo alrededor de la muestra que se vacía mientras se ve el color (rondas de memoria)
            swatchRing = new GameObject("Ring", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            swatchRing.rectTransform.SetParent(game, false);
            UIKit.Place(swatchRing.rectTransform, 0.5f, 1f, Vector2.one * (Layout.SwatchSize + 2f * RingMargin), new Vector2(0, Layout.SwatchY));
            swatchRing.sprite = RingSprite();
            swatchRing.type = Image.Type.Filled;
            swatchRing.fillMethod = Image.FillMethod.Radial360;
            swatchRing.fillOrigin = (int)Image.Origin360.Top;
            swatchRing.fillClockwise = false;
            swatchRing.color = UIKit.Ink;
            swatchRing.raycastTarget = false;
            swatchRing.gameObject.SetActive(false);
        }

        // Rayas diagonales (blancas; se tiñen de negro para las rayas oscuras) recortadas con la forma de la
        // muestra. coverage: parte tapada (0-1)
        private static readonly Dictionary<int, Sprite> stripesCache = new Dictionary<int, Sprite>();
        public static Sprite StripesSprite(float coverage, Shape shape = Shape.Square)
        {
            int key = (int)shape * 1000 + Mathf.RoundToInt(coverage * 100f);
            Sprite cached;
            if (stripesCache.TryGetValue(key, out cached) && cached != null) return cached;
            const int size = 180;
            const float radius = size * 0.175f;   // como la muestra (radio ≈ 17,5 % del lado)
            const float period = 36f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float inside;
                    if (shape == Shape.Square)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                        inside = Mathf.Clamp01(radius - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) + 0.5f);
                    }
                    else inside = ShapeCoverage(shape, x, y, size);
                    float t = Mathf.Repeat((x + y) / period, 1f);
                    float edge = 1.5f / period;
                    float stripe = Mathf.Clamp01(Mathf.Min(t, coverage - t) / edge + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255f * inside * stripe));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            stripesCache[key] = sprite;
            return sprite;
        }

        // ---------------- Formas (círculo, triángulo, rombo) ----------------

        // Distancia con signo al borde de la forma (positiva dentro), con u, v de -1 a 1 en su caja
        private static float ShapeDistance(Shape shape, float u, float v)
        {
            switch (shape)
            {
                case Shape.Circle: return 0.96f - Mathf.Sqrt(u * u + v * v);
                case Shape.Diamond:
                {
                    // Rombo con los lados apenas abombados (superelipse con exponente 1,1): ocupa más que uno recto
                    const float p = 1.1f;
                    float r = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), p) + Mathf.Pow(Mathf.Abs(v), p), 1f / p);
                    return (0.99f - r) * 0.74f;
                }
                case Shape.Triangle:
                {
                    // Triángulo equilátero apuntando hacia arriba, centrado en su caja
                    const float bottom = -0.9f, top = 0.97f;
                    float slope = (top - bottom) / 1f;
                    float sideD = (top - slope * Mathf.Abs(u) - v) / Mathf.Sqrt(1f + slope * slope);
                    return Mathf.Min(v - bottom, sideD);
                }
                default:
                {
                    // Cuadrado redondeado (como la muestra)
                    const float rr = 0.4f, edge = 0.99f;
                    float qx = Mathf.Abs(u) - (edge - rr), qy = Mathf.Abs(v) - (edge - rr);
                    return -(new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - rr);
                }
            }
        }

        // Cobertura (0-1, con antialias) del píxel (x, y) de una imagen cuadrada de lado «size»
        private static float ShapeCoverage(Shape shape, int x, int y, int size)
        {
            float half = size * 0.5f;
            float u = (x + 0.5f - half) / half, v = (y + 0.5f - half) / half;
            return Mathf.Clamp01(ShapeDistance(shape, u, v) * half + 0.5f);
        }

        private static readonly Dictionary<Shape, Sprite> shapeCache = new Dictionary<Shape, Sprite>();
        public static Sprite ShapeSprite(Shape shape)
        {
            Sprite cached;
            if (shapeCache.TryGetValue(shape, out cached) && cached != null) return cached;
            const int size = 160;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255f * ShapeCoverage(shape, x, y, size)));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            shapeCache[shape] = sprite;
            return sprite;
        }

        // Las formas con picos (rombo y triángulo) se dibujan algo más grandes que su casilla para que, a la vista,
        // ocupen lo mismo que el cuadrado (sin llegar a tocar a las vecinas)
        public static float ShapeScale(Shape shape) => shape == Shape.Diamond ? 1.07f : shape == Shape.Triangle ? 1.07f : shape == Shape.Circle ? 1.03f : 1f;

        // Cambia la forma de una imagen. El cuadrado es el de siempre (sprite redondeado, 9-slice).
        // baseSize: lado de la casilla sin escalar
        public static void SetShape(Image img, Shape shape, float squareRadius, float baseSize)
        {
            img.rectTransform.sizeDelta = Vector2.one * baseSize * ShapeScale(shape);
            SetShape(img, shape, squareRadius);
        }

        public static void SetShape(Image img, Shape shape, float squareRadius)
        {
            if (shape == Shape.Square)
            {
                img.sprite = UIKit.Rounded;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = squareRadius;
                img.preserveAspect = false;
            }
            else
            {
                img.sprite = ShapeSprite(shape);
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
            }
        }

        // Contorno redondeado (para el anillo de tiempo de la muestra de memoria)
        // Anillo de tiempo de la muestra de memoria: contorno de la forma de la figura, a la misma distancia de su
        // borde por todos los lados (RingMargin, RingGap y RingThickness en unidades de pantalla)
        private const float RingMargin = 40f, RingGap = 12f, RingThickness = 9f;
        private static readonly Dictionary<Shape, Sprite> ringCache = new Dictionary<Shape, Sprite>();
        public static Sprite RingSprite(Shape shape = Shape.Square)
        {
            Sprite cached;
            if (ringCache.TryGetValue(shape, out cached) && cached != null) return cached;
            const int size = 240;
            float swatch = Layout.SwatchSize * ShapeScale(shape), ring = swatch + 2f * RingMargin;
            float k = ring / swatch, half = size * 0.5f, unitsHalf = swatch * 0.5f;
            float pxU = k / half, gap = RingGap / unitsHalf, thick = RingThickness / unitsHalf;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f - half) / half * k, v = (y + 0.5f - half) / half * k;
                    float d = ShapeDistance(shape, u, v);
                    float a = Mathf.Clamp01((-gap - d) / pxU + 0.5f) * Mathf.Clamp01((d + gap + thick) / pxU + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            ringCache[shape] = sprite;
            return sprite;
        }

        // Pone el anillo de tiempo con la forma y el tamaño de la muestra
        public void FitRing(Shape shape)
        {
            swatchRing.sprite = RingSprite(shape);
            swatchRing.rectTransform.sizeDelta = Vector2.one * (Layout.SwatchSize * ShapeScale(shape) + 2f * RingMargin);
        }

        private void BuildBoard()
        {
            board = UIKit.Rect("Board", game);
            UIKit.Place(board, 0.5f, 0f, Vector2.one * Layout.BoardSize, new Vector2(0, Layout.BoardBottom + Layout.BoardSize * 0.5f));

            for (int i = 0; i < 64; i++)
            {
                var img = UIKit.Box("Tile" + i, board, Color.white, 0.5f, raycast: true);
                var t = img.gameObject.AddComponent<Tile>();
                t.index = i; t.image = img;
                img.gameObject.SetActive(false);
                tiles.Add(t);
            }

            countdownText = UIKit.Text("Countdown", game, "", 300, UIKit.Ink, black: true);
            UIKit.Place(countdownText.rectTransform, 0.5f, 0f, new Vector2(600, 400), board.anchoredPosition);

            stampText = UIKit.Text("Stamp", game, "", 150, UIKit.Ink, black: true);
            UIKit.Place(stampText.rectTransform, 0.5f, 0f, new Vector2(1000, 200), board.anchoredPosition + new Vector2(0, 60));
            stampSub = UIKit.Text("StampSub", game, "", 56, UIKit.Ink, black: true);
            UIKit.Place(stampSub.rectTransform, 0.5f, 0f, new Vector2(1000, 90), board.anchoredPosition - new Vector2(0, 80));

            // Aviso grande entre la muestra y el tablero (entra con rebote y se va hacia arriba)
            banner = UIKit.Text("Banner", game, "", 104, UIKit.Ink, black: true);
            UIKit.Place(banner.rectTransform, 0.5f, 0f, new Vector2(1060, 160), new Vector2(0, Layout.BoardBottom + Layout.BoardSize + 40));
            banner.raycastTarget = false;
            banner.enableWordWrapping = false;
        }

        public float TileSize { get; private set; }

        public void LayoutTiles(int n)
        {
            float gap = n <= 3 ? 26f : n <= 5 ? 18f : 12f;
            float size = (Layout.BoardSize - gap * (n + 1)) / n;
            TileSize = size;
            float radius = n <= 3 ? 0.45f : n <= 5 ? 0.7f : 1.1f;
            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                bool on = i < n * n;
                t.gameObject.SetActive(on);
                if (!on) continue;
                int x = i % n, y = i / n;
                var rt = t.image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = Vector2.one * size;
                rt.anchoredPosition = new Vector2(gap + size * 0.5f + x * (size + gap), -(gap + size * 0.5f + y * (size + gap)));
                rt.localScale = Vector3.one;
                t.image.pixelsPerUnitMultiplier = radius;
            }
        }

        // ---------------- Pantallas ----------------

        public CanvasGroup Overlay(string name, float dim = 0.92f)
        {
            var rt = UIKit.Stretch(UIKit.Rect(name, safe));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = UIKit.WithAlpha(UIKit.Paper, dim);
            img.raycastTarget = true;   // bloquea los toques al tablero
            var cg = rt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.interactable = cg.blocksRaycasts = false;
            rt.gameObject.SetActive(false);
            return cg;
        }

        public static Color WithAlphaTitle(int i, float a) => UIKit.WithAlpha(TitleColors[i % TitleColors.Length], a);

        public static string RainbowTitle(string word)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < word.Length; i++)
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(TitleColors[i % TitleColors.Length])).Append('>').Append(word[i]).Append("</color>");
            return sb.ToString();
        }

        // Carta de mejora: muestra de color (o «x2»), título y descripción
        // Carta de mejora: muestra de color (o «x2»), título, efecto concreto en esta partida, efecto en la
        // puntuación media por acierto y probabilidad de que la carta aparezca en una oferta
        public static Button CardButton(Transform parent, Card card, RunState run, Vector2 size, Action onClick)
        {
            var cfg = run.cfg;
            var b = UIKit.Button("Card", parent, "", Color.white, size, onClick);
            var root = (RectTransform)b.transform;
            float chipSize = size.y - 70;
            var chip = UIKit.Box("Chip", root, card.type == CardType.DoubleMult ? UIKit.Ink : cfg.families[card.family].SwatchColor, 0.45f);
            UIKit.Place(chip.rectTransform, 0f, 0.5f, new Vector2(chipSize, chipSize), new Vector2(35 + chipSize * 0.5f, 0));
            string glyph = card.type == CardType.MoreChance ? "%" : card.type == CardType.MorePoints ? "+" : "x2";
            var g = UIKit.Text("Glyph", chip.transform, glyph, 84, card.type == CardType.DoubleMult ? UIKit.Paper : UIKit.Ink, black: true);
            UIKit.Stretch(g.rectTransform);

            float textX = chipSize + 65;
            var title = UIKit.Text("Title", root, card.Title(cfg), 54, UIKit.Ink, black: true);
            title.alignment = TextAlignmentOptions.Left;
            UIKit.Place(title.rectTransform, 0f, 1f, new Vector2(size.x - textX - 220, 70), new Vector2(textX + (size.x - textX - 220) * 0.5f, -52));

            var chance = UIKit.Text("Chance", root, $"{run.OfferChance(card) * 100f:0}% <size=70%>CHANCE</size>", 34, UIKit.InkSoft, black: true);
            chance.alignment = TextAlignmentOptions.Right;
            UIKit.Place(chance.rectTransform, 1f, 1f, new Vector2(240, 60), new Vector2(-150, -52));

            var effect = UIKit.Text("Effect", root, run.EffectText(card), 38, UIKit.Ink);
            effect.alignment = TextAlignmentOptions.Left;
            UIKit.Place(effect.rectTransform, 0f, 1f, new Vector2(size.x - textX - 30, 56), new Vector2(textX + (size.x - textX - 30) * 0.5f, -122));

            float before = run.AveragePointsPerHit(), after = run.AveragePointsWith(card);
            float pct = before > 0 ? (after / before - 1f) * 100f : 0f;
            var score = UIKit.Text("Score", root,
                $"Avg per hit {before:0} → <b>{after:0}</b>  <color=#{ColorUtility.ToHtmlStringRGB(pct >= 0 ? new Color32(0x4E, 0x8F, 0x5A, 0xFF) : (Color32)UIKit.Danger)}>{(pct >= 0 ? "+" : "")}{pct:0}%</color>",
                34, UIKit.InkSoft);
            score.alignment = TextAlignmentOptions.Left;
            UIKit.Place(score.rectTransform, 0f, 1f, new Vector2(size.x - textX - 30, 50), new Vector2(textX + (size.x - textX - 30) * 0.5f, -182));
            return b;
        }

        // Panel «Your colors»: probabilidad de cada color como objetivo y sus puntos por acierto (sin multiplicador)
        public static void ColorOdds(RectTransform row, RunState run)
        {
            for (int i = row.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(row.GetChild(i).gameObject);
            var cfg = run.cfg;
            int n = cfg.families.Length;
            float gap = 14f, w = (row.rect.width > 0 ? row.rect.width : 940f), cw = (w - gap * (n - 1)) / n;
            for (int f = 0; f < n; f++)
            {
                var chip = UIKit.Box("Odds", row, cfg.families[f].SwatchColor, 0.8f);
                UIKit.Place(chip.rectTransform, 0f, 0.5f, new Vector2(cw, 130), new Vector2(cw * 0.5f + f * (cw + gap), 0));
                var t = UIKit.Text("T", chip.transform,
                    $"{run.FamilyChance(f) * 100f:0}%\n<size=70%>{run.FamilyPoints(f):0} pts</size>", 40, UIKit.Ink, black: true);
                UIKit.Stretch(t.rectTransform);
            }
        }

        // Fila de mini-cartas (pantalla de fin de partida e historial)
        public static void MiniCards(RectTransform row, List<string> cardIds, ColorifyConfig cfg, float size = 64f)
        {
            for (int i = row.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(row.GetChild(i).gameObject);
            if (cardIds == null) return;
            int max = Mathf.Min(cardIds.Count, 14);
            float gap = 10f, total = max * size + (max - 1) * gap;
            for (int i = 0; i < max; i++)
            {
                string id = cardIds[i];
                Color c = UIKit.Ink; string glyph = "x2";
                int sep = id.IndexOf('_');
                if (sep > 0 && id != "double_mult")
                {
                    string fam = id.Substring(sep + 1);
                    glyph = id.StartsWith("chance") ? "%" : "+";
                    foreach (var f in cfg.families) if (f.id == fam) c = f.SwatchColor;
                }
                var chip = UIKit.Box("Mini", row, c, 1.2f);
                UIKit.Place(chip.rectTransform, 0.5f, 0.5f, Vector2.one * size, new Vector2(-total * 0.5f + size * 0.5f + i * (size + gap), 0));
                var t = UIKit.Text("G", chip.transform, glyph, size * 0.5f, glyph == "x2" ? UIKit.Paper : UIKit.Ink, black: true);
                UIKit.Stretch(t.rectTransform);
            }
        }
    }
}
