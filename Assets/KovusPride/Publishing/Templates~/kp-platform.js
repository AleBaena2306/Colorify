// Kovu's Pride · Publicación: adaptador común para los portales web. Lo copia el menú de publicación en el
// WebGL Template de cada plataforma junto a su SDK. Unity habla con window.KPPlatform y no conoce ningún SDK.
//   window.KP_STORE: 'crazygames' | 'playgama' | 'gamedistribution' | 'itch' (lo define el index.html)
//   window.KP_STORAGE_KEYS: claves de guardado que se leen antes de arrancar (Playgama guarda por su SDK)
//   preload()                  Promise: la plantilla la espera ANTES de cargar Unity (Playgama: iniciar el SDK y
//                              leer el guardado). Nunca falla: a los 8 s se resuelve aunque el SDK no responda.
//   init(emit)                 emit(tipo, datos): 1 listo · 2 fin de anuncio · 3 pausa · 4 silencio · 5 empieza anuncio
//   message(name)              loading_stopped · game_ready · gameplay_started · gameplay_stopped · happytime
//   showAd(id, type, placement) type: 'interstitial' | 'rewarded'. Siempre acaba con emit(2, {id, ok})
//   storageGet(key) / storageSet(key, value)   guardado de la plataforma (solo Playgama; null si no hay)
//   setScore(board, score)     clasificación de la plataforma, si la tiene
// Si un SDK falla o no responde, el juego sigue: «listo» sin anuncios a los 8 s y anuncios que caducan a los 60 s.
(function () {
  'use strict';
  var store = window.KP_STORE || 'none';
  var emit = function () {};
  var ready = false;
  var current = null;   // { id, rewarded, earned, timer, seen }
  var info0 = {};       // lo que se sabe del SDK tras preload (idioma, audio)
  var cache = {};       // guardado leído en preload

  function sendReady(info) {
    if (ready) return;
    ready = true;
    info = info || {};
    emit(1, { store: store, ads: info.ads ? 1 : 0, rewarded: info.rewarded ? 1 : 0, user: info.user || '',
              lang: info.lang || info0.lang || '' });
  }

  function adStarted() { if (current) emit(5, { id: current.id }); }

  function adFinished(ok) {
    if (!current) return;
    var c = current; current = null;
    clearTimeout(c.timer);
    emit(2, { id: c.id, ok: ok ? 1 : 0 });
  }

  function beginAd(id, type, noStartMs) {
    if (current) { emit(2, { id: id, ok: 0 }); return false; }
    current = { id: id, rewarded: type === 'rewarded', earned: false, seen: false };
    current.timer = setTimeout(function () { adFinished(false); }, 60000);
    // Si el SDK decide no enseñar nada (demasiado pronto, sin inventario) puede que no avise: no se espera
    if (noStartMs) {
      var c = current;
      setTimeout(function () { if (current === c && !c.seen) adFinished(false); }, noStartMs);
    }
    return true;
  }

  // Dos fotogramas pintados después: el primer fotograma jugable ya está en pantalla
  function afterPaint(fn) {
    var raf = window.requestAnimationFrame || function (f) { return setTimeout(f, 16); };
    raf(function () { raf(fn); });
  }

  // ---------------- CrazyGames (HTML5 SDK v3) ----------------
  var cg = {
    sdk: function () { return window.CrazyGames && window.CrazyGames.SDK; },
    init: function () {
      var sdk = cg.sdk();
      if (!sdk) { sendReady(); return; }
      sdk.init().then(function () {
        // Anuncios solo en CrazyGames (y en local con su SDK); en Basic Launch el propio SDK responde adsDisabled
        var ads = sdk.environment === 'crazygames' || sdk.environment === 'local';
        try {
          if (sdk.game && sdk.game.settings && sdk.game.settings.muteAudio) emit(4, { on: 1 });
          if (sdk.game && sdk.game.addSettingsChangeListener)
            sdk.game.addSettingsChangeListener(function (s) { emit(4, { on: s && s.muteAudio ? 1 : 0 }); });
        } catch (e) { }
        var finish = function (user) { sendReady({ ads: ads, rewarded: ads, user: user && user.username }); };
        try {
          if (sdk.user && sdk.user.isUserAccountAvailable) sdk.user.getUser().then(finish).catch(function () { finish(null); });
          else finish(null);
        } catch (e) { finish(null); }
      }).catch(function (e) { console.warn('[KPPlatform] CrazyGames init', e); sendReady(); });
    },
    message: function (name) {
      var sdk = cg.sdk(); if (!sdk || !sdk.game) return;
      if (name === 'gameplay_started') sdk.game.gameplayStart();
      else if (name === 'gameplay_stopped') sdk.game.gameplayStop();
      else if (name === 'loading_stopped' && sdk.game.loadingStop) sdk.game.loadingStop();
      else if (name === 'happytime' && sdk.game.happytime) sdk.game.happytime();
    },
    showAd: function (id, type) {
      var sdk = cg.sdk();
      if (!sdk || !beginAd(id, type)) { if (!sdk) emit(2, { id: id, ok: 0 }); return; }
      sdk.ad.requestAd(type === 'rewarded' ? 'rewarded' : 'midgame', {
        adStarted: adStarted,
        adFinished: function () { adFinished(true); },
        adError: function (e) { console.log('[KPPlatform] CrazyGames ad', e && e.code); adFinished(false); },
      });
    },
  };

  // ---------------- Playgama Bridge (JS) ----------------
  // Requisitos de Playgama que se cumplen aquí: el SDK se inicia antes que el juego (preload), el guardado va por
  // bridge.storage, se lee platform.language, se respeta isAudioEnabled y los eventos de pausa y audio, game_ready
  // se envía con el primer fotograma jugable y el intervalo entre intersticiales lo decide el SDK.
  var pg = {
    ok: false,
    preload: function () {
      var b = window.bridge;
      if (!b) return Promise.resolve();
      return b.initialize().then(function () {
        pg.ok = true;
        try { info0.lang = b.platform.language || ''; } catch (e) { }
        try { info0.audio = b.platform.isAudioEnabled !== false; } catch (e) { info0.audio = true; }
        var keys = window.KP_STORAGE_KEYS || [];
        if (!keys.length) return;
        return b.storage.get(keys).then(function (data) {
          for (var i = 0; i < keys.length; i++) {
            var v = data && data[i];
            if (v !== null && v !== undefined) cache[keys[i]] = typeof v === 'string' ? v : JSON.stringify(v);
          }
        }).catch(function (e) { console.warn('[KPPlatform] Playgama storage.get', e); });
      }).catch(function (e) { console.warn('[KPPlatform] Playgama init', e); });
    },
    init: function () {
      var b = window.bridge;
      if (!b || !pg.ok) { sendReady(); return; }
      var E = b.EVENT_NAME || {};
      try {
        if (E.INTERSTITIAL_STATE_CHANGED) b.advertisement.on(E.INTERSTITIAL_STATE_CHANGED, function (s) {
          if (!current || current.rewarded) return;
          current.seen = true;
          if (s === 'opened') adStarted();
          else if (s === 'closed') adFinished(true);
          else if (s === 'failed') adFinished(false);
        });
        if (E.REWARDED_STATE_CHANGED) b.advertisement.on(E.REWARDED_STATE_CHANGED, function (s) {
          if (!current || !current.rewarded) return;
          current.seen = true;
          if (s === 'opened') adStarted();
          else if (s === 'rewarded') current.earned = true;   // la recompensa solo con «rewarded»
          else if (s === 'closed') adFinished(current.earned);
          else if (s === 'failed') adFinished(false);
        });
        if (E.PAUSE_STATE_CHANGED) b.platform.on(E.PAUSE_STATE_CHANGED, function (p) { emit(3, { on: p ? 1 : 0 }); });
        if (E.AUDIO_STATE_CHANGED) b.platform.on(E.AUDIO_STATE_CHANGED, function (on) { emit(4, { on: on ? 0 : 1 }); });
      } catch (e) { console.warn('[KPPlatform] Playgama listeners', e); }
      var a = b.advertisement || {};
      sendReady({ ads: a.isInterstitialSupported, rewarded: a.isRewardedSupported, user: b.player && b.player.name });
      if (info0.audio === false) emit(4, { on: 1 });   // el estado inicial no llega como evento
      try { if (a.interstitialState === 'opened' || a.rewardedState === 'opened') emit(3, { on: 1 }); } catch (e) { }
    },
    message: function (name) {
      var b = window.bridge; if (!b || !b.platform || !pg.ok) return;
      if (name === 'game_ready') { afterPaint(function () { try { b.platform.sendMessage('game_ready'); } catch (e) { } }); return; }
      var map = { happytime: 'player_got_achievement' };
      if (map[name]) try { b.platform.sendMessage(map[name]); } catch (e) { }
    },
    showAd: function (id, type, placement) {
      var b = window.bridge;
      if (!b || !pg.ok) { emit(2, { id: id, ok: 0 }); return; }
      if (!beginAd(id, type, type === 'rewarded' ? 4000 : 1500)) return;
      try {
        if (type === 'rewarded') b.advertisement.showRewarded(placement || undefined);
        else b.advertisement.showInterstitial(placement || undefined);
      } catch (e) { adFinished(false); }
    },
    storageGet: function (key) { return Object.prototype.hasOwnProperty.call(cache, key) ? cache[key] : null; },
    storageSet: function (key, value) {
      cache[key] = value;
      var b = window.bridge; if (!b || !pg.ok) return;
      try { b.storage.set([key], [value]).catch(function (e) { console.warn('[KPPlatform] storage.set', e); }); }
      catch (e) { console.warn('[KPPlatform] storage.set', e); }
    },
    setScore: function (board, score) {
      var b = window.bridge; if (!b || !pg.ok || !b.leaderboards) return;
      try {
        if (b.leaderboards.type === 'not_available') return;
        b.leaderboards.setScore(board, score).catch(function () { });
      } catch (e) { }
    },
  };

  // ---------------- GameDistribution (HTML5 SDK) ----------------
  // El index.html define GD_OPTIONS con onEvent → window.__kpGdEvent (los eventos que llegan antes se guardan)
  var gd = {
    init: function () {
      var queued = window.__kpGdQueue || [];
      window.__kpGdEvent = gd.onEvent;
      queued.forEach(gd.onEvent);
      setTimeout(function () { sendReady({ ads: !!window.gdsdk, rewarded: !!window.gdsdk }); }, 8000);
    },
    onEvent: function (ev) {
      var name = ev && ev.name;
      if (name === 'SDK_READY') sendReady({ ads: true, rewarded: true });
      else if (name === 'SDK_GAME_PAUSE') adStarted();
      else if (name === 'SDK_REWARDED_WATCH_COMPLETE') { if (current) current.earned = true; }
      else if (name === 'SDK_GAME_START') { if (current) adFinished(current.rewarded ? current.earned : true); }
    },
    message: function () { },
    showAd: function (id, type) {
      var sdk = window.gdsdk;
      if (!sdk || typeof sdk.showAd !== 'function' || !beginAd(id, type)) { if (!sdk) emit(2, { id: id, ok: 0 }); return; }
      try {
        var p = type === 'rewarded' ? sdk.showAd('rewarded') : sdk.showAd();
        if (p && typeof p.catch === 'function') p.catch(function () { adFinished(false); });
      } catch (e) { adFinished(false); }
    },
  };

  // ---------------- itch.io: sin SDK ni anuncios ----------------
  var none = { init: function () { sendReady(); }, message: function () { }, showAd: function (id) { emit(2, { id: id, ok: 0 }); } };

  var impl = store === 'crazygames' ? cg : store === 'playgama' ? pg : store === 'gamedistribution' ? gd : none;

  window.KPPlatform = {
    store: store,
    preload: function () {
      var p = impl.preload ? impl.preload() : null;
      if (!p) return Promise.resolve();
      var timeout = new Promise(function (r) { setTimeout(r, 8000); });   // nunca se bloquea la carga del juego
      return Promise.race([p.catch(function () { }), timeout]);
    },
    init: function (e) {
      emit = e || emit;
      setTimeout(function () { sendReady(); }, 8000);   // si el SDK no responde, se juega sin anuncios
      try { impl.init(); } catch (err) { console.warn('[KPPlatform] init', err); sendReady(); }
    },
    message: function (name) { try { impl.message(name); } catch (err) { } },
    showAd: function (id, type, placement) {
      try { impl.showAd(id, type, placement); } catch (err) { adFinished(false); emit(2, { id: id, ok: 0 }); }
    },
    storageGet: function (key) { try { return impl.storageGet ? impl.storageGet(key) : null; } catch (err) { return null; } },
    storageSet: function (key, value) { try { if (impl.storageSet) impl.storageSet(key, value); } catch (err) { } },
    setScore: function (board, score) { try { if (impl.setScore) impl.setScore(board, score); } catch (err) { } },
  };
})();
