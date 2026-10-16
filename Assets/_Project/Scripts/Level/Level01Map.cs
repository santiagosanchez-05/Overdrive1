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
            "#.......#..........#.............#",
            "#.//////#..........#.............#",
            "#.//////#..........#.............#",
            "#..................#.............#",
            "#..........m.......#......k......#",
            "#.P.B...#..........#.............#",
            "#.......#..........#.............#",
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
            "#..........#...........#......XX.#",
            "##################################",
        };

        public static readonly string[] UpperRows =
        {
            "        ##########################",
            "        #.........#..............#",
            "        ......k...#.............C#",
            "        ..........#..............#",
            "        #..OO............OO......#",
            "        #..OO............OO......#",
            "        #......b..#..........m...#",
            "        #.........#.....s........#",
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

        public static LevelLayout Create() => new LevelLayout(
            new LevelMap(GroundRows, CellSize, 0, Floors.BaseY(0)),
            new LevelMap(UpperRows, CellSize, 1, Floors.BaseY(1)));
    }
}
