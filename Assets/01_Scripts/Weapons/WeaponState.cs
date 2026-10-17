using UnityEngine;

namespace Overdrive.Weapons
{
    /// <summary>
    /// Estado de un arma concreta: munición restante y enfriamiento.
    /// No hay recarga: sin munición, un arma de fuego solo sirve para lanzarla.
    /// </summary>
    public sealed class WeaponState
    {
        private float cooldownLeft;

        public WeaponDefinition Definition { get; }
        public int Ammo { get; private set; }

        public WeaponState(WeaponDefinition definition)
        {
            Definition = definition;
            Ammo = definition.IsMelee ? 0 : definition.MaxAmmo;
        }

        public bool IsMelee => Definition.IsMelee;
        public bool IsEmpty => !IsMelee && Ammo <= 0;
        public bool Ready => cooldownLeft <= 0f;

        public float CooldownLeft => cooldownLeft;

        /// <summary>Rebobinar: vuelve a la munición y el enfriamiento guardados.</summary>
        public void Restore(int ammo, float cooldown)
        {
            Ammo = ammo;
            cooldownLeft = cooldown;
        }

        public void Tick(float deltaTime)
        {
            cooldownLeft = Mathf.Max(0f, cooldownLeft - deltaTime);
        }

        /// <summary>
        /// Intenta golpear o disparar: respeta el enfriamiento y gasta un cartucho/bala.
        /// Los enemigos disparan sin gastar munición (<paramref name="consumeAmmo"/> = false).
        /// </summary>
        public bool TryUse(bool consumeAmmo = true)
        {
            if (!Ready || (consumeAmmo && IsEmpty)) return false;
            if (!IsMelee && consumeAmmo) Ammo--;
            cooldownLeft = Definition.UseInterval;
            return true;
        }
    }
}
