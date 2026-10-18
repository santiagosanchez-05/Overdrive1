using UnityEngine;

namespace Overdrive.Weapons
{
    /// <summary>
    /// Cómo se lleva cada arma en la mano, por separado para cada vista (jugador y enemigos, en todos los niveles):
    /// tamaño, inclinación y, en primera persona, dónde va en la pantalla.
    /// Tercera persona (cenital): los personajes están a escala 1,3, así que las armas crecen un poco con ellos;
    /// el bate se lleva levantado y el cuchillo hacia delante, no como una lanza.
    /// Primera persona: el arma va pegada a la cámara y se reduce para no tapar la pantalla; las de fuego
    /// apuntan a la mira, las de cuerpo a cuerpo tienen su propia pose.
    /// En el suelo y en vuelo el arma tiene su tamaño normal (1).
    /// </summary>
    public static class WeaponViewScale
    {
        public static float ThirdPerson(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Bat: return 1.0f;
                case WeaponType.Knife: return 1.0f;
                case WeaponType.Shotgun: return 1.0f;
                case WeaponType.Smg: return 1.15f;
                case WeaponType.Revolver: return 1.25f;
                default: return 1f;
            }
        }

        public static float FirstPerson(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Bat: return 0.5f;
                case WeaponType.Knife: return 0.55f;
                case WeaponType.Shotgun: return 0.5f;
                case WeaponType.Smg: return 0.55f;
                case WeaponType.Revolver: return 0.7f;
                default: return 0.6f;
            }
        }

        /// <summary>Inclinación del arma en la mano en tercera persona (respecto a hacia donde mira el personaje).</summary>
        public static Quaternion ThirdPersonHold(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Bat: return Quaternion.Euler(-62f, 0f, 0f);  // levantado, punta hacia arriba
                case WeaponType.Knife: return Quaternion.Euler(12f, 0f, 0f); // hacia delante, punta algo baja
                default: return Quaternion.identity;                         // armas de fuego: al frente
            }
        }

        /// <summary>
        /// Pose en primera persona de las armas cuerpo a cuerpo, respecto a la cámara: posición (derecha, abajo,
        /// delante) y giro. Las armas de fuego no la usan: apuntan a la mira.
        /// </summary>
        public static bool FirstPersonMeleePose(WeaponType type, out Vector3 offset, out Quaternion rotation)
        {
            switch (type)
            {
                case WeaponType.Bat:
                    offset = new Vector3(0.3f, -0.34f, 0.42f);
                    rotation = Quaternion.Euler(-58f, -18f, -22f); // por encima del hombro derecho
                    return true;
                case WeaponType.Knife:
                    offset = new Vector3(0.27f, -0.3f, 0.45f);
                    rotation = Quaternion.Euler(28f, -10f, 0f);    // hacia delante, punta algo baja
                    return true;
                default:
                    offset = Vector3.zero;
                    rotation = Quaternion.identity;
                    return false;
            }
        }
    }
}
