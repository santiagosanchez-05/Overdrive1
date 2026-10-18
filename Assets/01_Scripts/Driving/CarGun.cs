using Overdrive.CameraRig;
using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Driving
{
    /// <summary>
    /// Pistola del coche: se apunta con el ratón (al suelo bajo el cursor) y se dispara con el clic izquierdo
    /// (mantenido, en ráfaga). Disparo instantáneo con trazadora: lo que toque primero (camioneta o pared) para la bala.
    /// </summary>
    public class CarGun : MonoBehaviour
    {
        [SerializeField] private Car car;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private float fireInterval = 0.13f;
        [SerializeField] private float damage = 12f;
        [SerializeField] private float range = 75f;
        [SerializeField] private float muzzleHeight = 1.8f;
        [SerializeField] private Material tracerMaterial;

        private float cooldown;

        /// <summary>Para las pruebas: punto al que apuntar y si se dispara, en lugar del ratón.</summary>
        public Vector3? AimOverride { get; set; }
        public bool? FireOverride { get; set; }
        public Vector3 AimPoint { get; private set; }
        public bool HasAim { get; private set; }
        public int ShotsFired { get; private set; }
        public float Damage => damage;
        public float Range => range;
        public Vector3 Muzzle => car != null ? car.transform.position + Vector3.up * muzzleHeight : transform.position;

        private void Update()
        {
            if (car == null || car.Destroyed) return;
            UpdateAim();
            cooldown -= Time.deltaTime;
            bool firing = FireOverride ?? GameInput.AttackHeld;
            if (firing && HasAim && cooldown <= 0f)
            {
                cooldown = fireInterval;
                Fire(AimPoint);
            }
        }

        private void UpdateAim()
        {
            if (AimOverride.HasValue)
            {
                AimPoint = AimOverride.Value;
                HasAim = true;
                return;
            }
            if (aimCamera == null) aimCamera = Camera.main;
            Vector3 point = default;
            HasAim = aimCamera != null &&
                     DrivingLogic.AimOnPlane(aimCamera.ScreenPointToRay(GameInput.MousePosition), car.transform.position.y + 1f, out point);
            if (HasAim) AimPoint = point;
        }

        /// <summary>Dispara hacia un punto (horizontal, a la altura del pecho de las camionetas). Devuelve la camioneta tocada.</summary>
        public Car Fire(Vector3 point)
        {
            ShotsFired++;
            Vector3 from = Muzzle;
            Vector3 dir = point - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = car.transform.forward;
            dir.Normalize();
            Vector3 end = from + dir * range;
            Car hitCar = null;
            foreach (RaycastHit hit in SortedHits(from, dir))
            {
                if (hit.collider.transform.IsChildOf(car.transform)) continue;
                end = hit.point;
                hitCar = hit.collider.GetComponentInParent<Car>();
                break;
            }
            if (hitCar != null && !hitCar.IsPlayer && !hitCar.Destroyed) hitCar.TakeDamage(damage);
            else hitCar = null;
            GameSounds.Play(Sound.Smg);
            CameraShake.Shake(0.04f);
            if (Application.isPlaying) Tracer(from, end, hitCar != null);
            return hitCar;
        }

        private RaycastHit[] SortedHits(Vector3 from, Vector3 dir)
        {
            RaycastHit[] hits = Physics.SphereCastAll(from, 0.25f, dir, range, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            return hits;
        }

        private void Tracer(Vector3 from, Vector3 to, bool hit)
        {
            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = 0.18f;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = tracerMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (hit)
            {
                var spark = new GameObject("Spark").AddComponent<Light>();
                spark.transform.SetParent(go.transform, false);
                spark.transform.position = to;
                spark.color = new Color(1f, 0.7f, 0.3f);
                spark.range = 5f;
                spark.intensity = 4f;
            }
            Destroy(go, 0.06f);
        }
    }
}
