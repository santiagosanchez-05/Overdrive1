using System;
using UnityEngine;

namespace Overdrive.Core
{
    /// <summary>
    /// Ruido del juego: los disparos lo emiten y los enemigos dentro del radio lo oyen.
    /// </summary>
    public static class NoiseSystem
    {
        /// <summary>Posición del ruido y radio en el que se oye.</summary>
        public static event Action<Vector3, float> NoiseMade;

        public static void Emit(Vector3 position, float radius)
        {
            if (radius > 0f) NoiseMade?.Invoke(position, radius);
        }

        public static bool CanHear(Vector3 listener, Vector3 source, float radius)
        {
            Vector3 d = listener - source;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }
    }
}
