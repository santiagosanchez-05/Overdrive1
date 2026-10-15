using System;
using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Player
{
    /// <summary>Vida del jugador: muere de un solo golpe o disparo.</summary>
    [DisallowMultipleComponent]
    public class PlayerLife : MonoBehaviour
    {
        [SerializeField] private Transform body;

        private readonly Health health = new Health();

        public bool IsDead => health.IsDead;
        public Transform Body => body;

        public event Action Died;

        public bool Kill()
        {
            if (!health.Kill()) return false;
            if (body != null) body.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Died?.Invoke();
            return true;
        }
    }
}
