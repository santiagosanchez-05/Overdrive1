using System;
using System.Collections.Generic;

namespace Overdrive.Level
{
    /// <summary>Un nivel de varios pisos: un <see cref="LevelMap"/> por piso, todos con la misma rejilla.</summary>
    public sealed class LevelLayout
    {
        public LevelMap[] Floors { get; }
        public LevelMap Ground => Floors[0];

        /// <summary>Luces de ambiente del nivel (ver <see cref="AmbientLight"/>).</summary>
        public AmbientLight[] Ambience { get; set; } = new AmbientLight[0];

        public LevelLayout(params LevelMap[] floors)
        {
            if (floors == null || floors.Length == 0) throw new ArgumentException("El nivel no tiene pisos.");
            for (int i = 0; i < floors.Length; i++)
            {
                if (floors[i].Width != floors[0].Width || floors[i].Height != floors[0].Height)
                    throw new ArgumentException($"El piso {i} no tiene el mismo tamaño que la planta baja.");
                if (floors[i].FloorIndex != i)
                    throw new ArgumentException($"El piso {i} tiene índice {floors[i].FloorIndex}.");
            }
            Floors = floors;
        }

        /// <summary>Todas las celdas de todos los pisos.</summary>
        public IEnumerable<(LevelMap map, int col, int row, char c)> AllCells()
        {
            foreach (LevelMap map in Floors)
                foreach (var (col, row, c) in map.Cells())
                    yield return (map, col, row, c);
        }

        public List<(LevelMap map, int col, int row)> FindAll(char ch)
        {
            var result = new List<(LevelMap, int, int)>();
            foreach (var (map, col, row, c) in AllCells())
                if (c == ch) result.Add((map, col, row));
            return result;
        }

        public int EnemyCount
        {
            get
            {
                int count = 0;
                foreach (var cell in AllCells())
                    if (LevelMap.IsEnemy(cell.c)) count++;
                return count;
            }
        }
    }
}
