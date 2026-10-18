using System;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Muebles del mapa ASCII: cada dígito es un prop de Mnostva_Art (Assets/Mnostva_Art/Prefabs).
    /// El constructor lo escala para que su lado más largo mida <see cref="Entry.Footprint"/> metros
    /// y lo pega a la pared más cercana de su celda, con el lado largo paralelo a la pared.
    /// </summary>
    public static class PropCatalog
    {
        public readonly struct Entry
        {
            public readonly char Code;
            public readonly string Prefab;
            public readonly float Footprint;
            /// <summary>Color del halo neón (Overdrive/Aura) sobre el prop, o transparente si no tiene.</summary>
            public readonly Color Halo;

            public Entry(char code, string prefab, float footprint, Color halo)
            {
                Code = code; Prefab = prefab; Footprint = footprint; Halo = halo;
            }

            public bool HasHalo => Halo.a > 0f;
        }

        private static readonly Color NoHalo = new Color(0f, 0f, 0f, 0f);
        public static readonly Color NeonPink = new Color(1f, 0.15f, 0.65f, 1f);

        public static readonly Entry[] Entries =
        {
            new Entry('0', "Bed", 2.1f, NoHalo),
            new Entry('1', "Couch", 2.2f, NoHalo),
            new Entry('2', "Table", 1.5f, NoHalo),
            new Entry('3', "Fridge", 0.9f, NoHalo),
            new Entry('4', "Washing_Machine", 0.8f, NoHalo),
            new Entry('5', "Cabinet_1", 1.3f, NoHalo),
            new Entry('6', "Storage_Box_1", 1.0f, NoHalo),
            new Entry('7', "Storage_Box_2", 1.0f, NoHalo),
            new Entry('8', "Plant_1", 0.7f, NoHalo),
            new Entry('9', "Lamp", 0.6f, NeonPink),
            // Letras: más mobiliario para ambientar las salas.
            new Entry('h', "Chair", 0.7f, NoHalo),
            new Entry('n', "Cabinet_2", 1.2f, NoHalo),
            new Entry('p', "Plant_2", 0.8f, NoHalo),
            new Entry('y', "Vinyl_Player", 0.9f, NeonPink),
            new Entry('f', "Flower", 0.5f, NoHalo),
        };

        public static bool IsProp(char c) => Array.Exists(Entries, e => e.Code == c);

        public static Entry Get(char code)
        {
            foreach (Entry e in Entries)
                if (e.Code == code) return e;
            throw new ArgumentException($"'{code}' no es un prop del catálogo.");
        }
    }
}
