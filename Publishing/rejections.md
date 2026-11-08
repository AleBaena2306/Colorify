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
