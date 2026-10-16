using Overdrive.CameraRig;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Player;
using Overdrive.Weapons;
using UnityEngine;
using UnityEngine.AI;

namespace Overdrive.Enemies
{
    /// <summary>
    /// IA del enemigo: percibe (cono de visión, paredes, ruido), decide con <see cref="EnemyBrain"/>,
    /// se mueve con NavMeshAgent y ataca. Con armas cuerpo a cuerpo persigue; con armas de fuego dispara a distancia.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAI : MonoBehaviour
    {
        [SerializeField] private Enemy enemy;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private PlayerLife player;
        [SerializeField] private Vector3[] patrolPoints = new Vector3[0];

        [Header("Percepción")]
        [SerializeField] private float viewRange = 14f;
        [SerializeField] private float viewHalfAngle = 55f;
        [SerializeField] private float proximity = 1.5f;
        [SerializeField] private float eyeHeight = 1.6f;

        [Header("Movimiento")]
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float chaseSpeed = 5f;
        [SerializeField] private float turnSpeed = 540f;
        [SerializeField] private float repathInterval = 0.2f;

        [Header("Combate")]
        [SerializeField] private float meleeWindup = 0.3f;
        [SerializeField] private float aimTolerance = 15f;
        [SerializeField] private float retrieveRadius = 10f;
        [SerializeField] private float pickupDistance = 1f;
        [SerializeField] private BrainSettings brainSettings = BrainSettings.Default;

        private EnemyBrain brain;
        private WeaponItem nearbyWeapon;
        private PlayerLife huntTarget;
        private float huntTimer;
        private float attackCooldown;
        private float windupLeft;
        private bool winding;
        private float repathTimer;
        private bool wasDown;

        public Enemy Enemy => enemy;
        public NavMeshAgent Agent => agent;
        public PlayerLife Player => player;
        public Vector3[] PatrolPoints => patrolPoints;
        public EnemyBrain Brain => brain ??= new EnemyBrain(transform.position, patrolPoints, brainSettings);
        public bool IsWindingUp => winding;
        public Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;
        private Vector3 ShotOrigin => transform.position + Vector3.up;

        public WeaponKind CurrentWeaponKind
        {
            get
            {
                if (enemy.Weapon == null) return WeaponKind.None;
                return enemy.Weapon.Definition.IsMelee ? WeaponKind.Melee : WeaponKind.Ranged;
            }
        }

        public float AttackRange => enemy.Weapon != null ? enemy.Weapon.Definition.EnemyAttackRange : WeaponDefinition.UnarmedRange;

        private void Awake()
        {
            // No se guarda en el prefab: el giro lo controla ApplyRotation.
            if (agent != null) agent.updateRotation = false;
        }

        private void OnEnable() => NoiseSystem.NoiseMade += OnNoise;
        private void OnDisable() => NoiseSystem.NoiseMade -= OnNoise;

        private void OnNoise(Vector3 position, float radius) => HearNoise(position, radius);

        /// <summary>Recibe un ruido; si está dentro del radio y de pie, va a investigarlo.</summary>
        public bool HearNoise(Vector3 position, float radius)
        {
            if (enemy.Posture != Posture.Standing) return false;
            if (!NoiseSystem.CanHear(transform.position, position, radius)) return false;
            Brain.HearNoise(position);
            return true;
        }

        public PlayerLife HuntTarget => huntTarget;

        /// <summary>
        /// Modo caza (refuerzos): sabe aproximadamente dónde está el jugador y va a por él
        /// aunque no lo vea. Al verlo, persigue y ataca como cualquier enemigo.
        /// </summary>
        public void Hunt(PlayerLife target)
        {
            huntTarget = target;
            huntTimer = 0f;
            UpdateHunt(0f);
        }

        /// <summary>Cada segundo actualiza el destino hacia la posición actual del jugador.</summary>
        public void UpdateHunt(float deltaTime)
        {
            if (huntTarget == null || huntTarget.IsDead) return;
            huntTimer -= deltaTime;
            if (huntTimer > 0f) return;
            huntTimer = HuntRefresh;
            Brain.HearNoise(huntTarget.transform.position);
        }

        private const float HuntRefresh = 1f;

        /// <summary>Cono de visión + línea de vista: no ve a través de paredes.</summary>
        public bool CanSee(PlayerLife target)
        {
            if (target == null || target.IsDead) return false;
            Vector3 targetPosition = target.transform.position;
            // Solo ve a quien está en su mismo piso (ni a través de huecos ni de la escalera).
            if (!Floors.SameFloor(transform.position, targetPosition)) return false;
            if (!VisionLogic.InCone(transform.position, transform.forward, targetPosition, viewRange, viewHalfAngle, proximity))
                return false;
            return LineOfSight.IsClear(EyePosition, targetPosition + Vector3.up, target.transform);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            attackCooldown = Mathf.Max(0f, attackCooldown - dt);

            if (enemy.Posture != Posture.Standing)
            {
                SetAgentActive(false);
                winding = false;
                wasDown = true;
                UpdateHandVisual();
                return;
            }
            if (wasDown)
            {
                wasDown = false;
                Brain.OnRecovered();
            }
            SetAgentActive(true);
            UpdateHunt(dt);

            Perception perception = BuildPerception();
            Brain.Tick(dt, perception);
            ApplyMovement(dt);
            ApplyRotation(dt);
            TryPickupWeapon();
            TickAttack(dt);
            UpdateHandVisual();
        }

        private Perception BuildPerception()
        {
            var p = new Perception
            {
                SelfPosition = transform.position,
                CanSeePlayer = CanSee(player),
                PlayerPosition = player != null ? player.transform.position : Vector3.zero,
                PlayerDead = player == null || player.IsDead,
                Weapon = CurrentWeaponKind,
                AttackRange = AttackRange,
            };

            nearbyWeapon = enemy.Weapon == null ? FindNearbyWeapon() : null;
            p.HasNearbyWeapon = nearbyWeapon != null;
            if (nearbyWeapon != null) p.NearbyWeaponPosition = nearbyWeapon.transform.position;
            return p;
        }

        private WeaponItem FindNearbyWeapon()
        {
            WeaponItem best = null;
            float bestDistance = retrieveRadius;
            foreach (WeaponItem item in WeaponItem.All)
            {
                if (!item.IsOnGround || !Floors.SameFloor(transform.position, item.transform.position)) continue;
                float distance = EnemyBrain.FlatDistance(transform.position, item.transform.position);
                if (distance > bestDistance) continue;
                best = item;
                bestDistance = distance;
            }
            return best;
        }

        private void ApplyMovement(float dt)
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            agent.speed = Brain.State == AIState.Idle || Brain.State == AIState.Patrol ? walkSpeed : chaseSpeed;

            if (Brain.HasMoveTarget && !winding)
            {
                repathTimer -= dt;
                if (repathTimer > 0f && agent.hasPath) return;
                repathTimer = repathInterval;
                agent.isStopped = false;
                agent.SetDestination(Brain.MoveTarget);
            }
            else if (agent.hasPath)
            {
                agent.ResetPath();
            }
        }

