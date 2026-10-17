using Overdrive.Combat;
using Overdrive.Player;
using Overdrive.Weapons;
using UnityEngine;

namespace Overdrive.Enemies
{
    /// <summary>
    /// Anima el modelo 3D del enemigo (Enemy_Mask_Character): quieto, caminar/perseguir,
    /// ataque con cuchillo/daga o bate, y caída/muerte. Al levantarse o rebobinar vuelve de pie al instante.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyAnimator : MonoBehaviour
    {
        public const string MoveParam = "Move";
        public const string DownParam = "Down";
        public const string StepSpeedParam = "StepSpeed";
        public const string StabKnife = "StabKnife";
        public const string SwingBat = "SwingBat";
        public const string LocomotionState = "Locomotion";

        [SerializeField] private Animator animator;
        [SerializeField] private Enemy enemy;
        [SerializeField] private EnemyAI ai;
        [SerializeField] private float damping = 0.08f;
        [SerializeField] private float walkClipSpeed = 2.2f;

        private Vector3 lastPosition;
        private bool wasDown;
        private bool wasWinding;
        private Transform handBone;
        private Vector3 handRest;
        private bool handRestCaptured;

        public Animator Animator => animator;
        public float WalkClipSpeed => walkClipSpeed;

        public static string AttackTrigger(WeaponType? weapon) =>
            weapon == WeaponType.Bat ? SwingBat : StabKnife;

        private void OnEnable()
        {
            lastPosition = transform.position;
        }

        private void Update()
        {
            if (animator == null) return;
            float dt = Time.deltaTime;
            Vector3 velocity = dt > 0f ? (transform.position - lastPosition) / dt : Vector3.zero;
            lastPosition = transform.position;

            bool down = enemy != null && enemy.Posture != Posture.Standing;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float maxSpeed = ai != null && ai.Agent != null && ai.Agent.speed > 0.1f ? ai.Agent.speed : 5f;
            float move = down ? 0f : Mathf.Clamp01(flat.magnitude / maxSpeed);

            animator.SetFloat(MoveParam, move, damping, dt);
            animator.SetFloat(StepSpeedParam, PlayerAnimLogic.StepSpeed(flat.magnitude, walkClipSpeed), damping, dt);
            animator.SetBool(DownParam, down);

            if (wasDown && !down)
            {
                animator.Play(LocomotionState, 0, 0f);
                animator.Update(0f);
            }
            wasDown = down;

            bool winding = ai != null && ai.IsWindingUp;
            if (winding && !wasWinding && !down)
            {
                WeaponType? type = enemy != null && enemy.Weapon != null ? enemy.Weapon.Type : (WeaponType?)null;
                string trigger = AttackTrigger(type);
                animator.ResetTrigger(StabKnife);
                animator.ResetTrigger(SwingBat);
                animator.SetTrigger(trigger);
            }
            wasWinding = winding;
        }

        private void LateUpdate()
        {
            Transform hand = enemy != null ? enemy.Hand : null;
            if (hand == null || animator == null || !animator.isHuman) return;
            if (!handRestCaptured)
            {
                handRest = hand.localPosition;
                handRestCaptured = true;
                handBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            bool follow = handBone != null && (enemy == null || enemy.Posture == Posture.Standing);
            hand.position = follow ? handBone.position + transform.forward * 0.08f : hand.parent.TransformPoint(handRest);
        }
    }
}
