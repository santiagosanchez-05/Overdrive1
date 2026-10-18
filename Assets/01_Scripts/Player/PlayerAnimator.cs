using Overdrive.Combat;
using Overdrive.Weapons;
using UnityEngine;

namespace Overdrive.Player
{
    /// <summary>Valores de animación del jugador (lógica pura, comprobable en el editor).</summary>
    public static class PlayerAnimLogic
    {
        /// <summary>
        /// Parámetro Move del Animator: 0 quieto, 1 corriendo hacia donde mira, -1 corriendo de espaldas.
        /// Se corre "de espaldas" cuando el movimiento va contra la mirada (más de ~110 grados).
        /// </summary>
        public static float Move(Vector3 velocity, Vector3 facing, float maxSpeed)
        {
            velocity.y = 0f;
            facing.y = 0f;
            float speed = velocity.magnitude;
            if (maxSpeed <= 0f || speed < 0.05f) return 0f;
            float amount = Mathf.Clamp01(speed / maxSpeed);
            bool backwards = facing.sqrMagnitude > 0f && Vector3.Dot(velocity / speed, facing.normalized) < -0.35f;
            return backwards ? -amount : amount;
        }

        /// <summary>
        /// Velocidad de reproducción de las animaciones de correr para que los pasos vayan al ritmo real
        /// (sin patinar): velocidad del jugador / velocidad a la que avanza el clip. Quieto, a velocidad normal.
        /// </summary>
        public static float StepSpeed(float speed, float clipSpeed)
        {
            if (speed < 0.05f || clipSpeed <= 0.01f) return 1f;
            return Mathf.Clamp(speed / clipSpeed, 0.6f, 1.8f);
        }

        /// <summary>Trigger de ataque según el arma: puñetazo sin arma o con arma de fuego, bate o daga.</summary>
        public static string AttackTrigger(WeaponType? weapon)
        {
            switch (weapon)
            {
                case WeaponType.Bat: return PlayerAnimator.SwingBat;
                case WeaponType.Knife: return PlayerAnimator.StabKnife;
                default: return PlayerAnimator.Punch;
            }
        }
    }

    /// <summary>
    /// Anima el modelo del jugador: correr hacia delante o de espaldas, correr con arma de fuego,
    /// puñetazo, bate, daga, remate y muerte. Al rebobinar vuelve a la pose de pie al instante.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAnimator : MonoBehaviour
    {
        public const string MoveParam = "Move";
        public const string ArmedParam = "Armed";
        public const string DeadParam = "Dead";
        public const string StepSpeedParam = "StepSpeed";
        public const string Punch = "Punch";
        public const string SwingBat = "SwingBat";
        public const string StabKnife = "StabKnife";
        public const string LocomotionState = "Locomotion";

        [SerializeField] private Animator animator;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerLife life;
        [SerializeField] private PlayerWeapons weapons;
        [SerializeField] private PlayerMelee melee;
        [SerializeField] private PlayerFinisher finisher;
        [SerializeField] private float damping = 0.08f;
        [Tooltip("m/s a los que avanza cada clip con el modelo a escala (los mide el constructor).")]
        [SerializeField] private float runClipSpeed = 5f;
        [SerializeField] private float backClipSpeed = 3f;
        [SerializeField] private float gunRunClipSpeed = 5f;

        private Vector3 lastPosition;
        private bool wasDead;
        private bool wasFinishing;
        private Transform handBone;
        private bool handIsSocket;
        private Vector3 handRest;
        private bool handRestCaptured;

        public Animator Animator => animator;
        public float RunClipSpeed => runClipSpeed;
        public float BackClipSpeed => backClipSpeed;
        public float GunRunClipSpeed => gunRunClipSpeed;

        private void OnEnable()
        {
            lastPosition = transform.position;
            if (melee != null) melee.Punched += OnPunch;
            if (weapons != null) weapons.Swung += OnSwing;
        }

        private void OnDisable()
        {
            if (melee != null) melee.Punched -= OnPunch;
            if (weapons != null) weapons.Swung -= OnSwing;
        }

        private void Update()
        {
            if (animator == null) return;
            float dt = Time.deltaTime;
            Vector3 velocity = dt > 0f ? (transform.position - lastPosition) / dt : Vector3.zero;
            lastPosition = transform.position;

            bool dead = life != null && life.IsDead;
            float maxSpeed = controller != null ? controller.MoveSpeed : 6f;
            float move = dead ? 0f : PlayerAnimLogic.Move(velocity, transform.forward, maxSpeed);
            animator.SetFloat(MoveParam, move, damping, dt);
            bool armed = IsArmedWithGun();
            animator.SetFloat(ArmedParam, armed ? 1f : 0f, damping, dt);
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float clipSpeed = move < 0f ? backClipSpeed : armed ? gunRunClipSpeed : runClipSpeed;
            animator.SetFloat(StepSpeedParam, PlayerAnimLogic.StepSpeed(flat.magnitude, clipSpeed), damping, dt);
            animator.SetBool(DeadParam, dead);

            // Rebobinar: de muerto a vivo sin esperar a la transición.
            if (wasDead && !dead)
            {
                animator.Play(LocomotionState, 0, 0f);
                animator.Update(0f);
            }
            wasDead = dead;

            // El remate son puñetazos en el suelo.
            bool finishing = finisher != null && finisher.IsFinishing;
            if (finishing && !wasFinishing) animator.SetTrigger(Punch);
            wasFinishing = finishing;
        }

        /// <summary>El arma va en la mano del modelo: el punto Hand sigue al WeaponSocket de la mano derecha (no su giro).</summary>
        private void LateUpdate()
        {
            Transform hand = weapons != null ? weapons.Hand : null;
            if (hand == null || animator == null || !animator.isHuman) return;
            if (!handRestCaptured)
            {
                handRest = hand.localPosition;
                handRestCaptured = true;
                handBone = HandSocket.Find(animator, out handIsSocket);
            }
            if (Overdrive.Core.ViewMode.FirstPerson) return; // en primera persona el arma la coloca la cámara
            bool follow = handBone != null && (life == null || !life.IsDead);
            hand.position = follow ? HandSocket.GripPosition(handBone, handIsSocket, transform.forward) : hand.parent.TransformPoint(handRest);
        }

        private bool IsArmedWithGun()
        {
            WeaponItem current = weapons != null ? weapons.Current : null;
            return current != null && !current.Definition.IsMelee;
        }

        private void OnPunch() => Trigger(PlayerAnimLogic.AttackTrigger(null));

        private void OnSwing(WeaponType type) => Trigger(PlayerAnimLogic.AttackTrigger(type));

        private void Trigger(string name)
        {
            if (animator == null || (life != null && life.IsDead)) return;
            animator.ResetTrigger(Punch);
            animator.ResetTrigger(SwingBat);
            animator.ResetTrigger(StabKnife);
            animator.SetTrigger(name);
        }
    }
}
