using System;
using UnityEngine;

namespace Overdrive.Core
{
    /// <summary>Vista de juego: cenital (por defecto) o primera persona. Se cambia con V y se recuerda.</summary>
    public static class ViewMode
    {
        private const string Key = "firstPerson";

        public static event Action<bool> Changed;

        public static bool FirstPerson
        {
            get => PlayerPrefs.GetInt(Key, 0) == 1;
            set
            {
                if (value == FirstPerson) return;
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                Changed?.Invoke(value);
            }
        }

        /// <summary>Sensibilidad del ratón en primera persona (grados por unidad de movimiento).</summary>
        public static float Sensitivity
        {
            get => PlayerPrefs.GetFloat("mouseSensitivity", 2.2f);
            set => PlayerPrefs.SetFloat("mouseSensitivity", Mathf.Clamp(value, 0.2f, 10f));
        }
    }
}
