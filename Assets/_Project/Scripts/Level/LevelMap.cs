using System;
using System.Collections.Generic;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Mapa ASCII del nivel. Fila 0 = norte (+Z), columna 0 = oeste (-X).
    /// Leyenda:
    ///   # pared   . suelo   P inicio del jugador   X zona de salida
    ///   b enemigo con bate   k con cuchillo   s con escopeta   m con subfusil
    /// </summary>
    public sealed class LevelMap
    {
        public const char Wall = '#';
        public const char Floor = '.';
        public const char PlayerSpawn = 'P';
        public const char Exit = 'X';
        public const string EnemyChars = "bksm";

        public readonly struct Block
        {
            public readonly int Col, Row, Width, Height;

            public Block(int col, int row, int width, int height)
            {
                Col = col; Row = row; Width = width; Height = height;
            }

            public string Name => $"Wall_c{Col}_r{Row}_{Width}x{Height}";
        }

        private readonly string[] rows;

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }

        public LevelMap(string[] rows, float cellSize)
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
        }

        public char At(int col, int row)
        {
            if (col < 0 || row < 0 || col >= Width || row >= Height) return Wall;
            return rows[row][col];
        }

        public bool IsWall(int col, int row) => At(col, row) == Wall;
        public static bool IsEnemy(char c) => EnemyChars.IndexOf(c) >= 0;

        /// <summary>Centro de la celda en el mundo, a la altura del suelo (y = 0).</summary>
        public Vector3 CellToWorld(int col, int row) => new Vector3(col * CellSize, 0f, -row * CellSize);

        public Vector3 BlockCenter(Block b) => new Vector3(
            (b.Col + (b.Width - 1) * 0.5f) * CellSize,
            0f,
            -(b.Row + (b.Height - 1) * 0.5f) * CellSize);

        /// <summary>Centro del rectángulo completo del mapa.</summary>
        public Vector3 Center => new Vector3((Width - 1) * 0.5f * CellSize, 0f, -(Height - 1) * 0.5f * CellSize);

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

        /// <summary>
        /// Agrupa las paredes en bloques: primero tramos horizontales de 2 o más celdas,
        /// luego tramos verticales con las celdas sueltas restantes.
        /// </summary>
        public List<Block> WallBlocks()
        {
            var blocks = new List<Block>();
            var covered = new bool[Width, Height];

            for (int r = 0; r < Height; r++)
            {
                int c = 0;
                while (c < Width)
                {
                    if (!IsWall(c, r)) { c++; continue; }
                    int start = c;
                    while (c < Width && IsWall(c, r)) c++;
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
                    if (!IsWall(c, r) || covered[c, r]) { r++; continue; }
                    int start = r;
                    while (r < Height && IsWall(c, r) && !covered[c, r]) r++;
                    blocks.Add(new Block(c, start, 1, r - start));
                }
            }

            return blocks;
        }

        /// <summary>Celdas transitables alcanzables desde (col, row) moviéndose en 4 direcciones.</summary>
        public HashSet<(int, int)> FloodFill(int col, int row)
        {
            var visited = new HashSet<(int, int)>();
            if (IsWall(col, row)) return visited;
            var queue = new Queue<(int, int)>();
            queue.Enqueue((col, row));
            visited.Add((col, row));
            while (queue.Count > 0)
            {
                var (c, r) = queue.Dequeue();
                foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nc = c + dc, nr = r + dr;
                    if (IsWall(nc, nr) || visited.Contains((nc, nr))) continue;
                    visited.Add((nc, nr));
                    queue.Enqueue((nc, nr));
                }
            }
            return visited;
        }
    }
}