        private void ApplyRotation(float dt)
        {
            Vector3 direction = Brain.HasLookTarget
                ? Brain.LookTarget - transform.position
                : (agent.enabled ? agent.desiredVelocity : Vector3.zero);
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
        }

        private void TryPickupWeapon()
        {
            if (Brain.State != AIState.Retrieve || nearbyWeapon == null) return;
            if (EnemyBrain.FlatDistance(transform.position, nearbyWeapon.transform.position) > pickupDistance) return;
            enemy.Equip(nearbyWeapon);
            nearbyWeapon = null;
        }

        private void TickAttack(float dt)
        {
            if (winding)
            {
                windupLeft -= dt;
                if (windupLeft > 0f) return;
                winding = false;
                PerformMeleeHit();
                return;
            }

            if (!Brain.WantsAttack || attackCooldown > 0f || player == null) return;
            if (!IsFacing(player.transform.position, aimTolerance)) return;

            if (CurrentWeaponKind == WeaponKind.Ranged)
            {
                if (FireAt(player) > 0) attackCooldown = enemy.Weapon.Definition.EnemyAttackInterval;
            }
            else
            {
                winding = true;
                windupLeft = meleeWindup;
                float interval = enemy.Weapon != null ? enemy.Weapon.Definition.EnemyAttackInterval : WeaponDefinition.UnarmedEnemyInterval;
                attackCooldown = meleeWindup + interval;
            }
        }

        public bool IsFacing(Vector3 point, float tolerance)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            return to.sqrMagnitude < 0.0001f || Vector3.Angle(transform.forward, to) <= tolerance;
        }

        /// <summary>Golpe cuerpo a cuerpo (arma o puños): mata al jugador si está al alcance y delante.</summary>
        public bool PerformMeleeHit()
        {
            if (player == null || player.IsDead) return false;
            WeaponDefinition def = enemy.Weapon != null ? enemy.Weapon.Definition : null;
            float range = def != null && def.IsMelee ? def.MeleeRange : WeaponDefinition.UnarmedRange;
            float halfAngle = def != null && def.IsMelee ? def.MeleeHalfAngle : WeaponDefinition.UnarmedHalfAngle;

            Vector3 target = player.transform.position;
            if (!MeleeTargeting.InArc(transform.position, transform.forward, target, range, halfAngle)) return false;
            if (!LineOfSight.IsClear(ShotOrigin, target + Vector3.up, player.transform)) return false;
            CameraShake.Shake(0.3f);
            return player.Kill();
        }

        /// <summary>Dispara al objetivo con su arma de fuego, sin gastar munición. Devuelve los proyectiles disparados.</summary>
        public int FireAt(PlayerLife target, System.Collections.Generic.List<Projectile> spawned = null)
        {
            if (target == null || CurrentWeaponKind != WeaponKind.Ranged) return 0;
            Vector3 direction = target.transform.position + Vector3.up - ShotOrigin;
            return enemy.Weapon.TryFire(ShotOrigin, direction, Faction.Enemy, spawned, consumeAmmo: false);
        }

        private void SetAgentActive(bool active)
        {
            if (agent == null || agent.enabled == active) return;
            agent.enabled = active;
        }

        private void UpdateHandVisual()
        {
            if (enemy.Hand == null) return;
            enemy.Hand.localRotation = winding ? Quaternion.Euler(0f, -70f, 0f) : Quaternion.identity;
        }
    }
}
