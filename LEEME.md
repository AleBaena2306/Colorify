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
- **No uses todavía «Web (móvil + PC)»**: publica en el portfolio y Colorify aún no tiene página (game.json).
  Antes hay que decidir cómo probarlo en móviles sin que aparezca en la web (ver proyectos/colorify.md).

## Estructura

```
Assets/Colorify/Scripts/Core      OkColor (OKLab), ColorifyConfig, RunState (rondas, puntos, cartas), ColorBoard (generador)
Assets/Colorify/Scripts/Game      ColorifyGame (bucle y pantallas), ColorifyView (UI por código), UIKit + Juice, Sfx, SaveData
Assets/Colorify/Scripts/Platform  Analytics, Ads (H5 Games Ads), Haptics: puentes web
Assets/Colorify/Editor            ColorifySetup, ColorifyWebBuild (copia adaptada de MassboundWebBuild)
Assets/Colorify/Resources/Fonts   Nunito ExtraBold y Black (OFL)
Assets/Plugins/WebGL              KovusPrideWeb/Analytics.jslib (de MASSBOUND) + KovusPrideExtras.jslib (vibración)
```

## Limitaciones conocidas

- Código comprobado con un compilador de C# fuera de Unity, pero **no ejecutado en Unity**: si algo falla al
  importar, pégame el error de la consola (o vincula el PC y lo leo del Editor.log).
- Sonidos sintetizados provisionales (sustituibles por clips en el objeto «Sfx» en Play o con el pack de Kenney).
- Sin ranking global todavía: necesita el proyecto de Firebase del estudio (paso tuyo, 5 min, ver el plan).
- La analítica web no envía nada hasta que la página del portfolio implemente `window.kpAnalytics` con consentimiento.
