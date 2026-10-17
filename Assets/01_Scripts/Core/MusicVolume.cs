using UnityEngine;

namespace Overdrive.Core
{
    /// <summary>
    /// Música de un nivel: va junto a su AudioSource y su volumen sigue el ajuste "Música"
    /// del menú principal y del menú de pausa. Sigue sonando (más bajo) con el juego en pausa.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicVolume : MonoBehaviour
    {
        [Tooltip("Volumen de la pista con el ajuste al máximo.")]
        [Range(0f, 1f)] public float baseVolume = 0.7f;

        [Tooltip("Multiplicador del volumen mientras el juego está en pausa.")]
        [Range(0f, 1f)] public float pausedFactor = 0.4f;

        private AudioSource source;
        private float music = 1f;

        private void Awake() => source = GetComponent<AudioSource>();

        private void OnEnable()
        {
            AudioPrefs.MusicChanged += Apply;
            Apply(AudioPrefs.Music);
            if (source.clip != null && !source.isPlaying) source.Play();
        }

        private void OnDisable() => AudioPrefs.MusicChanged -= Apply;

        private void Update() => Apply(music);

        private void Apply(float value)
        {
            music = value;
            float pause = GameInput.Blocked ? pausedFactor : 1f;
            source.volume = baseVolume * music * pause;
        }
    }
}
