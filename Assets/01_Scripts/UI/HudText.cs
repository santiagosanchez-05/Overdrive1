using System.Text;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Weapons;
using UnityEngine;

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

        /// <summary>Instrucción al completar el nivel según si hay siguiente escena configurada.</summary>
        public static string CompleteActionHint(string nextScene)
        {
            if (string.IsNullOrEmpty(nextScene)) return CompleteHint;
            if (string.Equals(nextScene, "Menu", System.StringComparison.OrdinalIgnoreCase))
                return "ESPACIO · Volver al menú  |  R · Reiniciar";
            return "ESPACIO · Siguiente nivel  |  R · Reiniciar";
        }

        /// <summary>Arma actual y munición: "Escopeta  4/6", "Bate", "Subfusil  0/30 (vacía)".</summary>
        /// <param name="capacity">Cargador de esta arma concreta si no es el de su tipo (0 = el de su tipo).</param>
        public static string Weapon(WeaponDefinition definition, int ammo, int capacity = 0)
        {
            if (definition == null) return Unarmed;
            if (definition.IsMelee) return definition.DisplayName;
            int max = capacity > 0 ? capacity : definition.MaxAmmo;
            string text = $"{definition.DisplayName}  {ammo}/{max}";
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
        public const string NoRewindsLeft = "No quedan rebobinados";

        /// <summary>Al morir: cuántos rebobinados quedan y con qué tecla se usa.</summary>
        public static string RewindHint(int usesLeft) =>
            usesLeft > 0 ? $"T: rebobinar 5 s (quedan {usesLeft})" : NoRewindsLeft;

        /// <summary>Esquina del HUD: rebobinados disponibles.</summary>
        public static string Rewinds(int usesLeft) => $"Rebobinados: {usesLeft}";

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

        // ---------- Puntuación ----------

        public const string NewRecord = "¡Nuevo récord!";

        /// <summary>Puntos con separador de miles: 12400 → "12.400".</summary>
        public static string Points(int points)
        {
            string digits = System.Math.Abs(points).ToString();
            var sb = new StringBuilder();
            if (points < 0) sb.Append('-');
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append('.');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }

        /// <summary>Indicador de combo, o null si no hay combo que mostrar (menos de 2 muertes seguidas).</summary>
        public static string Combo(int combo, int multiplier) => combo >= 2 ? $"x{multiplier} COMBO" : null;

        /// <summary>Texto flotante de unos puntos ganados: "+800" o "+1.600 x2".</summary>
        public static string Gain(int points, int multiplier) =>
            multiplier > 1 ? $"+{Points(points)} x{multiplier}" : $"+{Points(points)}";

        /// <summary>Etiqueta de las muertes especiales, o null para las normales.</summary>
        public static string KillLabel(KillCause cause)
        {
            switch (cause)
            {
                case KillCause.Finisher: return "REMATE";
                case KillCause.Backstab: return "POR LA ESPALDA";
                default: return null;
            }
        }

        /// <summary>Etiqueta del texto flotante: la de la muerte especial y/o "SIGILO", o null si no hay.</summary>
        public static string PopupLabel(KillCause? cause, bool stealth)
        {
            string kill = cause.HasValue ? KillLabel(cause.Value) : null;
            if (!stealth) return kill;
            return kill != null ? $"{kill} + SIGILO" : "SIGILO";
        }

        /// <summary>Segundos como minutos:segundos: 95 → "1:35".</summary>
        public static string Time(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        public static string Record(int best) => $"Récord: {Points(best)}";

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
