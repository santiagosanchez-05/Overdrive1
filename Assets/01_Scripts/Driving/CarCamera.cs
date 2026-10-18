using UnityEngine;

namespace Overdrive.Driving
{
    /// <summary>
    /// Cámara cenital de la persecución: siempre con el coche en el centro de la pantalla. Lo sigue exactamente (sin
    /// adelantarse ni quedarse atrás), desde arriba y un poco por detrás.
    /// </summary>
    public class CarCamera : MonoBehaviour
    {
        [SerializeField] private Car target;
        [SerializeField] private float height = 30f;
        [SerializeField] private float back = 10f;

        public Car Target => target;
        public Vector3 Offset => new Vector3(0f, height, -back);

        // Después de que el coche se mueva (también con interpolación de la física).
        private void LateUpdate() => Follow();

        /// <summary>Coloca la cámara con el coche en el centro. Público para comprobarlo en el editor.</summary>
        public void Follow()
        {
            if (target == null) return;
            Vector3 focus = target.transform.position;
            transform.position = focus + Offset;
            transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.forward);
        }

        public void Snap() => Follow();
    }
}
