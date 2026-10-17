using Overdrive.Core;

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
            "#9......#...1......#5..........67#",
            "#.//////#..........#.............#",
            "#.//////#..........#.............#",
            "#..................#.............#",
            "#..........m.......#......k......#",
            "#.P.B...#..........#.............#",
            "#......8#.........2#.............#",
            "####..#######..#########..########",
            "#............................DRR.#",
            "#............................DRR.#",
            "######..##########..######..######",
            "#..........#...........#.........#",
            "#..........#...........#.........#",
            "#..........#.....s.....#....m....#",
            "#................................#",
            "#...s......................b.....#",
            "#..........#...........#.........#",
            "#..........#...........#......XX.#",
            "#..........#34........2#......XX.#",
            "##################################",
        };

        public static readonly string[] UpperRows =
        {
            "        ##########################",
            "        #.........#..............#",
            "        ......k...#.........C....#",
            "        ..........#..............#",
            "        #..OO............OO......#",
            "        #..OO............OO......#",
            "        #......b..#..........m...#",
            "        #5.......2#.....s........#",
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

        public static LevelLayout Create() => new LevelLayout(
            new LevelMap(GroundRows, CellSize, 0, Floors.BaseY(0)),
            new LevelMap(UpperRows, CellSize, 1, Floors.BaseY(1)))
        {
            Ambience = Ambience,
        };
    }
}
