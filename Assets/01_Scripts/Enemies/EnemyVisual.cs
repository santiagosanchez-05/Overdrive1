using Overdrive.Combat;
using Overdrive.Weapons;
using UnityEngine;

namespace Overdrive.Enemies
{
    /// <summary>Qué modelo y qué animaciones usa cada enemigo (lógica pura, comprobable en el editor).</summary>
    public static class EnemyVisualLogic
    {
        /// <summary>Índice del modelo según el arma con la que empieza: 0 bate (o desarmado), 1 cuchillo, 2 escopeta, 3 subfusil.</summary>
        public static int ModelIndex(WeaponType? weapon)
        {
            switch (weapon)
            {
                case WeaponType.Knife: return 1;
                case WeaponType.Shotgun: return 2;
                case WeaponType.Smg:
                case WeaponType.Revolver: return 3;
                default: return 0;
            }
        }

        /// <summary>Parámetro Speed: 0 quieto, 0.5 andando (patrulla), 1 corriendo (persecución).</summary>
        public static float Speed(float metersPerSecond, float walkSpeed, float runSpeed)
        {
            if (metersPerSecond < 0.1f) return 0f;
            if (metersPerSecond <= walkSpeed) return 0.5f * Mathf.Clamp01(metersPerSecond / Mathf.Max(walkSpeed, 0.01f));
            return 0.5f + 0.5f * Mathf.Clamp01((metersPerSecond - walkSpeed) / Mathf.Max(runSpeed - walkSpeed, 0.01f));
        }

        /// <summary>Trigger de ataque cuerpo a cuerpo: puñalada con cuchillo, golpe de bate con todo lo demás.</summary>
        public static string AttackTrigger(WeaponType? weapon) =>
            weapon == WeaponType.Knife ? EnemyVisual.StabParam : EnemyVisual.SwingParam;
    }

    /// <summary>
    /// Aspecto del enemigo: al empezar enciende el modelo que corresponde a su arma (cada tipo de enemigo se ve distinto)
    /// y lo anima: quieto, andando, corriendo, golpe cuerpo a cuerpo y caída al ser derribado o morir.
    /// La cápsula sigue existiendo para la lógica (postura, colisión, rebobinar), pero no se ve.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyVisual : MonoBehaviour
    {
        public const string SpeedParam = "Speed";
        public const string DownParam = "Down";
        public const string SwingParam = "Swing";
        public const string StabParam = "Stab";
        public const string LocomotionState = "Locomotion";

        [SerializeField] private Enemy enemy;
        [SerializeField] private EnemyAI ai;
        [Tooltip("Modelos por arma: bate, cuchillo, escopeta, subfusil.")]
        [SerializeField] private GameObject[] models = new GameObject[4];
        [SerializeField] private Renderer[] capsuleRenderers;
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float runSpeed = 5f;
        [SerializeField] private float damping = 0.1f;

        private GameObject active;
        private Animator animator;
        private WeaponType? startWeapon;
        private Vector3 lastPosition;
        private bool wasDown;
        private bool wasWinding;
        private Transform handBone;
        private Vector3 handRest;
        private bool handRestCaptured;

        public GameObject ActiveModel => active;
        public Animator Animator => animator;
        public GameObject[] Models => models;

        private void Awake()
        {
            startWeapon = enemy != null && enemy.Weapon != null ? enemy.Weapon.Definition.Type : (WeaponType?)null;
            Apply(startWeapon);
            lastPosition = transform.position;
        }

        /// <summary>Enciende el modelo del arma indicada y apaga los demás. Público para el constructor y las pruebas.</summary>
        public void Apply(WeaponType? weapon)
        {
            int index = EnemyVisualLogic.ModelIndex(weapon);
            for (int i = 0; i < models.Length; i++)
                if (models[i] != null) models[i].SetActive(i == index);
            active = index < models.Length ? models[index] : null;
            animator = active != null ? active.GetComponent<Animator>() : null;
            handBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            bool hasModel = active != null;
            if (capsuleRenderers != null)
                foreach (Renderer r in capsuleRenderers)
                    if (r != null) r.forceRenderingOff = hasModel; // FloorVisibility toca "enabled", no esto
        }

        private void Update()
        {
            if (animator == null || enemy == null) return;
            float dt = Time.deltaTime;
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;

            bool down = enemy.Posture != Posture.Standing;
            float speed = down || dt <= 0f ? 0f : EnemyVisualLogic.Speed(delta.magnitude / dt, walkSpeed, runSpeed);
            animator.SetFloat(SpeedParam, speed, damping, dt);
            animator.SetBool(DownParam, down);

            // Se levanta del derribo (o se rebobina): de pie al instante.
            if (wasDown && !down)
            {
                animator.Play(LocomotionState, 0, 0f);
                animator.Update(0f);
            }
            wasDown = down;

            bool winding = ai != null && ai.IsWindingUp;
            if (winding && !wasWinding && !down)
            {
                WeaponType? held = enemy.Weapon != null ? enemy.Weapon.Definition.Type : (WeaponType?)null;
                animator.SetTrigger(EnemyVisualLogic.AttackTrigger(held));
            }
            wasWinding = winding;
        }

        /// <summary>El arma va en la mano del modelo: el punto Hand sigue la posición de la mano derecha (no su giro).</summary>
        private void LateUpdate()
        {
            Transform hand = enemy != null ? enemy.Hand : null;
            if (hand == null || handBone == null) return;
            if (!handRestCaptured)
            {
                handRest = hand.localPosition;
                handRestCaptured = true;
            }
            bool follow = enemy.Posture == Posture.Standing;
            hand.position = follow ? handBone.position + transform.forward * 0.08f : hand.parent.TransformPoint(handRest);
        }
    }
}
