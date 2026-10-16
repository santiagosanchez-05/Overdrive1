using UnityEngine;

namespace Overdrive.Core
{
    /// <summary>Alturas de los pisos del edificio. El piso i tiene el suelo en y = i * Height.</summary>
    public static class Floors
    {
        public const float Height = 4f;

        /// <summary>Diferencia de altura máxima para considerar que dos cosas están en el mismo piso.</summary>
        public const float SameFloorTolerance = 2f;

        /// <summary>Piso al que pertenece una altura (los pies de un personaje o un objeto en el suelo).</summary>
        public static int IndexOf(float y) => Mathf.Max(0, Mathf.FloorToInt((y + 1f) / Height));

        public static float BaseY(int index) => index * Height;

        public static bool SameFloor(Vector3 a, Vector3 b) => Mathf.Abs(a.y - b.y) < SameFloorTolerance;
    }
}
