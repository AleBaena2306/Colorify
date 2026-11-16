using System;
using System.Collections.Generic;
// TODO SDK: el using del SDK, dentro del #if para que el proyecto compile sin él
#if KP_PUB_VOODOO
#endif

namespace KovusPride.Publishing.Mobile
{
    // Voodoo · build: COLORIFY > Build > Android publisher > Voodoo (símbolo KP_PUB_VOODOO)
    // SDK: TinySauce (o el SDK que indique su equipo).
    // Para integrarlo: importar el paquete que envíen y rellenar los TODO de abajo, cada llamada dentro de
    // #if KP_PUB_VOODOO … #endif. Sin rellenar, la build funciona igual que ahora (sin anuncios) y escribe los niveles en el log.
    public class VoodooAdapter : PublisherAdapter
    {
        protected override string Name => "Voodoo";

#if KP_PUB_VOODOO
        // Descomentar cada método a medida que se rellena. Mientras esté comentado, se usa el de PublisherAdapter.

        // public override void Init(Action<bool, bool> ready)
        // {
        //     // TODO SDK: inicializar (claves e ids van en la configuración del SDK, no aquí)
        //     ready?.Invoke(true, true);   // (hay intersticiales, hay bonificados)
        // }

        // public override void LevelStarted(int level)
        // {
        //     // TODO SDK: evento de nivel empezado con «level»
        // }

        // public override void LevelEnded(int level, bool completed, long score, int seconds)
        // {
        //     // TODO SDK: nivel completado si «completed»; si no, nivel fallado. Añadir «score» si el SDK lo admite.
        // }

        // public override void Event(string name, IReadOnlyDictionary<string, object> parameters)
        // {
        //     // Opcional: solo si el publisher pide eventos propios (tutorial_complete, theme_select…)
        // }

        // public override void ShowInterstitial(string placement, Action done)
        // {
        //     // TODO SDK: mostrar intersticial y llamar a done() al cerrarse o si no hay anuncio
        // }

        // public override void ShowRewarded(string placement, Action<bool> done)
        // {
        //     // TODO SDK: mostrar bonificado; done(true) solo si se gana la recompensa, done(false) si no hay o se cierra antes
        // }
#endif
    }
}
