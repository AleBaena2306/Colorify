using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KovusPride.Publishing.Mobile
{
    // Publisher móvil con el que se compila la build de Android. Lo decide el símbolo de la build
    // (COLORIFY > Build > Android publisher > …): KP_PUB_CRAZYLABS, KP_PUB_HOMA, KP_PUB_VOODOO, KP_PUB_KWALEE,
    // KP_PUB_SAYGAMES, KP_PUB_SUPERSONIC, KP_PUB_TAPNATION, KP_PUB_MONDAYOFF, KP_PUB_SUPERCENT, KP_PUB_BOOMBIT,
    // KP_PUB_ROLLIC, KP_PUB_AZUR o KP_PUB_TEST (anuncios simulados, para probar el flujo en el móvil).
    // Sin símbolo: None, y todo sigue como antes (APK sin anuncios ni SDK).
    public enum Publisher { None, Test, CrazyLabs, Homa, Voodoo, Kwalee, SayGames, Supersonic, Tapnation, MondayOff, Supercent, BoomBit, Rollic, Azur }

    // Capa común para el SDK de un publisher móvil. El juego no llama nunca a un SDK concreto:
    //   · Los anuncios pasan por StorePlatform (ShowInterstitial / ShowRewarded), que aquí los manda al adaptador.
    //   · La analítica pasa por Colorify.Analytics.Log, que reenvía cada evento a OnAnalyticsEvent.
    //     game_start / game_end del modo Normal se convierten en «nivel empezado / terminado» (lo que piden todos
    //     los publishers para medir retención y duración de sesión). Cada partida es un nivel, numerado desde 1.
    // Integrar el SDK de un publisher = rellenar su adaptador (Adapters/<Publisher>Adapter.cs): 4–6 llamadas marcadas
    // con TODO. El resto del juego no se toca.
    public static class PublisherSdk
    {
        public static Publisher Current
        {
            get
            {
#if KP_PUB_TEST
                return Publisher.Test;
#elif KP_PUB_CRAZYLABS
                return Publisher.CrazyLabs;
#elif KP_PUB_HOMA
                return Publisher.Homa;
#elif KP_PUB_VOODOO
                return Publisher.Voodoo;
#elif KP_PUB_KWALEE
                return Publisher.Kwalee;
#elif KP_PUB_SAYGAMES
                return Publisher.SayGames;
#elif KP_PUB_SUPERSONIC
                return Publisher.Supersonic;
#elif KP_PUB_TAPNATION
                return Publisher.Tapnation;
#elif KP_PUB_MONDAYOFF
                return Publisher.MondayOff;
#elif KP_PUB_SUPERCENT
                return Publisher.Supercent;
#elif KP_PUB_BOOMBIT
                return Publisher.BoomBit;
#elif KP_PUB_ROLLIC
                return Publisher.Rollic;
#elif KP_PUB_AZUR
                return Publisher.Azur;
#else
                return Publisher.None;
#endif
            }
        }

        public static bool Active => Current != Publisher.None;

        private static IPublisherAdapter adapter;
        public static IPublisherAdapter Adapter => adapter ?? (adapter = Create(Current));

        private static IPublisherAdapter Create(Publisher p)
        {
            switch (p)
            {
                case Publisher.Test: return new TestAdapter();
                case Publisher.CrazyLabs: return new CrazyLabsAdapter();
                case Publisher.Homa: return new HomaAdapter();
                case Publisher.Voodoo: return new VoodooAdapter();
                case Publisher.Kwalee: return new KwaleeAdapter();
                case Publisher.SayGames: return new SayGamesAdapter();
                case Publisher.Supersonic: return new SupersonicAdapter();
                case Publisher.Tapnation: return new TapnationAdapter();
                case Publisher.MondayOff: return new MondayOffAdapter();
                case Publisher.Supercent: return new SupercentAdapter();
                case Publisher.BoomBit: return new BoomBitAdapter();
                case Publisher.Rollic: return new RollicAdapter();
                case Publisher.Azur: return new AzurAdapter();
                default: return null;
            }
        }

        private static bool initialized;

        // ready(intersticiales, bonificados) llega siempre, una vez. En el Editor se simulan los anuncios (como en los
        // portales web) para poder probar el flujo sin el SDK.
        public static void Init(Action<bool, bool> ready)
        {
            if (!Active || initialized) { ready?.Invoke(false, false); return; }
            initialized = true;
            Debug.Log($"[Publisher] {Current}: iniciando");
            if (Application.isEditor) { ready?.Invoke(true, true); return; }
            try { Adapter.Init(ready); }
            catch (Exception e) { Debug.LogWarning($"[Publisher] {Current}: {e.Message}"); ready?.Invoke(false, false); }
        }

        // done(ok) llega siempre: en un intersticial ok da igual; en un bonificado, ok = visto entero
        public static void ShowAd(bool rewarded, string placement, Action<bool> done)
        {
            if (!Active) { done?.Invoke(false); return; }
            bool finished = false;
            Action<bool> once = ok => { if (finished) return; finished = true; done?.Invoke(ok); };
            try
            {
                if (rewarded) Adapter.ShowRewarded(placement ?? "", once);
                else Adapter.ShowInterstitial(placement ?? "", () => once(true));
            }
            catch (Exception e) { Debug.LogWarning($"[Publisher] {Current}: {e.Message}"); once(false); }
        }

        // ---------------- Analítica ----------------
        private const string LevelKey = "kp_pub_level";
        private static int currentLevel;

        // Lo llama Colorify.Analytics.Log con cada evento del juego
        public static void OnAnalyticsEvent(string name, IDictionary<string, object> parameters)
        {
            if (!Active) return;
            var p = parameters != null ? new Dictionary<string, object>(parameters) : new Dictionary<string, object>();
            try
            {
                string mode = p.TryGetValue("mode", out var m) ? m as string : null;
                if (name == "game_start" && mode == "normal")
                {
                    currentLevel = PlayerPrefs.GetInt(LevelKey, 0) + 1;
                    PlayerPrefs.SetInt(LevelKey, currentLevel);
                    PlayerPrefs.Save();
                    Adapter.LevelStarted(currentLevel);
                }
                else if (name == "game_end" && mode == "normal" && currentLevel > 0)
                {
                    // Partida sin fin: se cuenta como completada si termina por tiempo; abandonarla es un fallo
                    string result = p.TryGetValue("result", out var r) ? r as string : "";
                    long score = p.TryGetValue("points", out var pts) ? Convert.ToInt64(pts) : 0;
                    int seconds = p.TryGetValue("duration_sec", out var d) ? Convert.ToInt32(d) : 0;
                    Adapter.LevelEnded(currentLevel, result != "quit", score, seconds);
                    currentLevel = 0;
                }
                Adapter.Event(name, p);
            }
            catch (Exception e) { Debug.LogWarning($"[Publisher] {Current}: {e.Message}"); }
        }
    }

    // Lo que tiene que hacer el SDK de cada publisher. Los métodos que un SDK no necesite se quedan como están.
    public interface IPublisherAdapter
    {
        // Arrancar el SDK y avisar de qué anuncios hay: ready(intersticiales, bonificados). Llamar siempre a ready.
        void Init(Action<bool, bool> ready);
        void LevelStarted(int level);
        void LevelEnded(int level, bool completed, long score, int seconds);
        // Resto de eventos del juego (tutorial, colección, anuncios…), por si el publisher los quiere
        void Event(string name, IReadOnlyDictionary<string, object> parameters);
        // Llamar siempre a done al cerrarse (o si no hay anuncio)
        void ShowInterstitial(string placement, Action done);
        // done(true) solo si el jugador gana la recompensa
        void ShowRewarded(string placement, Action<bool> done);
    }

    // Base de los adaptadores: sin SDK no hay anuncios y los eventos solo se escriben en el log
    public abstract class PublisherAdapter : IPublisherAdapter
    {
        protected abstract string Name { get; }

        public virtual void Init(Action<bool, bool> ready)
        {
            Debug.LogWarning($"[Publisher] {Name}: el SDK todavía no está integrado (Adapters/{Name}Adapter.cs)");
            ready?.Invoke(false, false);
        }

        public virtual void LevelStarted(int level) => Debug.Log($"[Publisher] {Name}: nivel {level} empezado");

        public virtual void LevelEnded(int level, bool completed, long score, int seconds) =>
            Debug.Log($"[Publisher] {Name}: nivel {level} {(completed ? "completado" : "fallado")} · {score} pts · {seconds} s");

        public virtual void Event(string name, IReadOnlyDictionary<string, object> parameters) { }

        public virtual void ShowInterstitial(string placement, Action done) => done?.Invoke();

        public virtual void ShowRewarded(string placement, Action<bool> done) => done?.Invoke(false);
    }

    // KP_PUB_TEST: anuncios simulados de 1 s en el móvil, para comprobar el flujo (continuar con anuncio, intersticial
    // al salir, eventos de nivel en el logcat) antes de tener el SDK de nadie
    public class TestAdapter : PublisherAdapter
    {
        protected override string Name => "Test";

        public override void Init(Action<bool, bool> ready)
        {
            Debug.Log("[Publisher] Test: anuncios simulados");
            ready?.Invoke(true, true);
        }

        public override void ShowInterstitial(string placement, Action done) =>
            Runner.Get().StartCoroutine(Fake($"intersticial «{placement}»", () => done?.Invoke()));

        public override void ShowRewarded(string placement, Action<bool> done) =>
            Runner.Get().StartCoroutine(Fake($"bonificado «{placement}»", () => done?.Invoke(true)));

        private static IEnumerator Fake(string what, Action end)
        {
            Debug.Log($"[Publisher] Test: anuncio {what}…");
            yield return new WaitForSecondsRealtime(1f);
            end();
        }

        private class Runner : MonoBehaviour
        {
            private static Runner instance;
            public static Runner Get()
            {
                if (instance != null) return instance;
                var go = new GameObject("KovusPridePublisherTest");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Runner>();
                return instance;
            }
        }
    }
}
