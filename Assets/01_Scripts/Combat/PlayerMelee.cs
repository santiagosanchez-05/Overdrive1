using System;
using System.Collections.Generic;
using Overdrive.CameraRig;
using Overdrive.Core;
using Overdrive.Enemies;
using UnityEngine;

namespace Overdrive.Combat
{
    /// <summary>
    /// Puñetazo: derriba al enemigo de pie más cercano frente al jugador.
    /// La entrada la decide PlayerWeapons (solo se pega puñetazos sin arma).
    /// </summary>
    public class PlayerMelee : MonoBehaviour
    {
        [SerializeField] private Transform fist;
        [SerializeField] private float range = 1.6f;
        [SerializeField] private float halfAngle = 60f;
        [SerializeField] private float cooldown = 0.35f;
        [SerializeField] private float punchVisualTime = 0.12f;

        private readonly List<MeleeTargeting.Target> targets = new List<MeleeTargeting.Target>();
        private float cooldownLeft;
        private float punchVisualLeft;
        private Vector3 fistRestPosition;
        private bool fistRestCaptured;

        public Transform Fist => fist;

        /// <summary>Se lanzó un puñetazo (golpee o no a alguien).</summary>
        public event Action Punched;
        public bool CanPunch => cooldownLeft <= 0f;

        private void Update()
        {
            Tick(Time.deltaTime);
            UpdateFistVisual(Time.deltaTime);
        }

        public void ResetForRewind()
        {
            cooldownLeft = 0f;
            punchVisualLeft = 0f;
        }

        /// <summary>Avanza el enfriamiento. Público para simularlo en el editor.</summary>
        public void Tick(float deltaTime)
        {
            cooldownLeft = Mathf.Max(0f, cooldownLeft - deltaTime);
        }

        /// <summary>
        /// Lanza un puñetazo. Devuelve el enemigo golpeado o null si no golpeó a nadie.
        /// Normalmente lo derriba; por la espalda y sin que te haya detectado, lo mata en silencio.
        /// </summary>
        public Enemy TryPunch(IReadOnlyList<Enemy> candidates)
        {
            if (!CanPunch) return null;
            cooldownLeft = cooldown;
            Punched?.Invoke();
            punchVisualLeft = punchVisualTime;
            CameraShake.Shake(0.1f);
            GameSounds.Play(Sound.Swing);

            Enemy.CollectTargets(candidates, targets);
            int index = MeleeTargeting.FindPunchTarget(transform.position, transform.forward, targets, range, halfAngle);
            if (index < 0) return null;
            Enemy enemy = candidates[index];
            GameSounds.Play(Sound.Punch);

            var ai = enemy.GetComponent<EnemyAI>();
            if (ai != null && ai.IsUnawareOf(transform.position))
                return enemy.ReceiveLethalHit(KillCause.Backstab) ? enemy : null;
            return enemy.ReceiveKnockdown() ? enemy : null;
        }

        private void UpdateFistVisual(float deltaTime)
        {
            if (fist == null) return;
            if (!fistRestCaptured)
            {
                fistRestPosition = fist.localPosition;
                fistRestCaptured = true;
            }
            punchVisualLeft = Mathf.Max(0f, punchVisualLeft - deltaTime);
            float push = punchVisualLeft > 0f ? 0.6f : 0f;
            fist.localPosition = fistRestPosition + new Vector3(0f, 0f, push);
        }
    }
}
