using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Player
{
    /// <summary>
    /// Vista cenital: gira al jugador hacia el punto del suelo bajo el mouse.
    /// Primera persona: el ratón gira al jugador y se apunta a lo que hay en el centro de la pantalla.
    /// </summary>
    public class PlayerAim : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField] private PlayerLife life;

        /// <summary>Radio alrededor del jugador (a escala 1) en el que el ratón no lo gira.</summary>
        public const float AimDeadZone = 0.45f;

        public Vector3 AimPoint { get; private set; }
        public bool HasAim { get; private set; }
        public Camera AimCamera => aimCamera;
        public PlayerLife Life => life;

        private void Update()
        {
            if (aimCamera == null) aimCamera = Camera.main;
            if (aimCamera == null) return;

            if (ViewMode.FirstPerson)
            {
                UpdateFirstPerson();
                return;
            }

            Ray ray = aimCamera.ScreenPointToRay(GameInput.MousePosition);
            // El plano de apuntado es el suelo del piso en el que está el jugador.
            HasAim = AimMath.TryGetPointOnPlane(ray, transform.position.y, out Vector3 point);
            if (!HasAim) return;
            AimPoint = point;

            if (life != null && life.IsDead) return;
            // Con el ratón casi encima del jugador (p. ej. en el centro de la pantalla al empezar) no se gira:
            // cualquier temblor del cursor le haría dar vueltas sin parar.
            Vector3 flat = point - transform.position;
            flat.y = 0f;
            if (flat.magnitude < AimDeadZone * transform.lossyScale.x) return;
            if (AimMath.TryGetFlatLookRotation(transform.position, point, out Quaternion rotation))
                transform.rotation = rotation;
        }

        private void UpdateFirstPerson()
        {
            if (life == null || !life.IsDead)
                transform.Rotate(0f, GameInput.LookDelta.x * ViewMode.Sensitivity, 0f, Space.World);

            // Lo primero que toca el rayo del centro de la pantalla (sin contar al propio jugador).
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            AimPoint = ray.GetPoint(60f);
            float best = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(ray, 60f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= best) continue;
                best = hit.distance;
                AimPoint = hit.point;
            }
            HasAim = true;
        }

        /// <summary>
        /// Dirección horizontal del disparo desde <paramref name="origin"/> hacia el punto apuntado
        /// (la mira). Si el punto está demasiado cerca, hacia donde mira el jugador.
        /// </summary>
        public Vector3 ShotDirection(Vector3 origin)
        {
            // Primera persona: directo a lo que hay bajo la mira, también hacia arriba o hacia el suelo.
            if (ViewMode.FirstPerson && aimCamera != null)
            {
                Vector3 look = aimCamera.transform.forward;
                Vector3 toAim = AimPoint - origin;
                if (HasAim && toAim.sqrMagnitude > 0.25f && Vector3.Dot(toAim, look) > 0f) return toAim.normalized;
                return look;
            }

            Vector3 forward = transform.forward;
            if (!HasAim) return forward;
            Vector3 to = AimPoint - origin;
            to.y = 0f;
            // Solo si el punto queda por delante: evita disparar hacia atrás con el ratón pegado al jugador.
            if (to.sqrMagnitude < 0.36f || Vector3.Dot(to, forward) <= 0f) return forward;
            return to.normalized;
        }
    }
}
