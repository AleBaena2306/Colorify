using UnityEngine;

namespace Colorify
{
    // Color en el espacio OKLab / OKLCH (Björn Ottosson). Es perceptualmente uniforme: la distancia
    // euclídea entre dos colores se parece a lo diferentes que los ve una persona. Así la dificultad
    // («cuánto se parecen los distractores») se controla con un solo número.
    // Generalizable: candidato a KovusPride.Core (utilidades de color).
    [System.Serializable]
    public struct OkColor
    {
        public float L, a, b;

        public OkColor(float l, float a, float b) { L = l; this.a = a; this.b = b; }

        public static OkColor FromLch(float l, float c, float hueDeg)
        {
            float h = hueDeg * Mathf.Deg2Rad;
            return new OkColor(l, c * Mathf.Cos(h), c * Mathf.Sin(h));
        }

        public float Chroma => Mathf.Sqrt(a * a + b * b);
        public float HueDeg
        {
            get { float h = Mathf.Atan2(b, a) * Mathf.Rad2Deg; return h < 0 ? h + 360f : h; }
        }

        public static float Distance(OkColor x, OkColor y)
        {
            float dl = x.L - y.L, da = x.a - y.a, db = x.b - y.b;
            return Mathf.Sqrt(dl * dl + da * da + db * db);
        }

        // Color de Unity (componentes sRGB, como en el Inspector). Si el color cae fuera de sRGB,
        // reduce el croma conservando luminosidad y tono.
        public Color ToColor()
        {
            if (TryToSrgb(this, out Color c)) return c;
            float chroma = Chroma, hue = HueDeg, lo = 0f, hi = chroma;
            for (int i = 0; i < 14; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (TryToSrgb(FromLch(L, mid, hue), out _)) lo = mid; else hi = mid;
            }
            TryToSrgb(FromLch(L, lo, hue), out c);
            return c;
        }

        public static OkColor FromColor(Color c)
        {
            float r = ToLinear(c.r), g = ToLinear(c.g), bl = ToLinear(c.b);
            float l = Cbrt(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * bl);
            float m = Cbrt(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * bl);
            float s = Cbrt(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * bl);
            return new OkColor(
                0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s,
                1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s,
                0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s);
        }

        private static bool TryToSrgb(OkColor o, out Color c)
        {
            float l = o.L + 0.3963377774f * o.a + 0.2158037573f * o.b;
            float m = o.L - 0.1055613458f * o.a - 0.0638541728f * o.b;
            float s = o.L - 0.0894841775f * o.a - 1.2914855480f * o.b;
            l = l * l * l; m = m * m * m; s = s * s * s;
            float r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
            float g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
            float bl = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;
            const float eps = 0.0005f;
            bool inside = r >= -eps && r <= 1 + eps && g >= -eps && g <= 1 + eps && bl >= -eps && bl <= 1 + eps;
            c = new Color(ToSrgb(Mathf.Clamp01(r)), ToSrgb(Mathf.Clamp01(g)), ToSrgb(Mathf.Clamp01(bl)), 1f);
            return inside;
        }

        private static float ToLinear(float x) => x <= 0.04045f ? x / 12.92f : Mathf.Pow((x + 0.055f) / 1.055f, 2.4f);
        private static float ToSrgb(float x) => x <= 0.0031308f ? 12.92f * x : 1.055f * Mathf.Pow(x, 1f / 2.4f) - 0.055f;
        private static float Cbrt(float x) => x < 0 ? -Mathf.Pow(-x, 1f / 3f) : Mathf.Pow(x, 1f / 3f);
    }
}
