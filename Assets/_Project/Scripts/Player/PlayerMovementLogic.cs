using UnityEngine;

namespace Overdrive.Player
{
    /// <summary>Movimiento relativo al mundo (W = +Z, D = +X), independiente de la cámara.</summary>
    public static class PlayerMovementLogic
    {
        public static Vector3 WorldVelocity(Vector2 input, float speed)
        {
            var direction = new Vector3(input.x, 0f, input.y);
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            return direction * speed;
        }
    }
}
