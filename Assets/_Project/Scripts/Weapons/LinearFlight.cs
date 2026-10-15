using UnityEngine;

namespace Overdrive.Weapons
{
    /// <summary>Vuelo en línea recta y horizontal con distancia máxima (balas y armas lanzadas).</summary>
    public sealed class LinearFlight
    {
        public Vector3 Position { get; private set; }
        public Vector3 Direction { get; private set; }
        public float Speed { get; private set; }
        public float MaxDistance { get; private set; }
        public float Travelled { get; private set; }
        public bool Active { get; private set; }

        public void Launch(Vector3 position, Vector3 direction, float speed, float maxDistance)
        {
            direction.y = 0f;
            Position = position;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            Speed = speed;
            MaxDistance = maxDistance;
            Travelled = 0f;
            Active = true;
        }

        /// <summary>Distancia a recorrer en este paso (sin aplicarla todavía).</summary>
        public float NextStep(float deltaTime)
        {
            return Active ? Mathf.Min(Speed * deltaTime, MaxDistance - Travelled) : 0f;
        }

        public void Advance(float distance)
        {
            Position += Direction * distance;
            Travelled += distance;
            if (Travelled >= MaxDistance - 0.0001f) Active = false;
        }

        public void Stop()
        {
            Active = false;
        }
    }
}
