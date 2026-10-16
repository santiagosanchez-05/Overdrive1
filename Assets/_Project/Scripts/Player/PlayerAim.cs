using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Player
{
    /// <summary>Gira al jugador hacia el punto del suelo bajo el mouse.</summary>
    public class PlayerAim : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField] private PlayerLife life;

        public Vector3 AimPoint { get; private set; }
        public bool HasAim { get; private set; }
        public Camera AimCamera => aimCamera;
        public PlayerLife Life => life;

        private void Update()
        {
            if (aimCamera == null) aimCamera = Camera.main;
            if (aimCamera == null) return;

            Ray ray = aimCamera.ScreenPointToRay(GameInput.MousePosition);
            // El plano de apuntado es el suelo del piso en el que está el jugador.
            HasAim = AimMath.TryGetPointOnPlane(ray, transform.position.y, out Vector3 point);
            if (!HasAim) return;
            AimPoint = point;

            if (life != null && life.IsDead) return;
            if (AimMath.TryGetFlatLookRotation(transform.position, point, out Quaternion rotation))
                transform.rotation = rotation;
        }
    }
}
