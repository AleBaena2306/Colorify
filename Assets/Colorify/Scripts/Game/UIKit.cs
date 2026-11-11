using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Colorify
{
    // Piezas de UI creadas por código (sin prefabs ni sprites): cuadrados redondeados, textos y botones.
    // Generalizable: candidato a KovusPride.UI (fábrica de UI en runtime y «juice» básico).
    public static class UIKit
    {
        // Paleta del tema actual (Collection.Theme). Por defecto «Paper»; ColorifyGame.ApplyTheme la cambia.
        public static Color Paper = new Color32(0xF3, 0xEE, 0xE6, 0xFF);     // fondo crema mate
        public static Color PaperDark = new Color32(0xE6, 0xDF, 0xD3, 0xFF);
        public static Color Ink = new Color32(0x3B, 0x38, 0x36, 0xFF);       // texto
        public static Color InkSoft = new Color32(0x8A, 0x84, 0x7D, 0xFF);
        public static Color Danger = new Color32(0xD9, 0x6C, 0x5F, 0xFF);

        private static Sprite rounded;
        private static TMP_FontAsset fontBold, fontBlack;

        public static Sprite Rounded
        {
            get
            {
                if (rounded == null) rounded = MakeRounded(64, 22);
                return rounded;
            }
        }

        public static TMP_FontAsset FontBold => fontBold != null ? fontBold : (fontBold = LoadFont("Fonts/Nunito-ExtraBold"));
        public static TMP_FontAsset FontBlack => fontBlack != null ? fontBlack : (fontBlack = LoadFont("Fonts/Nunito-Black"));

        // Primero el asset SDF (lo crea COLORIFY > Setup); si no está, se genera en tiempo de ejecución desde el TTF
        private static TMP_FontAsset LoadFont(string path)
        {
            var asset = Resources.Load<TMP_FontAsset>(path + " SDF");
            if (asset != null) return asset;
            var ttf = Resources.Load<Font>(path);
            if (ttf != null)
            {
                asset = TMP_FontAsset.CreateFontAsset(ttf);
                if (asset != null) return asset;
            }
            Debug.LogWarning($"[Colorify] No se encontró la fuente {path}; se usa la de TextMeshPro por defecto.");
            return TMP_Settings.defaultFontAsset;
        }

        private static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        // Ancla en (ax, ay) del padre, con tamaño fijo y posición relativa al ancla
        public static RectTransform Place(RectTransform rt, float ax, float ay, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        // radiusScale: 1 = esquinas de 22 unidades; menor = esquinas más grandes
        public static Image Box(string name, Transform parent, Color color, float radiusScale = 1f, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = radiusScale;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color, bool black = false)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = black ? FontBlack : FontBold;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public static Button Button(string name, Transform parent, string label, Color color, Vector2 size, System.Action onClick, float fontSize = 64f)
        {
            var img = Box(name, parent, color, 0.6f, raycast: true);
            img.rectTransform.sizeDelta = size;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.None;
            var lbl = Text("Label", img.transform, label, fontSize, Ink, black: true);
            Stretch(lbl.rectTransform);
            b.onClick.AddListener(() => onClick?.Invoke());
            img.gameObject.AddComponent<PressScale>();
            return b;
        }

        public static string Thousands(long n) => n.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }

    // Botón que se encoge al pulsarlo y rebota al soltarlo
    // Solo toca la escala mientras se pulsa o vuelve a su tamaño: así no pisa las animaciones de entrada
    // (antes guardaba la escala al activarse y podía quedarse con la de una animación a medias)
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float pressed = 0.92f;
        private float target = 1f, current = 1f;

        public void OnPointerDown(PointerEventData e) => target = pressed;
        public void OnPointerUp(PointerEventData e) => target = 1f;
        public void OnPointerExit(PointerEventData e) => target = 1f;

        private void Update()
        {
            if (target >= 1f && current >= 1f) return;
            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
            if (target >= 1f && current > 0.995f) current = 1f;
            transform.localScale = Vector3.one * current;
        }
    }

    // Ajusta el CanvasScaler: en pantallas más anchas que 9:16 (ordenador) manda la altura; en móvil, la anchura
    public class ScalerFit : MonoBehaviour
    {
        public float designAspect = 9f / 16f;
        private CanvasScaler scaler;
        private void Awake() => scaler = GetComponent<CanvasScaler>();
        private void Update()
        {
            if (scaler == null || Screen.height == 0) return;
            scaler.matchWidthOrHeight = (float)Screen.width / Screen.height > designAspect ? 1f : 0f;
        }
    }

    // Mantiene el contenido dentro de la zona segura (muescas y barras del sistema)
    public class SafeArea : MonoBehaviour
    {
        private Rect last;
        private void Update()
        {
            var safe = Screen.safeArea;
            if (safe == last || Screen.width == 0 || Screen.height == 0) return;
            last = safe;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    // Animaciones con tiempo real (siguen aunque el juego esté en pausa) y efectos: rebote, sacudida,
    // partículas y textos flotantes. Una animación nueva sobre el mismo objeto sustituye a la anterior.
    public class Juice : MonoBehaviour
    {
        private readonly Dictionary<(int, string), Coroutine> running = new Dictionary<(int, string), Coroutine>();
        private readonly Dictionary<int, Vector2> shakeBase = new Dictionary<int, Vector2>();
        private RectTransform fxLayer;
        private readonly List<Image> particlePool = new List<Image>();
        private readonly List<TextMeshProUGUI> textPool = new List<TextMeshProUGUI>();

        public void Init(RectTransform fx) => fxLayer = fx;

        private void Run(Object target, string kind, IEnumerator routine)
        {
            var key = (target.GetInstanceID(), kind);
            if (running.TryGetValue(key, out var c) && c != null) StopCoroutine(c);
            running[key] = StartCoroutine(routine);
        }

        public static float EaseOutBack(float t) { const float c = 1.70158f; t -= 1f; return 1f + (c + 1f) * t * t * t + c * t * t; }
        public static float EaseOutCubic(float t) { t = 1f - t; return 1f - t * t * t; }

        public void Punch(Transform t, float amount = 0.15f, float duration = 0.22f) => Run(t, "scale", PunchRoutine(t, amount, duration));

        private IEnumerator PunchRoutine(Transform t, float amount, float duration)
        {
            for (float e = 0; e < duration; e += Time.unscaledDeltaTime)
            {
                float k = e / duration;
                t.localScale = Vector3.one * (1f + amount * Mathf.Sin(k * Mathf.PI) * (1f - k));
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        public void PopIn(Transform t, float delay = 0f, float duration = 0.28f, float from = 0.6f) => Run(t, "scale", PopRoutine(t, delay, duration, from));

        private IEnumerator PopRoutine(Transform t, float delay, float duration, float from)
        {
            t.localScale = Vector3.one * from;
            for (float d = 0; d < delay; d += Time.unscaledDeltaTime) yield return null;
            for (float e = 0; e < duration; e += Time.unscaledDeltaTime)
            {
                t.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, EaseOutBack(e / duration));
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        public void Shake(RectTransform rt, float amplitude = 18f, float duration = 0.25f)
        {
            int id = rt.GetInstanceID();
            if (!shakeBase.ContainsKey(id)) shakeBase[id] = rt.anchoredPosition;
            Run(rt, "shake", ShakeRoutine(rt, id, amplitude, duration));
        }

        private IEnumerator ShakeRoutine(RectTransform rt, int id, float amplitude, float duration)
        {
            Vector2 basePos = shakeBase[id];
            for (float e = 0; e < duration; e += Time.unscaledDeltaTime)
            {
                float k = 1f - e / duration;
                rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(e * 90f) * amplitude * k, Mathf.Cos(e * 70f) * amplitude * 0.35f * k);
                yield return null;
            }
            rt.anchoredPosition = basePos;
            shakeBase.Remove(id);
        }

        public void Fade(CanvasGroup cg, float to, float duration = 0.2f, System.Action done = null) => Run(cg, "fade", FadeRoutine(cg, to, duration, done));

        private IEnumerator FadeRoutine(CanvasGroup cg, float to, float duration, System.Action done)
        {
            float from = cg.alpha;
            if (to > 0f) cg.gameObject.SetActive(true);
            for (float e = 0; e < duration; e += Time.unscaledDeltaTime)
            {
                cg.alpha = Mathf.Lerp(from, to, e / duration);
                yield return null;
            }
            cg.alpha = to;
            cg.interactable = cg.blocksRaycasts = to > 0.5f;
            if (to <= 0f) cg.gameObject.SetActive(false);
            done?.Invoke();
        }

        public void ColorFlash(Graphic g, Color flash, float duration = 0.25f) => Run(g, "color", FlashRoutine(g, flash, duration));

        private IEnumerator FlashRoutine(Graphic g, Color flash, float duration)
        {
            Color baseColor = g.color;
            for (float e = 0; e < duration; e += Time.unscaledDeltaTime)
            {
                g.color = Color.Lerp(flash, baseColor, e / duration);
                yield return null;
            }
            g.color = baseColor;
        }

        // ---------------- Partículas (cuadraditos de UI) ----------------

        public void Burst(Vector3 worldPos, Color color, int count = 10, float speed = 900f, float size = 26f)
        {
            if (fxLayer == null) return;
            Vector2 local = fxLayer.InverseTransformPoint(worldPos);
            for (int i = 0; i < count; i++)
            {
                var img = GetParticle();
                img.color = color;
                img.rectTransform.sizeDelta = Vector2.one * size * Random.Range(0.6f, 1.2f);
                img.rectTransform.anchoredPosition = local;
                float ang = Random.Range(0f, Mathf.PI * 2f);
                var vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed * Random.Range(0.4f, 1f);
                StartCoroutine(ParticleRoutine(img, vel, Random.Range(0.35f, 0.6f)));
            }
        }

        private Image GetParticle()
        {
            foreach (var p in particlePool) if (!p.gameObject.activeSelf) { p.gameObject.SetActive(true); return p; }
            var img = UIKit.Box("Particle", fxLayer, Color.white, 2.5f);
            particlePool.Add(img);
            return img;
        }

        private IEnumerator ParticleRoutine(Image img, Vector2 vel, float life)
        {
            var rt = img.rectTransform;
            float rot = Random.Range(-360f, 360f);
            Color c = img.color;
            for (float e = 0; e < life; e += Time.unscaledDeltaTime)
            {
                float dt = Time.unscaledDeltaTime;
                vel *= 1f - 4f * dt;
                vel.y -= 1400f * dt;
                rt.anchoredPosition += vel * dt;
                rt.localRotation = Quaternion.Euler(0, 0, rot * e);
                float k = e / life;
                rt.localScale = Vector3.one * (1f - k * 0.6f);
                img.color = new Color(c.r, c.g, c.b, 1f - k * k);
                yield return null;
            }
            img.gameObject.SetActive(false);
        }

        // ---------------- Textos flotantes («+120», «-2s») ----------------

        public void FloatText(Vector3 worldPos, string text, Color color, float size = 72f, float rise = 160f)
        {
            if (fxLayer == null) return;
            TextMeshProUGUI t = null;
            foreach (var p in textPool) if (!p.gameObject.activeSelf) { t = p; break; }
            if (t == null)
            {
                t = UIKit.Text("Float", fxLayer, "", size, color, black: true);
                t.rectTransform.sizeDelta = new Vector2(600, 120);
                textPool.Add(t);
            }
            t.gameObject.SetActive(true);
            t.transform.SetAsLastSibling();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.rectTransform.anchoredPosition = fxLayer.InverseTransformPoint(worldPos);
            StartCoroutine(FloatRoutine(t, rise));
        }

        private IEnumerator FloatRoutine(TextMeshProUGUI t, float rise)
        {
            var rt = t.rectTransform;
            Vector2 start = rt.anchoredPosition;
            Color c = t.color;
            const float life = 0.7f;
            for (float e = 0; e < life; e += Time.unscaledDeltaTime)
            {
                float k = e / life;
                rt.anchoredPosition = start + Vector2.up * rise * EaseOutCubic(k);
                rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) / 0.85f));
                t.color = new Color(c.r, c.g, c.b, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                yield return null;
            }
            t.gameObject.SetActive(false);
        }
    }
}
