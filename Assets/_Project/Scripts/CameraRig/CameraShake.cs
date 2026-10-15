using UnityEngine;

namespace Overdrive.CameraRig
{
    /// <summary>Aplica el temblor de cámara moviendo este transform (hijo del rig, padre de la cámara).</summary>
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private float maxOffset = 0.5f;
        [SerializeField] private float decayPerSecond = 2.5f;
        [SerializeField] private float frequency = 25f;

        private readonly ShakeState state = new ShakeState();
        private static CameraShake active;

        public ShakeState State => state;

        /// <summary>Añade temblor a la cámara activa, si existe. Valores típicos: 0.2 golpe, 0.35 disparo.</summary>
        public static void Shake(float amount)
        {
            if (active != null) active.state.Add(amount);
        }

        private void OnEnable() => active = this;

        private void OnDisable()
        {
            if (active == this) active = null;
        }

        private void LateUpdate()
        {
            state.Tick(Time.deltaTime, decayPerSecond);
            float intensity = state.Intensity;
            if (intensity <= 0f)
            {
                transform.localPosition = Vector3.zero;
                return;
            }
            float t = Time.time * frequency;
            transform.localPosition = new Vector3(
                (Mathf.PerlinNoise(t, 0f) * 2f - 1f) * maxOffset * intensity,
                0f,
                (Mathf.PerlinNoise(0f, t) * 2f - 1f) * maxOffset * intensity);
        }
    }
}
