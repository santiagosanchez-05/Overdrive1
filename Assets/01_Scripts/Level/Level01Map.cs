using System.Collections.Generic;
using System.Linq;
using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Nivel 1: planta baja con seis salas y pasillo central, y un primer piso sobre las dos salas del norte.
    /// Se sube por la escalera de la sala inicial. El maletín está arriba; al cogerlo, los refuerzos
    /// entran por la puerta del extremo este del pasillo. Leyenda en <see cref="LevelMap"/>.
    /// </summary>
    public static class Level01Map
    {
        public const float CellSize = 1.5f;

        public static readonly string[] GroundRows =
        {
            "##################################",
            "#9......#p..1.h2h.9#5..h2h..1..67#",
            "#.//////#..........#.............#",
            "#.//////#..........#.............#",
            "#.................f#.............#",
            "#..........m.......#......k......#",
            "#.P.B.V.#..........#.............#",
            "#h.....8#y.......h2#n8........2hp#",
            "####..#######..#########..########",
            "#............................DRR.#",
            "#............................DRR.#",
            "######..##########..######..######",
            "#1.h......p#p.1.......9#p.....2h9#",
            "#..........#...........#.........#",
            "#f.........#.....s.....#....m....#",
            "#................................#",
            "#...s......................b.....#",
            "#..........#...........#.........#",
            "#..........#...........#......XX.#",
            "#9.h2h....n#34..h2h..h2#1.....XX.#",
            "##################################",
        };

        public static readonly string[] UpperRows =
        {
            "        ##########################",
            "        #p..1....9#.h2h.........p#",
            "        ......k...#.........C....#",
            "        ..........#..............#",
            "        #..OO............OO......#",
            "        #..OO............OO......#",
            "        #......b..#..........m...#",
            "        #5......h2#y....s.......8#",
            "        ##########################",
            "                                  ",
            "                                  ",
            "                                  ",
            "                                  ",
            "                                  ",
            "                                  ",
            "                                  ",
            "                                  ",
            "                                  ",
            "                                  ",
            "                                  ",
            "                                  ",
        };

        /// <summary>Luces de ambiente oscuras: carmesí en el pasillo, violeta en las salas, brasas en las mesas y luz cayendo por los huecos.</summary>
        public static readonly AmbientLight[] Ambience =
        {
            // Pasillo central: halos carmesí colgando y una columna roja al fondo oeste.
            new AmbientLight(0, 6f, 9.5f, AmbientKind.HaloCrimson),
            new AmbientLight(0, 15f, 9.5f, AmbientKind.HaloCrimson),
            new AmbientLight(0, 23f, 9.5f, AmbientKind.HaloCrimson),
            new AmbientLight(0, 2f, 9.5f, AmbientKind.BeamCrimson, 2.5f),

            // Luz violeta que cae por los huecos del piso de arriba.
            new AmbientLight(0, 11.5f, 4.5f, AmbientKind.BeamViolet, 4f),
            new AmbientLight(0, 25.5f, 4.5f, AmbientKind.BeamViolet, 4f),

            // Salas de la planta baja.
            new AmbientLight(0, 26f, 3f, AmbientKind.HaloCrimson),
            new AmbientLight(0, 5.5f, 15.5f, AmbientKind.HaloViolet),
            new AmbientLight(0, 17f, 17f, AmbientKind.HaloViolet),

            // Brasas sobre las mesas.
            new AmbientLight(0, 18f, 7f, AmbientKind.Ember),
            new AmbientLight(0, 22f, 19f, AmbientKind.Ember),
            new AmbientLight(0, 30f, 16f, AmbientKind.Ember),

            // Piso de arriba.
            new AmbientLight(1, 13f, 6f, AmbientKind.HaloViolet),
            new AmbientLight(1, 23f, 2f, AmbientKind.HaloCrimson),
            new AmbientLight(1, 17f, 7f, AmbientKind.Ember),
        };

        public static LevelLayout Create()
        {
            var ground = new LevelMap(GroundRows, CellSize, 0, Floors.BaseY(0));
            var upper = new LevelMap(UpperRows, CellSize, 1, Floors.BaseY(1));
            return new LevelLayout(ground, upper)
            {
                Ambience = Ambience,
                LedCables = LedCables.Concat(EdgeCables(ground, upper)).ToArray(),
                Doorways = Doorways,
            };
        }

        private const float EdgeHeight = 2.25f;
        private const float EdgeSag = 0.18f;
        private const float EdgeSpacing = 2.5f;

        /// <summary>
        /// Cables colgando por el borde alto de algunas paredes, pegados a ellas (se ven sobre todo en primera persona,
        /// junto al techo). Se calculan sobre el mapa para que sus anclajes siempre toquen pared.
        /// </summary>
        private static LedCable[] EdgeCables(LevelMap ground, LevelMap upper)
        {
            return new[]
            {
                // Planta baja.
                Edge(ground, LedColor.Magenta, true, 0.62f, 0.62f, 7.38f),      // sala inicial, pared norte
                Edge(ground, LedColor.Teal, true, 0.62f, 8.62f, 18.38f),        // sala norte, pared norte
                Edge(ground, LedColor.Violet, false, 32.38f, 0.62f, 7.38f),     // sala de las cajas, pared este
                Edge(ground, LedColor.Crimson, true, 11.62f, 0.62f, 10.38f),    // sala oeste, pared norte
                Edge(ground, LedColor.Magenta, false, 0.62f, 12.62f, 18.38f),   // sala oeste, pared oeste
                Edge(ground, LedColor.ColdBlue, true, 19.38f, 23.62f, 32.38f),  // sala sureste, pared sur
                // Piso de arriba.
                Edge(upper, LedColor.ColdBlue, true, 0.62f, 8.62f, 17.38f),     // ala oeste, pared norte
                Edge(upper, LedColor.Crimson, true, 7.38f, 18.62f, 32.38f),     // ala este, pared sur
            }.Where(cable => cable.HasValue).Select(cable => cable.Value).ToArray();
        }

        /// <summary>
        /// Un cable a lo largo de una pared (alongColumns: va de columna a columna en la fila fija; si no, de fila a fila).
        /// Los anclajes van cada ~2,5 celdas y se saltan los huecos de puerta: sin pared no hay dónde sujetarlos.
        /// </summary>
        private static LedCable? Edge(LevelMap map, LedColor color, bool alongColumns, float fixedCoord, float from, float to)
        {
            var anchors = new List<Vector3>();
            int steps = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(to - from) / EdgeSpacing));
            for (int i = 0; i <= steps; i++)
            {
                float along = Mathf.Lerp(from, to, (float)i / steps);
                foreach (float shift in new[] { 0f, 0.5f, -0.5f, 1f, -1f })
                {
                    float a = Mathf.Clamp(along + shift, Mathf.Min(from, to), Mathf.Max(from, to));
                    Vector3 p = alongColumns ? LedCable.P(a, EdgeHeight, fixedCoord) : LedCable.P(fixedCoord, EdgeHeight, a);
                    if (!TouchesWall(map, p)) continue;
                    if (anchors.Count == 0 || anchors[anchors.Count - 1] != p) anchors.Add(p);
                    break;
                }
            }
            if (anchors.Count < 2) return null;
            // Sin curvarse hacia los lados (bow 0): pegado a la pared no hay sitio.
            return new LedCable(map.FloorIndex, color, LedLayout.Hanging, anchors.ToArray(), sag: EdgeSag, bow: 0f);
        }

        /// <summary>El punto toca una pared (misma regla con la que el verificador comprueba los anclajes).</summary>
        private static bool TouchesWall(LevelMap map, Vector3 p)
        {
            int col = Mathf.RoundToInt(p.x), row = Mathf.RoundToInt(p.z);
            return map.IsWall(Mathf.RoundToInt(p.x + 0.4f), row) || map.IsWall(Mathf.RoundToInt(p.x - 0.4f), row) ||
                   map.IsWall(col, Mathf.RoundToInt(p.z + 0.4f)) || map.IsWall(col, Mathf.RoundToInt(p.z - 0.4f));
        }

        /// <summary>
        /// Puertas batientes en los huecos de 2 celdas (3 m) entre las salas y el pasillo, y entre salas.
        /// Cerradas tapan la vista y paran las balas; se abren al pasar el jugador o un enemigo.
        /// </summary>
        public static readonly Doorway[] Doorways =
        {
            // Pared norte del pasillo (fila 8): sala inicial, sala del sofá y sala de las cajas.
            new Doorway(0, 4, 8, false), new Doorway(0, 13, 8, false), new Doorway(0, 24, 8, false),
            // Pared sur del pasillo (fila 11): salas del sur.
            new Doorway(0, 6, 11, false), new Doorway(0, 18, 11, false), new Doorway(0, 26, 11, false),
            // Entre la sala inicial y la del sofá, y entre las tres salas del sur.
            new Doorway(0, 8, 4, true), new Doorway(0, 11, 15, true), new Doorway(0, 23, 15, true),
        };

        /// <summary>
        /// Cables de LEDs, como en el piso de un dealer: tiras detrás del sofá y en la base de las paredes,
        /// cables sueltos serpenteando por el suelo y guirnaldas colgando de pared a pared. Una tira está rota y parpadea.
        /// Coordenadas: (columna, altura sobre el suelo del piso, fila). Las caras de las paredes están en x.5.
        /// </summary>
        public static readonly LedCable[] LedCables =
        {
            // Sala inicial: guirnalda magenta cruzando en diagonal.
            new LedCable(0, LedColor.Magenta, LedLayout.Hanging,
                new[] { LedCable.P(0.6f, 2.2f, 4.6f), LedCable.P(7.4f, 2.2f, 6.9f) }, sag: 0.6f),

            // Sala norte: tira violeta en el suelo detrás del sofá.
            new LedCable(0, LedColor.Violet, LedLayout.Lying,
                new[] { LedCable.P(10.6f, 0.03f, 0.62f), LedCable.P(13.6f, 0.03f, 0.62f) }),

            // Sala de las cajas: guirnalda carmesí en V de pared a pared.
            new LedCable(0, LedColor.Crimson, LedLayout.Hanging,
                new[] { LedCable.P(19.62f, 2.3f, 1.2f), LedCable.P(22f, 2.3f, 7.38f), LedCable.P(32.38f, 2.3f, 1.4f) }, sag: 0.5f),

            // Pasillo: guirnalda carmesí en zigzag entre las dos paredes.
            new LedCable(0, LedColor.Crimson, LedLayout.Hanging,
                new[]
                {
                    LedCable.P(2f, 2.3f, 8.62f), LedCable.P(9f, 2.3f, 10.38f), LedCable.P(12f, 2.3f, 8.62f),
                    LedCable.P(17f, 2.3f, 10.38f), LedCable.P(22f, 2.3f, 8.62f), LedCable.P(28f, 2.3f, 10.38f),
                }, sag: 0.45f),

            // Sala oeste: tira violeta en L por la base de las paredes oeste y sur.
            new LedCable(0, LedColor.Violet, LedLayout.Lying,
                new[]
                {
                    LedCable.P(0.62f, 0.03f, 12.8f), LedCable.P(0.62f, 0.03f, 18.7f),
                    LedCable.P(1.3f, 0.03f, 19.38f), LedCable.P(5.5f, 0.03f, 19.38f),
                }),

            // Sala central sur: cable verde azulado suelto serpenteando por el suelo.
            new LedCable(0, LedColor.Teal, LedLayout.Lying,
                new[]
                {
                    LedCable.P(13.5f, 0.03f, 13.4f), LedCable.P(15.2f, 0.03f, 15f), LedCable.P(14.4f, 0.03f, 16.6f),
                    LedCable.P(16.4f, 0.03f, 17.9f), LedCable.P(18.6f, 0.03f, 17.1f), LedCable.P(20.6f, 0.03f, 18.4f),
                }),

            // Cocina: tira azul fría en el zócalo de los muebles.
            new LedCable(0, LedColor.ColdBlue, LedLayout.Lying,
                new[] { LedCable.P(27.4f, 0.03f, 12.15f), LedCable.P(31.6f, 0.03f, 12.15f) }),

            // Piso de arriba, ala oeste: tira carmesí rota (parpadea) en la base de la pared norte.
            new LedCable(1, LedColor.Crimson, LedLayout.Lying,
                new[] { LedCable.P(9.4f, 0.03f, 0.62f), LedCable.P(17.4f, 0.03f, 0.62f) }, flicker: true),

            // Piso de arriba, ala este: tira violeta tras el salón y guirnalda azul hacia el maletín.
            new LedCable(1, LedColor.Violet, LedLayout.Lying,
                new[] { LedCable.P(19.6f, 0.03f, 0.62f), LedCable.P(23.6f, 0.03f, 0.62f) }),
            new LedCable(1, LedColor.ColdBlue, LedLayout.Hanging,
                new[] { LedCable.P(18.62f, 2.2f, 6.2f), LedCable.P(27f, 2.2f, 0.62f) }, sag: 0.5f),
        };
    }
}
