using System.Collections.Generic;
using UnityEngine;

namespace Overdrive.Weapons
{
    /// <summary>Elige el arma del suelo más cercana al alcance.</summary>
    public static class PickupLogic
    {
        public static int FindNearest(Vector3 origin, IReadOnlyList<Vector3> positions, float radius)
        {
            int best = -1;
            float bestSqr = radius * radius;
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 d = positions[i] - origin;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    best = i;
                    bestSqr = sqr;
                }
            }
            return best;
        }
    }
}
