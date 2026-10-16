using Overdrive.Core;
using Overdrive.Weapons;

namespace Overdrive.UI
{
    /// <summary>Textos del HUD. Lógica pura para probarla sin dibujar nada.</summary>
    public static class HudText
    {
        public const string Unarmed = "Puños";
        public const string Dead = "Presiona R para reiniciar";
        public const string Complete = "Nivel completado";
        public const string CompleteHint = "Presiona R para jugar otra vez";
        public const string GoToExit = "¡Ve a la salida!";

        /// <summary>Arma actual y munición: "Escopeta  4/6", "Bate", "Subfusil  0/30 (vacía)".</summary>
        public static string Weapon(WeaponDefinition definition, int ammo)
        {
            if (definition == null) return Unarmed;
            if (definition.IsMelee) return definition.DisplayName;
            string text = $"{definition.DisplayName}  {ammo}/{definition.MaxAmmo}";
            return ammo <= 0 ? text + " (vacía)" : text;
        }

        public static string Enemies(int remaining) => $"Enemigos: {remaining}";

        /// <summary>Mensaje grande del centro de la pantalla, o null si no hay.</summary>
        public static string CenterMessage(LevelState state)
        {
            switch (state)
            {
                case LevelState.Failed: return Dead;
                case LevelState.Complete: return Complete;
                default: return null;
            }
        }

        /// <summary>Mensaje pequeño bajo el central, o null si no hay.</summary>
        public static string SubMessage(LevelState state)
        {
            switch (state)
            {
                case LevelState.ExitOpen: return GoToExit;
                case LevelState.Complete: return CompleteHint;
                default: return null;
            }
        }
    }
}
