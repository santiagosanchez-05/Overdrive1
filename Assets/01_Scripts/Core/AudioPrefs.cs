using System;
using UnityEngine;

namespace Overdrive.Core
{
    /// <summary>Volúmenes guardados en PlayerPrefs: general (AudioListener) y música.</summary>
    public static class AudioPrefs
    {
        private const string MasterKey = "volume";
        private const string MusicKey = "musicVolume";

        /// <summary>Se lanza al cambiar el volumen de la música.</summary>
        public static event Action<float> MusicChanged;

        public static float Master
        {
            get => PlayerPrefs.GetFloat(MasterKey, 1f);
            set
            {
                value = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MasterKey, value);
                AudioListener.volume = value;
            }
        }

        public static float Music
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.8f);
            set
            {
                value = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MusicKey, value);
                MusicChanged?.Invoke(value);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnStart() => AudioListener.volume = Master;
    }
}
