using System;
using UnityEngine;

namespace Overdrive.Core
{
    /// <summary>
    /// Busca la superficie del suelo bajo un punto (suelo, losas y rampas son BoxCollider;
    /// los personajes usan cápsulas y se ignoran). Sin suelo debajo, usa la altura del piso.
    /// </summary>
    public static class GroundProbe
    {
        private const float StartAbove = 0.5f;
        private const float MaxDepth = 10f;
        private static readonly Comparison<RaycastHit> ByDistance = (a, b) => a.distance.CompareTo(b.distance);

        public static float GroundY(Vector3 point)
        {
            Vector3 origin = point + Vector3.up * StartAbove;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, MaxDepth, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, ByDistance);
            foreach (RaycastHit hit in hits)
                if (hit.collider is BoxCollider) return hit.point.y;
            return Floors.BaseY(Floors.IndexOf(point.y));
        }
    }
}
