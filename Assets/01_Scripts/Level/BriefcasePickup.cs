using Overdrive.Core;
using Overdrive.Player;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// El maletín (la mercancía): se recoge al pasar por encima y se lleva a la espalda.
    /// Recogerlo hace entrar a los refuerzos (ver <see cref="LevelManager"/>).
    /// </summary>
    public class BriefcasePickup : MonoBehaviour, IRewindable
    {
        [SerializeField] private PlayerLife player;
        [SerializeField] private float pickupRadius = 1.1f;
        [SerializeField] private Vector3 carriedOffset = new Vector3(0f, 1.1f, -0.45f);
        [Tooltip("Halo dorado (shader Overdrive/Aura) que marca el objetivo mientras está en el suelo.")]
        [SerializeField] private GameObject glow;

        public GameObject Glow => glow;

        public PlayerLife Player => player;
        public bool IsCollected { get; private set; }

        private sealed class Snapshot
        {
            public bool Collected;
            public Transform Parent;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        public object Capture() => new Snapshot
        {
            Collected = IsCollected,
            Parent = transform.parent,
            LocalPosition = transform.localPosition,
            LocalRotation = transform.localRotation,
        };

        public void Restore(object snapshot)
        {
            var s = (Snapshot)snapshot;
            IsCollected = s.Collected;
            transform.SetParent(s.Parent, false);
            transform.localPosition = s.LocalPosition;
            transform.localRotation = s.LocalRotation;
            if (glow != null) glow.SetActive(!IsCollected);
        }

        private void Update()
        {
            if (!IsCollected && player != null && !player.IsDead) TryCollect(player.transform);
        }

        /// <summary>Lo recoge si el jugador está encima, en el mismo piso. Público para simularlo en el editor.</summary>
        public bool TryCollect(Transform collector)
        {
            if (IsCollected || collector == null) return false;
            Vector3 d = collector.position - transform.position;
            if (Mathf.Abs(d.y) > 1.5f) return false;
            d.y = 0f;
            if (d.magnitude > pickupRadius) return false;

            IsCollected = true;
            if (glow != null) glow.SetActive(false);
            transform.SetParent(collector, false);
            transform.localPosition = carriedOffset;
            transform.localRotation = Quaternion.identity;
            return true;
        }
    }
}
