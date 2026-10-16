using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Player
{
    /// <summary>Los pasos del jugador hacen ruido; agachado (Ctrl) camina en silencio.</summary>
    public class PlayerFootsteps : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private float stepLength = 1.4f;
        [SerializeField] private float noiseRadius = 5f;
        [Tooltip("Saltos más grandes que esto (remate, reinicio) no cuentan como pasos.")]
        [SerializeField] private float maxStepJump = 3f;

        private FootstepCounter counter;
        private Vector3 lastPosition;
        private bool hasLast;

        public PlayerController Controller => controller;
        public float NoiseRadius => noiseRadius;

        private FootstepCounter Counter => counter ??= new FootstepCounter(stepLength);

        private void Update()
        {
            if (controller != null && controller.Life != null && controller.Life.IsDead) return;
            Step(transform.position, controller != null && controller.IsSneaking);
        }

        /// <summary>Registra el movimiento hasta <paramref name="position"/>. Devuelve true si hizo ruido.</summary>
        public bool Step(Vector3 position, bool sneaking)
        {
            if (!hasLast)
            {
                lastPosition = position;
                hasLast = true;
                return false;
            }
            Vector3 delta = position - lastPosition;
            delta.y = 0f;
            lastPosition = position;
            float distance = delta.magnitude;
            if (distance > maxStepJump) return false;

            if (sneaking)
            {
                Counter.Reset();
                return false;
            }
            if (!Counter.Advance(distance)) return false;
            NoiseSystem.Emit(position, noiseRadius);
            return true;
        }
    }
}
