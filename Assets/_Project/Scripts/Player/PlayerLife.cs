using System;
using Overdrive.CameraRig;
using Overdrive.Combat;
using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Player
{
    /// <summary>Vida del jugador: muere de un solo golpe o disparo.</summary>
    [DisallowMultipleComponent]
    public class PlayerLife : MonoBehaviour
    {
        [SerializeField] private Transform body;

        private readonly Health health = new Health();

        public bool IsDead => health.IsDead;
        public Transform Body => body;

        public event Action Died;

        public bool Kill()
        {
            if (!health.Kill()) return false;
            ApplyPose();
            BloodSpawner.SpawnAt(transform.position, 1.2f);
            CameraShake.Shake(0.5f);
            Died?.Invoke();
            return true;
        }

        /// <summary>Rebobinar: vuelve a estar vivo (o muerto) sin sangre ni eventos.</summary>
        public void Restore(bool dead)
        {
            health.Restore(dead);
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (body == null) return;
            bool lying = health.IsDead;
            body.localRotation = lying ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            body.localPosition = new Vector3(0f, lying ? 0.4f : 0.9f, 0f);
        }
    }
}
