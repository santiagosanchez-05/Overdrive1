using System.Collections.Generic;
using Overdrive.Core;
using Overdrive.Player;
using Overdrive.Weapons;
using UnityEngine;
using UnityEngine.Rendering;

namespace Overdrive.CameraRig
{
    /// <summary>
    /// Vista en primera persona (V la activa o desactiva). La cámara va en los ojos del jugador:
    /// el ratón gira al jugador (horizontal) y mira arriba/abajo (vertical). El cuerpo del jugador
    /// solo proyecta sombra para no tapar la vista; el arma en la mano sí se ve.
    /// Se añade sola al rig de la cámara cenital (<see cref="TopDownCamera"/>).
    /// </summary>
    [RequireComponent(typeof(TopDownCamera))]
    public class FirstPersonCamera : MonoBehaviour
    {
        [SerializeField] private float eyeHeight = 1.7f;
        [SerializeField] private float eyeForward = 0.18f;
        [SerializeField] private Vector2 pitchLimits = new Vector2(-35f, 40f);
        [SerializeField] private float fieldOfView = 70f;
        [SerializeField] private float nearClip = 0.05f;

        private TopDownCamera topDown;
        private float pitch;
        private bool active;
        private float savedFov, savedNear;
        private readonly Dictionary<Renderer, ShadowCastingMode> hidden = new Dictionary<Renderer, ShadowCastingMode>();

        private Transform Target => topDown.Target;
        private Camera Cam => topDown.Cam;

        private void Awake()
        {
            topDown = GetComponent<TopDownCamera>();
            if (Crosshair.Instance == null) new GameObject("Crosshair").AddComponent<Crosshair>();
        }

        private void OnDisable()
        {
            if (active) Exit();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (GameInput.ToggleViewPressed) ViewMode.FirstPerson = !ViewMode.FirstPerson;
            UpdateCursor();
        }

        private void LateUpdate()
        {
            if (Target == null || Cam == null) return;
            if (ViewMode.FirstPerson != active)
            {
                if (ViewMode.FirstPerson) Enter();
                else Exit();
            }
            if (!active) return;

            pitch = Mathf.Clamp(pitch - GameInput.LookDelta.y * ViewMode.Sensitivity, pitchLimits.x, pitchLimits.y);
            Quaternion yaw = Quaternion.Euler(0f, Target.eulerAngles.y, 0f);
            Cam.transform.SetPositionAndRotation(
                Target.position + Vector3.up * eyeHeight + yaw * Vector3.forward * eyeForward,
                yaw * Quaternion.Euler(pitch, 0f, 0f));
            HideBody(); // por si el jugador cogió o soltó algo
        }

        private void Enter()
        {
            active = true;
            pitch = 0f;
            topDown.enabled = false;
            savedFov = Cam.fieldOfView;
            savedNear = Cam.nearClipPlane;
            Cam.fieldOfView = fieldOfView;
            Cam.nearClipPlane = nearClip;
            HideBody();
        }

        private void Exit()
        {
            active = false;
            topDown.enabled = true;
            if (Cam != null)
            {
                Cam.fieldOfView = savedFov;
                Cam.nearClipPlane = savedNear;
            }
            topDown.ApplyCameraOffset();
            topDown.SnapToTarget();
            foreach (KeyValuePair<Renderer, ShadowCastingMode> r in hidden)
                if (r.Key != null) r.Key.shadowCastingMode = r.Value;
            hidden.Clear();
        }

        /// <summary>El cuerpo pasa a "solo sombra"; lo que cuelga de un arma se ve normal.</summary>
        private void HideBody()
        {
            foreach (Renderer r in Target.GetComponentsInChildren<Renderer>(true))
            {
                bool isWeapon = r.GetComponentInParent<WeaponItem>() != null;
                if (isWeapon)
                {
                    if (hidden.TryGetValue(r, out ShadowCastingMode mode))
                    {
                        r.shadowCastingMode = mode;
                        hidden.Remove(r);
                    }
                    continue;
                }
                if (hidden.ContainsKey(r) || r is TrailRenderer) continue;
                hidden[r] = r.shadowCastingMode;
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        private static void UpdateCursor()
        {
            // En pausa se usa el cursor normal; jugando, la mira sustituye al cursor.
            bool paused = GameInput.Blocked;
            Cursor.visible = paused;
            Cursor.lockState = !paused && ViewMode.FirstPerson ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}
