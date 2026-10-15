using System;
using UnityEngine;

namespace Overdrive.CameraRig
{
    /// <summary>Calcula cuánto se adelanta la cámara hacia el punto apuntado.</summary>
    public static class CameraLookAhead
    {
        [Serializable]
        public struct Settings
        {
            [Tooltip("Fracción de la distancia jugador-mouse que se adelanta la cámara.")]
            public float normalFactor;
            public float normalMax;
            [Tooltip("Igual que arriba, con Shift pulsado.")]
            public float farFactor;
            public float farMax;

            public static Settings Default => new Settings
            {
                normalFactor = 0.25f,
                normalMax = 3f,
                farFactor = 0.6f,
                farMax = 9f,
            };
        }

        public static Vector3 ComputeOffset(Vector3 player, Vector3 aimPoint, bool lookFar, Settings settings)
        {
            Vector3 toAim = aimPoint - player;
            toAim.y = 0f;
            float factor = lookFar ? settings.farFactor : settings.normalFactor;
            float max = lookFar ? settings.farMax : settings.normalMax;
            return Vector3.ClampMagnitude(toAim * factor, max);
        }

        public static Vector3 ComputeFocus(Vector3 player, Vector3 aimPoint, bool lookFar, Settings settings)
        {
            return player + ComputeOffset(player, aimPoint, lookFar, settings);
        }
    }
}
