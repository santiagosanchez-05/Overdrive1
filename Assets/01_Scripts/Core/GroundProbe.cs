using System;
using UnityEngine;

namespace Overdrive.Core
{
    /// <summary>
    /// Busca suelo, losas y rampas de caja o malla, ignorando cápsulas y triggers.
    /// Sin suelo debajo, GroundY conserva la altura lógica del piso como respaldo.
    /// </summary>
    public static class GroundProbe
    {
        private const float StartAbove = 0.5f;
        private const float MaxDepth = 10f;
        private static readonly Comparison<RaycastHit> ByDistance = (a, b) => a.distance.CompareTo(b.distance);

        public static float GroundY(Vector3 point)
            => TrySurface(point, out var surface) ? surface.point.y : Floors.BaseY(Floors.IndexOf(point.y));

        public static bool TrySurface(Vector3 point, out RaycastHit surface)
        {
            Vector3 origin = point + Vector3.up * StartAbove;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, MaxDepth, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, ByDistance);
            foreach (RaycastHit hit in hits)
                if ((hit.collider is BoxCollider || hit.collider is MeshCollider) && hit.normal.y > .5f)
                {
                    surface = hit;
                    return true;
                }
            surface = default;
            return false;
        }
    }
}
