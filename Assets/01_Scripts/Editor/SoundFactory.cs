using Overdrive.Core;
using UnityEditor;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Efectos de sonido del nivel: qué archivo de la carpeta de audio va con cada sonido.
    /// Solo asigna archivos que existan y campos que estén vacíos; los demás se rellenan a mano en el Inspector.
    /// No crea ningún sonido.
    /// </summary>
    internal static class SoundFactory
    {
        public static string AudioPath => AssetFactory.NumberedFolder("Audio", "07_Audio");

        /// <summary>Sonidos con archivo conocido en la carpeta de audio del equipo.</summary>
        public static readonly (Sound sound, string field, string file)[] Assignments =
        {
            (Sound.Shotgun, "shotgun", "escopeta2.mp3"),
            (Sound.Smg, "smg", "Posible_Subfucil.mp3"),
            (Sound.ShotKill, "shotKill", "head-shot_Blood.mp3"),
            (Sound.Swing, "swing", "Golpe_Aire.wav"),
            (Sound.Punch, "punch", "Golpe.wav"),
            (Sound.BatHit, "batHit", "Bat_Golpe.mp3"),
            (Sound.KnifeHit, "knifeHit", "Cortada_Daga1.mp3"),
            (Sound.Kick, "kick", "Pisada_polvoriento.mp3"),
            (Sound.Throw, "throwWeapon", "Lanzar_Arma.mp3"),
            (Sound.ThrownHit, "thrownHit", "GolpeTirado.mp3"),
            (Sound.EnemyFall, "enemyFall", "Caida.wav"),
            (Sound.PlayerDeath, "playerDeath", "gameOverPixel.mp3"),
            (Sound.WeaponChange, "weaponChange", "Change_Weapon.mp3"),
        };

        public static AudioClip Load(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPath}/{file}");

        /// <summary>Añade GameSounds al objeto de sistemas y le asigna los clips conocidos que estén vacíos.</summary>
        public static GameSounds EnsureGameSounds(GameObject systems)
        {
            var sounds = BuilderUtil.EnsureComponent<GameSounds>(systems);
            var so = new SerializedObject(sounds);
            foreach (var (sound, field, file) in Assignments)
            {
                SerializedProperty clip = so.FindProperty($"{field}.clip");
                // Respeta los clips que alguien haya puesto a mano en el Inspector.
                if (clip.objectReferenceValue == null)
                {
                    clip.objectReferenceValue = Load(file);
                    SerializedProperty vol = so.FindProperty($"{field}.volume");
                    if (vol != null)
                    {
                        vol.floatValue = sound == Sound.Shotgun || sound == Sound.ShotKill || sound == Sound.Kick || sound == Sound.ThrownHit
                            ? 0.8f
                            : sound == Sound.Smg ? 0.5f : 0.2f;
                    }
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return sounds;
        }
    }
}
