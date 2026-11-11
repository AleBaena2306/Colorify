using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colorify
{
    // Tutorial de COLLECTION tras la primera partida (una sola vez por jugador): todo en negro translúcido menos el
    // botón COLLECTION del menú, una mano que lo señala (Kenney Cursor Pack, CC0) y no se puede tocar nada más hasta
    // pulsarlo. Enseña el camino menú → COLLECTION y engancha con lo que ya se ha desbloqueado en esa partida.
    // Técnica: canvas anidados con orden propio (velo 50 < botón 60 < mano 70). El velo recibe todos los toques
    // salvo los del botón, que queda por encima. Al terminar, el botón vuelve a su orden normal.
    public partial class ColorifyGame
    {
        private const int CoachDimOrder = 50, CoachTargetOrder = 60, CoachHandOrder = 70;
        private const float CoachDelay = 0.55f;      // deja que el menú termine de aparecer
        private const float CoachDimAlpha = 0.78f;
        private const float CoachTapPeriod = 1.15f;  // un «toque» de la mano cada 1,15 s

        private RectTransform coachRoot;
        private Canvas coachTargetCanvas;
        private Coroutine coachRoutine;
        private float coachShownAt;
        private static Sprite handSprite;
        private Button overAgain, overMenu;   // PLAY AGAIN y MENU del fin de partida

        private bool CoachActive => coachRoot != null;
        private bool CollectionCoachPending => save != null && !save.collectionCoachDone && save.normalRunsPlayed > 0;

        // Se llama al mostrar el menú
        private void MaybeStartCollectionCoach()
        {
            if (!CollectionCoachPending || CoachActive || coachRoutine != null || menuCollection == null) return;
            coachRoutine = StartCoroutine(CollectionCoach());
        }

        private IEnumerator CollectionCoach()
        {
            for (float t = 0f; t < CoachDelay; t += Time.unscaledDeltaTime) yield return null;
            // Si mientras tanto el jugador ya ha salido del menú (o hay otra pantalla delante), será la próxima vez
            if (state != State.Menu || !v.menu.gameObject.activeSelf || !menuCollection.gameObject.activeInHierarchy || OverlayAboveMenu())
            {
                coachRoutine = null;
                yield break;
            }

            BuildCoach(out var group, out var halo, out var hand);
            var target = (RectTransform)menuCollection.transform;
            sfx.Play(Sfx.Id.Card);
            Haptics.Pulse(30);
            Analytics.Log("tutorial_start", ("step", "collection"), ("runs", save.normalRunsPlayed));

            while (CoachActive)
            {
                // Otra pantalla ha tomado el control (invitación del portal, etc.): se quita sin darlo por visto
                if (state != State.Menu) { CloseCoach(); break; }
                // Unity puede perder el orden propio del canvas anidado al reactivar objetos: se reafirma
                if (!coachTargetCanvas.overrideSorting || coachTargetCanvas.sortingOrder != CoachTargetOrder)
                {
                    coachTargetCanvas.overrideSorting = true;
                    coachTargetCanvas.sortingOrder = CoachTargetOrder;
                }

                float e = Time.unscaledTime - coachShownAt;
                group.alpha = Mathf.Clamp01(e / 0.3f);

                // Posición y tamaño del botón en el espacio del tutorial (siguen a cambios de tamaño de ventana)
                Vector2 center = coachRoot.InverseTransformPoint(target.TransformPoint(target.rect.center));
                float ratio = target.parent != null ? target.parent.lossyScale.x / Mathf.Max(0.0001f, coachRoot.lossyScale.x) : 1f;
                Vector2 size = target.rect.size * ratio;

                // Mano: se acerca, «pulsa» y se retira. El dedo cae en la parte baja derecha del botón
                float p = (e % CoachTapPeriod) / CoachTapPeriod;
                Vector2 touch = center + new Vector2(size.x * 0.22f, -size.y * 0.18f);
                Vector2 rest = touch + new Vector2(70f, -80f);
                float approach = p < 0.42f ? Juice.EaseOutCubic(p / 0.42f) : p < 0.62f ? 1f : 1f - Juice.EaseOutCubic((p - 0.62f) / 0.38f);
                hand.anchoredPosition = Vector2.LerpUnclamped(rest, touch, approach);
                bool pressing = p >= 0.42f && p < 0.62f;
                hand.localScale = Vector3.one * (pressing ? 0.86f : 1f);

                // Halo: onda que sale del botón con cada toque
                halo.rectTransform.anchoredPosition = center;
                float rp = p >= 0.42f ? (p - 0.42f) / 0.58f : 1f;
                halo.rectTransform.sizeDelta = size + Vector2.one * Mathf.Lerp(10f, 110f, Juice.EaseOutCubic(rp));
                halo.color = UIKit.WithAlpha(Color.white, Mathf.Lerp(0.55f, 0f, rp));
                yield return null;
            }
            coachRoutine = null;
        }

        private void BuildCoach(out CanvasGroup group, out Image halo, out RectTransform hand)
        {
            var target = (RectTransform)menuCollection.transform;
            coachRoot = UIKit.Stretch(UIKit.Rect("CollectionCoach", v.canvas.transform));   // pantalla completa, fuera de la zona segura también
            coachRoot.SetAsLastSibling();
            group = coachRoot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = true;   // bloquea desde el primer fotograma
            var dimCanvas = coachRoot.gameObject.AddComponent<Canvas>();
            dimCanvas.overrideSorting = true;
            dimCanvas.sortingOrder = CoachDimOrder;
            coachRoot.gameObject.AddComponent<GraphicRaycaster>();

            var dim = UIKit.Stretch(UIKit.Rect("Dim", coachRoot)).gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, CoachDimAlpha);
            dim.raycastTarget = true;

            halo = UIKit.Box("Halo", coachRoot, Color.white, 0.6f);
            halo.rectTransform.anchorMin = halo.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            // Texto encima del botón: qué hay dentro y qué hacer
            long life = save.lifetimePoints;
            bool fresh = AnyNew(life, Collection.Kind.Theme, Collection.Kind.Color, Collection.Kind.Emblem, Collection.Kind.NameColor) || HasUnseenAwards;
            var title = UIKit.Text("Title", coachRoot, fresh ? "YOU UNLOCKED SOMETHING!" : "THEMES & COLORS TO UNLOCK", 66, Color.white, black: true);
            var sub = UIKit.Text("Sub", coachRoot, "Tap COLLECTION to see it", 42, UIKit.WithAlpha(Color.white, 0.85f));
            if (!fresh) sub.text = "Every point you score counts. Tap COLLECTION";
            foreach (var t in new[] { title, sub })
            {
                t.enableAutoSizing = true;
                t.fontSizeMax = t.fontSize;
                t.fontSizeMin = t.fontSize * 0.55f;
                t.enableWordWrapping = false;
                t.overflowMode = TextOverflowModes.Ellipsis;
            }
            Vector2 c = coachRoot.InverseTransformPoint(target.TransformPoint(target.rect.center));
            float top = c.y + target.rect.height * 0.5f;
            UIKit.Place(title.rectTransform, 0.5f, 0.5f, new Vector2(980, 90), new Vector2(0, top + 190f));
            UIKit.Place(sub.rectTransform, 0.5f, 0.5f, new Vector2(980, 60), new Vector2(0, top + 115f));
            juice.PopIn(title.transform, 0.1f, 0.35f, 0.6f);

            // Mano en su propio canvas, por encima del botón; el pivote es la punta del dedo
            hand = UIKit.Rect("Hand", coachRoot);
            hand.anchorMin = hand.anchorMax = new Vector2(0.5f, 0.5f);
            hand.pivot = new Vector2(0.30f, 0.92f);
            hand.sizeDelta = new Vector2(190f, 190f);
            var handImg = hand.gameObject.AddComponent<Image>();
            handImg.sprite = HandSprite();
            handImg.preserveAspect = true;
            handImg.raycastTarget = false;
            if (handImg.sprite == null) handImg.color = Color.clear;
            var handCanvas = hand.gameObject.AddComponent<Canvas>();
            handCanvas.overrideSorting = true;
            handCanvas.sortingOrder = CoachHandOrder;

            // El botón COLLECTION sube por encima del velo y es lo único que se puede pulsar
            coachTargetCanvas = menuCollection.GetComponent<Canvas>();
            if (coachTargetCanvas == null) coachTargetCanvas = menuCollection.gameObject.AddComponent<Canvas>();
            if (menuCollection.GetComponent<GraphicRaycaster>() == null) menuCollection.gameObject.AddComponent<GraphicRaycaster>();
            coachTargetCanvas.overrideSorting = true;
            coachTargetCanvas.sortingOrder = CoachTargetOrder;
            coachShownAt = Time.unscaledTime;
        }

        private static Sprite HandSprite()
        {
            if (handSprite != null) return handSprite;
            handSprite = Resources.Load<Sprite>("UI/HandPoint");
            if (handSprite != null) return handSprite;
            var tex = Resources.Load<Texture2D>("UI/HandPoint");
            if (tex == null) { Debug.LogWarning("[Colorify] Falta Resources/UI/HandPoint.png (mano del tutorial)."); return null; }
            handSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return handSprite;
        }

        // ¿Hay alguna pantalla (ranking, online, colección…) delante del menú?
        private bool OverlayAboveMenu()
        {
            foreach (var e in backActions)
                if (e.cg != null && e.cg.gameObject.activeSelf && e.cg.alpha > 0.01f) return true;
            return false;
        }

        // El jugador ha pulsado COLLECTION (por cualquier camino): tutorial superado para siempre
        private void CompleteCollectionCoach()
        {
            bool wasShowing = CoachActive;
            if (!save.collectionCoachDone)
            {
                save.collectionCoachDone = true;
                save.Save();
            }
            if (!wasShowing) return;
            Analytics.Log("tutorial_complete", ("step", "collection"), ("seconds", Mathf.RoundToInt(Time.unscaledTime - coachShownAt)));
            CloseCoach();
        }

        // Primera partida (con el tutorial pendiente): el fin de partida deja un solo camino, NEXT, que lleva al
        // menú, donde espera el tutorial. El resto de partidas, PLAY AGAIN y MENU como siempre.
        private void ApplyFirstRunOverButtons(bool first)
        {
            if (overAgain == null || overMenu == null) return;
            bool guide = first && !save.collectionCoachDone;
            overAgain.gameObject.SetActive(!guide);
            var rt = (RectTransform)overMenu.transform;
            UIKit.Place(rt, 0.5f, 0.5f, guide ? new Vector2(560, 160) : new Vector2(420, 120), new Vector2(0, guide ? -180 : -350));
            var img = overMenu.GetComponent<Image>();
            if (img != null) img.color = guide ? ColorifyView.TitleColors[3] : UIKit.PaperDark;
            var label = overMenu.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) { label.text = guide ? "NEXT" : "MENU"; label.fontSize = guide ? 70 : 50; }
        }

        private void CloseCoach()
        {
            if (coachTargetCanvas != null) coachTargetCanvas.overrideSorting = false;
            if (coachRoot != null) Destroy(coachRoot.gameObject);
            coachRoot = null;
        }
    }
}
