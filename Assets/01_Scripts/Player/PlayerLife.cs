using System;
using Overdrive.CameraRig;
using Overdrive.Combat;
using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Player
{
    /// <summary>
    /// Vida del jugador: 100 puntos. Las balas y golpes en el cuerpo quitan vida; un disparo o cuchillazo
    /// en la cabeza mata de una vez (el bate solo quita vida, también en la cabeza).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerLife : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private float maxHealth = 100f;

        private readonly Health health = new Health();
        private float hp = -1f;
        private HeadHitbox headHitbox;

        public bool IsDead => health.IsDead;
        public float MaxHealth => maxHealth;
        public float CurrentHealth => health.IsDead ? 0f : (hp < 0f ? maxHealth : hp);

        /// <summary>Recibió daño sin morir (cantidad, en la cabeza).</summary>
        public event Action<float, bool> Damaged;

        private void Start()
        {
            headHitbox = HeadHitbox.Ensure(transform);
            headHitbox.SetActive(!IsDead);
        }

        /// <summary>
        /// Impacto con daño. En la cabeza con un arma que mata a la cabeza, muere; si no, pierde vida y muere
        /// al llegar a 0. Devuelve true si este impacto lo mató.
        /// </summary>
        public bool TakeHit(float amount, bool head, bool headKills)
        {
            if (IsDead) return false;
            if (head && headKills) return Kill();
            hp = CurrentHealth - amount;
            if (hp <= 0f) return Kill();
            CameraShake.Shake(head ? 0.35f : 0.2f);
            GameSounds.Play(Sound.Punch);
            Damaged?.Invoke(amount, head);
            return false;
        }
        public Transform Body => body;

        public event Action Died;

        public bool Kill()
        {
            if (!health.Kill()) return false;
            hp = 0f;
            if (headHitbox != null) headHitbox.SetActive(false);
            ApplyPose();
            BloodSpawner.SpawnAt(transform.position, 1.2f);
            CameraShake.Shake(0.5f);
            GameSounds.Play(Sound.PlayerDeath);
            Died?.Invoke();
            return true;
        }

        /// <summary>Rebobinar: vuelve a estar vivo (o muerto) sin sangre ni eventos.</summary>
        public void Restore(bool dead)
        {
            health.Restore(dead);
            hp = dead ? 0f : maxHealth;
            if (headHitbox != null) headHitbox.SetActive(!dead);
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
