namespace Overdrive.Combat
{
    /// <summary>Cómo murió un enemigo: cada forma da distintos puntos.</summary>
    public enum KillCause
    {
        /// <summary>Sin causa conocida (pruebas del editor).</summary>
        Other,
        /// <summary>Bate o cuchillo.</summary>
        Melee,
        /// <summary>Bala o perdigón.</summary>
        Shot,
        /// <summary>Remate en el suelo con Espacio.</summary>
        Finisher,
        /// <summary>Puñetazo por la espalda sin que te haya detectado.</summary>
        Backstab,
    }

    /// <summary>Cómo se derribó a un enemigo.</summary>
    public enum KnockdownCause
    {
        Punch,
        Thrown,
        /// <summary>Caerle encima por un hueco.</summary>
        Stomp,
    }
}
