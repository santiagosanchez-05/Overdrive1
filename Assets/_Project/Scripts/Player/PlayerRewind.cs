using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Weapons;
using UnityEngine;

namespace Overdrive.Player
{
    /// <summary>
    /// Representa al jugador al rebobinar: posición, vida y arma en la mano.
    /// Al restaurar, cancela lo que estuviera a medias (remate, caída, enfriamientos).
    /// </summary>
    [RequireComponent(typeof(PlayerLife))]
    public class PlayerRewind : MonoBehaviour, IRewindable
    {
        [SerializeField] private PlayerLife life;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerWeapons weapons;
        [SerializeField] private PlayerFinisher finisher;
        [SerializeField] private PlayerMelee melee;
        [SerializeField] private PlayerFootsteps footsteps;

        public PlayerLife Life => life;
        public PlayerController Controller => controller;
        public PlayerWeapons Weapons => weapons;
        public PlayerFinisher Finisher => finisher;
        public PlayerMelee Melee => melee;
        public PlayerFootsteps Footsteps => footsteps;

        private sealed class Snapshot
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public bool Dead;
            public WeaponItem Weapon;
        }

        public object Capture() => new Snapshot
        {
            Position = transform.position,
            Rotation = transform.rotation,
            Dead = life.IsDead,
            Weapon = weapons != null ? weapons.Current : null,
        };

        public void Restore(object snapshot)
        {
            var s = (Snapshot)snapshot;

            CharacterController cc = controller != null ? controller.Controller : null;
            if (cc != null) cc.enabled = false;
            transform.SetPositionAndRotation(s.Position, s.Rotation);
            if (cc != null) cc.enabled = true;

            life.Restore(s.Dead);
            if (controller != null) controller.ResetMotion();
            if (finisher != null) finisher.ResetForRewind();
            if (melee != null) melee.ResetForRewind();
            if (footsteps != null) footsteps.ResetTracking();
            if (weapons != null)
            {
                weapons.RestoreCurrent(s.Weapon);
                weapons.enabled = true;
            }
        }
    }
}
