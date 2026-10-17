using System.Collections.Generic;
using Overdrive.Enemies;
using Overdrive.Player;
using UnityEngine;

namespace Overdrive.Combat
{
    /// <summary>Caer por un hueco junto a (o encima de) un enemigo lo derriba.</summary>
    public class PlayerStomp : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private float minDrop = 1.5f;
        [SerializeField] private float radius = 1.4f;

        public PlayerController Controller => controller;
        public float MinDrop => minDrop;

        private void OnEnable()
        {
            if (controller != null) controller.Landed += OnLanded;
        }

        private void OnDisable()
        {
            if (controller != null) controller.Landed -= OnLanded;
        }

        private void OnLanded(float drop)
        {
            if (drop >= minDrop) Stomp(Enemy.All);
        }

        /// <summary>Derriba a los enemigos de pie junto al punto de caída. Devuelve cuántos derribó.</summary>
        public int Stomp(IReadOnlyList<Enemy> enemies)
        {
            int count = 0;
            Vector3 position = transform.position;
            foreach (Enemy enemy in enemies)
            {
                if (enemy == null || enemy.Posture != Posture.Standing) continue;
                Vector3 enemyPosition = enemy.transform.position;
                // Puede haber aterrizado sobre la cabeza del enemigo (hasta 2,5 m por encima de sus pies).
                float dy = position.y - enemyPosition.y;
                if (dy < -0.5f || dy > 2.5f) continue;
                if (MeleeTargeting.FlatDistance(position, enemyPosition) > radius) continue;
                if (enemy.ReceiveKnockdown()) count++;
            }
            return count;
        }
    }
}
