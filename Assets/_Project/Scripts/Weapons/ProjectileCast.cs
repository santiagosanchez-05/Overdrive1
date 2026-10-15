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

            /// <summary>Pared u otro obstáculo sin vida.</summary>
            public bool IsObstacle => Enemy == null && Player == null;
        }

        private static readonly Comparison<RaycastHit> ByDistance = (a, b) => a.distance.CompareTo(b.distance);

        public static bool TryCast(Vector3 origin, Vector3 direction, float distance, float radius, Faction owner, out Hit hit)
        {
            hit = default;
            if (distance <= 0f) return false;

            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, ByDistance);
            foreach (RaycastHit h in hits)
            {
                var enemy = h.collider.GetComponentInParent<Enemy>();
                var player = h.collider.GetComponentInParent<PlayerLife>();
                if (enemy != null && owner == Faction.Enemy) continue;
                if (player != null && owner == Faction.Player) continue;

                hit.Enemy = enemy;
                hit.Player = player;
                hit.Distance = h.distance;
                // Si el barrido empieza solapado, Unity devuelve punto cero: usamos el origen.
                hit.Point = h.distance > 0f ? h.point : origin;
                return true;
            }
            return false;
        }
    }
}
