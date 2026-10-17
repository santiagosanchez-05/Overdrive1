using System;
using System.Collections.Generic;
using Overdrive.Weapons;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Mapa ASCII de un piso. Fila 0 = norte (+Z), columna 0 = oeste (-X).
    /// Leyenda:
    ///   # pared   . suelo   P inicio del jugador   X zona de salida
    ///   b enemigo con bate   k con cuchillo   s con escopeta   m con subfusil
    ///   B K S M: la misma arma tirada en el suelo, sin enemigo
    ///   / escalera (rampa que sube hacia el este hasta el piso de arriba)
    ///   ' ' sin suelo (fuera del piso)   O hueco en el suelo por el que se cae al piso de abajo
    ///   C maletín   D puerta cerrada hasta que llegan los refuerzos   R punto de entrada de un refuerzo
    ///   0-9 muebles (ver <see cref="PropCatalog"/>), pegados a la pared más cercana
    /// </summary>
    public sealed class LevelMap
    {
        public const char Wall = '#';
        public const char Floor = '.';
        public const char PlayerSpawn = 'P';
        public const char Exit = 'X';
        public const char Stairs = '/';
        public const char Void = ' ';
        public const char Hole = 'O';
        public const char Briefcase = 'C';
        public const char Door = 'D';
        public const char Reinforcement = 'R';
        public const string EnemyChars = "bksm";
        public const string FloorWeaponChars = "BKSM";

        public readonly struct Block
        {
            public readonly int Col, Row, Width, Height;

            public Block(int col, int row, int width, int height)
            {
                Col = col; Row = row; Width = width; Height = height;
            }

            public string Name => NameWith("Wall");
            public string NameWith(string prefix) => $"{prefix}_c{Col}_r{Row}_{Width}x{Height}";
        }

        private readonly string[] rows;

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }

        /// <summary>Índice del piso (0 = planta baja).</summary>
        public int FloorIndex { get; }

        /// <summary>Altura del suelo de este piso.</summary>
        public float BaseY { get; }

        public LevelMap(string[] rows, float cellSize, int floorIndex = 0, float baseY = 0f)
        {
            if (rows == null || rows.Length == 0) throw new ArgumentException("El mapa está vacío.");
            Width = rows[0].Length;
            for (int r = 0; r < rows.Length; r++)
            {
                if (rows[r].Length != Width)
                    throw new ArgumentException($"La fila {r} mide {rows[r].Length}, se esperaban {Width}.");
            }
            this.rows = rows;
            Height = rows.Length;
            CellSize = cellSize;
            FloorIndex = floorIndex;
            BaseY = baseY;
        }

        public char At(int col, int row)
        {
            if (col < 0 || row < 0 || col >= Width || row >= Height) return Wall;
            return rows[row][col];
        }

        public bool IsWall(int col, int row) => At(col, row) == Wall;

        /// <summary>Se puede pisar: no es pared, ni vacío, ni hueco.</summary>
        public bool IsWalkable(int col, int row)
        {
            char c = At(col, row);
            return c != Wall && c != Void && c != Hole;
        }

        /// <summary>Tiene losa de suelo (en los pisos superiores, también bajo las paredes).</summary>
        public bool HasSlab(int col, int row)
        {
            char c = At(col, row);
            return c != Void && c != Hole && c != Stairs;
        }

        public static bool IsEnemy(char c) => EnemyChars.IndexOf(c) >= 0;
        public static bool IsFloorWeapon(char c) => FloorWeaponChars.IndexOf(c) >= 0;

        /// <summary>Arma asociada a una letra de enemigo o de arma en el suelo.</summary>
        public static WeaponType WeaponFor(char c)
        {
            switch (char.ToLowerInvariant(c))
            {
                case 'b': return WeaponType.Bat;
                case 'k': return WeaponType.Knife;
                case 's': return WeaponType.Shotgun;
                case 'm': return WeaponType.Smg;
                default: throw new ArgumentException($"'{c}' no es una letra de arma.");
            }
        }

        /// <summary>Centro de la celda en el mundo, a la altura del suelo de este piso.</summary>
        public Vector3 CellToWorld(int col, int row) => new Vector3(col * CellSize, BaseY, -row * CellSize);

        public Vector3 BlockCenter(Block b) => new Vector3(
            (b.Col + (b.Width - 1) * 0.5f) * CellSize,
            BaseY,
            -(b.Row + (b.Height - 1) * 0.5f) * CellSize);

        /// <summary>Centro del rectángulo completo del mapa.</summary>
        public Vector3 Center => new Vector3((Width - 1) * 0.5f * CellSize, BaseY, -(Height - 1) * 0.5f * CellSize);

        public IEnumerable<(int col, int row, char c)> Cells()
        {
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                    yield return (c, r, rows[r][c]);
        }

        public List<(int col, int row)> FindAll(char ch)
        {
            var result = new List<(int, int)>();
            foreach (var cell in Cells())
                if (cell.c == ch) result.Add((cell.col, cell.row));
            return result;
        }

        /// <summary>Rectángulo mínimo que contiene todas las celdas con la letra, o null si no hay.</summary>
        public Block? BoundsOf(char ch)
        {
            List<(int col, int row)> cells = FindAll(ch);
            if (cells.Count == 0) return null;
            int minC = int.MaxValue, maxC = int.MinValue, minR = int.MaxValue, maxR = int.MinValue;
            foreach (var (col, row) in cells)
            {
                minC = Math.Min(minC, col); maxC = Math.Max(maxC, col);
                minR = Math.Min(minR, row); maxR = Math.Max(maxR, row);
            }
            return new Block(minC, minR, maxC - minC + 1, maxR - minR + 1);
        }

        /// <summary>Agrupa las paredes en bloques (ver <see cref="Blocks"/>).</summary>
        public List<Block> WallBlocks() => Blocks((c, r) => IsWall(c, r));

        /// <summary>Agrupa las losas de suelo de un piso superior en bloques.</summary>
        public List<Block> SlabBlocks() => Blocks((c, r) => HasSlab(c, r));

        /// <summary>
        /// Agrupa las celdas que cumplen <paramref name="include"/> en bloques: primero tramos horizontales
        /// de 2 o más celdas, luego tramos verticales con las celdas sueltas restantes.
        /// </summary>
        public List<Block> Blocks(Func<int, int, bool> include)
        {
            var blocks = new List<Block>();
            var covered = new bool[Width, Height];

            for (int r = 0; r < Height; r++)
            {
                int c = 0;
                while (c < Width)
                {
                    if (!include(c, r)) { c++; continue; }
                    int start = c;
                    while (c < Width && include(c, r)) c++;
                    int length = c - start;
                    if (length < 2) continue;
                    blocks.Add(new Block(start, r, length, 1));
                    for (int i = start; i < c; i++) covered[i, r] = true;
                }
            }

            for (int c = 0; c < Width; c++)
            {
                int r = 0;
                while (r < Height)
                {
                    if (!include(c, r) || covered[c, r]) { r++; continue; }
                    int start = r;
                    while (r < Height && include(c, r) && !covered[c, r]) r++;
                    blocks.Add(new Block(c, start, 1, r - start));
                }
            }

            return blocks;
        }

        private bool IsFlatWalkable(int col, int row) =>
            IsWalkable(col, row) && At(col, row) != Stairs && At(col, row) != Door && !PropCatalog.IsProp(At(col, row));

        /// <summary>
        /// Lado de la celda que toca pared (dirección en celdas: (1,0) este, (-1,0) oeste, (0,1) sur, (0,-1) norte),
        /// o (0,0) si no toca ninguna. Con dos paredes (esquina) elige primero norte/sur.
        /// </summary>
        public (int dc, int dr) WallSideOf(int col, int row)
        {
            foreach (var (dc, dr) in new[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
                if (IsWall(col + dc, row + dr)) return (dc, dr);
            return (0, 0);
        }

        /// <summary>
        /// Ruta de patrulla de ida y vuelta: el tramo recto más largo (hasta <paramref name="maxCells"/>)
        /// en las cuatro direcciones desde la celda. Vacía si no hay sitio para moverse al menos 2 celdas.
        /// </summary>
        public Vector3[] PatrolRoute(int col, int row, int maxCells)
        {
            int bestLength = 0;
            (int dc, int dr) best = (0, 0);
            foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int length = 0;
                // Deja una celda de margen con paredes y huecos para que el agente no roce ni caiga.
                while (length < maxCells
                       && IsFlatWalkable(col + dc * (length + 1), row + dr * (length + 1))
                       && IsFlatWalkable(col + dc * (length + 2), row + dr * (length + 2)))
                    length++;
                if (length > bestLength)
                {
                    bestLength = length;
                    best = (dc, dr);
                }
            }
            if (bestLength < 2) return new Vector3[0];
            return new[] { CellToWorld(col, row), CellToWorld(col + best.dc * bestLength, row + best.dr * bestLength) };
        }

        /// <summary>Celdas transitables alcanzables desde (col, row) moviéndose en 4 direcciones.</summary>
        public HashSet<(int, int)> FloodFill(int col, int row)
        {
            var visited = new HashSet<(int, int)>();
            if (!IsWalkable(col, row)) return visited;
            var queue = new Queue<(int, int)>();
            queue.Enqueue((col, row));
            visited.Add((col, row));
            while (queue.Count > 0)
            {
                var (c, r) = queue.Dequeue();
                foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nc = c + dc, nr = r + dr;
                    if (!IsWalkable(nc, nr) || visited.Contains((nc, nr))) continue;
                    visited.Add((nc, nr));
                    queue.Enqueue((nc, nr));
                }
            }
            return visited;
        }
    }
}
