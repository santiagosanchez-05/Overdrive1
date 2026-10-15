using System;
using UnityEngine;

namespace Overdrive.Enemies
{
    /// <summary>Línea de vista con física: las paredes tapan, los demás enemigos no.</summary>
    public static class LineOfSight
    {
        private static readonly Comparison<RaycastHit> ByDistance = (a, b) => a.distance.CompareTo(b.distance);

        public static bool IsClear(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.001f) return true;

            RaycastHit[] hits = Physics.RaycastAll(from, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, ByDistance);
            foreach (RaycastHit hit in hits)
            {
                if (target != null && hit.collider.transform.IsChildOf(target)) return true;
                if (hit.collider.GetComponentInParent<Enemy>() != null) continue;
                return false;
            }
            return true;
        }
    }
}
