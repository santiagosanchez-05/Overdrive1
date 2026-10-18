using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Overdrive.Core
{
    /// <summary>Efectos de sonido del juego.</summary>
    public enum Sound
    {
        Shotgun,
        Smg,
        /// <summary>Puñetazo o arma cuerpo a cuerpo al aire.</summary>
        Swing,
        /// <summary>Puñetazo que conecta (tuyo o de un enemigo desarmado).</summary>
        Punch,
        /// <summary>Golpe de bate que conecta.</summary>
        BatHit,
        /// <summary>Cuchillada que conecta.</summary>
        KnifeHit,
        /// <summary>Cada golpe del remate a un enemigo en el suelo.</summary>
        Kick,
        /// <summary>Un arma lanzada (clic derecho) que impacta contra un enemigo y lo derriba.</summary>
        ThrownHit,
        /// <summary>Una bala o perdigón tuyo mata a un enemigo.</summary>
        ShotKill,
        /// <summary>Un enemigo cae al suelo (derribado o muerto).</summary>
        EnemyFall,
        Throw,
        PlayerDeath,
        /// <summary>El jugador recoge un arma del suelo o cambia la suya por otra (E).</summary>
        WeaponChange,
    }

    /// <summary>
    /// Reproduce los efectos de sonido. Cada sonido tiene su clip, volumen y variación de tono
    /// (para que los disparos seguidos no suenen idénticos); todo se ajusta en el Inspector.
    /// Un sonido sin clip simplemente no suena. Uso desde cualquier script: <c>GameSounds.Play(Sound.Shotgun)</c>.
    /// </summary>
    public class GameSounds : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.8f;
            [Tooltip("Variación aleatoria del tono en cada reproducción (0 = siempre igual).")]
            [Range(0f, 0.3f)] public float pitchVariation = 0.08f;
        }

        [Header("Disparos")]
        [SerializeField] private Entry shotgun = new Entry();
        [SerializeField] private Entry smg = new Entry { volume = 0.5f };
        [Tooltip("Una bala o perdigón tuyo mata a un enemigo (escopeta o subfusil).")]
        [SerializeField] private Entry shotKill = new Entry();

        [Header("Cuerpo a cuerpo")]
        [Tooltip("Puñetazo al aire (también el enemigo desarmado antes de pegarte).")]
        [SerializeField] private Entry swing = new Entry { volume = 0.5f };
        [Tooltip("Puñetazo que conecta.")]
        [SerializeField] private Entry punch = new Entry();
        [Tooltip("Cada golpe con el bate en la mano, acierte o no (tuyo o de un enemigo).")]
        [SerializeField] private Entry batHit = new Entry();
        [Tooltip("Cada golpe con el cuchillo en la mano, acierte o no (tuyo o de un enemigo).")]
        [SerializeField] private Entry knifeHit = new Entry();
        [Tooltip("Cada golpe del remate (Espacio) a un enemigo en el suelo.")]
        [SerializeField] private Entry kick = new Entry();

        [Header("Recoger armas")]
        [Tooltip("Recoger un arma del suelo o cambiar la tuya por otra (E).")]
        [SerializeField] private Entry weaponChange = new Entry { pitchVariation = 0.03f };

        [Header("Armas lanzadas")]
        [SerializeField] private Entry throwWeapon = new Entry { volume = 0.7f };
        [Tooltip("El arma lanzada (clic derecho) golpea a un enemigo y lo derriba.")]
        [SerializeField] private Entry thrownHit = new Entry();

        [Header("Personajes")]
        [SerializeField] private Entry enemyFall = new Entry { volume = 0.7f };
        [SerializeField] private Entry playerDeath = new Entry { pitchVariation = 0f };

        [Tooltip("Sonidos que pueden sonar a la vez (el subfusil y la escopeta disparan muy seguido).")]
        [SerializeField] private int voices = 16;

        private static GameSounds active;
        private AudioSource[] sources;
        private int next;
        private float sfxVolume = 1f;

        public static GameSounds Active => active;

        public Entry Get(Sound sound)
        {
            switch (sound)
            {
                case Sound.Shotgun: return shotgun;
                case Sound.Smg: return smg;
                case Sound.Swing: return swing;
                case Sound.Punch: return punch;
                case Sound.BatHit: return batHit;
                case Sound.KnifeHit: return knifeHit;
                case Sound.Kick: return kick;
                case Sound.ThrownHit: return thrownHit;
                case Sound.ShotKill: return shotKill;
                case Sound.EnemyFall: return enemyFall;
                case Sound.Throw: return throwWeapon;
                case Sound.PlayerDeath: return playerDeath;
                case Sound.WeaponChange: return weaponChange;
                default: return null;
            }
        }

        /// <summary>Reproduce el sonido en la escena activa, si hay un GameSounds y el sonido tiene clip.</summary>
        public static void Play(Sound sound)
        {
            if (active != null) active.PlayLocal(sound);
        }

        private void Awake()
        {
            sources = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                // Cámara cenital: sonido 2D, se oye igual en toda la pantalla.
                source.spatialBlend = 0f;
                sources[i] = source;
            }
        }

        private void OnEnable()
        {
            active = this;
            sfxVolume = AudioPrefs.Sfx;
            AudioPrefs.SfxChanged += OnSfxChanged;
        }

        private void OnDisable()
        {
            if (active == this) active = null;
            AudioPrefs.SfxChanged -= OnSfxChanged;
        }

        /// <summary>El ajuste "Efectos" del menú: escala también lo que ya está sonando.</summary>
        private void OnSfxChanged(float value)
        {
            float ratio = sfxVolume > 0f ? value / sfxVolume : 0f;
            sfxVolume = value;
            if (sources == null) return;
            foreach (AudioSource source in sources)
                if (source.isPlaying) source.volume = ratio > 0f ? source.volume * ratio : 0f;
        }

        private void PlayLocal(Sound sound)
        {
            Entry entry = Get(sound);
            if (entry == null || entry.clip == null || sources == null) return;
            AudioSource source = sources[next];
            next = (next + 1) % sources.Length;
            source.Stop();
            source.clip = entry.clip;
            source.volume = entry.volume * sfxVolume;
            source.pitch = 1f + Random.Range(-entry.pitchVariation, entry.pitchVariation);
            source.Play();
        }
    }
}
