using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Player
{
    /// <summary>Mueve al jugador con WASD en ejes del mundo usando un CharacterController.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private CharacterController controller;
        [SerializeField] private PlayerLife life;
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float gravity = 25f;

        private float verticalSpeed;

        /// <summary>Bloquea el movimiento (por ejemplo, durante un remate).</summary>
        public bool MovementLocked { get; set; }

        public CharacterController Controller => controller;
        public PlayerLife Life => life;
        public float MoveSpeed => moveSpeed;

        private void Update()
        {
            if (life != null && life.IsDead) return;

            Vector3 velocity = MovementLocked
                ? Vector3.zero
                : PlayerMovementLogic.WorldVelocity(GameInput.Move, moveSpeed);

            verticalSpeed = controller.isGrounded ? -1f : verticalSpeed - gravity * Time.deltaTime;
            velocity.y = verticalSpeed;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
