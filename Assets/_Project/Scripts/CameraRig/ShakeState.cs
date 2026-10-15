using UnityEngine;

namespace Overdrive.CameraRig
{
    /// <summary>
    /// Temblor basado en "trauma": cada golpe suma trauma, que decae con el tiempo.
    /// El desplazamiento es proporcional a trauma² para que los golpes pequeños apenas se noten.
    /// </summary>
    public sealed class ShakeState
    {
        public float Trauma { get; private set; }

        public void Add(float amount)
        {
            Trauma = Mathf.Clamp01(Trauma + amount);
        }

        public void Tick(float deltaTime, float decayPerSecond)
        {
            Trauma = Mathf.Max(0f, Trauma - decayPerSecond * deltaTime);
        }

        public float Intensity => Trauma * Trauma;
    }
}
