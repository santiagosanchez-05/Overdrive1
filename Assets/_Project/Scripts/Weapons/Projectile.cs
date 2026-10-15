using Overdrive.Combat;
using UnityEngine;

namespace Overdrive.Weapons
{
    /// <summary>Bala visible y rápida. Mata a quien toque del otro bando y se destruye contra las paredes.</summary>
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float maxDistance = 40f;
        [SerializeField] private float radius = 0.08f;

        private readonly LinearFlight flight = new LinearFlight();

        public Faction Owner { get; private set; }
        public bool Active => flight.Active;
        public LinearFlight Flight => flight;

        public void Launch(Vector3 origin, Vector3 direction, float speed, Faction owner)
        {
            Owner = owner;
            flight.Launch(origin, direction, speed, maxDistance);
            transform.SetPositionAndRotation(origin, Quaternion.LookRotation(flight.Direction, Vector3.up));
        }

        private void Update() => Step(Time.deltaTime);

        /// <summary>Avanza la bala. Público para simularlo en el editor.</summary>
        public void Step(float deltaTime)
        {
            if (!flight.Active) return;
            float distance = flight.NextStep(deltaTime);
            if (ProjectileCast.TryCast(flight.Position, flight.Direction, distance, radius, Owner, out ProjectileCast.Hit hit))
            {
                if (hit.Enemy != null) hit.Enemy.ReceiveLethalHit();
                if (hit.Player != null) hit.Player.Kill();
                Despawn();
                return;
            }
            flight.Advance(distance);
            transform.position = flight.Position;
            if (!flight.Active) Despawn();
        }

        private void Despawn()
        {
            flight.Stop();
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
