# Publicación en portales web

Sistema de Kovu's Pride para sacar cada juego en CrazyGames, Playgama, GameDistribution e itch.io con un solo
menú por plataforma. El portfolio sigue con su propio menú (`COLORIFY > Build > Web (móvil + PC)`).

## Uso

En Unity, `COLORIFY > Build >`:

| Menú | Qué hace |
|---|---|
| CrazyGames | Build con el SDK v3 de CrazyGames. Zip listo y abre el portal de desarrolladores. |
| Playgama | Build con Playgama Bridge. Zip listo; Claude lo sube por MCP (o a mano en el portal). |
| GameDistribution | Build con el SDK HTML5 de GameDistribution. Necesita el Game ID en `publish.json`. |
| itch.io | Build sin anuncios. Si `butler` está instalado, la sube a `itchTarget` directamente. |
| Todos los portales | Las cuatro seguidas. |

Cada build:

1. Comprueba que los scripts están compilados y que la plataforma es Web (si no, ofrece cambiarla).
2. Compila con la plantilla `KovusPridePortal` y el símbolo `KP_STORE_<PLATAFORMA>`, solo para esa build.
   Al acabar deja los ajustes del proyecto como estaban.
3. Añade el SDK de la plataforma al `index.html` y copia `kp-platform.js`.
4. Comprueba los requisitos (tamaño total, número de archivos, descarga inicial y rutas relativas). Si alguno
   obligatorio falla, no genera el zip y dice por qué.
5. Deja `Builds/Publish/<plataforma>/<juego>-<plataforma>-<versión>.zip` y apunta el resultado en `history.md`.

## Archivos

- `publish.json`: configuración del juego (Game ID de GameDistribution, destino de itch.io, ruta de butler,
  segundos mínimos entre intersticiales y `storageKeys`, las claves de guardado que Playgama lee al arrancar).
- `history.md`: cada build y su resultado (lo escribe el menú).
- `rejections.md`: rechazos y avisos de QA de los portales. Cada uno se convierte en una comprobación nueva.

## Cómo funciona por dentro

- `Assets/KovusPride/Publishing/StorePlatform.cs`: la única API que usa el juego (inicio, partida activa,
  intersticial, bonificado, nombre del jugador, pausa y silencio pedidos por el portal, guardado con
  `LoadString`/`SaveString`, clasificación con `SubmitScore`, `AllowsOwnServer`, `AllowsEscapeKey`). Las reglas de
  anuncios se cumplen aquí: nunca un intersticial en partida y audio en pausa durante el anuncio; como mínimo 3
  minutos entre intersticiales y ninguno en los primeros 90 s, salvo en Playgama, donde decide su SDK.
- `Plugins/WebGL/KovusPridePlatform.jslib`: puente de C# a `window.KPPlatform`.
- `Templates~/kp-platform.js`: el adaptador de cada SDK (CrazyGames v3, Playgama Bridge, GameDistribution).
  Si un SDK no responde en 8 s, el juego sigue sin anuncios; un anuncio que no termina caduca a los 60 s.
- `Assets/WebGLTemplates/KovusPridePortal`: plantilla común. El juego llena toda la ventana en cualquier
  proporción (la interfaz de Unity se adapta), sin zoom ni selección de texto. Antes de cargar Unity espera a
  `KPPlatform.preload()`: en Playgama, iniciar su SDK y leer el guardado.
- `Editor/KovusPridePublish.cs`: el menú de build.

En el portfolio no se define ningún símbolo y todo sigue como antes (anuncios de AdSense, inicio de sesión con
Google y servidor de la propia web). En los portales:

- el servidor online es siempre `alejandrobaenaportfolio.com`;
- no hay inicio de sesión con Google y se juega con el nombre generado;
- los anuncios son los del portal.

## Lo que hay que hacer una vez por plataforma

- **CrazyGames:** crear el juego en el portal y subir el zip (o arrastrar la carpeta). El primer envío es una
  Basic Launch: sin anuncios y sin integración obligatoria. Si las métricas son buenas, CrazyGames invita al
  lanzamiento completo (anuncios, módulo de datos y cuenta de CrazyGames).
- **Playgama:** conectar su servidor MCP en Claude (`https://developer.playgama.com/api/mcp`, OAuth) para que
  Claude suba las builds. Si no, subir el zip en el portal. Lo que su revisión exige y ya resuelve el sistema: SDK
  v2 como primer script e iniciado antes que Unity, guardado por su SDK (nada de PlayerPrefs ni caché en
  IndexedDB), `platform.language`, `isAudioEnabled` y eventos de pausa y audio, `game_ready` con el primer
  fotograma jugable, intersticiales sin temporizadores propios, sin Esc, sin servidor propio y llenar la ventana.
  Lo que depende del juego: música y efectos en archivos (ogg/mp3, no sintetizados), botón de silencio, tutorial,
  título igual que en la ficha, tres portadas y capturas (estas últimas, a mano en su consola).
- **GameDistribution:** crear el juego, copiar su Game ID en `publish.json` y compilar. Los anuncios se prueban en
  el iframe de la pestaña Upload.
- **itch.io:** crear la página (Kind of project: HTML), instalar butler y ejecutar `butler login` una vez. Tras la
  primera subida, marcar el canal `html5` como «This file will be played in the browser».
