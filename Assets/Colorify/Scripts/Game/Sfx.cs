using System;
using UnityEngine;

namespace Colorify
{
    // Efectos de sonido: archivos ogg de Kenney (CC0) en Resources/Audio/sfx_<id>.ogg. Playgama rechaza el sonido
    // sintetizado por código, así que la síntesis solo queda como reserva si faltara algún archivo.
    public class Sfx : MonoBehaviour
    {
        public enum Id { Hit, HitWarm, Wrong, Tick, Go, Clear, Lose, Card, Click, Victory }

        [Tooltip("Clips propios opcionales, en el orden del enum Id. Vacío = sonido sintetizado")]
        public AudioClip[] overrides = new AudioClip[Enum.GetValues(typeof(Id)).Length];
        public static bool Enabled = true;

        private AudioClip[] clips;
        private AudioSource[] sources;
        private int next;
        private const int Rate = 44100;

        public static Sfx Create(Transform parent)
        {
            var go = new GameObject("Sfx");
            go.transform.SetParent(parent, false);
            return go.AddComponent<Sfx>();
        }

        private void Awake()
        {
            sources = new AudioSource[8];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
            }
            int n = Enum.GetValues(typeof(Id)).Length;
            clips = new AudioClip[n];
            clips[(int)Id.Hit] = Make("hit", 0.09f, t => Blip(t, 620f, 980f, 0.09f) * 0.6f + Blip(t, 1240f, 1960f, 0.09f) * 0.15f);
            clips[(int)Id.HitWarm] = Make("hitWarm", 0.16f, t => Blip(t, 660f, 990f, 0.08f) * 0.5f + Delay(t, 0.06f, u => Blip(u, 990f, 1320f, 0.1f) * 0.5f));
            clips[(int)Id.Wrong] = Make("wrong", 0.2f, t => Square(t, 150f - 60f * t) * Env(t, 0.2f) * 0.35f);
            clips[(int)Id.Tick] = Make("tick", 0.03f, t => Sine(t, 1500f) * Env(t, 0.03f) * 0.35f);
            clips[(int)Id.Go] = Make("go", 0.22f, t => (Sine(t, 880f) + 0.4f * Sine(t, 1320f)) * Env(t, 0.22f) * 0.45f);
            clips[(int)Id.Clear] = Make("clear", 0.42f, t => Arp(t, new[] { 523f, 659f, 784f, 1046f }, 0.075f) * 0.5f);
            clips[(int)Id.Lose] = Make("lose", 0.5f, t => Sine(t, 420f - 260f * t) * Env(t, 0.5f) * 0.5f);
            clips[(int)Id.Card] = Make("card", 0.07f, t => (Noise() * 0.3f + Sine(t, 760f)) * Env(t, 0.07f) * 0.4f);
            clips[(int)Id.Click] = Make("click", 0.035f, t => Sine(t, 980f) * Env(t, 0.035f) * 0.35f);
            clips[(int)Id.Victory] = Make("victory", 0.75f, t => Arp(t, new[] { 523f, 659f, 784f, 1046f, 1318f, 1568f }, 0.11f) * 0.5f);

            // Archivos reales (sustituyen a los sintetizados)
            foreach (Id id in Enum.GetValues(typeof(Id)))
            {
                var clip = Resources.Load<AudioClip>("Audio/sfx_" + id.ToString().ToLowerInvariant());
                if (clip != null) clips[(int)id] = clip;
                else Debug.LogWarning("[Colorify] Falta Resources/Audio/sfx_" + id.ToString().ToLowerInvariant() + ".ogg; se usa el sintetizado");
            }
        }

        public void Play(Id id, float pitch = 1f, float volume = 1f)
        {
            if (!Enabled) return;
            var clip = (int)id < overrides.Length && overrides[(int)id] != null ? overrides[(int)id] : clips[(int)id];
            if (clip == null) return;
            var src = sources[next];
            next = (next + 1) % sources.Length;
            src.pitch = pitch;
            src.PlayOneShot(clip, volume);
        }

        // ---------------- Síntesis ----------------

        private static readonly System.Random noiseRng = new System.Random(7);

        private static AudioClip Make(string name, float seconds, Func<float, float> wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Sine(float t, float f) => Mathf.Sin(2f * Mathf.PI * f * t);
        private static float Square(float t, float f) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
        private static float Noise() => (float)noiseRng.NextDouble() * 2f - 1f;
        private static float Env(float t, float len) => t >= len ? 0f : Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-5f * t / len);

        // Barrido de frecuencia corto: el «pop» de un acierto
        private static float Blip(float t, float f0, float f1, float len)
        {
            if (t < 0 || t >= len) return 0f;
            float k = t / len;
            float phase = 2f * Mathf.PI * (f0 * t + (f1 - f0) * t * k * 0.5f);
            return Mathf.Sin(phase) * Env(t, len);
        }

        private static float Delay(float t, float d, Func<float, float> f) => t < d ? 0f : f(t - d);

        private static float Arp(float t, float[] notes, float step)
        {
            int i = Mathf.Min(notes.Length - 1, (int)(t / step));
            float local = t - i * step;
            float tail = i == notes.Length - 1 ? step * 3f : step * 1.6f;
            return Sine(local, notes[i]) * Env(local, tail);
        }
    }
}
