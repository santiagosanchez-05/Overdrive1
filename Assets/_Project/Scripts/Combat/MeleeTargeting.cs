using System.Collections.Generic;
using UnityEngine;

namespace Overdrive.Combat
{
    /// <summary>Elige a quién golpea un ataque cuerpo a cuerpo o un remate.</summary>
    public static class MeleeTargeting
    {
        public readonly struct Target
        {
            public readonly Vector3 Position;
            public readonly Posture Posture;

            public Target(Vector3 position, Posture posture)
            {
                Position = position;
                Posture = posture;
            }
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>¿Está <paramref name="target"/> dentro del abanico frente al atacante?</summary>
        public static bool InArc(Vector3 origin, Vector3 forward, Vector3 target, float range, float halfAngle)
        {
            Vector3 toTarget = target - origin;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance > range) return false;
            if (distance < 0.01f) return true;
            forward.y = 0f;
            return Vector3.Angle(forward, toTarget) <= halfAngle;
        }

        /// <summary>
        /// Índice del enemigo más cercano dentro del abanico, o -1. Por defecto solo los que están de pie;
        /// las armas cuerpo a cuerpo también alcanzan a los derribados (<paramref name="includeDown"/>).
        /// </summary>
        public static int FindPunchTarget(Vector3 origin, Vector3 forward, IReadOnlyList<Target> candidates,
            float range, float halfAngle, bool includeDown = false)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                Target t = candidates[i];
                bool valid = t.Posture == Posture.Standing || (includeDown && t.Posture == Posture.Down);
                if (!valid) continue;
                if (!InArc(origin, forward, t.Position, range, halfAngle)) continue;
                float distance = FlatDistance(origin, t.Position);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>Índice del enemigo derribado más cercano a menos de <paramref name="radius"/>, o -1.</summary>
        public static int FindFinishTarget(Vector3 origin, IReadOnlyList<Target> candidates, float radius)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                Target t = candidates[i];
                if (t.Posture != Posture.Down) continue;
                float distance = FlatDistance(origin, t.Position);
                if (distance <= radius && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
