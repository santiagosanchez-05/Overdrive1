using System;

namespace Overdrive.Weapons
{
    public enum WeaponType
    {
        Bat,
        Knife,
        Shotgun,
        Smg,
    }

    /// <summary>Datos fijos de cada tipo de arma.</summary>
    public sealed class WeaponDefinition
    {
        public WeaponType Type { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsMelee { get; private set; }

        /// <summary>Segundos entre golpes o disparos.</summary>
        public float UseInterval { get; private set; }

        // Cuerpo a cuerpo
        public float MeleeRange { get; private set; }
        public float MeleeHalfAngle { get; private set; }

        // Armas de fuego (sin recarga)
        public int MaxAmmo { get; private set; }
        public int Pellets { get; private set; }
        public float SpreadAngle { get; private set; }
        public bool Automatic { get; private set; }
        public float ProjectileSpeed { get; private set; }
        public float NoiseRadius { get; private set; }
        public float Shake { get; private set; }

        public static readonly WeaponDefinition Bat = new WeaponDefinition
        {
            Type = WeaponType.Bat, DisplayName = "Bate", IsMelee = true,
            UseInterval = 0.45f, MeleeRange = 1.9f, MeleeHalfAngle = 70f, Shake = 0.3f,
        };

        public static readonly WeaponDefinition Knife = new WeaponDefinition
        {
            Type = WeaponType.Knife, DisplayName = "Cuchillo", IsMelee = true,
            UseInterval = 0.3f, MeleeRange = 1.6f, MeleeHalfAngle = 55f, Shake = 0.25f,
        };

        public static readonly WeaponDefinition Shotgun = new WeaponDefinition
        {
            Type = WeaponType.Shotgun, DisplayName = "Escopeta", IsMelee = false,
            UseInterval = 0.6f, MaxAmmo = 6, Pellets = 6, SpreadAngle = 30f, Automatic = false,
            ProjectileSpeed = 40f, NoiseRadius = 18f, Shake = 0.4f,
        };

        public static readonly WeaponDefinition Smg = new WeaponDefinition
        {
            Type = WeaponType.Smg, DisplayName = "Subfusil", IsMelee = false,
            UseInterval = 0.08f, MaxAmmo = 30, Pellets = 1, SpreadAngle = 6f, Automatic = true,
            ProjectileSpeed = 45f, NoiseRadius = 15f, Shake = 0.12f,
        };

        public static WeaponDefinition Get(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Bat: return Bat;
                case WeaponType.Knife: return Knife;
                case WeaponType.Shotgun: return Shotgun;
                case WeaponType.Smg: return Smg;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
