using System;
using UnityEngine;

namespace Overdrive.Enemies
{
    public enum AIState
    {
        /// <summary>Quieto en su puesto (o volviendo a él).</summary>
        Idle,
        Patrol,
        /// <summary>Va a mirar un ruido o el último sitio donde vio al jugador.</summary>
        Investigate,
        Chase,
        Attack,
        /// <summary>Desarmado: va a por un arma del suelo.</summary>
        Retrieve,
    }

    public enum WeaponKind
    {
        None,
        Melee,
        Ranged,
    }

    [Serializable]
    public struct BrainSettings
    {
        [Tooltip("Tiempo sin ver al jugador antes de pasar a buscarlo.")]
        public float loseSightTime;
        [Tooltip("Tiempo que se queda buscando en el último punto conocido.")]
        public float searchWaitTime;
        [Tooltip("Retraso entre ver al jugador y poder atacar.")]
        public float reactionTime;
        public float arriveDistance;
        public float patrolWaitTime;

        public static BrainSettings Default => new BrainSettings
        {
            loseSightTime = 2.5f,
            searchWaitTime = 2.5f,
            reactionTime = 0.35f,
            arriveDistance = 0.6f,
            patrolWaitTime = 1f,
        };
    }

    /// <summary>Lo que el enemigo percibe en un instante.</summary>
    public struct Perception
    {
        public Vector3 SelfPosition;
        public bool CanSeePlayer;
        public Vector3 PlayerPosition;
        public bool PlayerDead;
        public WeaponKind Weapon;
        /// <summary>Distancia a la que puede atacar con su arma actual.</summary>
        public float AttackRange;
        public bool HasNearbyWeapon;
        public Vector3 NearbyWeaponPosition;
    }

    /// <summary>
    /// Máquina de estados del enemigo: patrulla o quieto → detecta → persigue → ataca.
    /// Lógica pura: no mueve nada, solo decide adónde ir, hacia dónde mirar y si atacar.
    /// </summary>
    public sealed class EnemyBrain
    {
        private readonly BrainSettings settings;
        private readonly Vector3 home;
        private readonly Vector3[] patrol;
        private int patrolIndex;
        private float timer;
        private float sinceSeen = float.MaxValue;
        private float reactionLeft;

        public AIState State { get; private set; }
        public bool HasMoveTarget { get; private set; }
        public Vector3 MoveTarget { get; private set; }
        public bool HasLookTarget { get; private set; }
        public Vector3 LookTarget { get; private set; }
        public bool WantsAttack { get; private set; }
        public Vector3 LastKnownPlayerPosition { get; private set; }
        public int PatrolIndex => patrolIndex;

        public bool IsAlerted => State == AIState.Chase || State == AIState.Attack || State == AIState.Investigate || State == AIState.Retrieve;

        public EnemyBrain(Vector3 home, Vector3[] patrol, BrainSettings settings)
        {
            this.home = home;
            this.patrol = patrol ?? Array.Empty<Vector3>();
            this.settings = settings;
            State = this.patrol.Length >= 2 ? AIState.Patrol : AIState.Idle;
        }

        /// <summary>Un ruido cercano: si no está ya persiguiendo, va a investigarlo.</summary>
        public void HearNoise(Vector3 position)
        {
            if (State == AIState.Chase || State == AIState.Attack || State == AIState.Retrieve) return;
            LastKnownPlayerPosition = position;
            State = AIState.Investigate;
            timer = 0f;
        }

        /// <summary>Al levantarse tras un derribo necesita reaccionar de nuevo antes de atacar.</summary>
        public void OnRecovered()
        {
            reactionLeft = settings.reactionTime;
        }

        public void Tick(float deltaTime, Perception p)
        {
            WantsAttack = false;
            HasLookTarget = false;

            if (p.PlayerDead)
            {
                if (State == AIState.Chase || State == AIState.Attack) State = AIState.Idle;
            }
            else if (p.CanSeePlayer)
            {
                LastKnownPlayerPosition = p.PlayerPosition;
                sinceSeen = 0f;
                if (State != AIState.Chase && State != AIState.Attack && State != AIState.Retrieve)
                {
                    State = AIState.Chase;
                    reactionLeft = settings.reactionTime;
                }
            }
            else if (sinceSeen < float.MaxValue)
            {
                sinceSeen += deltaTime;
            }
            reactionLeft = Mathf.Max(0f, reactionLeft - deltaTime);

            // Desarmado y alerta: primero intenta conseguir un arma, salvo que tenga al jugador encima.
            bool playerInReach = p.CanSeePlayer && FlatDistance(p.SelfPosition, p.PlayerPosition) <= p.AttackRange;
            if (IsAlerted && p.Weapon == WeaponKind.None && p.HasNearbyWeapon && !playerInReach && !p.PlayerDead)
                State = AIState.Retrieve;
            else if (State == AIState.Retrieve)
                State = sinceSeen <= settings.loseSightTime ? AIState.Chase : AIState.Investigate;

            switch (State)
            {
                case AIState.Idle:
                    MoveTo(home, p.SelfPosition);
                    break;
                case AIState.Patrol:
                    TickPatrol(deltaTime, p.SelfPosition);
                    break;
                case AIState.Investigate:
                    TickInvestigate(deltaTime, p.SelfPosition);
                    break;
                case AIState.Retrieve:
                    SetMove(p.NearbyWeaponPosition);
                    break;
                case AIState.Chase:
                case AIState.Attack:
                    TickCombat(p);
                    break;
            }
        }

        private void TickCombat(Perception p)
        {
            if (!p.CanSeePlayer && sinceSeen > settings.loseSightTime)
            {
                State = AIState.Investigate;
                timer = 0f;
                SetMove(LastKnownPlayerPosition);
                return;
            }

            HasLookTarget = true;
            LookTarget = LastKnownPlayerPosition;
            float distance = FlatDistance(p.SelfPosition, LastKnownPlayerPosition);
            if (p.CanSeePlayer && distance <= p.AttackRange)
            {
                State = AIState.Attack;
                HasMoveTarget = false;
                WantsAttack = reactionLeft <= 0f;
            }
            else
            {
                State = AIState.Chase;
                SetMove(LastKnownPlayerPosition);
            }
        }

        private void TickPatrol(float deltaTime, Vector3 self)
        {
            Vector3 point = patrol[patrolIndex];
            if (MoveTo(point, self)) return;
            timer += deltaTime;
            if (timer < settings.patrolWaitTime) return;
            timer = 0f;
            patrolIndex = (patrolIndex + 1) % patrol.Length;
        }

        private void TickInvestigate(float deltaTime, Vector3 self)
        {
            if (MoveTo(LastKnownPlayerPosition, self)) return;
            timer += deltaTime;
            if (timer < settings.searchWaitTime) return;
            timer = 0f;
            patrolIndex = 0;
            sinceSeen = float.MaxValue;
            State = patrol.Length >= 2 ? AIState.Patrol : AIState.Idle;
        }

        /// <summary>Se mueve hacia el punto; devuelve true mientras no haya llegado.</summary>
        private bool MoveTo(Vector3 point, Vector3 self)
        {
            if (FlatDistance(self, point) <= settings.arriveDistance)
            {
                HasMoveTarget = false;
                return false;
            }
            SetMove(point);
            return true;
        }

        private void SetMove(Vector3 point)
        {
            HasMoveTarget = true;
            MoveTarget = point;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
