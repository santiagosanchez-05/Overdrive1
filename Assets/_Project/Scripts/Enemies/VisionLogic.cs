using UnityEngine;

namespace Overdrive.Enemies
{
    /// <summary>Cono de visión. La línea de vista (paredes) se comprueba aparte con física.</summary>
    public static class VisionLogic
    {
        /// <summary>
        /// ¿Está el objetivo dentro del cono? A muy poca distancia (<paramref name="proximity"/>)
        /// se le nota aunque esté a la espalda.
        /// </summary>
        public static bool InCone(Vector3 eye, Vector3 forward, Vector3 target, float range, float halfAngle, float proximity)
        {
            Vector3 toTarget = target - eye;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance > range) return false;
            if (distance <= proximity) return true;
            forward.y = 0f;
            return Vector3.Angle(forward, toTarget) <= halfAngle;
        }
    }
}
