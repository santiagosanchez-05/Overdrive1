using Overdrive.Core;
using Overdrive.Enemies;
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
        public const string FindBriefcase = "Objetivo: el maletín del piso de arriba";
        public const string CarryingBriefcase = "Maletín: lo tienes";

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

        public const string Sneaking = "Sigilo";

        /// <summary>Indicador sobre la cabeza de un enemigo: "?" si sospecha, "!" si te ha detectado.</summary>
        public static string AwarenessMark(Awareness awareness)
        {
            switch (awareness)
            {
                case Awareness.Suspicious: return "?";
                case Awareness.Alerted: return "!";
                default: return null;
            }
        }

        /// <summary>Línea del maletín en la esquina, o null si todavía no lo lleva.</summary>
        public static string Briefcase(bool carrying) => carrying ? CarryingBriefcase : null;

        /// <summary>Mensaje pequeño bajo el central, o null si no hay.</summary>
        /// <param name="needsObjective">Todavía falta recoger el maletín.</param>
        public static string SubMessage(LevelState state, bool needsObjective = false)
        {
            switch (state)
            {
                case LevelState.Playing: return needsObjective ? FindBriefcase : null;
                case LevelState.ExitOpen: return GoToExit;
                case LevelState.Complete: return CompleteHint;
                default: return null;
            }
        }
    }
}
