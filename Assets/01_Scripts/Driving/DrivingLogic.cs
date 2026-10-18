using UnityEngine;

namespace Overdrive.Driving
{
    /// <summary>Reglas de la persecución en coche (lógica pura, comprobable en el editor).</summary>
    public static class DrivingLogic
    {
        /// <summary>Por debajo de esta velocidad de choque (m/s, en la dirección del impacto) un golpe no hace daño.</summary>
        public const float HarmlessSpeed = 4f;

        /// <summary>
        /// Velocidad con la que cada coche "entra" en el choque: la de su velocidad que va hacia el otro.
        /// El que embiste es el que más entra; el embestido recibe el daño.
        /// </summary>
        public static float Approach(Vector3 velocity, Vector3 towardOther)
        {
            towardOther.y = 0f;
            velocity.y = 0f;
            return towardOther.sqrMagnitude > 0f ? Mathf.Max(0f, Vector3.Dot(velocity, towardOther.normalized)) : 0f;
        }

        /// <summary>Velocidad a la que se acercan los dos coches en el momento del choque (relativa, no absoluta).</summary>
        public static float Closing(Vector3 myVelocity, Vector3 otherVelocity, Vector3 towardOther)
        {
            towardOther.y = 0f;
            if (towardOther.sqrMagnitude <= 0f) return 0f;
            Vector3 relative = otherVelocity - myVelocity;
            relative.y = 0f;
            return Mathf.Max(0f, -Vector3.Dot(relative, towardOther.normalized));
        }

        /// <summary>
        /// Daño de un choque a quien lo recibe: crece con la velocidad relativa del impacto por encima del umbral,
        /// repartida según quién embiste (si el otro iba hacia mí y yo no hacia él, me lo llevo entero).
        /// </summary>
        public static float RamDamage(float closing, float attackerApproach, float victimApproach, float damagePerMps)
        {
            if (closing <= HarmlessSpeed) return 0f;
            float share = attackerApproach / Mathf.Max(attackerApproach + victimApproach, 0.01f);
            return (closing - HarmlessSpeed) * share * damagePerMps;
        }

        /// <summary>Giro (grados por segundo) según la velocidad: parado no gira, a media velocidad gira lo máximo.</summary>
        public static float TurnRate(float speed, float maxTurnRate, float fullTurnSpeed)
        {
            float t = Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(fullTurnSpeed, 0.01f));
            return maxTurnRate * t * Mathf.Sign(speed == 0f ? 1f : speed);
        }

        /// <summary>Cuánto se pierde de la velocidad lateral (derrape) en un paso: con freno de mano se agarra menos.</summary>
        public static float LateralKeep(float grip, bool handbrake, float dt) =>
            Mathf.Exp(-(handbrake ? grip * 0.25f : grip) * dt);

        /// <summary>Hacia dónde apunta un perseguidor: delante del objetivo según su velocidad (se anticipa).</summary>
        public static Vector3 InterceptPoint(Vector3 hunter, Vector3 target, Vector3 targetVelocity, float hunterSpeed)
        {
            float distance = Vector3.Distance(hunter, target);
            float time = Mathf.Clamp(distance / Mathf.Max(hunterSpeed, 5f), 0f, 1.5f);
            return target + targetVelocity * time;
        }

        // ---------- Dealers por carriles ----------

        /// <summary>Desplazamiento lateral del carril de un dealer: los carriles exteriores (izquierdo -1, derecho +1).</summary>
        public static float LaneOffset(int side, float roadWidth) => Mathf.Sign(side == 0 ? 1 : side) * roadWidth * 0.36f;

        /// <summary>
        /// ¿Se lanza a embestir? Solo si va a la par del jugador (poco adelantado o atrasado), no demasiado lejos de lado
        /// y ya pasó su tiempo de espera desde el último intento.
        /// </summary>
        public static bool ShouldAttack(float alongGap, float lateralGap, float cooldown) =>
            cooldown <= 0f && Mathf.Abs(alongGap) < 5f && Mathf.Abs(lateralGap) < 14f;

        /// <summary>Hacia dónde va de lado: a su carril, o hacia el jugador (un poco más allá, para golpearlo de verdad) si embiste.</summary>
        public static float TargetLateral(float laneOffset, bool attacking, float playerLateral) =>
            attacking ? playerLateral + Mathf.Sign(playerLateral - laneOffset) * 1.5f : laneOffset;

        /// <summary>Velocidad que busca: la del jugador, más o menos según si va atrasado o adelantado respecto a su sitio.</summary>
        public static float DesiredSpeed(float playerSpeed, float alongError, float maxSpeed) =>
            Mathf.Clamp(playerSpeed + alongError * 0.8f, 0f, maxSpeed);

        // ---------- Disparo ----------

        /// <summary>Punto del suelo bajo el ratón: corte del rayo de la cámara con el plano horizontal a la altura dada.</summary>
        public static bool AimOnPlane(Ray ray, float height, out Vector3 point)
        {
            point = default;
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;
            float t = (height - ray.origin.y) / ray.direction.y;
            if (t <= 0f) return false;
            point = ray.origin + ray.direction * t;
            return true;
        }

        /// <summary>Ángulo con signo (grados, sobre Y) de "forward" a "to", en el plano.</summary>
        public static float SignedAngle(Vector3 forward, Vector3 to)
        {
            forward.y = 0f;
            to.y = 0f;
            return Vector3.SignedAngle(forward, to, Vector3.up);
        }
    }
}
