using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Driving
{
    /// <summary>
    /// Conducción del jugador: el coche avanza siempre solo por la autopista; A/D lo mueve de lado entre los carriles
    /// y W/S acelera o frena un poco (sin llegar a pararse).
    /// </summary>
    [RequireComponent(typeof(Car))]
    public class PlayerCarInput : MonoBehaviour
    {
        [SerializeField] private Car car;
        [SerializeField] private float cruiseSpeed = 28f;
        [SerializeField] private float minSpeed = 20f;
        [SerializeField] private float maxSpeed = 36f;
        [SerializeField] private float speedChange = 10f;

        /// <summary>Para las pruebas: si no es null, se usa en lugar del teclado (x = de lado, y = acelerar/frenar).</summary>
        public Vector2? Override { get; set; }
        public bool Locked { get; set; }
        public float CruiseSpeed => cruiseSpeed;
        public float MinSpeed => minSpeed;

        private void Start()
        {
            RailCar rail = car != null ? car.Rail : null;
            if (rail != null && rail.Speed <= 0f) rail.Speed = cruiseSpeed;
        }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>Aplica la entrada. Público para simularlo en el editor.</summary>
        public void Tick(float dt)
        {
            RailCar rail = car != null ? car.Rail : null;
            if (rail == null) return;
            if (Locked || car.Destroyed)
            {
                rail.LateralInput = 0f;
                rail.Speed = Mathf.MoveTowards(rail.Speed, 0f, 15f * dt);
                return;
            }
            Vector2 move = Override ?? GameInput.Move;
            rail.LateralInput = move.x;
            float wanted = move.y > 0f ? maxSpeed : move.y < 0f ? minSpeed : cruiseSpeed;
            rail.Speed = Mathf.MoveTowards(rail.Speed, wanted, speedChange * dt);
        }
    }
}
