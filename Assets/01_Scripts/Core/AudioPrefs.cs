using System;
using UnityEngine;

namespace Overdrive.Core
{
    /// <summary>Volúmenes guardados en PlayerPrefs: general (AudioListener), música y efectos de sonido.</summary>
    public static class AudioPrefs
    {
        private const string MasterKey = "volume";
        private const string MusicKey = "musicVolume";
        private const string SfxKey = "sfxVolume";
        private const string VersionKey = "audioPrefsVersion";

        /// <summary>Música y efectos empiezan a la mitad.</summary>
        public const float DefaultMusic = 0.5f;
        public const float DefaultSfx = 0.5f;

        /// <summary>
        /// Sube al cambiar los valores por defecto: a quien ya tenía volúmenes guardados se le ponen
        /// una vez los nuevos (versión 2: música y efectos a la mitad).
        /// </summary>
        private const int Version = 2;

        /// <summary>Se lanza al cambiar el volumen de la música.</summary>
        public static event Action<float> MusicChanged;

        /// <summary>Se lanza al cambiar el volumen de los efectos de sonido.</summary>
        public static event Action<float> SfxChanged;

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
            get
            {
                Migrate();
                return PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
            }
            set
            {
                value = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MusicKey, value);
                MusicChanged?.Invoke(value);
            }
        }

        /// <summary>Volumen de los efectos (disparos, golpes...): multiplica el volumen de cada sonido de GameSounds.</summary>
        public static float Sfx
        {
            get
            {
                Migrate();
                return PlayerPrefs.GetFloat(SfxKey, DefaultSfx);
            }
            set
            {
                value = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(SfxKey, value);
                SfxChanged?.Invoke(value);
            }
        }

        private static void Migrate()
        {
            if (PlayerPrefs.GetInt(VersionKey, 0) >= Version) return;
            PlayerPrefs.SetInt(VersionKey, Version);
            PlayerPrefs.SetFloat(MusicKey, DefaultMusic);
            PlayerPrefs.SetFloat(SfxKey, DefaultSfx);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnStart() => AudioListener.volume = Master;
    }
}
