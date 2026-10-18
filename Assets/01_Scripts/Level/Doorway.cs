namespace Overdrive.Level
{
    /// <summary>
    /// Hueco de 2 celdas en una pared, donde va una puerta. Horizontal (pared a lo largo de las columnas):
    /// ocupa (Col, Row) y (Col + 1, Row). Vertical (pared a lo largo de las filas): (Col, Row) y (Col, Row + 1).
    /// </summary>
    public readonly struct Doorway
    {
        public readonly int Floor;
        public readonly int Col;
        public readonly int Row;
        public readonly bool Vertical;

        public Doorway(int floor, int col, int row, bool vertical)
        {
            Floor = floor; Col = col; Row = row; Vertical = vertical;
        }

        public (int col, int row) Second => Vertical ? (Col, Row + 1) : (Col + 1, Row);

        /// <summary>Celdas de pared que lo cierran a cada lado.</summary>
        public (int col, int row) WallBefore => Vertical ? (Col, Row - 1) : (Col - 1, Row);
        public (int col, int row) WallAfter => Vertical ? (Col, Row + 2) : (Col + 2, Row);

        public string Name => $"Door_{(Vertical ? "V" : "H")}_c{Col}_r{Row}_f{Floor}";
    }
}
