using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Luces de antro: cicla colores neón, pulsa la intensidad y hace girar los focos.
    /// </summary>
    public class ClubLights : MonoBehaviour
    {
        [SerializeField] private Light[] lights = new Light[0];
        [SerializeField] private Color[] palette =
        {
            new Color(0.1f, 1f, 0.45f), new Color(1f, 0.55f, 0.15f),
            new Color(1f, 0.1f, 0.8f), new Color(0.1f, 0.6f, 1f)
        };
        [SerializeField] private float colorSpeed = 0.25f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseAmount = 0.3f;
        [SerializeField] private float spinSpeed = 30f;

        private float[] baseIntensity;

        public void Setup(Light[] l) => lights = l;

        private void Start()
        {
            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) baseIntensity[i] = lights[i].intensity;
        }

        private void Update()
        {
            float t = Time.time;
            for (int i = 0; i < lights.Length; i++)
            {
                Light l = lights[i];
                if (l == null) continue;

                float c = (t * colorSpeed + i * 0.37f) % 1f * palette.Length;
                int a = (int)c;
                l.color = Color.Lerp(palette[a % palette.Length], palette[(a + 1) % palette.Length], c - a);
                l.intensity = baseIntensity[i] * (1f + pulseAmount * Mathf.Sin(t * pulseSpeed + i));

                if (l.type == LightType.Spot)
                    l.transform.rotation = Quaternion.Euler(70f + 15f * Mathf.Sin(t + i), t * spinSpeed + i * 45f, 0f);
            }
        }
    }
}
