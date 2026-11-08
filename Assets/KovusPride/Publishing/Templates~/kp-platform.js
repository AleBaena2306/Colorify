// Kovu's Pride · Publicación: adaptador común para los portales web. Lo copia el menú de publicación en el
// WebGL Template de cada plataforma junto a su SDK. Unity habla con window.KPPlatform y no conoce ningún SDK.
//   window.KP_STORE: 'crazygames' | 'playgama' | 'gamedistribution' | 'itch' (lo define el index.html)
//   init(emit)                 emit(tipo, datos): 1 listo · 2 fin de anuncio · 3 pausa · 4 silencio · 5 empieza anuncio
//   message(name)              loading_stopped · game_ready · gameplay_started · gameplay_stopped · happytime
//   showAd(id, type, placement) type: 'interstitial' | 'rewarded'. Siempre acaba con emit(2, {id, ok})
// Si un SDK falla o no responde, el juego sigue: «listo» sin anuncios a los 8 s y anuncios que caducan a los 60 s.
(function () {
  'use strict';
  var store = window.KP_STORE || 'none';
  var emit = function () {};
  var ready = false;
  var current = null;   // { id, rewarded, earned, timer }

  function sendReady(info) {
    if (ready) return;
    ready = true;
    info = info || {};
    emit(1, { store: store, ads: info.ads ? 1 : 0, rewarded: info.rewarded ? 1 : 0, user: info.user || '' });
  }

  function adStarted() { if (current) emit(5, { id: current.id }); }

  function adFinished(ok) {
    if (!current) return;
    var c = current; current = null;
    clearTimeout(c.timer);
    emit(2, { id: c.id, ok: ok ? 1 : 0 });
  }

  function beginAd(id, type) {
    if (current) { emit(2, { id: id, ok: 0 }); return false; }
    current = { id: id, rewarded: type === 'rewarded', earned: false };
    current.timer = setTimeout(function () { adFinished(false); }, 60000);
    return true;
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
  var pg = {
    init: function () {
      var b = window.bridge;
      if (!b) { sendReady(); return; }
      b.initialize().then(function () {
        var E = b.EVENT_NAME || {};
        try {
          if (E.INTERSTITIAL_STATE_CHANGED) b.advertisement.on(E.INTERSTITIAL_STATE_CHANGED, function (s) {
            if (!current || current.rewarded) return;
            if (s === 'opened') adStarted();
            else if (s === 'closed') adFinished(true);
            else if (s === 'failed') adFinished(false);
          });
          if (E.REWARDED_STATE_CHANGED) b.advertisement.on(E.REWARDED_STATE_CHANGED, function (s) {
            if (!current || !current.rewarded) return;
            if (s === 'opened') adStarted();
            else if (s === 'rewarded') current.earned = true;
            else if (s === 'closed') adFinished(current.earned);
            else if (s === 'failed') adFinished(false);
          });
          if (E.PAUSE_STATE_CHANGED) b.platform.on(E.PAUSE_STATE_CHANGED, function (p) { emit(3, { on: p ? 1 : 0 }); });
          if (E.AUDIO_STATE_CHANGED) b.platform.on(E.AUDIO_STATE_CHANGED, function (on) { emit(4, { on: on ? 0 : 1 }); });
        } catch (e) { console.warn('[KPPlatform] Playgama listeners', e); }
        var a = b.advertisement || {};
        sendReady({ ads: a.isInterstitialSupported, rewarded: a.isRewardedSupported, user: b.player && b.player.name });
      }).catch(function (e) { console.warn('[KPPlatform] Playgama init', e); sendReady(); });
    },
    message: function (name) {
      var b = window.bridge; if (!b || !b.platform) return;
      var map = { game_ready: 'game_ready', gameplay_started: 'gameplay_started', gameplay_stopped: 'gameplay_stopped',
                  happytime: 'player_got_achievement' };
      if (map[name]) try { b.platform.sendMessage(map[name]); } catch (e) { }
    },
    showAd: function (id, type, placement) {
      var b = window.bridge;
      if (!b || !beginAd(id, type)) { if (!b) emit(2, { id: id, ok: 0 }); return; }
      try {
        if (type === 'rewarded') b.advertisement.showRewarded(placement || 'reward');
        else b.advertisement.showInterstitial(placement || 'break');
      } catch (e) { adFinished(false); }
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
    init: function (e) {
      emit = e || emit;
      setTimeout(function () { sendReady(); }, 8000);   // si el SDK no responde, se juega sin anuncios
      try { impl.init(); } catch (err) { console.warn('[KPPlatform] init', err); sendReady(); }
    },
    message: function (name) { try { impl.message(name); } catch (err) { } },
    showAd: function (id, type, placement) {
      try { impl.showAd(id, type, placement); } catch (err) { adFinished(false); emit(2, { id: id, ok: 0 }); }
    },
  };
})();
