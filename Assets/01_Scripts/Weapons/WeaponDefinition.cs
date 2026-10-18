using System;
using Overdrive.Core;

namespace Overdrive.Weapons
{
    public enum WeaponType
    {
        Bat,
        Knife,
        Shotgun,
        Smg,
        Revolver,
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
        /// <summary>Distancia que recorre cada bala o perdigón antes de desaparecer.</summary>
        public float ProjectileRange { get; private set; }
        public float NoiseRadius { get; private set; }
        public float Shake { get; private set; }

        /// <summary>Vida que quita un golpe o una bala (perdigón, en la escopeta) en el cuerpo. La vida es 100.</summary>
        public float Damage { get; private set; } = 100f;

        /// <summary>Un impacto en la cabeza mata de una vez (armas de fuego y cuchillo; el bate no).</summary>
        public bool HeadKills { get; private set; } = true;

        /// <summary>Vida que quita el arma al lanzarla y acertar en el cuerpo.</summary>
        public float ThrownDamage { get; private set; } = 15f;

        /// <summary>Lanzada a la cabeza, mata (solo el cuchillo).</summary>
        public bool ThrownHeadKills { get; private set; }

        /// <summary>Puñetazo de un enemigo desarmado al jugador.</summary>
        public const float UnarmedDamage = 12f;

        /// <summary>Sonido del golpe cuerpo a cuerpo cuando conecta.</summary>
        public Sound HitSound { get; private set; } = Sound.Punch;

        /// <summary>Segundos entre ataques cuando la usa un enemigo (más lento que el jugador, por justicia).</summary>
        public float EnemyAttackInterval { get; private set; }

        /// <summary>Distancia desde la que un enemigo ataca con esta arma.</summary>
        public float EnemyAttackRange { get; private set; }

        /// <summary>Puños de un enemigo desarmado.</summary>
        public const float UnarmedRange = 1.3f;
        public const float UnarmedHalfAngle = 60f;
        public const float UnarmedEnemyInterval = 0.9f;

        public static readonly WeaponDefinition Bat = new WeaponDefinition
        {
            Type = WeaponType.Bat, DisplayName = "Bate", IsMelee = true, HitSound = Sound.BatHit,
            UseInterval = 0.45f, MeleeRange = 1.9f, MeleeHalfAngle = 70f, Shake = 0.3f,
            Damage = 34f, HeadKills = false, ThrownDamage = 20f, ThrownHeadKills = false,
            EnemyAttackInterval = 0.9f, EnemyAttackRange = 1.5f,
        };

        public static readonly WeaponDefinition Knife = new WeaponDefinition
        {
            Type = WeaponType.Knife, DisplayName = "Cuchillo", IsMelee = true, HitSound = Sound.KnifeHit,
            UseInterval = 0.3f, MeleeRange = 1.6f, MeleeHalfAngle = 55f, Shake = 0.25f,
            Damage = 40f, HeadKills = true, ThrownDamage = 40f, ThrownHeadKills = true,
            EnemyAttackInterval = 0.7f, EnemyAttackRange = 1.3f,
        };

        public static readonly WeaponDefinition Shotgun = new WeaponDefinition
        {
            Type = WeaponType.Shotgun, DisplayName = "Escopeta", IsMelee = false,
            UseInterval = 0.6f, MaxAmmo = 6, Pellets = 6, SpreadAngle = 30f, Automatic = false,
            Damage = 20f, HeadKills = true,
            ProjectileSpeed = 40f, ProjectileRange = 10f, NoiseRadius = 18f, Shake = 0.4f,
            EnemyAttackInterval = 1.2f, EnemyAttackRange = 8f,
        };

        public static readonly WeaponDefinition Smg = new WeaponDefinition
        {
            Type = WeaponType.Smg, DisplayName = "Subfusil", IsMelee = false,
            UseInterval = 0.08f, MaxAmmo = 30, Pellets = 1, SpreadAngle = 6f, Automatic = true,
            Damage = 25f, HeadKills = true,
            ProjectileSpeed = 45f, ProjectileRange = 35f, NoiseRadius = 15f, Shake = 0.12f,
            EnemyAttackInterval = 0.3f, EnemyAttackRange = 11f,
        };

        public static readonly WeaponDefinition Revolver = new WeaponDefinition
        {
            Type = WeaponType.Revolver, DisplayName = "Revólver", IsMelee = false,
            UseInterval = 0.35f, MaxAmmo = 6, Pellets = 1, SpreadAngle = 2f, Automatic = false,
            Damage = 55f, HeadKills = true,
            ProjectileSpeed = 55f, ProjectileRange = 30f, NoiseRadius = 14f, Shake = 0.22f,
            EnemyAttackInterval = 0.9f, EnemyAttackRange = 10f,
        };

        public static WeaponDefinition Get(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Bat: return Bat;
                case WeaponType.Knife: return Knife;
                case WeaponType.Shotgun: return Shotgun;
                case WeaponType.Smg: return Smg;
                case WeaponType.Revolver: return Revolver;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
