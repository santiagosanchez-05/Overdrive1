using Overdrive.Player;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// El maletín (la mercancía): se recoge al pasar por encima y se lleva a la espalda.
    /// Recogerlo hace entrar a los refuerzos (ver <see cref="LevelManager"/>).
    /// </summary>
    public class BriefcasePickup : MonoBehaviour
    {
        [SerializeField] private PlayerLife player;
        [SerializeField] private float pickupRadius = 1.1f;
        [SerializeField] private Vector3 carriedOffset = new Vector3(0f, 1.1f, -0.45f);

        public PlayerLife Player => player;
        public bool IsCollected { get; private set; }

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
            transform.SetParent(collector, false);
            transform.localPosition = carriedOffset;
            transform.localRotation = Quaternion.identity;
            return true;
        }
    }
}
