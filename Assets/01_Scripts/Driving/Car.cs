using System;
using Overdrive.CameraRig;
using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Driving
{
    /// <summary>
    /// Coche arcade (el del jugador y los de los dealers): acelera, frena, gira según la velocidad y derrapa con el
    /// freno de mano. Tiene vida: al chocar con otro coche, recibe daño el que es embestido (el que menos "entra" en el
    /// choque). Sin vida explota, vuelca y queda como chatarra. Lo conducen <see cref="PlayerCarInput"/> o <see cref="EnemyCarAI"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Car : MonoBehaviour
    {
        [Header("Conducción")]
        [SerializeField] private float acceleration = 22f;
        [SerializeField] private float brakeForce = 38f;
        [SerializeField] private float maxSpeed = 34f;
        [SerializeField] private float reverseSpeed = 10f;
        [SerializeField] private float maxTurnRate = 120f;
        [SerializeField] private float fullTurnSpeed = 9f;
        [SerializeField] private float grip = 7f;
        [SerializeField] private float rollingDrag = 0.35f;

        [Header("Choques")]
        [SerializeField] private float maxHealth = 100f;
        [Tooltip("Daño que hace al embestir, por cada m/s de impacto por encima del umbral.")]
        [SerializeField] private float ramDamagePerMps = 6f;
        [SerializeField] private bool isPlayer;

        [Header("Explosión")]
        [SerializeField] private Material explosionMaterial;
        [SerializeField] private Renderer[] bodyRenderers = new Renderer[0];
        [SerializeField] private Material wreckMaterial;

        private Rigidbody body;
        private RailCar rail;
        private Vector3 velocity;
        private float health;
        private float hurtCooldown;

        public float Throttle { get; set; }
        public float Steer { get; set; }
        public bool Handbrake { get; set; }
        /// <summary>Tope de velocidad extra (los perseguidores aceleran para alcanzar al jugador).</summary>
        public float SpeedBonus { get; set; }

        public Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();
        public float Health => health;
        public float MaxHealth => maxHealth;
        public float Health01 => maxHealth > 0f ? health / maxHealth : 0f;
        public bool IsPlayer => isPlayer;
        public bool Destroyed { get; private set; }
        public float RamDamagePerMps => ramDamagePerMps;
        public float MaxSpeed => maxSpeed;
        /// <summary>Velocidad del paso de física anterior (la de antes del choque).</summary>
        public Vector3 LastVelocity { get; private set; }
        /// <summary>Velocidad del coche: la que marca su movimiento sobre la carretera (o la de la física si no va sobre ella).</summary>
        public Vector3 Velocity
        {
            get => Rail != null ? velocity : Body.linearVelocity;
            set => velocity = value;
        }
        public float ForwardSpeed => Rail != null ? Rail.Speed : Vector3.Dot(Body.linearVelocity, transform.forward);
        public RailCar Rail => rail != null ? rail : rail = GetComponent<RailCar>();

        public event Action<Car, float> Damaged;
        public event Action<Car> Exploded;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            health = maxHealth;
        }

        /// <summary>Deja el coche como nuevo (al construir, en pruebas o al reiniciar).</summary>
        public void ResetCar()
        {
            health = maxHealth;
            Destroyed = false;
            hurtCooldown = 0f;
            if (Rail == null) Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
        }

        private void FixedUpdate()
        {
            if (Rail == null) Drive(Time.fixedDeltaTime); // sobre la carretera lo mueve RailCar
        }

        /// <summary>Un paso de conducción. Público para simularlo en el editor.</summary>
        public void Drive(float dt)
        {
            Rigidbody rb = Body;
            hurtCooldown = Mathf.Max(0f, hurtCooldown - dt);
            LastVelocity = rb.linearVelocity;
            if (Destroyed) return;

            Vector3 forward = transform.forward;
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            float speed = Vector3.Dot(v, forward);

            // Acelerar, frenar (acelerar al revés frena primero) y marcha atrás.
            float throttle = Mathf.Clamp(Throttle, -1f, 1f);
            float top = maxSpeed + SpeedBonus;
            if (throttle > 0f && speed < top) v += forward * (acceleration * throttle * dt);
            else if (throttle < 0f)
            {
                if (speed > 0.5f) v -= forward * Mathf.Min(brakeForce * -throttle * dt, speed);
                else if (speed > -reverseSpeed) v += forward * (acceleration * 0.6f * throttle * dt);
            }
            else if (Mathf.Abs(throttle) < 0.01f) v *= Mathf.Exp(-rollingDrag * dt);
            if (Handbrake) v *= Mathf.Exp(-1.2f * dt);

            // Agarre: se pierde la mayor parte de la velocidad lateral (con freno de mano se derrapa).
            Vector3 lateral = Vector3.Project(v, transform.right);
            v = v - lateral + lateral * DrivingLogic.LateralKeep(grip, Handbrake, dt);
            rb.linearVelocity = new Vector3(v.x, 0f, v.z);

            // Girar según la velocidad (parado no gira); con freno de mano gira más.
            float turn = DrivingLogic.TurnRate(speed, maxTurnRate * (Handbrake ? 1.35f : 1f), fullTurnSpeed) * Mathf.Clamp(Steer, -1f, 1f);
            rb.angularVelocity = new Vector3(0f, turn * Mathf.Deg2Rad, 0f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            var other = collision.rigidbody != null ? collision.rigidbody.GetComponent<Car>() : null;
            if (other == null) return;
            HitBy(other);
        }

        /// <summary>Daño por el choque con otro coche (lo calcula cada coche para sí mismo). Público para simularlo.</summary>
        public float HitBy(Car other)
        {
            if (Destroyed || hurtCooldown > 0f || other == null || other.isPlayer == isPlayer) return 0f;
            Vector3 toOther = other.transform.position - transform.position;
            float mine = DrivingLogic.Approach(LastVelocity, toOther);
            float theirs = DrivingLogic.Approach(other.LastVelocity, -toOther);
            float closing = DrivingLogic.Closing(LastVelocity, other.LastVelocity, toOther);
            float damage = DrivingLogic.RamDamage(closing, theirs, mine, other.ramDamagePerMps);
            if (damage <= 0f) return 0f;
            hurtCooldown = 0.35f;
            TakeDamage(damage);
            GameSounds.Play(Sound.ThrownHit);
            CameraShake.Shake(Mathf.Clamp(damage / 60f, 0.15f, 0.6f));
            return damage;
        }

        public void TakeDamage(float damage)
        {
            if (Destroyed || damage <= 0f) return;
            health = Mathf.Max(0f, health - damage);
            Damaged?.Invoke(this, damage);
            if (health <= 0f) Explode();
        }

        /// <summary>Explota: vuelca, se oscurece y deja de conducirse.</summary>
        public void Explode()
        {
            if (Destroyed) return;
            Destroyed = true;
            health = 0f;
            Rigidbody rb = Body;
            if (Rail == null && !rb.isKinematic)
            {
                rb.constraints = RigidbodyConstraints.None;
                if (Application.isPlaying)
                {
                    rb.AddForce(Vector3.up * 7f + rb.linearVelocity * 0.2f, ForceMode.VelocityChange);
                    rb.AddTorque(new Vector3(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(-6f, 6f)), ForceMode.VelocityChange);
                }
            }
            if (Application.isPlaying) SpawnBlast();
            if (wreckMaterial != null)
                foreach (Renderer r in bodyRenderers)
                    if (r != null) r.sharedMaterials = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Repeat(wreckMaterial, r.sharedMaterials.Length));
            GameSounds.Play(Sound.Shotgun);
            CameraShake.Shake(0.7f);
            Exploded?.Invoke(this);
        }

        private void SpawnBlast()
        {
            var blast = new GameObject("Blast");
            blast.transform.position = transform.position + Vector3.up;
            var light = blast.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.15f);
            light.range = 14f;
            light.intensity = 9f;
            if (explosionMaterial != null)
            {
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(ball.GetComponent<Collider>());
                ball.transform.SetParent(blast.transform, false);
                ball.GetComponent<Renderer>().sharedMaterial = explosionMaterial;
            }
            blast.AddComponent<BlastFade>();
            // Fuego que queda en la chatarra.
            var fire = new GameObject("Fire").AddComponent<Light>();
            fire.transform.SetParent(transform, false);
            fire.transform.localPosition = Vector3.up * 1.5f;
            fire.color = new Color(1f, 0.45f, 0.1f);
            fire.range = 6f;
            fire.intensity = 2.5f;
        }
    }

    /// <summary>Bola de fuego que crece y se apaga en medio segundo.</summary>
    public class BlastFade : MonoBehaviour
    {
        private float t;
        private Light flash;

        private void Awake() => flash = GetComponent<Light>();

        private void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.6f);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 7f, k);
            if (flash != null) flash.intensity = Mathf.Lerp(9f, 0f, k);
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
