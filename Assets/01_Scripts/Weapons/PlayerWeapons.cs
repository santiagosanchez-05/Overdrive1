using System.Collections.Generic;
using Overdrive.CameraRig;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Player;
using UnityEngine;

namespace Overdrive.Weapons
{
    /// <summary>
    /// Armas del jugador: E recoge o intercambia, clic derecho lanza, clic izquierdo ataca.
    /// Sin arma, el clic izquierdo es un puñetazo.
    /// </summary>
    public class PlayerWeapons : MonoBehaviour
    {
        [SerializeField] private Transform hand;
        [SerializeField] private PlayerLife life;
        [SerializeField] private PlayerFinisher finisher;
        [SerializeField] private PlayerMelee melee;
        [SerializeField] private float pickupRadius = 1.6f;
        [SerializeField] private float swingTime = 0.15f;

        private readonly List<MeleeTargeting.Target> targets = new List<MeleeTargeting.Target>();
        private readonly List<WeaponItem> grounded = new List<WeaponItem>();
        private readonly List<Vector3> positions = new List<Vector3>();
        private WeaponItem current;
        private float swingLeft;

        public WeaponItem Current => current;
        public Transform Hand => hand;
        public PlayerLife Life => life;
        public PlayerFinisher Finisher => finisher;
        public PlayerMelee Melee => melee;

        /// <summary>Los disparos y lanzamientos salen del centro del cuerpo, nunca desde dentro de una pared.</summary>
        public Vector3 ShotOrigin => transform.position + Vector3.up;

        private void Update()
        {
            UpdateSwingVisual(Time.deltaTime);
            if (life != null && life.IsDead) return;
            if (finisher != null && finisher.IsFinishing) return;

            if (GameInput.PickupPressed) TryPickup(WeaponItem.All);
            if (GameInput.ThrowPressed) Throw();

            if (current == null)
            {
                if (GameInput.AttackPressed && melee != null) melee.TryPunch(Enemy.All);
            }
            else
            {
                Attack(GameInput.AttackPressed, GameInput.AttackHeld, Enemy.All);
            }
        }

        /// <summary>Rebobinar: vuelve a llevar el arma guardada (el arma restaura su propia posición).</summary>
        public void RestoreCurrent(WeaponItem weapon)
        {
            current = weapon;
            swingLeft = 0f;
        }

        /// <summary>Recoge el arma del suelo más cercana; si ya lleva una, la deja en su lugar.</summary>
        public WeaponItem TryPickup(IReadOnlyList<WeaponItem> items)
        {
            grounded.Clear();
            positions.Clear();
            foreach (WeaponItem item in items)
            {
                if (item == null || !item.IsOnGround) continue;
                grounded.Add(item);
                positions.Add(item.transform.position);
            }

            int index = PickupLogic.FindNearest(transform.position, positions, pickupRadius);
            if (index < 0) return null;

            WeaponItem picked = grounded[index];
            if (current != null) current.DropAt(picked.transform.position, picked.transform.eulerAngles.y);
            Equip(picked);
            return picked;
        }

        public void Equip(WeaponItem weapon)
        {
            current = weapon;
            weapon.AttachTo(hand);
        }

        /// <summary>Lanza el arma actual hacia donde mira el jugador.</summary>
        public WeaponItem Throw()
        {
            if (current == null) return null;
            WeaponItem thrown = current;
            current = null;
            thrown.Throw(ShotOrigin, transform.forward, Faction.Player);
            return thrown;
        }

        /// <summary>Ataca con el arma actual. Devuelve true si golpeó o disparó.</summary>
        public bool Attack(bool pressed, bool held, IReadOnlyList<Enemy> enemies)
        {
            if (current == null) return false;
            WeaponDefinition def = current.Definition;

            if (def.IsMelee)
            {
                if (!pressed || !current.State.TryUse()) return false;
                swingLeft = swingTime;
                CameraShake.Shake(0.1f);
                GameSounds.Play(Sound.Swing);
                Enemy.CollectTargets(enemies, targets);
                int index = MeleeTargeting.FindPunchTarget(transform.position, transform.forward, targets,
                    def.MeleeRange, def.MeleeHalfAngle, includeDown: true);
                if (index >= 0 && enemies[index].ReceiveLethalHit(KillCause.Melee))
                {
                    CameraShake.Shake(def.Shake);
                    GameSounds.Play(def.HitSound);
                }
                return true;
            }

            bool trigger = def.Automatic ? held : pressed;
            if (!trigger) return false;
            return current.TryFire(ShotOrigin, transform.forward, Faction.Player) > 0;
        }

        private void UpdateSwingVisual(float deltaTime)
        {
            if (hand == null) return;
            swingLeft = Mathf.Max(0f, swingLeft - deltaTime);
            float t = swingTime > 0f ? 1f - swingLeft / swingTime : 1f;
            hand.localRotation = swingLeft > 0f ? Quaternion.Euler(0f, Mathf.Lerp(-70f, 70f, t), 0f) : Quaternion.identity;
        }
    }
}
