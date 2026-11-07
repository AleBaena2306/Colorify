# Colorify · kit de arranque (Unity 6000.2.6f2, URP 2D)

Núcleo jugable completo y configurable: tablero con cuenta atrás, cuadrículas de 2×2 a 8×8, puntuación por color
(cálido = más puntos y más raro) × velocidad × multiplicador, rondas con objetivo y tiempo (3 por plazo, la última
«jefe»), cartas de mejora, saltar rondas, victoria en la primera ronda 8×8 superada, modo sin fin, fin de partida con
las cartas elegidas, mejores partidas locales, continuar con anuncio bonificado (simulado en el Editor), pausa,
sonido y vibración, analítica y build web. Sin prefabs ni sprites: la UI se monta sola al pulsar Play.

## Puesta en marcha (≈10 min)

1. **Unity Hub → New project** → Unity 6000.2.6f2 → plantilla **Universal 2D** → nombre `Colorify` →
   ubicación `C:\Users\baena\Desktop\Unity\ProyectosUnity\ProyectosUnity\` → Create.
2. Cierra Unity un momento (o no, también vale) y **copia el contenido de este kit** dentro de la carpeta del
   proyecto: la carpeta `Assets` se fusiona con la existente y `.gitignore` va en la raíz del proyecto.
3. En Unity: **COLORIFY > Setup > Configurar proyecto**. Importa TextMeshPro, crea las fuentes, la configuración,
   la escena `Assets/Colorify/Scenes/Main.unity` y pone vertical, nombre y paquete (`com.KovusPride.Colorify`).
4. Vista **Game** en 1080×1920 (o Simulator con un móvil) → **Play**.
   - Trampas solo en el Editor: **N** supera la ronda, **K** la pierde. **Esc/P** pausa.
5. **Git**: con GitHub Desktop, *Add local repository* → la carpeta del proyecto → crear el repositorio privado
   `AleBaena2306/Colorify` y hacer el primer commit («Kit de arranque»). Después, avísame para añadirlo a la sesión.

## Dónde se ajusta el diseño

`Assets/Colorify/Resources/ColorifyConfig` (Inspector): colores y sus puntos/probabilidades, tiempos, objetivos,
crecimiento exponencial, ventana de velocidad, umbral de «ronda rápida», dificultad de color (separación OKLab),
cartas, rerolls por plazo y reglas del anuncio de continuar.

**COLORIFY > Herramienta de dificultad**: tabla por plazo con dos multiplicadores (1 = el valor base, sin límite
por arriba):
- **Dificultad ×**: multiplica el objetivo de puntos de ese plazo.
- **Parecido ×**: cuánto se parecen los distractores al color objetivo (2 = el doble de parecidos).

Muestra al momento los objetivos, el tiempo y la distancia de color resultantes, avisa si los colores quedan casi
indistinguibles y dibuja un tablero de ejemplo del plazo elegido. Tiene relleno rápido (inicio × crecimiento por
plazo) y funciona en Play: los cambios se aplican desde el siguiente tablero. Los plazos posteriores al último de la
tabla repiten sus valores.

Los valores son un punto de partida razonable, **no están equilibrados**: el día 2 del plan es jugar y ajustar.

## Build web

- Prueba en el navegador: **COLORIFY > Build > Web: prueba rápida (solo PC, sin publicar)**.
- **COLORIFY > Build > Web (móvil + PC)** compila y publica en el portfolio (la ficha sigue en WIP).

## Modo Normal: calentamiento, formas, efectos y puntos

El menú tiene PLAY (modo Normal), ONLINE y RANKING. El modo Supervivencia (rondas y cartas) ya no sale en el menú
ni en el ranking, pero su código sigue en el proyecto (`RunState`, cartas) por si se recupera.

- **Calentamiento**: los primeros `normalWarmupHits` aciertos (8) son en 2×2 con colores normales.
- **Cuadrícula**: luego 3×3, 4×4 y 5×5 como máximo (`normalSpecialMaxGrid`), un tamaño cada `normalHitsPerGridStep`
  aciertos. El crecimiento ya no es solo vertical (más casillas), sino horizontal: tableros especiales.
- **Formas** (`Specials.cs`): tras el calentamiento, cada tablero sale en cuadrado, círculo, triángulo o rombo, las
  cuatro igual de probables. No cuentan como especial. El rombo y el triángulo se dibujan un 7 % más grandes que su
  casilla (`ColorifyView.ShapeScale`) para no verse pequeños al lado del cuadrado.
- **Especiales**: efectos sobre la muestra (rayas blancas, rayas oscuras, memoria, tambaleo, encoger hasta
  desaparecer y giro), sobre cualquier forma. Tras el calentamiento la mitad de los tableros llevan uno
  (`normalSpecialStartShare`) y, a los `normalSpecialRampHits` aciertos (40), todos. Cada `normalUnlockEveryHits`
  aciertos (4) se desbloquea uno nuevo, en el orden que a los jugadores les resulta más fácil (lo aprende el
  servidor), y el tablero siguiente lo enseña con su aviso.
- **Puntos**: cada acierto da puntos según el tamaño, la dificultad aprendida del tablero y la rapidez frente al
  tiempo medio de los jugadores en ese tablero. Los aciertos rápidos encadenados suben el multiplicador (X2!, X3!…
  hasta `chainMax`); un acierto lento o un fallo lo devuelve a x1. El chip de la esquina muestra el multiplicador.
- **Ritmo**: el servidor (`GET /api/colorify/tuning`) ajusta cuánto tiempo da cada acierto para que una partida
  dure unos 5 minutos, con las partidas acabadas por tiempo (no las abandonadas). Sin conexión, valores de referencia.
- Un acierto siempre te deja como mínimo en el 25 % del tiempo máximo (`normalHitFloorFraction`).
- **Avisos**: al entrar en un especial nuevo o crecer la cuadrícula sale un aviso grande con rebote entre la muestra
  y el tablero.
- **Carreras**: la primera ronda es de calentamiento (2×2), luego hasta 5×5 con especiales; los elige el servidor
  (`kinds.js`) igual para todos. **Duelo 1v1**: sigue con sus reglas (aciertos, rayas y memoria por tamaño, hasta 8×8).

## Online (duelos y ranking global)

El servidor está en el repositorio del portfolio (`worker/`), en el mismo Cloudflare que la web:
`/api/colorify/play` (WebSocket de los duelos), `/api/colorify/scores` (ranking) y `/api/colorify/health`.

- **Duelo**: FIND A MATCH (cualquiera), CREATE ROOM (código de 4 cifras para jugar con quien quieras) o JOIN WITH CODE.
  Las victorias en duelos privados no cuentan para el ranking. Reglas del modo Normal, a la vez y con los mismos tableros (semilla del servidor). Si el rival se
  queda sin tiempo, ganas en cuanto le superas en aciertos; si te quedas tú antes, pierdes. Sin pausa.
- **Carrera** (2 a 8 jugadores): sala con código de 4 cifras (CREATE ROOM / JOIN WITH CODE) o QUICK PLAY,
  que entra en la sala pública abierta con más gente o crea una. El anfitrión elige jugadores (2-8), rondas (3-30)
  y si la sala es pública. En cada ronda todos ven el mismo tablero; puntos = puesto (10, 8, 6, 5, 4, 3, 2, 1)
  + rapidez (hasta 5). Un fallo congela 1,5 s; la ronda acaba cuando todos aciertan o a los 8 s. La cuadrícula
  pasa de 2×2 a 8×8 y los colores se parecen cada vez más. Ajustes del servidor: `worker/colorify/race.js`.
- **Bot**: si nadie busca partida en 6 s, juega un bot (etiqueta BOT) con las reglas del modo Normal. Aprende de las
  partidas reales: tiempo por acierto y fallos según el tamaño del tablero (partidas del modo Normal, duelos y
  carreras). Su nivel y sus trofeos siguen a los del jugador. En la carrera, el anfitrión añade bots con + BOT.
- **Trofeos** (como Clash Royale): solo en duelos públicos entre personas (mismo nivel: +30 / −20; contra alguien
  con más trofeos se gana más y se pierde menos) y en carreras de salas públicas (de +25 el 1.º a −15 el último).
  Salas privadas y fantasmas no cuentan. Nunca bajan de 0. Ligas: Grey 0, Teal 200, Sage 500, Amber 900,
  Coral 1400, Sky 2000 y Rainbow 3000. El emparejamiento junta trofeos parecidos (la tolerancia crece con la espera).
- **Ranking global**: Normal (puntos) y trofeos.
- **Nombre y cuenta** (CHANGE NAME): nombre libre de 3-16 caracteres con filtro de palabras (PlayerNames.cs y
  names.js). En el ordenador se escribe en la casilla (Enter guarda); en el navegador del móvil se abre el cuadro
  de texto del navegador; en Android, el teclado del sistema. Sin cuenta, el nombre escrito dura hasta cerrar el
  juego. Con «Sign in with Google» se guarda, queda reservado y los trofeos y récords siguen en cualquier
  dispositivo; se puede cerrar sesión y borrar la cuenta. El ID de cliente de Google está en wrangler.jsonc.
- `ColorifyConfig > Online`: URL del servidor (en la web se usa el dominio desde el que se juega) y si se envían puntuaciones.
- En el editor funciona contra el servidor real: para probar un duelo, abre el juego en el editor y en el móvil
  (o en dos pestañas) y pulsa FIND A MATCH en los dos.

## Estructura

```
Assets/Colorify/Scripts/Core      OkColor (OKLab), ColorifyConfig, RunState (rondas, puntos, cartas), ColorBoard (generador)
Assets/Colorify/Scripts/Game      ColorifyGame (bucle y pantallas), ColorifyView (UI por código), UIKit + Juice, Sfx, SaveData
Assets/Colorify/Scripts/Platform  Analytics, Ads (H5 Games Ads), Haptics: puentes web
Assets/Colorify/Scripts/Online    KpSocket (WebSocket web/nativo), OnlineApi + DuelConnection, PlayerNames
Assets/Colorify/Editor            ColorifySetup, ColorifyWebBuild (copia adaptada de MassboundWebBuild)
Assets/Colorify/Resources/Fonts   Nunito ExtraBold y Black (OFL)
Assets/Plugins/WebGL              KovusPrideWeb/Analytics.jslib (de MASSBOUND) + KovusPrideExtras.jslib (vibración) + KovusPrideSocket.jslib (WebSocket)
```

## Limitaciones conocidas

- Código comprobado con un compilador de C# fuera de Unity, pero **no ejecutado en Unity**: si algo falla al
  importar, pégame el error de la consola (o vincula el PC y lo leo del Editor.log).
- Sonidos sintetizados provisionales (sustituibles por clips en el objeto «Sfx» en Play o con el pack de Kenney).
- Ranking global y duelos en Cloudflare (Durable Objects, plan gratuito) en lugar de Firebase.
- Anti-trampas básico: el servidor valida nombres, límites y ritmo de aciertos, pero no comprueba cada toque.
- La analítica web no envía nada hasta que la página del portfolio implemente `window.kpAnalytics` con consentimiento.
