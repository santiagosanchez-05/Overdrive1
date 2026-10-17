using Overdrive.Core;
using Overdrive.Player;
using UnityEngine;

namespace Overdrive.CameraRig
{
    /// <summary>
    /// Cámara cenital ligeramente inclinada que sigue al jugador y se adelanta hacia el mouse.
    /// Jerarquía: CameraRig (este script, sigue al foco) > Shake > Main Camera (offset fijo).
    /// </summary>
    public class TopDownCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private PlayerAim aim;
        [SerializeField] private Camera cam;
        [SerializeField, Range(45f, 90f)] private float pitch = 70f;
        [SerializeField] private float distance = 20f;
        [SerializeField] private float smoothTime = 0.12f;
        [SerializeField] private CameraLookAhead.Settings lookAhead = CameraLookAhead.Settings.Default;

        private Vector3 velocity;

        public Transform Target => target;
        public PlayerAim Aim => aim;
        public Camera Cam => cam;

        private void Awake()
        {
            ApplyCameraOffset();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 aimPoint = aim != null && aim.HasAim ? aim.AimPoint : target.position;
            Vector3 focus = CameraLookAhead.ComputeFocus(target.position, aimPoint, GameInput.LookFar, lookAhead);
            transform.position = Vector3.SmoothDamp(transform.position, focus, ref velocity, smoothTime);
        }

        /// <summary>Coloca la cámara hija para que mire al origen del rig con el ángulo configurado.</summary>
        public void ApplyCameraOffset()
        {
            if (cam == null) return;
            Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
            cam.transform.localRotation = rotation;
            cam.transform.localPosition = rotation * new Vector3(0f, 0f, -distance);
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.position;
            velocity = Vector3.zero;
        }
    }
}
