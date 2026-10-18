using Overdrive.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Overdrive.CameraRig
{
    /// <summary>
    /// Sangre en la cámara al morir: salpicaduras y regueros por los bordes de la pantalla, viñeta roja
    /// y un latido. Aparece de golpe al morir y se va al rebobinar o reiniciar. La textura se genera
    /// por código (no hace falta ningún asset). La crea <see cref="TopDownCamera"/>.
    /// </summary>
    public class DeathBloodOverlay : MonoBehaviour
    {
        [SerializeField] private float fadeIn = 0.35f;
        [SerializeField] private float fadeOut = 0.25f;
        [SerializeField] private float pulseSpeed = 1.6f;
        [SerializeField, Range(0f, 1f)] private float tintAlpha = 0.28f;

        private static Texture2D texture;

        private PlayerLife life;
        private RawImage splatter;
        private Image tint;
        private float amount;
        private float deathTime;
        private bool wasDead;

        public void Init(PlayerLife target) => life = target;

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30; // debajo de la mira (40) y de la pausa (50)

            tint = Layer<Image>("Tint");
            tint.color = new Color(0.45f, 0f, 0f, 0f);
            splatter = Layer<RawImage>("Splatter");
            splatter.texture = Texture;
            splatter.color = new Color(1f, 1f, 1f, 0f);
        }

        private T Layer<T>(string name) where T : Graphic
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var g = rt.gameObject.AddComponent<T>();
            g.raycastTarget = false;
            return g;
        }

        private void Update()
        {
            bool dead = life != null && life.IsDead;
            if (dead && !wasDead) deathTime = Time.unscaledTime;
            wasDead = dead;

            float target = dead ? 1f : 0f;
            float speed = dead ? 1f / fadeIn : 1f / fadeOut;
            amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime * speed);

            // Golpe inicial más fuerte y luego un latido suave.
            float since = Time.unscaledTime - deathTime;
            float hit = dead ? Mathf.Clamp01(1f - since / 0.6f) * 0.25f : 0f;
            float pulse = dead ? 0.08f * Mathf.Sin(since * pulseSpeed * Mathf.PI * 2f) : 0f;
            float a = Mathf.Clamp01(amount * (0.92f + pulse) + hit);

            splatter.color = new Color(1f, 1f, 1f, a);
            tint.color = new Color(0.45f, 0f, 0f, tintAlpha * amount + hit * 0.4f);
            splatter.enabled = tint.enabled = a > 0.001f || amount > 0.001f;
        }

        // ---------------------------------------------------------------- Textura

        private static Texture2D Texture
        {
            get
            {
                if (texture == null) texture = Generate(512, 288, 1337);
                return texture;
            }
        }

        /// <summary>Salpicaduras irregulares en los bordes, regueros que bajan desde arriba y viñeta.</summary>
        private static Texture2D Generate(int w, int h, int seed)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            var alpha = new float[w * h];

            // Viñeta: más sangre cuanto más cerca del borde.
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(u * u * 0.85f + v * v);
                float n = Mathf.PerlinNoise(x * 0.025f + 3f, y * 0.025f + 7f);
                alpha[y * w + x] = Mathf.Clamp01((d - 0.72f + (n - 0.5f) * 0.35f) * 2.2f);
            }

            // Salpicaduras: un manchón central y gotas alrededor, sobre todo cerca de los bordes.
            for (int i = 0; i < 26; i++)
            {
                bool edge = R() < 0.8f;
                float cx = edge ? (R() < 0.5f ? R() * 0.22f : 1f - R() * 0.22f) : R();
                float cy = edge ? R() : (R() < 0.5f ? R() * 0.2f : 1f - R() * 0.2f);
                float radius = Mathf.Lerp(10f, 46f, R() * R());
                Blob(alpha, w, h, cx * w, cy * h, radius, rng);
                int drops = 4 + rng.Next(8);
                for (int k = 0; k < drops; k++)
                {
                    float ang = R() * Mathf.PI * 2f, dist = radius * Mathf.Lerp(1.1f, 2.6f, R());
                    Blob(alpha, w, h, cx * w + Mathf.Cos(ang) * dist, cy * h + Mathf.Sin(ang) * dist, Mathf.Lerp(2f, 6f, R()), rng);
                }
            }

            // Regueros que bajan desde el borde de arriba (en la textura, y alto = arriba).
            for (int i = 0; i < 14; i++)
            {
                float x0 = R() * w, width = Mathf.Lerp(2f, 6f, R()), length = Mathf.Lerp(0.15f, 0.55f, R()) * h;
                for (int s = 0; s < length; s++)
                {
                    float y = h - 1 - s;
                    float taper = 1f - s / length;
                    float wob = Mathf.Sin(s * 0.08f + i) * 1.5f;
                    Blob(alpha, w, h, x0 + wob, y, width * (0.5f + 0.5f * taper), null);
                }
                Blob(alpha, w, h, x0, h - 1 - length, width * 1.4f, null); // gota al final
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "DeathBlood" };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                float a = Mathf.Clamp01(alpha[i]);
                // Rojo oscuro, más brillante donde la capa es fina.
                float shade = Mathf.Lerp(0.55f, 0.28f, a);
                px[i] = new Color(shade, 0.01f, 0.02f, a * 0.95f);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static void Blob(float[] alpha, int w, int h, float cx, float cy, float r, System.Random rng)
        {
            int x0 = Mathf.Max(0, (int)(cx - r * 1.3f)), x1 = Mathf.Min(w - 1, (int)(cx + r * 1.3f));
            int y0 = Mathf.Max(0, (int)(cy - r * 1.3f)), y1 = Mathf.Min(h - 1, (int)(cy + r * 1.3f));
            float seed = rng != null ? (float)rng.NextDouble() * 100f : cx * 0.37f;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - cx, dy = y - cy;
                float ang = Mathf.Atan2(dy, dx);
                // Borde irregular.
                float edge = r * (0.75f
                    + 0.3f * Mathf.PerlinNoise(seed + Mathf.Cos(ang) * 1.3f, seed + Mathf.Sin(ang) * 1.3f)
                    + 0.18f * Mathf.PerlinNoise(seed + 40f + Mathf.Cos(ang) * 4.5f, seed + Mathf.Sin(ang) * 4.5f));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // Borde suave y centro más denso, como sangre líquida.
                float a = Mathf.Clamp01((edge - d) / Mathf.Max(1.5f, r * 0.35f)) * 0.85f + Mathf.Clamp01(1f - d / edge) * 0.15f;
                int i = y * w + x;
                if (a > alpha[i]) alpha[i] = a;
            }
        }
    }
}
