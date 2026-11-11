# Rechazos y avisos de los portales

Una línea por rechazo o aviso de QA: fecha · plataforma · versión · qué dijeron · qué se cambió en el sistema.
Cada caso nuevo se convierte en una comprobación de KovusPridePublish para que no se repita.


## 08/11/2026 · Playgama · requisitos detectados antes del primer envío (no es un rechazo)
- Sonido sintetizado por código: no se acepta (3.3.2) → efectos de Kenney en ogg y soporte de música.
- Guardado fuera de su SDK (PlayerPrefs, IndexedDB): prohibido (3.2.1) → StorePlatform.LoadString/SaveString y sin caché de build.
- Tecla Esc prohibida (1.5.2) → StorePlatform.AllowsEscapeKey.
- SDK v2 como primer script e iniciado antes del motor; game_ready con el primer fotograma jugable.
- Sin temporizadores propios de intersticiales: decide su SDK.
- Llenar la ventana en cualquier proporción, también 1:1 (1.6.8) → plantilla a pantalla completa.
- Servidor propio: fuera en Playgama (1.2.5) → StorePlatform.AllowsOwnServer.
- Subida por MCP: las URLs de subida (S3 de Playgama) están bloqueadas desde el entorno de Claude; hasta que se permita
  ese dominio, el zip y las portadas se suben a mano en la consola.

## 10/11/2026 · CrazyGames · subida
- «Archive files are not supported»: el portal no acepta zip; hay que arrastrar los archivos de la carpeta web. La
  herramienta ahora abre esa carpeta y lo indica (Profile.uploadFolder).
- Aviso «Compression could not be detected automatically»: es por usar Brotli con Decompression Fallback (archivos
  .unityweb). Es un aviso, no un error. Recomiendan su «custom build» (CrazySDK de Unity); no es obligatoria.
- Rechazo al subir: «Found .unityweb files… build your game without the Decompression fallback option». CrazyGames
  exige Brotli sin Decompression Fallback (archivos .br). Corregido en el perfil de CrazyGames.
- Formulario: si se marca «online multiplayer», CrazyGames exige su SDK de multijugador (actualizar la sala, listener de
  unión, InviteLink, isInstantMultiplayer, mantener amigos en la sala). Integrado: StorePlatform.UpdateRoom/LeftRoom en
  duelos privados y carreras, invitaciones (inviteParams y addJoinRoomListener → JoinFromInvite), entrada directa a una
  sala privada de carrera con isInstantMultiplayer y el nombre de la cuenta de CrazyGames en el juego.
- Al subir como juego Unity: «Cannot find Build directory» y, sobre todo, CrazyGames IGNORA el index.html de los juegos
  Unity (docs: Unity export tips) — se perderían el SDK HTML5 y kp-platform.js. Solución: subirlo como juego HTML5 (su
  propia documentación lo recomienda si el juego usa JavaScript propio), con Decompression Fallback (archivos .unityweb,
  porque como HTML5 no se garantiza el Content-Encoding). Se deshace el cambio anterior de quitar el fallback.
- Vista previa de CrazyGames: kp-platform.js devolvió 404 (el archivo suelto no llegó o no se sirvió) y el juego arrancó
  sin SDK («intersticiales False»). Ahora el adaptador va dentro del index.html.
- Playgama, sandbox (11/11/2026, avisos de jugadores y telemetría): en la primera partida se iba el sonido, en torno al
  primer anuncio. Causas posibles, todas cubiertas en kp-platform.js: el AudioContext de Unity queda «suspended» o
  «interrupted» tras un anuncio o al cambiar de pestaña y Unity no lo reanuda (ahora se reanuda al acabar el anuncio, al
  volver a la pestaña y con cada toque); el estado de audio se leía en preload y un «audio activado» anterior a los
  listeners se perdía (ahora se lee en vivo cada 1,5 s); había más «interstitial opened» que «closed» (ahora un anuncio
  cerrado sin aviso se da por terminado y se levanta su pausa).

## 11/11/2026 · CrazyGames · 0.01 · RECHAZADO en la revisión de Basic Launch
- Correo: «for now it won't be moving forward to release on our platform». Motivo del equipo: «The overall quality of
  the game does not yet meet the expectations of our platform». Remiten a sus «New games» como ejemplo y a sus
  requisitos y guías de calidad. Sin fallos técnicos concretos.
- Su FAQ permite volver a enviarlo solo con mejoras importantes; quien envía repetidamente juegos que no cumplen puede
  tener restricciones. No se reenvía hasta tener una versión claramente mejor (y con una lista de cambios).
- Hipótesis propias (sin confirmar por CrazyGames): aspecto de prototipo (casillas planas, el HUD de partida se ve
  detrás del menú), género «encuentra el color» con muchos clones, primeros segundos poco vistosos, vídeos con cursor.
