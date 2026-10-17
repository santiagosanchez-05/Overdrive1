namespace Overdrive.Level
{
    /// <summary>Luces de ambiente oscuras (shaders Overdrive/Aura y Overdrive/LightBeam). No bloquean ni entran en el NavMesh.</summary>
    public enum AmbientKind
    {
        /// <summary>Halo carmesí oscuro colgando del techo, latido lento.</summary>
        HaloCrimson,
        /// <summary>Halo violeta profundo, grande y tenue.</summary>
        HaloViolet,
        /// <summary>Brasa pequeña que parpadea, apoyada sobre una mesa.</summary>
        Ember,
        /// <summary>Columna de luz violeta que cae del techo (por ejemplo, por un hueco del piso de arriba).</summary>
        BeamViolet,
        /// <summary>Columna de luz carmesí tenue que cae del techo.</summary>
        BeamCrimson,
    }

    /// <summary>
    /// Una luz de ambiente en coordenadas de celda (admite medias celdas, p. ej. 11.5 = entre dos celdas).
    /// <see cref="Height"/> es la altura sobre el suelo de su piso (para los haces, su largo hasta el suelo).
    /// En las brasas se ignora: se apoyan sobre lo que haya debajo.
    /// </summary>
    public readonly struct AmbientLight
    {
        public readonly int Floor;
        public readonly float Col, Row;
        public readonly AmbientKind Kind;
        public readonly float Height;

        public AmbientLight(int floor, float col, float row, AmbientKind kind, float height = 2.3f)
        {
            Floor = floor; Col = col; Row = row; Kind = kind; Height = height;
        }

        public bool IsBeam => Kind == AmbientKind.BeamViolet || Kind == AmbientKind.BeamCrimson;

        public string Name => $"Amb_{Kind}_f{Floor}_c{(int)(Col * 10)}_r{(int)(Row * 10)}";
    }
}
