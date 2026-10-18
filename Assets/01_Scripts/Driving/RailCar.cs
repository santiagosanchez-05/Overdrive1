using UnityEngine;

namespace Overdrive.Driving
{
    /// <summary>Movimiento sobre la carretera (lógica pura, comprobable en el editor).</summary>
    public static class RailLogic
    {
        /// <summary>Hasta dónde puede ir de lado un coche sin salirse de la calzada.</summary>
        public static float MaxLateral(float roadWidth, float carWidth) => Mathf.Max(0f, roadWidth * 0.5f - carWidth * 0.5f - 0.4f);

        /// <summary>¿Se tocan dos coches? (distancias a lo largo de la carretera y de lado, con sus medidas).</summary>
        public static bool Touching(float alongGap, float lateralGap, float lengthA, float lengthB, float widthA, float widthB) =>
            Mathf.Abs(alongGap) < (lengthA + lengthB) * 0.5f * 0.9f && Mathf.Abs(lateralGap) < (widthA + widthB) * 0.5f;

        /// <summary>
        /// Velocidad de un dealer: muy rápido mientras está lejos de su sitio junto al jugador; al llegar, la del jugador
        /// con una pequeña corrección.
        /// </summary>
        public static float ChaserSpeed(float playerSpeed, float alongError, float catchUp)
        {
            if (alongError > 6f) return playerSpeed + catchUp;
            if (alongError < -6f) return Mathf.Max(playerSpeed - 6f, 0f);
            return playerSpeed + alongError * 1.2f;
        }

        /// <summary>Giro visual (grados) del coche según lo rápido que se mueve de lado respecto a lo que avanza.</summary>
        public static float Yaw(float lateralRate, float speed) =>
            Mathf.Clamp(Mathf.Atan2(lateralRate, Mathf.Max(speed, 5f)) * Mathf.Rad2Deg, -18f, 18f);
    }

    /// <summary>
    /// Coche que va sobre la carretera: avanza solo a su velocidad por el eje y se mueve de lado sin salirse de la calzada.
    /// No usa física: los choques solo lo desplazan un poco de lado (<see cref="Nudge"/>), nunca lo giran ni lo frenan.
    /// Al ser destruido frena hasta pararse y vuelca.
    /// </summary>
    [RequireComponent(typeof(Car))]
    public class RailCar : MonoBehaviour
    {
        [SerializeField] private RoadPath road;
        [SerializeField] private float lateralSpeed = 9f;
        [SerializeField] private float width = 2.6f;
        [SerializeField] private float length = 6f;

        private Car car;
        private float nudgeVelocity;
        private float lastLateral;
        private float wreckTime;

        public RoadPath Road { get => road; set => road = value; }
        public float Along { get; set; }
        public float Lateral { get; set; }
        public float Speed { get; set; }
        /// <summary>Hacia dónde quiere ir de lado: -1..1 a su velocidad lateral (jugador o IA).</summary>
        public float LateralInput { get; set; }
        public float LateralSpeed => lateralSpeed;
        public float Width => width;
        public float Length => length;
        public float LateralRate { get; private set; }
        public Car Car => car != null ? car : car = GetComponent<Car>();

        public void SetSize(float carWidth, float carLength)
        {
            width = carWidth;
            length = carLength;
        }

        /// <summary>Coloca el coche en su sitio de la carretera según su posición actual.</summary>
        public void SnapToRoad()
        {
            if (road == null) return;
            Along = road.Project(transform.position, out float lateral);
            Lateral = lateral;
            lastLateral = lateral;
            Apply(0f);
        }

        /// <summary>Empujón de lado (choque): se mueve un poco y se recupera enseguida.</summary>
        public void Nudge(float side, float strength) => nudgeVelocity += Mathf.Sign(side) * strength;

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        /// <summary>Un paso de movimiento. Público para simularlo en el editor.</summary>
        public void Step(float dt)
        {
            if (road == null || dt <= 0f) return;
            if (Car.Destroyed)
            {
                // Chatarra: frena hasta pararse en la carretera mientras vuelca.
                wreckTime += dt;
                Speed = Mathf.MoveTowards(Speed, 0f, 18f * dt);
                Along += Speed * dt;
                float roll = Mathf.Min(wreckTime / 0.6f, 1f) * 160f;
                Apply(dt, Quaternion.LookRotation(road.TangentAt(Along), Vector3.up) * Quaternion.Euler(0f, wreckTime * 40f, roll));
                return;
            }

            Along = Mathf.Min(Along + Speed * dt, road.Length);
            float max = RailLogic.MaxLateral(road.Width, width);
            Lateral += (Mathf.Clamp(LateralInput, -1f, 1f) * lateralSpeed + nudgeVelocity) * dt;
            nudgeVelocity *= Mathf.Exp(-7f * dt);
            if (Mathf.Abs(Lateral) > max)
            {
                Lateral = Mathf.Clamp(Lateral, -max, max);
                nudgeVelocity = 0f;
            }
            Apply(dt);
        }

        private void Apply(float dt, Quaternion? overrideRotation = null)
        {
            LateralRate = dt > 0f ? (Lateral - lastLateral) / dt : 0f;
            lastLateral = Lateral;
            Vector3 tangent = road.TangentAt(Along);
            Vector3 pos = road.PointAt(Along) + road.RightAt(Along) * Lateral;
            pos.y = transform.position.y;
            Quaternion rot = overrideRotation ?? Quaternion.LookRotation(tangent, Vector3.up) * Quaternion.Euler(0f, RailLogic.Yaw(LateralRate, Speed), 0f);
            Car.Velocity = tangent * Speed + road.RightAt(Along) * LateralRate;
            Rigidbody rb = Car.Body;
            if (Application.isPlaying && rb != null && rb.isKinematic)
            {
                rb.MovePosition(pos);
                rb.MoveRotation(rot);
            }
            else
            {
                transform.SetPositionAndRotation(pos, rot);
                if (rb != null) { rb.position = pos; rb.rotation = rot; }
            }
        }
    }
}
