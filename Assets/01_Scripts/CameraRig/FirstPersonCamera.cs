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
        [Tooltip("Altura de los ojos si no hay otros personajes con los que compararse.")]
        [SerializeField] private float eyeHeight = 1.7f;
        [Tooltip("Los ojos van un poco por encima del hueso de la cabeza de los demás personajes.")]
        [SerializeField] private float eyeAboveHead = 0.08f;
        [SerializeField] private float eyeForward = 0.18f;
        [SerializeField] private Vector2 pitchLimits = new Vector2(-35f, 40f);
        [SerializeField] private float fieldOfView = 70f;
        [SerializeField] private float nearClip = 0.05f;

        [Header("Arma en pantalla")]
        [Tooltip("Dónde va la mano respecto a la cámara (derecha, abajo, delante).")]
        [SerializeField] private Vector3 viewmodelOffset = new Vector3(0.22f, -0.24f, 0.55f);
        [Tooltip("Rapidez con la que la punta del arma sigue a la mira (balanceo).")]
        [SerializeField] private float viewmodelFollow = 18f;

        private TopDownCamera topDown;
        private PlayerWeapons weapons;
        private PlayerAim aim;
        private Quaternion viewmodelRotation = Quaternion.identity;
        private float pitch;
        private float currentEye;
        private float eyeRefresh;
        private float eyeTarget;
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

            // Altura de los ojos igual a la de los demás personajes (se revisa de vez en cuando).
            eyeRefresh -= Time.deltaTime;
            if (eyeRefresh <= 0f)
            {
                eyeRefresh = 1f;
                eyeTarget = MeasureEyeHeight();
            }
            currentEye = Mathf.MoveTowards(currentEye, eyeTarget, Time.deltaTime * 0.5f); // sin saltos

            pitch = Mathf.Clamp(pitch - GameInput.LookDelta.y * ViewMode.Sensitivity, pitchLimits.x, pitchLimits.y);
            Quaternion yaw = Quaternion.Euler(0f, Target.eulerAngles.y, 0f);
            Cam.transform.SetPositionAndRotation(
                Target.position + Vector3.up * currentEye + yaw * Vector3.forward * eyeForward,
                yaw * Quaternion.Euler(pitch, 0f, 0f));
            HideBody(); // por si el jugador cogió o soltó algo
            PlaceViewmodel();
        }

        /// <summary>La mano (y el arma que lleve) abajo a la derecha de la vista, apuntando a la mira.</summary>
        private void PlaceViewmodel()
        {
            if (weapons == null) weapons = Target.GetComponent<PlayerWeapons>();
            if (aim == null) aim = Target.GetComponent<PlayerAim>();
            Transform hand = weapons != null ? weapons.Hand : null;
            if (hand == null) return;

            Transform cam = Cam.transform;
            // Cuerpo a cuerpo (bate, cuchillo): su propia pose, no apunta a la mira.
            WeaponItem held = weapons.Current;
            if (held != null && WeaponViewScale.FirstPersonMeleePose(held.Type, out Vector3 meleeOffset, out Quaternion meleeRotation))
            {
                Quaternion meleeTarget = cam.rotation * meleeRotation;
                viewmodelRotation = Quaternion.Slerp(viewmodelRotation, meleeTarget, 1f - Mathf.Exp(-viewmodelFollow * Time.deltaTime));
                Quaternion meleeSwing = hand.localRotation;
                hand.SetPositionAndRotation(cam.TransformPoint(meleeOffset), viewmodelRotation * meleeSwing);
                return;
            }
            Vector3 position = cam.TransformPoint(viewmodelOffset);
            Vector3 aimPoint = aim != null && aim.HasAim ? aim.AimPoint : cam.position + cam.forward * 30f;
            Vector3 toAim = aimPoint - position;
            if (toAim.sqrMagnitude < 1f) toAim = cam.forward; // muy cerca: hacia delante
            Quaternion target = Quaternion.LookRotation(toAim.normalized, cam.up);
            viewmodelRotation = Quaternion.Slerp(viewmodelRotation, target, 1f - Mathf.Exp(-viewmodelFollow * Time.deltaTime));

            // El golpe con arma cuerpo a cuerpo (PlayerWeapons gira la mano en Update) se suma encima.
            Quaternion swing = hand.localRotation;
            hand.SetPositionAndRotation(position, viewmodelRotation * swing);
        }

        /// <summary>
        /// Altura de los ojos: la cabeza de los enemigos de pie (su hueso Head respecto a sus pies), así el
        /// jugador en primera persona mira a la misma altura que ellos. Sin enemigos, el valor por defecto.
        /// </summary>
        private float MeasureEyeHeight()
        {
            float best = 0f;
            foreach (Overdrive.Enemies.Enemy enemy in Overdrive.Enemies.Enemy.All)
            {
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.Posture != Overdrive.Combat.Posture.Standing) continue;
                Animator anim = enemy.GetComponentInChildren<Animator>();
                Transform head = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
                if (head == null) continue;
                best = Mathf.Max(best, head.position.y - enemy.transform.position.y);
            }
            return best > 0.5f ? best + eyeAboveHead : (currentEye > 0.5f ? currentEye : eyeHeight);
        }

        private void Enter()
        {
            active = true;
            currentEye = eyeTarget = MeasureEyeHeight();
            eyeRefresh = 1f;
            pitch = 0f;
            topDown.enabled = false;
            viewmodelRotation = Cam.transform.rotation;
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
            if (weapons != null && weapons.Hand != null) weapons.Hand.localRotation = Quaternion.identity;
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
