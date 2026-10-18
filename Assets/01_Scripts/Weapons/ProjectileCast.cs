using System;
using Overdrive.Combat;
using Overdrive.Enemies;
using Overdrive.Player;
using UnityEngine;

namespace Overdrive.Weapons
{
    /// <summary>Barrido físico de un proyectil: devuelve el primer impacto que no sea de su propio bando.</summary>
    public static class ProjectileCast
    {
        public struct Hit
        {
            public Enemy Enemy;
            public PlayerLife Player;
            public Vector3 Point;
            public float Distance;
            /// <summary>Le dio en la cabeza (la esfera <see cref="HeadHitbox"/>).</summary>
            public bool Head;

            /// <summary>Pared u otro obstáculo sin vida.</summary>
            public bool IsObstacle => Enemy == null && Player == null;
        }

        private static readonly Comparison<RaycastHit> ByDistance = (a, b) => a.distance.CompareTo(b.distance);

        /// <summary>Distancia tras el primer impacto en la que todavía cuenta atravesar la cabeza del mismo personaje.</summary>
        public const float HeadPriorityDistance = 0.45f;

        public static bool TryCast(Vector3 origin, Vector3 direction, float distance, float radius, Faction owner, out Hit hit)
        {
            hit = default;
            if (distance <= 0f) return false;

            // Con triggers: así entran las zonas de impacto de cabeza y cuerpo (HeadHitbox); el resto de triggers se ignoran.
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction, distance, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, ByDistance);
            foreach (RaycastHit h in hits)
            {
                if (h.collider.isTrigger && !HeadHitbox.IsHitbox(h.collider)) continue;
                var enemy = h.collider.GetComponentInParent<Enemy>();
                var player = h.collider.GetComponentInParent<PlayerLife>();
                if (enemy != null && owner == Faction.Enemy) continue;
                if (player != null && owner == Faction.Player) continue;

                hit.Enemy = enemy;
                hit.Player = player;
                hit.Distance = h.distance;
                hit.Head = HeadHitbox.IsHead(h.collider);
                // Si la bala sigue y atraviesa también la cabeza del mismo personaje justo después, es tiro a la cabeza
                // (al apuntar a la cabeza desde abajo la bala roza antes el cuello o los hombros).
                if (!hit.Head && (enemy != null || player != null))
                {
                    Transform who = enemy != null ? enemy.transform : player.transform;
                    foreach (RaycastHit other in hits)
                        if (other.distance <= h.distance + HeadPriorityDistance && HeadHitbox.IsHead(other.collider) &&
                            other.collider.transform.IsChildOf(who))
                        {
                            hit.Head = true;
                            break;
                        }
                }
                // Si el barrido empieza solapado, Unity devuelve punto cero: usamos el origen.
                hit.Point = h.distance > 0f ? h.point : origin;
                return true;
            }
            return false;
        }
    }
}
