using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Hace parpadear las luces reales de una tira LED rota, sincronizadas con su shader (Overdrive/LedLine con _Flicker).
    /// </summary>
    public class FlickerLights : MonoBehaviour
    {
        [SerializeField] private Light[] lights = new Light[0];
        [SerializeField] private float baseIntensity = 1.5f;
        [SerializeField] private float seed;

        public Light[] Lights => lights;
        public float BaseIntensity => baseIntensity;
        public float Seed => seed;

        private void Update() => Apply(Time.timeSinceLevelLoad);

        /// <summary>Aplica el nivel de parpadeo de ese instante. Público para simularlo en el editor.</summary>
        public void Apply(float time)
        {
            float intensity = baseIntensity * LedFlicker.Level(time, seed);
            foreach (Light l in lights)
                if (l != null) l.intensity = intensity;
        }
    }
}
