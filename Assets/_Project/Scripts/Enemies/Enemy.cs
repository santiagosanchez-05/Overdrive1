using System;
using System.Collections.Generic;
using Overdrive.CameraRig;
using Overdrive.Combat;
using Overdrive.Weapons;
using UnityEngine;

namespace Overdrive.Enemies
{
    /// <summary>
    /// Cuerpo de un enemigo: recibe golpes, se cae, se levanta y muere.
    /// La IA (fase 4) se apoya en este componente.
    /// </summary>
    [DisallowMultipleComponent]
    public class Enemy : MonoBehaviour
    {
        private static readonly List<Enemy> all = new List<Enemy>();

        [SerializeField] private Transform body;
        [SerializeField] private Collider bodyCollider;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Material deadMaterial;
        [SerializeField] private float knockdownDuration = 3f;
        [SerializeField] private Transform hand;
        [SerializeField] private WeaponItem weapon;

        private readonly CombatState state = new CombatState();
        private Material aliveMaterial;

        /// <summary>Enemigos activos en la escena.</summary>
        public static IReadOnlyList<Enemy> All => all;

        /// <summary>Se dispara cuando muere cualquier enemigo.</summary>
        public static event Action<Enemy> AnyDied;

        public CombatState State => state;
        public Posture Posture => state.Posture;
        public bool IsDead => state.IsDead;
        public float KnockdownDuration => knockdownDuration;
        public Transform Body => body;
        public Collider BodyCollider => bodyCollider;
        public Renderer BodyRenderer => bodyRenderer;
        public Material DeadMaterial => deadMaterial;
        public Transform Hand => hand;
        public WeaponItem Weapon => weapon;

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        private void Update() => Tick(Time.deltaTime);

        /// <summary>Avanza el tiempo del derribo. Público para simularlo en el editor.</summary>
        public void Tick(float deltaTime)
        {
            if (state.Tick(deltaTime)) ApplyPosture();
        }

        /// <summary>Puñetazo o arma lanzada: derriba sin matar.</summary>
        public bool ReceiveKnockdown()
        {
            if (!state.KnockDown(knockdownDuration)) return false;
            DropWeapon();
            ApplyPosture();
            CameraShake.Shake(0.25f);
            return true;
        }

        /// <summary>Golpe con arma o disparo: mata.</summary>
        public bool ReceiveLethalHit()
        {
            if (!state.Kill()) return false;
            OnKilled();
            return true;
        }

        public bool BeginFinish() => state.BeginFinish();

        public void CancelFinish() => state.CancelFinish();

        public bool CompleteFinish()
        {
            if (!state.Finish()) return false;
            OnKilled();
            return true;
        }

        public void Equip(WeaponItem item)
        {
            weapon = item;
            if (item != null) item.AttachTo(hand);
        }

        /// <summary>Suelta el arma delante de sus pies. Devuelve el arma soltada o null.</summary>
        public WeaponItem DropWeapon()
        {
            if (weapon == null) return null;
            WeaponItem dropped = weapon;
            weapon = null;
            dropped.DropAt(transform.position + transform.forward * 0.7f, transform.eulerAngles.y + 90f);
            return dropped;
        }

        public MeleeTargeting.Target AsTarget() => new MeleeTargeting.Target(transform.position, state.Posture);

        public static void CollectTargets(IReadOnlyList<Enemy> enemies, List<MeleeTargeting.Target> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < enemies.Count; i++) buffer.Add(enemies[i].AsTarget());
        }

        private void OnKilled()
        {
            DropWeapon();
            ApplyPosture();
            BloodSpawner.SpawnAt(transform.position, 1.2f);
            CameraShake.Shake(0.35f);
            AnyDied?.Invoke(this);
        }

        private void ApplyPosture()
        {
            bool lying = state.Posture != Posture.Standing;
            if (body != null)
            {
                body.localRotation = lying ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
                body.localPosition = new Vector3(0f, lying ? 0.4f : 0.9f, 0f);
            }
            // En el suelo no bloquea: el jugador puede ponerse encima para rematarlo.
            if (bodyCollider != null) bodyCollider.enabled = !lying;
            if (bodyRenderer != null)
            {
                // Se recuerda el material de vivo para poder volver a él al rebobinar.
                if (aliveMaterial == null) aliveMaterial = bodyRenderer.sharedMaterial;
                Material wanted = state.IsDead && deadMaterial != null ? deadMaterial : aliveMaterial;
                if (wanted != null && bodyRenderer.sharedMaterial != wanted) bodyRenderer.sharedMaterial = wanted;
            }
        }

        /// <summary>Rebobinar: vuelve a la postura y el arma guardadas, sin efectos (ni sangre ni soltar el arma).</summary>
        public void RestoreState(CombatState.Snapshot snapshot, WeaponItem heldWeapon)
        {
            state.Restore(snapshot);
            weapon = heldWeapon;
            ApplyPosture();
        }
    }
}
