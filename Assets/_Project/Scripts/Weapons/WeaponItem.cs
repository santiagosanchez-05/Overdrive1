using System.Collections.Generic;
using Overdrive.CameraRig;
using Overdrive.Combat;
using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Weapons
{
    /// <summary>
    /// Un arma concreta durante toda su vida: en el suelo, en la mano de alguien o volando tras lanzarla.
    /// Conserva su munición al soltarla y recogerla.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponItem : MonoBehaviour
    {
        public enum Mode
        {
            Ground,
            Held,
            Thrown,
        }

        private static readonly List<WeaponItem> all = new List<WeaponItem>();

        [SerializeField] private WeaponType type;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Mode mode = Mode.Ground;
        [SerializeField] private float throwSpeed = 22f;
        [SerializeField] private float throwMaxDistance = 12f;
        [SerializeField] private float throwRadius = 0.25f;
        [SerializeField] private float groundHeight = 0.08f;
        [SerializeField] private float spinSpeed = 900f;

        private readonly LinearFlight flight = new LinearFlight();
        private WeaponState state;
        private Faction thrownBy;

        /// <summary>Armas activas en la escena (en cualquier modo).</summary>
        public static IReadOnlyList<WeaponItem> All => all;

        public WeaponType Type => type;
        public WeaponDefinition Definition => WeaponDefinition.Get(type);
        public WeaponState State => state ??= new WeaponState(Definition);
        public Mode CurrentMode => mode;
        public bool IsOnGround => mode == Mode.Ground;
        public Projectile ProjectilePrefab => projectilePrefab;

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        private void Update()
        {
            if (mode == Mode.Held) State.Tick(Time.deltaTime);
            else if (mode == Mode.Thrown) StepThrow(Time.deltaTime);
        }

        public void AttachTo(Transform hand)
        {
            mode = Mode.Held;
            flight.Stop();
            transform.SetParent(hand, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void DropAt(Vector3 position, float yaw)
        {
            mode = Mode.Ground;
            flight.Stop();
            transform.SetParent(null, true);
            position.y = groundHeight;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        public void Throw(Vector3 origin, Vector3 direction, Faction thrower)
        {
            transform.SetParent(null, true);
            mode = Mode.Thrown;
            thrownBy = thrower;
            flight.Launch(origin, direction, throwSpeed, throwMaxDistance);
            transform.position = origin;
        }

        /// <summary>Avanza el vuelo del arma lanzada. Público para simularlo en el editor.</summary>
        public void StepThrow(float deltaTime)
        {
            if (mode != Mode.Thrown) return;
            float distance = flight.NextStep(deltaTime);
            if (ProjectileCast.TryCast(flight.Position, flight.Direction, distance, throwRadius, thrownBy, out ProjectileCast.Hit hit))
            {
                // Un arma lanzada derriba, igual que un puñetazo.
                if (hit.Enemy != null) hit.Enemy.ReceiveKnockdown();
                Vector3 stop = flight.Position + flight.Direction * Mathf.Max(0f, hit.Distance - throwRadius);
                DropAt(stop, transform.eulerAngles.y);
                return;
            }
            flight.Advance(distance);
            transform.position = flight.Position;
            transform.Rotate(0f, spinSpeed * deltaTime, 0f, Space.World);
            if (!flight.Active) DropAt(flight.Position, transform.eulerAngles.y);
        }

        /// <summary>
        /// Dispara si es arma de fuego, tiene munición y no está en enfriamiento.
        /// Devuelve cuántos proyectiles salieron (0 si no disparó).
        /// </summary>
        public int TryFire(Vector3 origin, Vector3 direction, Faction owner, List<Projectile> spawned = null,
            bool consumeAmmo = true)
        {
            WeaponDefinition def = Definition;
            if (def.IsMelee || projectilePrefab == null) return 0;
            if (!State.TryUse(consumeAmmo)) return 0;

            float[] angles = ShotPattern.PelletAngles(def.Pellets, def.SpreadAngle, Random.Range(-1f, 1f));
            foreach (float angle in angles)
            {
                Projectile projectile = Instantiate(projectilePrefab);
                projectile.Launch(origin, ShotPattern.Rotate(direction, angle), def.ProjectileSpeed, owner);
                spawned?.Add(projectile);
            }
            NoiseSystem.Emit(origin, def.NoiseRadius);
            CameraShake.Shake(def.Shake);
            return angles.Length;
        }
    }
}
