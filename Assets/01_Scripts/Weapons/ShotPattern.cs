using UnityEngine;

namespace Overdrive.Weapons
{
    /// <summary>Direcciones de los perdigones o balas de un disparo.</summary>
    public static class ShotPattern
    {
        /// <summary>
        /// Ángulos (en grados, alrededor del eje Y) de cada perdigón.
        /// Varios perdigones: abanico uniforme de <paramref name="spread"/> grados.
        /// Uno solo: desviación aleatoria dentro del abanico.
        /// </summary>
        /// <param name="random">Valor entre -1 y 1 (0 = sin desviación).</param>
        public static float[] PelletAngles(int pellets, float spread, float random)
        {
            pellets = Mathf.Max(1, pellets);
            var angles = new float[pellets];
            if (pellets == 1)
            {
                angles[0] = random * spread * 0.5f;
                return angles;
            }
            float step = spread / (pellets - 1);
            float jitter = random * step * 0.25f;
            for (int i = 0; i < pellets; i++) angles[i] = -spread * 0.5f + i * step + jitter;
            return angles;
        }

        /// <summary>Gira la dirección alrededor del eje vertical; si apunta hacia arriba o abajo, mantiene esa inclinación.</summary>
        public static Vector3 Rotate(Vector3 forward, float angle)
        {
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            return Quaternion.AngleAxis(angle, Vector3.up) * forward.normalized;
        }
    }
}
