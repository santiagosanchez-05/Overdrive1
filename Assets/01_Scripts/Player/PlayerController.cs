using System;
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
        [Tooltip("Velocidad agachado (Ctrl), en silencio.")]
        [SerializeField] private float sneakSpeed = 3.2f;
        [SerializeField] private float gravity = 25f;
        [Tooltip("Velocidad hacia abajo en el suelo, para pegarse a la rampa al bajar.")]
        [SerializeField] private float groundStick = 6f;

        private float verticalSpeed;
        private bool airborne;
        private float airborneStartY;

        /// <summary>Bloquea el movimiento (por ejemplo, durante un remate).</summary>
        public bool MovementLocked { get; set; }

        public CharacterController Controller => controller;
        public PlayerLife Life => life;
        public float MoveSpeed => moveSpeed;
        public float SneakSpeed => sneakSpeed;

        /// <summary>Camina agachado (Ctrl): más lento y sin ruido de pasos.</summary>
        public bool IsSneaking { get; private set; }

        /// <summary>Aterrizaje tras una caída; el parámetro es la altura caída.</summary>
        public event Action<float> Landed;

        private void Update()
        {
            if (life != null && life.IsDead) return;

            IsSneaking = GameInput.Sneak;
            Vector3 velocity = MovementLocked
                ? Vector3.zero
                : PlayerMovementLogic.WorldVelocity(GameInput.Move, IsSneaking ? sneakSpeed : moveSpeed);

            verticalSpeed = controller.isGrounded ? -groundStick : verticalSpeed - gravity * Time.deltaTime;
            velocity.y = verticalSpeed;
            controller.Move(velocity * Time.deltaTime);
            TrackFall(controller.isGrounded, transform.position.y);
        }

        /// <summary>Rebobinar: sin velocidad acumulada, sin caída en curso y con el movimiento libre.</summary>
        public void ResetMotion()
        {
            verticalSpeed = 0f;
            airborne = false;
            MovementLocked = false;
        }

        /// <summary>
        /// Detecta caídas: al pasar de estar en el aire a tocar suelo, avisa con la altura caída.
        /// Público para simularlo en el editor.
        /// </summary>
        public void TrackFall(bool grounded, float y)
        {
            if (!grounded)
            {
                if (!airborne)
                {
                    airborne = true;
                    airborneStartY = y;
                }
                else
                {
                    airborneStartY = Mathf.Max(airborneStartY, y);
                }
                return;
            }
            if (!airborne) return;
            airborne = false;
            float drop = airborneStartY - y;
            if (drop > 0f) Landed?.Invoke(drop);
        }
    }
}
