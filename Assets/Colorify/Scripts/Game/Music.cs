using UnityEngine;

namespace Colorify
{
    // Música de fondo en bucle (Resources/Audio/music). Sigue al botón de sonido del juego; el silencio y la pausa
    // que pida la plataforma (anuncios, pestaña oculta) los aplica StorePlatform con AudioListener.pause.
    public class Music : MonoBehaviour
    {
        public const string ClipPath = "Audio/music";
        public float volume = 0.32f;

        private AudioSource source;

        public static Music Create(Transform parent)
        {
            var go = new GameObject("Music");
            go.transform.SetParent(parent, false);
            return go.AddComponent<Music>();
        }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.volume = volume;
            source.clip = Resources.Load<AudioClip>(ClipPath);
            if (source.clip == null) Debug.Log("[Colorify] Sin música (falta Resources/" + ClipPath + ")");
        }

        public void SetOn(bool on)
        {
            if (source == null || source.clip == null) return;
            if (on && !source.isPlaying) source.Play();
            else if (!on && source.isPlaying) source.Stop();
        }
    }
}
