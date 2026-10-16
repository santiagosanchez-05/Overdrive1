using UnityEngine;

namespace Overdrive.Combat
{
    /// <summary>Ataque por la espalda: un puñetazo a un enemigo que no te ha detectado lo mata en silencio.</summary>
    public static class StealthLogic
    {
        /// <summary>Ángulo mínimo entre hacia dónde mira el enemigo y el atacante para contar como "por la espalda".</summary>
        public const float BackAngle = 100f;

        public static bool IsBackstab(Vector3 victimPosition, Vector3 victimForward, Vector3 attackerPosition, bool victimDetectedAttacker)
        {
            if (victimDetectedAttacker) return false;
            Vector3 toAttacker = attackerPosition - victimPosition;
            toAttacker.y = 0f;
            victimForward.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f) return false;
            return Vector3.Angle(victimForward, toAttacker) >= BackAngle;
        }
    }
}
