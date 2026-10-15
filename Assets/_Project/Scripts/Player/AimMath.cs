using UnityEngine;

namespace Overdrive.Player
{
    /// <summary>Cálculos de apuntado: rayo del mouse contra el plano del suelo y giro en horizontal.</summary>
    public static class AimMath
    {
        public static bool TryGetPointOnPlane(Ray ray, float planeY, out Vector3 point)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }
            point = default;
            return false;
        }

        /// <summary>Rotación que mira de <paramref name="from"/> a <paramref name="to"/> ignorando la altura.</summary>
        public static bool TryGetFlatLookRotation(Vector3 from, Vector3 to, out Quaternion rotation)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                rotation = Quaternion.identity;
                return false;
            }
            rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            return true;
        }
    }
}
